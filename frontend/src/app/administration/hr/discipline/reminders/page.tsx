'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Play, BellRing, Eye } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { apiService } from '@/services/api.service';
import { useToast } from '@/hooks/use-toast';

const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

interface ReminderRun {
  id: string;
  startedAt: string;
  completedAt?: string | null;
  trigger: string;
  remindersQueued: number;
}

interface ReminderLogEntry {
  id: string;
  runId: string;
  kind: string;
  itemType: string;
  entityId: string;
  reference: string;
  dueDate?: string | null;
  daysRemaining: number;
  escalationTier: number;
  dispatchedAt: string;
}

interface ReminderPreviewItem {
  kind: string;
  itemType: string;
  entityId: string;
  reference: string;
  dueDate?: string | null;
  daysRemaining: number;
  escalationTier: number;
  dedupeKey: string;
}

interface RunResult {
  runId: string;
  remindersQueued: number;
  byKind: Record<string, number>;
}

/** What each sweep kind means, in the words someone reading the log would use. */
const KIND_LABELS: Record<string, string> = {
  WrittenQueryOverdue: 'Written query not issued (48 hours)',
  InvestigationOverdue: 'Investigation past four weeks',
  HearingUpcoming: 'Hearing coming up',
  AppealWindowClosing: 'Appeal window closing',
  AppealDecisionOverdue: 'Appeal decision overdue',
  CorrectiveActionOverdue: 'Corrective action past review',
  WarningExpiring: 'Warning about to expire',
  FineOverdue: 'Fine outstanding past due',
  GrievanceUnanswered: 'Grievance awaiting a response',
};

/** A grievance reminder points at the grievance; everything else points at the case. */
const targetHref = (kind: string, entityId: string) =>
  kind === 'GrievanceUnanswered' ? `/hr/grievances/${entityId}` : `/hr/discipline/${entityId}`;

function DaysCell({ days }: { days: number }) {
  return (
    <span className={days < 0 ? 'text-destructive' : undefined}>
      {days < 0 ? `${Math.abs(days)} overdue` : `${days} left`}
    </span>
  );
}

function TierCell({ tier }: { tier: number }) {
  if (tier <= 0) return <span className="text-muted-foreground">—</span>;
  return <Badge variant={tier >= 2 ? 'destructive' : 'outline'}>Tier {tier}</Badge>;
}

/**
 * Operates the discipline reminder sweep and shows what it has sent.
 *
 * The sweep runs daily on its own; run-now exists so HR can force one after a bulk edit rather than
 * waiting for tomorrow. Repeating it is safe — each reminder is deduped per item, due date and
 * ladder rung, so a second run today sends nothing new.
 */
export default function DisciplineRemindersPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [lastRun, setLastRun] = useState<RunResult | null>(null);
  const [asOf, setAsOf] = useState('');

  const { data: runs = [], isLoading } = useQuery({
    queryKey: ['hr', 'discipline-reminders', 'runs'],
    queryFn: () => apiService.get<ReminderRun[]>('/discipline/reminders/runs', { count: 20 }),
  });

  const { data: log = [] } = useQuery({
    queryKey: ['hr', 'discipline-reminders', 'log'],
    queryFn: () => apiService.get<ReminderLogEntry[]>('/discipline/reminders/log', { days: 14 }),
  });

  // Deliberately not auto-run: a preview is a question you ask, and asking it on every page load
  // would run nine sweeps over the whole tenant every time somebody opened this screen.
  const { data: pending, refetch: runPreview, isFetching: previewing } = useQuery({
    queryKey: ['hr', 'discipline-reminders', 'preview', asOf],
    queryFn: () =>
      apiService.get<ReminderPreviewItem[]>(
        '/discipline/reminders/preview',
        asOf ? { asOf: new Date(asOf).toISOString() } : {},
      ),
    enabled: false,
  });

  const run = useMutation({
    mutationFn: () => apiService.post<RunResult>('/discipline/reminders/run', {}),
    onSuccess: (result) => {
      setLastRun(result);
      toast({
        title: 'Sweep complete',
        description:
          result.remindersQueued === 0
            ? 'Nothing new to send — everything due has already been notified.'
            : `${result.remindersQueued} reminder(s) queued.`,
      });
      queryClient.invalidateQueries({ queryKey: ['hr', 'discipline-reminders'] });
    },
    onError: (error: any) =>
      toast({ title: 'Sweep failed', description: error?.message, variant: 'destructive' }),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Discipline reminders"
        description="The daily sweep over the disciplinary clocks and the grievance ladder: the 48-hour written query, the four-week investigation, hearings, both appeal windows, corrective actions, warnings, fines, and grievances nobody has answered."
        backHref="/administration/hr"
        actions={
          <Button onClick={() => run.mutate()} disabled={run.isPending}>
            {run.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Play className="mr-2 h-4 w-4" />
            )}
            Run now
          </Button>
        }
      />

      {lastRun && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Last run from this screen</CardTitle>
          </CardHeader>
          <CardContent>
            {lastRun.remindersQueued === 0 ? (
              <p className="text-sm text-muted-foreground">
                Nothing new. Everything currently due has already been notified — reminders are sent
                once per item, due date and rung.
              </p>
            ) : (
              <ul className="space-y-1 text-sm">
                {Object.entries(lastRun.byKind).map(([kind, count]) => (
                  <li key={kind} className="flex items-center justify-between">
                    <span>{KIND_LABELS[kind] ?? kind}</span>
                    <Badge variant="outline">{count}</Badge>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      )}

      {/* The question HR actually asks: what is this going to chase me about? Reads only — a
          preview claims nothing, so looking ahead cannot rob a later sweep of a reminder. */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            <Eye className="mr-2 inline h-4 w-4" />
            What a sweep would send
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-end gap-3">
            <div className="space-y-1">
              <Label htmlFor="asOf" className="text-xs">
                As at (leave blank for now)
              </Label>
              <Input
                id="asOf"
                type="date"
                value={asOf}
                onChange={(e) => setAsOf(e.target.value)}
                className="w-44"
              />
            </div>
            <Button variant="outline" onClick={() => runPreview()} disabled={previewing}>
              {previewing ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Eye className="mr-2 h-4 w-4" />
              )}
              Preview
            </Button>
            <p className="text-xs text-muted-foreground">
              Changes nothing and sends nothing. Setting a future date shows what will fall due by
              then.
            </p>
          </div>

          {pending && (
            pending.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                Nothing would be sent. Everything due by that date has already been notified.
              </p>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>What</TableHead>
                    <TableHead>Reference</TableHead>
                    <TableHead>Due</TableHead>
                    <TableHead>Days</TableHead>
                    <TableHead>Tier</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {pending.map((item) => (
                    <TableRow key={item.dedupeKey}>
                      <TableCell>
                        <div>{KIND_LABELS[item.kind] ?? item.kind}</div>
                        <div className="text-xs text-muted-foreground">{item.itemType}</div>
                      </TableCell>
                      <TableCell>
                        <Link
                          href={targetHref(item.kind, item.entityId)}
                          className="font-medium hover:underline"
                        >
                          {item.reference}
                        </Link>
                      </TableCell>
                      <TableCell>{fmtDate(item.dueDate)}</TableCell>
                      <TableCell>
                        <DaysCell days={item.daysRemaining} />
                      </TableCell>
                      <TableCell>
                        <TierCell tier={item.escalationTier} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Recent sweeps</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : runs.length === 0 ? (
            <EmptyState
              title="No sweeps yet"
              description="The daily sweep has not run in this tenant. Use Run now to start one."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Started</TableHead>
                  <TableHead>Completed</TableHead>
                  <TableHead>Trigger</TableHead>
                  <TableHead className="text-right">Reminders queued</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {runs.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>{fmtDateTime(r.startedAt)}</TableCell>
                    <TableCell>{fmtDateTime(r.completedAt)}</TableCell>
                    <TableCell>
                      <Badge variant={r.trigger === 'Manual' ? 'default' : 'outline'}>
                        {r.trigger}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">{r.remindersQueued}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            <BellRing className="mr-2 inline h-4 w-4" />
            Dispatched in the last 14 days
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {log.length === 0 ? (
            <EmptyState
              title="Nothing dispatched"
              description="No discipline reminders have gone out recently."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Sent</TableHead>
                  <TableHead>What</TableHead>
                  <TableHead>Reference</TableHead>
                  <TableHead>Due</TableHead>
                  <TableHead>Days</TableHead>
                  <TableHead>Tier</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {log.map((entry) => (
                  <TableRow key={entry.id}>
                    <TableCell>{fmtDateTime(entry.dispatchedAt)}</TableCell>
                    <TableCell>
                      <div>{KIND_LABELS[entry.kind] ?? entry.kind}</div>
                      <div className="text-xs text-muted-foreground">{entry.itemType}</div>
                    </TableCell>
                    <TableCell>
                      <Link
                        href={targetHref(entry.kind, entry.entityId)}
                        className="font-medium hover:underline"
                      >
                        {entry.reference}
                      </Link>
                    </TableCell>
                    <TableCell>{fmtDate(entry.dueDate)}</TableCell>
                    <TableCell>
                      <DaysCell days={entry.daysRemaining} />
                    </TableCell>
                    <TableCell>
                      <TierCell tier={entry.escalationTier} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <div className="space-y-2 text-xs text-muted-foreground">
        <p>
          A reminder carries a case or grievance number and a date, and nothing else — no name, no
          allegation, no grievance statement. Notifications travel further than the records they are
          about; open the record to see the detail.
        </p>
        <p>
          Escalation tiers: 1 is up to a week overdue and goes to HR; tiers 2 and 3 also reach
          administrators, because a disciplinary deadline nobody has actioned for a month is an
          institutional failure rather than an HR task. Muting a reminder is done by deactivating its
          notification topic — the sweep will not recreate or reactivate one.
        </p>
        <p>
          Obligations more than 90 days past due are not chased. At that point a nightly nudge changes
          nothing and would bury the items somebody can still act on; the breach stays on the queues
          and on the case, which is where a historical breach belongs.
        </p>
      </div>
    </div>
  );
}
