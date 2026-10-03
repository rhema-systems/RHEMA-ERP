'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, BellRing, Play, Eye, History } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { useToast } from '@/hooks/use-toast';
import { travelRemindersService } from '@/services/hr/travel-reminders.service';

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

/**
 * The kinds, their windows and who each reaches. Stated so an admin can see why something is or isn't due.
 *
 * ⚠ Worded from `StaffTravelReminderService` (its rungs, `TierFor` and the D-50 constants), not from intent. An
 * expiry is chased at 90, 30 and 7 days — an item first seen inside a rung gets that rung's notice only — and after
 * the date once in each of the windows 1–7, 8–30 and 31–90 days late. Before travel final closure lane 8 (slice 8b)
 * every kind went to the HR role in the app only, and a document got one notice before it lapsed (finding F2).
 */
const HORIZONS = [
  {
    kind: 'Travel document expiring',
    detail: '90, 30 and 7 days before expiry, then after it lapses, after a week and after a month — to its owner',
  },
  {
    kind: 'Visa expiring',
    detail: 'the same rungs, for a visa on a trip still to happen or under way — to the traveller',
  },
  {
    kind: 'Advance settlement overdue',
    detail: 'once its settlement deadline has passed, then after a week and after a month — to the traveller and the desk',
  },
  { kind: 'Trip departing', detail: 'once, when an approved trip is 14 days or less away — to the traveller' },
  {
    kind: 'Visa missing',
    detail: 'once, when an approved trip that needs a visa is 14 days or less away with none approved — to the traveller and the desk',
  },
  {
    kind: 'Approval waiting',
    detail: 'after 5 days, then after a week and a month — to the people its current stage is asking (the desk when nobody can be)',
  },
  {
    kind: 'Approval escalated',
    detail: 'once, when a request still waiting is 3 days or less from departure, or past it — to the desk',
  },
  {
    kind: 'Briefing unacknowledged',
    detail: 'once, when a trip 7 days or less away has a risk assessment the traveller has not acknowledged — to the traveller',
  },
  {
    kind: 'Claim window closing',
    detail: 'once, 7 days before a completed trip\'s claim window closes with no claim submitted — to the traveller',
  },
  {
    kind: 'Claim window passed',
    detail: 'once, when it has closed with a claim not submitted or advance cash still out — to the desk',
  },
];

/**
 * The travel reminder sweep: what it has done, and what it would do next.
 *
 * ⚠ **Preview is the point of this screen.** Every date in the engine is server-stamped, so without
 * an `asOf` seam an administrator can only ever see what happens to be true today — they cannot ask
 * "what will fire next month?" or check that a horizon is set sensibly before it matters. Area 9
 * had to add the same seam after the fact; travel has it from the start.
 *
 * Running a sweep by hand is safe to repeat. The engine dedupes on a unique key, so a second run
 * queues nothing new and reports what it declined to send again — `alreadySent` is not a failure
 * count.
 */
export default function TravelRemindersPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [asOf, setAsOf] = useState('');

  const {
    data: preview, isLoading: previewLoading, isError: previewFailed, error: previewError,
  } = useQuery({
    queryKey: ['travel-reminder-preview', asOf],
    queryFn: () => travelRemindersService.preview(asOf || undefined),
  });

  const { data: runs, isError: runsFailed, error: runsError } = useQuery({
    queryKey: ['travel-reminder-runs'],
    queryFn: () => travelRemindersService.getRuns(20),
  });

  const { data: log, isError: logFailed, error: logError } = useQuery({
    queryKey: ['travel-reminder-log'],
    queryFn: () => travelRemindersService.getLog(14),
  });

  const run = useMutation({
    mutationFn: () => travelRemindersService.run(),
    onSuccess: async (result) => {
      const notes = [
        result.retried > 0 ? `${result.retried} left unsent by an earlier sweep were sent now.` : '',
        result.alreadySent > 0 ? `${result.alreadySent} were already sent and were not repeated.` : '',
      ].filter(Boolean);
      toast({
        title: `Sweep complete — ${result.remindersQueued} sent`,
        description: notes.length ? notes.join(' ') : undefined,
      });
      await queryClient.invalidateQueries({ queryKey: ['travel-reminder-runs'] });
      await queryClient.invalidateQueries({ queryKey: ['travel-reminder-log'] });
      await queryClient.invalidateQueries({ queryKey: ['travel-reminder-preview'] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'The sweep could not run', description: e.message }),
  });

  const items = preview ?? [];
  const wouldFire = items.filter((i) => !i.alreadySent);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Travel reminders"
        description="Expiring passports and visas, overdue advances, departures, waiting approvals and closing claim windows — each to the people who act on it."
        backHref="/administration/hr"
        actions={
          <Button onClick={() => run.mutate()} disabled={run.isPending}>
            {run.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Play className="mr-2 h-4 w-4" />
            )}
            Run a sweep now
          </Button>
        }
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">What the sweep chases</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-2 md:grid-cols-2">
          {HORIZONS.map((h) => (
            <p key={h.kind} className="text-sm">
              <span className="font-medium">{h.kind}</span>
              <span className="text-muted-foreground"> — {h.detail}</span>
            </p>
          ))}
          <p className="text-xs text-muted-foreground md:col-span-2">
            The 90-day document horizon is our figure, not a TDC requirement: it is the shortest
            notice that still allows a Ghanaian passport renewal. A 30-day warning about a document
            that takes six weeks to replace is not a warning.
          </p>
          {/* Lane 8, slice 8b: who is told, as the sweep sends it — before it, every kind went to the HR role in the
              app only (finding F2), and this card said so. */}
          <p className="text-xs text-muted-foreground md:col-span-2">
            The traveller and the approvers are told in the app and by email; the desk (the HR role) in the app.
            A traveller with no login is emailed at the address on their employee record; with neither, the desk is
            told to tell them. Each reminder is sent once — a run that stopped before sending one leaves it for the next.
            The words and recipients of each are on Administration → Notification Topics, under StaffTravel.
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <Eye className="h-4 w-4" />
            What would fire
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-end gap-3">
            <div className="space-y-2">
              <Label htmlFor="as-of">Evaluate as if it were</Label>
              <Input
                id="as-of"
                type="date"
                value={asOf}
                onChange={(e) => setAsOf(e.target.value)}
                className="w-52"
              />
            </div>
            {asOf && (
              <Button variant="ghost" size="sm" onClick={() => setAsOf('')}>
                Back to today
              </Button>
            )}
            <p className="text-xs text-muted-foreground">
              Nothing is sent by previewing. Set a future date to see what is coming.
            </p>
          </div>

          {previewLoading ? (
            <div className="flex items-center justify-center p-6">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : previewFailed && !preview ? (
            <TravelQueryError error={previewError} what="the reminder preview" />
          ) : items.length === 0 ? (
            <EmptyState
              icon={BellRing}
              title="Nothing due"
              description={
                asOf
                  ? 'No reminder would fire on that date.'
                  : 'No reminder would fire today.'
              }
            />
          ) : (
            <>
              <p className="text-sm">
                <span className="font-medium">{wouldFire.length}</span> would be sent
                {items.length !== wouldFire.length && (
                  <span className="text-muted-foreground">
                    {' '}· {items.length - wouldFire.length} already sent and would be skipped
                  </span>
                )}
              </p>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Kind</TableHead>
                    <TableHead>Record</TableHead>
                    <TableHead>Due</TableHead>
                    <TableHead className="w-24">Days</TableHead>
                    <TableHead className="w-20">Tier</TableHead>
                    <TableHead>Sent to</TableHead>
                    <TableHead className="w-32" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {items.map((i) => (
                    <TableRow key={i.dedupeKey}>
                      <TableCell className="font-medium">{humanize(i.kind)}</TableCell>
                      <TableCell>{i.reference}</TableCell>
                      <TableCell className="whitespace-nowrap">{fmtDate(i.dueDate)}</TableCell>
                      <TableCell>{i.daysRemaining}</TableCell>
                      <TableCell>{i.escalationTier}</TableCell>
                      <TableCell>{(i.sentTo ?? []).join(', ') || '—'}</TableCell>
                      <TableCell>
                        {i.alreadySent && <Badge variant="outline">Already sent</Badge>}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <History className="h-4 w-4" />
            Recent sweeps
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {runsFailed && !runs ? (
            <div className="p-4">
              <TravelQueryError error={runsError} what="the recent sweeps" />
            </div>
          ) : (runs ?? []).length === 0 ? (
            <EmptyState title="No sweeps yet" description="Nothing has run for this tenant." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Started</TableHead>
                  <TableHead>Finished</TableHead>
                  <TableHead>Trigger</TableHead>
                  <TableHead className="text-right">Queued</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(runs ?? []).map((r) => (
                  <TableRow key={r.id}>
                    <TableCell className="whitespace-nowrap">{fmtDateTime(r.startedAt)}</TableCell>
                    <TableCell className="whitespace-nowrap">{fmtDateTime(r.completedAt)}</TableCell>
                    <TableCell>{r.trigger}</TableCell>
                    <TableCell className="text-right">{r.remindersQueued}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Sent in the last 14 days</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {logFailed && !log ? (
            <div className="p-4">
              <TravelQueryError error={logError} what="the reminder log" />
            </div>
          ) : (log ?? []).length === 0 ? (
            <EmptyState title="Nothing sent" description="No reminders in the last fortnight." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>When</TableHead>
                  <TableHead>Kind</TableHead>
                  <TableHead>Record</TableHead>
                  <TableHead>Due</TableHead>
                  <TableHead className="w-20">Tier</TableHead>
                  <TableHead>Sent</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(log ?? []).map((l) => (
                  <TableRow key={l.id}>
                    <TableCell className="whitespace-nowrap">{fmtDateTime(l.createdAt)}</TableCell>
                    <TableCell className="font-medium">{humanize(l.kind)}</TableCell>
                    <TableCell>{l.reference}</TableCell>
                    <TableCell className="whitespace-nowrap">{fmtDate(l.dueDate)}</TableCell>
                    <TableCell>{l.escalationTier}</TableCell>
                    <TableCell className="whitespace-nowrap">
                      {l.publishedAt ? fmtDateTime(l.publishedAt) : <Badge variant="outline">Not yet — next sweep</Badge>}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
