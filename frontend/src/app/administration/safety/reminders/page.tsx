'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AlarmClock, BellRing, Loader2, Play } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { safetyReminderService } from '@/services/hr/safety-reminder.service';
import { sheReminderKindLabel } from '@/types/hr/safety-reminders';
import type { SheReminderLogEntry, SheReminderRunResult } from '@/types/hr/safety-reminders';

/**
 * SHE reminder engine (slice 13, FRD §17). The engine runs hourly per tenant:
 * permit auto-expiry (FR-PTW-003), due-soon reminder ladders — the statutory
 * 180/90/60/30/14/7 sequence on regulatory reviews (FR-ENV-017–019) — and tiered
 * overdue escalation (FR-SHE-250; tiers 2–3 also notify SuperAdmin). Delivery goes
 * through the notification topics ("SafetyCompliance.*"), where admins retarget
 * recipients or mute a topic. This screen operates the engine and shows its history;
 * run-now shares the exact sweep the hourly job executes and is dedupe-safe.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

const tierBadge = (entry: SheReminderLogEntry) => {
  if (entry.escalationTier === 0)
    return <Badge variant="secondary">due in {entry.daysRemaining}d</Badge>;
  const label = `${-entry.daysRemaining}d overdue — tier ${entry.escalationTier}`;
  return <Badge variant={entry.escalationTier === 1 ? 'default' : 'destructive'}>{label}</Badge>;
};

export default function SafetyRemindersPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [running, setRunning] = useState(false);
  const [lastResult, setLastResult] = useState<SheReminderRunResult | null>(null);

  const { data: runs = [], isLoading: runsLoading } = useQuery({
    queryKey: ['hr', 'safety-reminders', 'runs'],
    queryFn: () => safetyReminderService.getRecentRuns(20),
  });
  const { data: log = [], isLoading: logLoading } = useQuery({
    queryKey: ['hr', 'safety-reminders', 'log'],
    queryFn: () => safetyReminderService.getRecentLog(14),
  });

  const runNow = async () => {
    setRunning(true);
    try {
      const result = await safetyReminderService.runNow();
      setLastResult(result);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-reminders'] });
      toast({
        title: 'Sweep complete',
        description: `${result.remindersQueued} reminder(s) queued, ${result.permitsExpired} permit(s) expired, ${result.riskAssessmentsExpired} risk assessment(s) expired.`,
      });
    } catch (error: any) {
      toast({
        title: 'Sweep failed',
        description: error?.message || 'Running the sweep failed.',
        variant: 'destructive',
      });
    } finally {
      setRunning(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="SHE Reminder Engine"
        description="Runs hourly: expires permits past their window, walks the due-date reminder ladders (180/90/60/30/14/7 on statutory reviews) and escalates overdue items by tier. Delivery is via the SafetyCompliance notification topics — retarget or mute them under notification settings."
        backHref="/administration/safety"
        actions={
          <Button onClick={runNow} disabled={running}>
            {running ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Play className="mr-2 h-4 w-4" />
            )}
            Run sweep now
          </Button>
        }
      />

      {lastResult && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">This sweep</CardTitle>
            <CardDescription>
              {lastResult.remindersQueued} reminder(s) queued · {lastResult.permitsExpired}{' '}
              permit(s) auto-expired · {lastResult.riskAssessmentsExpired} risk assessment(s)
              auto-expired. Re-running is safe — every rung fires once.
            </CardDescription>
          </CardHeader>
          {Object.keys(lastResult.queuedByKind).length > 0 && (
            <CardContent className="flex flex-wrap gap-2 pt-0">
              {Object.entries(lastResult.queuedByKind).map(([kind, count]) => (
                <Badge key={kind} variant="outline">
                  {sheReminderKindLabel(kind)}: {count}
                </Badge>
              ))}
            </CardContent>
          )}
        </Card>
      )}

      <Tabs defaultValue="log">
        <TabsList>
          <TabsTrigger value="log">Dispatch log ({log.length})</TabsTrigger>
          <TabsTrigger value="runs">Runs ({runs.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="log" className="mt-4 space-y-3">
          <p className="text-muted-foreground text-sm">
            Every reminder dispatched in the last 14 days. Each item + due date + ladder rung
            fires exactly once; a rescheduled due date re-arms its ladder.
          </p>
          {logLoading ? null : log.length === 0 ? (
            <EmptyState
              title="Nothing dispatched yet"
              description="Reminders appear here once a sweep finds something due, overdue or below reorder level."
              icon={BellRing}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Dispatched</TableHead>
                      <TableHead>Kind</TableHead>
                      <TableHead>Item</TableHead>
                      <TableHead>Due</TableHead>
                      <TableHead>Standing</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {log.map((entry) => (
                      <TableRow key={entry.id}>
                        <TableCell className="whitespace-nowrap">
                          {fmtDateTime(entry.dispatchedAt)}
                        </TableCell>
                        <TableCell>{sheReminderKindLabel(entry.kind)}</TableCell>
                        <TableCell className="max-w-[320px]">
                          <div className="truncate" title={entry.reference}>
                            {entry.reference}
                          </div>
                          <div className="text-muted-foreground text-xs">{entry.itemType}</div>
                        </TableCell>
                        <TableCell>{fmtDate(entry.dueDate)}</TableCell>
                        <TableCell>{tierBadge(entry)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="runs" className="mt-4 space-y-3">
          <p className="text-muted-foreground text-sm">
            The last 20 sweeps — hourly scheduled runs and manual ones.
          </p>
          {runsLoading ? null : runs.length === 0 ? (
            <EmptyState
              title="No runs yet"
              description="The hourly job runs a few minutes after the API starts; or run a sweep now."
              icon={AlarmClock}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Started</TableHead>
                      <TableHead>Trigger</TableHead>
                      <TableHead className="text-right">Reminders</TableHead>
                      <TableHead className="text-right">Permits expired</TableHead>
                      <TableHead className="text-right">RAs expired</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {runs.map((run) => (
                      <TableRow key={run.id}>
                        <TableCell className="whitespace-nowrap">
                          {fmtDateTime(run.startedAt)}
                        </TableCell>
                        <TableCell>
                          <Badge variant={run.trigger === 'Manual' ? 'default' : 'secondary'}>
                            {run.trigger}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-right">{run.remindersQueued}</TableCell>
                        <TableCell className="text-right">{run.permitsExpired}</TableCell>
                        <TableCell className="text-right">{run.riskAssessmentsExpired}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}
