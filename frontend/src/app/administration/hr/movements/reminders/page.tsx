'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Play, BellRing } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
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

interface RunResult {
  runId: string;
  remindersQueued: number;
  byKind: Record<string, number>;
}

/** What each sweep kind means, in the words someone reading the log would use. */
const KIND_LABELS: Record<string, string> = {
  TemporaryAssignmentEnding: 'Temporary assignment ending',
  TemporaryReturnOverdue: 'Return not processed',
  ApprovalOverdue: 'Approval overdue',
  AwaitingImplementation: 'Effective date reached, not implemented',
  EmployeeAcceptancePending: 'Awaiting the employee',
  EmployeeAcceptanceOverdue: 'Employee acceptance overdue',
  ActingAppointmentEnding: 'Acting appointment ending',
};

/**
 * Operates the staff-movement reminder sweep and shows what it has sent.
 *
 * The sweep runs daily on its own; run-now exists so HR can force one after a bulk edit rather than
 * waiting for tomorrow. Repeating it is safe — each reminder is deduped per item, due date and
 * ladder rung, so a second run today sends nothing new.
 */
export default function MovementRemindersPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [lastRun, setLastRun] = useState<RunResult | null>(null);

  const { data: runs = [], isLoading } = useQuery({
    queryKey: ['hr', 'movement-reminders', 'runs'],
    queryFn: () => apiService.get<ReminderRun[]>('/staff-movements/reminders/runs', { count: 20 }),
  });

  const { data: log = [] } = useQuery({
    queryKey: ['hr', 'movement-reminders', 'log'],
    queryFn: () => apiService.get<ReminderLogEntry[]>('/staff-movements/reminders/log', { days: 14 }),
  });

  const run = useMutation({
    mutationFn: () => apiService.post<RunResult>('/staff-movements/reminders/run', {}),
    onSuccess: (result) => {
      setLastRun(result);
      toast({
        title: 'Sweep complete',
        description:
          result.remindersQueued === 0
            ? 'Nothing new to send — everything due has already been notified.'
            : `${result.remindersQueued} reminder(s) queued.`,
      });
      queryClient.invalidateQueries({ queryKey: ['hr', 'movement-reminders'] });
    },
    onError: (error: any) =>
      toast({ title: 'Sweep failed', description: error?.message, variant: 'destructive' }),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Movement reminders"
        description="The daily sweep over movement dates: assignments ending, returns and approvals overdue, effective dates reached."
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
              description="No movement reminders have gone out recently."
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
                        href={`/hr/movements/${entry.entityId}`}
                        className="font-medium hover:underline"
                      >
                        {entry.reference}
                      </Link>
                    </TableCell>
                    <TableCell>{fmtDate(entry.dueDate)}</TableCell>
                    <TableCell>
                      {entry.daysRemaining < 0
                        ? `${Math.abs(entry.daysRemaining)} overdue`
                        : `${entry.daysRemaining} left`}
                    </TableCell>
                    <TableCell>
                      {entry.escalationTier > 0 ? (
                        <Badge variant={entry.escalationTier >= 2 ? 'destructive' : 'outline'}>
                          Tier {entry.escalationTier}
                        </Badge>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <p className="text-xs text-muted-foreground">
        Escalation tiers: 1 is up to a week overdue and goes to HR; tiers 2 and 3 also reach
        administrators, because a movement nobody has actioned for a month is not an HR task any
        more. Muting a reminder is done by deactivating its notification topic — the sweep will not
        recreate or reactivate one.
      </p>
    </div>
  );
}
