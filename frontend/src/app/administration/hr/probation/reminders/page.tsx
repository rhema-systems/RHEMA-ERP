'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, BellRing, CalendarSearch, Loader2, Play, UserX } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
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
import { probationReminderService } from '@/services/hr/probation.service';
import type { ProbationReminderKind } from '@/types/hr/probation';
import { toast } from 'sonner';

const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const KIND_LABEL: Record<ProbationReminderKind, string> = {
  ConfirmationFormDue: 'Confirmation form due',
  ProbationEndingSoon: 'Probation ending soon',
  ProbationOverdue: 'Probation overdue',
  ReviewOverdue: 'Review overdue',
  ReviewUnacknowledged: 'Review not acknowledged',
};

/** Tier 0 is a countdown, 1–3 are escalating overruns. Colour follows meaning, not order. */
const tierTone = (tier: number) =>
  tier === 0
    ? 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200'
    : tier === 1
      ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200'
      : 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200';

/**
 * The probation reminder engine (FR-HR-032 month-5 form, FR-HR-140 expiry notice).
 *
 * ⚠ **Preview is not a dry run of "today" — it takes a date.** A probation's end is months away, so
 * "what lands next month?" is the question HR actually has, and it is the only way to see the
 * ladder without waiting for the calendar. A preview claims no dedupe key, so looking ahead never
 * robs the real sweep of a reminder.
 */
export default function ProbationRemindersPage() {
  const qc = useQueryClient();
  const [asOf, setAsOf] = useState('');

  const { data: preview, isFetching: previewing } = useQuery({
    queryKey: ['probation-reminder-preview', asOf],
    queryFn: () => probationReminderService.preview(asOf || undefined),
  });

  const { data: runs } = useQuery({
    queryKey: ['probation-reminder-runs'],
    queryFn: () => probationReminderService.getRuns(20),
  });

  const { data: log } = useQuery({
    queryKey: ['probation-reminder-log'],
    queryFn: () => probationReminderService.getLog(14),
  });

  const run = useMutation({
    mutationFn: () => probationReminderService.run(),
    onSuccess: (result) => {
      const kinds = Object.entries(result.byKind)
        .map(([k, n]) => `${n} ${KIND_LABEL[k as ProbationReminderKind] ?? k}`)
        .join(', ');
      // Saying "0 queued" plainly matters: a sweep that finds nothing is a healthy outcome, and a
      // silent success would look identical to a sweep that never ran.
      toast.success(
        result.remindersQueued === 0
          ? 'Sweep ran — nothing new to send'
          : `Sweep ran — ${result.remindersQueued} queued (${kinds})`,
      );
      void qc.invalidateQueries({ queryKey: ['probation-reminder-runs'] });
      void qc.invalidateQueries({ queryKey: ['probation-reminder-log'] });
      void qc.invalidateQueries({ queryKey: ['probation-reminder-preview'] });
    },
    onError: (e: unknown) => toast.error(e instanceof Error ? e.message : 'Refused'),
  });

  const unrouted = (preview ?? []).filter(
    (p) => p.kind === 'ConfirmationFormDue' && !p.routedToEmployeeId,
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Probation reminders"
        description="The month-5 confirmation form, expiry notices, and the review queues."
        backHref="/administration/hr"
        actions={
          <Button onClick={() => run.mutate()} disabled={run.isPending}>
            {run.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Play className="mr-2 h-4 w-4" />
            )}
            Run the sweep now
          </Button>
        }
      />

      {unrouted.length > 0 && (
        // ⚠ The case the engine deliberately surfaces rather than suppresses: work that is due and
        // has nobody to do it. Hiding it would make an unconfigured tenant look quiet.
        <Card className="border-destructive">
          <CardContent className="flex flex-wrap items-center gap-3 p-4">
            <UserX className="h-5 w-5 text-destructive" />
            <div className="flex-1">
              <p className="font-medium">
                {unrouted.length} confirmation form{unrouted.length === 1 ? '' : 's'} with no
                confirming authority
              </p>
              <p className="text-sm text-muted-foreground">
                These are due but have nobody to route to.
              </p>
            </div>
            <Button variant="outline" size="sm" asChild>
              <Link href="/administration/hr/probation/confirming-authorities">Set authorities</Link>
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">What would fire</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-end gap-3">
            <div className="space-y-2">
              <Label htmlFor="asof">As at</Label>
              <Input
                id="asof"
                type="date"
                value={asOf}
                onChange={(e) => setAsOf(e.target.value)}
                className="w-48"
              />
            </div>
            <p className="pb-2 text-sm text-muted-foreground">
              {asOf
                ? `Showing what a sweep on ${fmtDate(asOf)} would queue. Nothing is sent.`
                : 'Showing what a sweep would queue today. Nothing is sent.'}
            </p>
            {asOf && (
              <Button variant="ghost" size="sm" className="pb-2" onClick={() => setAsOf('')}>
                Back to today
              </Button>
            )}
          </div>

          {previewing ? (
            <div className="flex justify-center p-8">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : !preview || preview.length === 0 ? (
            <EmptyState
              icon={CalendarSearch}
              title="Nothing due"
              description="No probation or review needs chasing at this date."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>What</TableHead>
                  <TableHead>Who / reference</TableHead>
                  <TableHead>Due</TableHead>
                  <TableHead className="text-right">Days</TableHead>
                  <TableHead>Routed to</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {preview.map((p) => (
                  <TableRow key={p.dedupeKey}>
                    <TableCell>
                      <Badge className={tierTone(p.escalationTier)} variant="secondary">
                        {KIND_LABEL[p.kind] ?? p.kind}
                      </Badge>
                      {p.escalationTier > 0 && (
                        <span className="ml-2 text-xs text-muted-foreground">
                          tier {p.escalationTier}
                        </span>
                      )}
                    </TableCell>
                    <TableCell>
                      <Link
                        href={`/hr/probation/${p.probationPeriodId}`}
                        className="hover:underline"
                      >
                        {p.reference}
                      </Link>
                    </TableCell>
                    <TableCell>{fmtDate(p.dueDate)}</TableCell>
                    <TableCell className="text-right">
                      {p.daysRemaining < 0 ? (
                        <span className="text-red-600">{Math.abs(p.daysRemaining)}d over</span>
                      ) : (
                        `${p.daysRemaining}d`
                      )}
                    </TableCell>
                    <TableCell>
                      {p.kind !== 'ConfirmationFormDue' ? (
                        <span className="text-muted-foreground">—</span>
                      ) : p.routedToName ? (
                        p.routedToName
                      ) : (
                        <span className="inline-flex items-center gap-1 text-sm text-destructive">
                          <AlertTriangle className="h-3 w-3" />
                          nobody
                        </span>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Tabs defaultValue="log">
        <TabsList>
          <TabsTrigger value="log">Sent ({log?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="runs">Runs ({runs?.length ?? 0})</TabsTrigger>
        </TabsList>

        <TabsContent value="log">
          <Card>
            <CardContent className="p-0">
              {!log || log.length === 0 ? (
                <EmptyState
                  icon={BellRing}
                  title="Nothing sent in the last fortnight"
                  description="Reminders appear here once a sweep queues them."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Sent</TableHead>
                      <TableHead>What</TableHead>
                      <TableHead>Reference</TableHead>
                      <TableHead>Due</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {log.map((l) => (
                      <TableRow key={l.id}>
                        <TableCell>{fmtDateTime(l.dispatchedAt)}</TableCell>
                        <TableCell>{KIND_LABEL[l.kind] ?? l.kind}</TableCell>
                        <TableCell>
                          <Link href={`/hr/probation/${l.probationPeriodId}`} className="hover:underline">
                            {l.reference}
                          </Link>
                        </TableCell>
                        <TableCell>{fmtDate(l.dueDate)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="runs">
          <Card>
            <CardContent className="p-0">
              {!runs || runs.length === 0 ? (
                <EmptyState
                  icon={Play}
                  title="No sweeps recorded"
                  description="The daily sweep records itself here, whether or not it finds anything."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Started</TableHead>
                      <TableHead>Trigger</TableHead>
                      <TableHead className="text-right">Queued</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {runs.map((r) => (
                      <TableRow key={r.id}>
                        <TableCell>{fmtDateTime(r.startedAt)}</TableCell>
                        <TableCell>{r.trigger}</TableCell>
                        {/* A run that queued nothing is still a run, and seeing it is how you tell
                            "quiet" from "not running". */}
                        <TableCell className="text-right">{r.remindersQueued}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
