'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CalendarCheck,
  CalendarDays,
  CheckCircle2,
  ClipboardList,
  Clock,
  Loader2,
  MapPin,
  Printer,
  Users,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { jobInterviewService } from '@/services/hr/interviews.service';
import { useAuth } from '@/hooks/use-auth';
import type {
  PanelistScorecardCandidate,
  PanelistScorecardState,
  PanelistScorecardWorklistItem,
} from '@/types/hr/interviews';

const clock = (t?: string | null) => (t ? t.slice(0, 5) : null);

const longDate = (iso: string) => {
  const d = new Date(`${iso}T00:00:00`);
  return Number.isNaN(d.getTime())
    ? iso
    : d.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'long', year: 'numeric' });
};

/**
 * How each card state reads on the row. ⚠ `Draft` is deliberately not a success colour: a draft is
 * private, it does not count as filed, and a panelist who reads it as "done" stops.
 */
const STATE: Record<PanelistScorecardState, { label: string; variant: 'default' | 'secondary' | 'outline' }> = {
  NotStarted: { label: 'Not started', variant: 'outline' },
  Draft: { label: 'Draft — not filed', variant: 'secondary' },
  Saved: { label: 'Filed', variant: 'default' },
  SignedOff: { label: 'Signed off', variant: 'default' },
};

/**
 * A panelist's own diary and scorecard worklist (round 4, lane F5).
 *
 * ⚠ **This is the *only* interview list a non-HR employee can read.** The schedule at
 * `/hr/recruitment/interviews` is HR's and answers 403 for a panelist — measured: "Only HR can list
 * interviews by status" and "Only HR can list the interview schedule" — so their route into a
 * session has to start here. It is backed by `me/scorecard-worklist`, which takes the employee from
 * the token; the id-bearing twin would mean the client fetching its own employee id and passing it
 * back, which is the shape that produced this module's authorization holes.
 *
 * **What changed in lane F5, and why.** This page used to list interview *numbers* and a role, while
 * describing itself as "the scorecards you owe" — and the only way to reach a scorecard was to open
 * HR's interview desk at `/hr/recruitment/interviews/{id}`, find the Candidates tab and press a
 * button on a row. Three clicks and a tab, through a screen built for the person who *scheduled* the
 * interview. It now shows the candidates, their slot times and the state of each of the caller's own
 * cards, and links straight to `/me/panel/{interviewId}/score/{intervieweeId}`.
 *
 * ⚠ That portal scorecard renders the **same** `InterviewScorecardForm` as HR's route. The earlier
 * decision against a portal scorecard was that it would mean "two scorecard forms against one upsert
 * endpoint, which is how a scorecard gets silently replaced" — an objection to two implementations,
 * not two routes. The form was extracted first.
 *
 * ⚠ Only the caller's own cards appear here. Scoring is blind until they file their own for a
 * candidate, and a worklist showing a colleague's mark would be the anchoring problem wearing a
 * different hat.
 *
 * An employee on no panels sees an empty list, not an error.
 */
export default function MyInterviewPanelPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const { hasAnyPermission } = useAuth();
  // Only shown to someone the diary would actually serve — a plain panelist gets a 403 there, and
  // a link that 403s is worse than no link.
  const canSeeSchedule = hasAnyPermission(['HR.Recruitment.Write', 'HR.Recruitment.Admin']);

  const worklist = useQuery({
    queryKey: ['hr', 'my-scorecard-worklist'],
    queryFn: () => jobInterviewService.getMyScorecardWorklist(),
  });

  const confirm = useMutation({
    mutationFn: (panelistId: string) => jobInterviewService.confirmPanelist(panelistId),
    onSuccess: () => {
      toast({ title: 'Assignment confirmed' });
      queryClient.invalidateQueries({ queryKey: ['hr', 'my-scorecard-worklist'] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'my-panel-slots'] });
    },
    onError: (error: any) =>
      toast({ title: 'Could not confirm', description: error?.message, variant: 'destructive' }),
  });

  const rows = useMemo(() => worklist.data ?? [], [worklist.data]);

  // Sessions still owing a scorecard come first — that is what a worklist is for. Within each group
  // the server's ordering (newest first) is kept.
  const { outstanding, settled } = useMemo(() => {
    const owed: PanelistScorecardWorklistItem[] = [];
    const done: PanelistScorecardWorklistItem[] = [];
    for (const row of rows) (row.isOpen && row.outstandingCount > 0 ? owed : done).push(row);
    return { outstanding: owed, settled: done };
  }, [rows]);

  const totalOwed = outstanding.reduce((n, row) => n + row.outstandingCount, 0);

  if (worklist.isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* G-9.6 (2026-09-15): the two halves of "interviews I am involved in" point at each other.
          HR's diary already linked here; this is the return trip, for the HR user who also sits on
          panels and had to remember two places to look.

          A single merged screen was considered and not built: the two lists answer different
          questions with different authority — this one is the caller's own commitments, which any
          employee may see, while the diary is every session in the organisation and is gated on
          recruitment permissions. Merging them would mean one screen whose contents silently change
          shape with the viewer's role, which is harder to reason about than two honest screens that
          link. */}
      <PageHeader
        title="My interview panel"
        description={
          totalOwed > 0
            ? `${totalOwed} scorecard${totalOwed === 1 ? '' : 's'} still to file.`
            : 'Sessions you are sitting on, and the scorecards you owe.'
        }
        backHref="/me"
        actions={
          canSeeSchedule ? (
            <Button variant="outline" asChild>
              <Link href="/hr/recruitment/interviews">
                <CalendarDays className="mr-1.5 h-4 w-4" />
                HR&apos;s full schedule
              </Link>
            </Button>
          ) : undefined
        }
      />

      {rows.length === 0 ? (
        <Card>
          <CardContent className="py-10">
            <EmptyState
              icon={Users}
              title="No panel assignments"
              description="You are not on any interview panels at the moment."
            />
          </CardContent>
        </Card>
      ) : (
        <>
          {outstanding.map((row) => (
            <SessionCard key={row.panelistId} row={row} onConfirm={confirm} />
          ))}

          {settled.length > 0 && (
            <div className="space-y-4">
              <h2 className="pt-2 text-sm font-medium text-muted-foreground">
                Nothing outstanding
              </h2>
              {settled.map((row) => (
                <SessionCard key={row.panelistId} row={row} onConfirm={confirm} />
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}

function SessionCard({
  row,
  onConfirm,
}: {
  row: PanelistScorecardWorklistItem;
  onConfirm: ReturnType<typeof useMutation<void, unknown, string>>;
}) {
  return (
    <Card>
      <CardHeader className="gap-3 space-y-0 pb-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="text-base font-semibold">{row.jobTitle}</h2>
              <Badge variant="outline">{row.interviewNumber}</Badge>
              <Badge variant="secondary">{humanizeEnum(row.role)}</Badge>
              {row.isRequired && (
                <span className="text-xs text-muted-foreground">attendance required</span>
              )}
              {!row.isOpen && <Badge variant="outline">{humanizeEnum(row.status)}</Badge>}
            </div>
            <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
              <span className="flex items-center gap-1.5">
                <CalendarDays className="h-3.5 w-3.5" />
                {longDate(row.scheduledDate)}
              </span>
              <span className="flex items-center gap-1.5">
                <Clock className="h-3.5 w-3.5" />
                {clock(row.startTime)} – {clock(row.endTime)}
              </span>
              {row.locationOrLink && (
                <span className="flex items-center gap-1.5">
                  <MapPin className="h-3.5 w-3.5" />
                  {row.locationOrLink}
                </span>
              )}
              <span>Round {row.round}</span>
              <span>{humanizeEnum(row.mode)}</span>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {!row.isConfirmed && row.isOpen && (
              <Button
                variant="outline"
                size="sm"
                disabled={onConfirm.isPending}
                onClick={() => onConfirm.mutate(row.panelistId)}
              >
                <CalendarCheck className="mr-1.5 h-4 w-4" />
                Confirm attendance
              </Button>
            )}
            {/* ⚠ `panelistId` is the caller's own seat, so this prints THEIR sheets — one per
                candidate — and nobody else's. */}
            <Button variant="outline" size="sm" asChild>
              <Link
                href={`/hr/recruitment/interviews/${row.interviewId}/paper?variant=ScoreSheet&panelistId=${row.panelistId}`}
                title="Your scoring sheets for this interview, to print and mark on"
              >
                <Printer className="mr-1.5 h-4 w-4" />
                Print my sheets
              </Link>
            </Button>
            <Button variant="ghost" size="sm" asChild>
              <Link href={`/hr/recruitment/interviews/${row.interviewId}`}>Session details</Link>
            </Button>
          </div>
        </div>

        {row.isConfirmed && (
          <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <CheckCircle2 className="h-3.5 w-3.5 text-green-600" />
            You have confirmed this assignment.
          </p>
        )}
      </CardHeader>

      <CardContent className="p-0">
        {row.candidates.length === 0 ? (
          <div className="px-6 pb-6">
            <p className="text-sm text-muted-foreground">
              Nobody is booked into this session yet. HR adds the candidates; your scorecards appear
              here once they do.
            </p>
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Candidate</TableHead>
                <TableHead className="w-[130px]">Your slot</TableHead>
                <TableHead className="w-[170px]">Your scorecard</TableHead>
                <TableHead className="w-[110px]">Your total</TableHead>
                <TableHead className="w-[150px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {row.candidates.map((c) => (
                <CandidateRow key={c.intervieweeId} row={row} candidate={c} />
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}

function CandidateRow({
  row,
  candidate,
}: {
  row: PanelistScorecardWorklistItem;
  candidate: PanelistScorecardCandidate;
}) {
  const state = STATE[candidate.state] ?? STATE.NotStarted;
  const slot = clock(candidate.slotStartTime);
  const slotEnd = clock(candidate.slotEndTime);

  return (
    <TableRow>
      <TableCell>
        <p className="font-medium">{candidate.candidateName}</p>
        <p className="text-xs text-muted-foreground">
          {candidate.applicationNumber}
          {candidate.candidateAttended === false && ' · recorded as a no-show'}
        </p>
      </TableCell>

      <TableCell className="text-sm">
        {slot ? (
          <span>
            {slot}
            {slotEnd ? ` – ${slotEnd}` : ''}
          </span>
        ) : (
          // The session window is the honest answer when the day was never apportioned — printing
          // the literal words "Session time" tells a panelist nothing about when to be there.
          <span className="text-muted-foreground">
            {clock(row.startTime)} – {clock(row.endTime)}
          </span>
        )}
      </TableCell>

      <TableCell>
        <Badge variant={state.variant}>{state.label}</Badge>
      </TableCell>

      <TableCell className="text-sm">
        {candidate.totalWeightedScore != null ? (
          <span>
            {Math.round(candidate.totalWeightedScore * 100) / 100}
            {candidate.recommendationName && (
              <span className="block text-xs text-muted-foreground">
                {humanizeEnum(candidate.recommendationName)}
              </span>
            )}
          </span>
        ) : (
          <span className="text-muted-foreground">—</span>
        )}
      </TableCell>

      <TableCell>
        <div className="flex justify-end">
          <Button
            variant={candidate.isComplete || !row.isOpen ? 'ghost' : 'default'}
            size="sm"
            asChild
          >
            <Link href={`/me/panel/${row.interviewId}/score/${candidate.intervieweeId}`}>
              <ClipboardList className="mr-1.5 h-4 w-4" />
              {candidate.isComplete
                ? 'View'
                : candidate.state === 'NotStarted'
                  ? 'Score'
                  : 'Continue'}
            </Link>
          </Button>
        </div>
      </TableCell>
    </TableRow>
  );
}
