'use client';

import { useQueries } from '@tanstack/react-query';
import { ClipboardCheck, EyeOff, Loader2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { useAuth } from '@/hooks/use-auth';
import { jobInterviewService } from '@/services/hr/interviews.service';
import type { JobInterviewDetail, JobInterviewScoreSummary } from '@/types/hr/interviews';

/**
 * Every panelist's scorecard for every candidate in the session, side by side.
 *
 * ⚠ **No aggregate score is shown, deliberately.** The server stores one weighted total per
 * panelist and does not combine them: there is no agreed rule for whether a chair outweighs a
 * member, or whether an unfinalised card counts. Averaging them here would invent one and make it
 * look official. The comparison a panel actually makes is card against card, which is what this is.
 *
 * Draft cards are shown alongside signed ones and marked as such — a card that has not been
 * finalised is the panelist's working note, not their verdict.
 *
 * ⚠ **Scoring is blind until you file your own** (round 4, lane F5). For a panelist who has not yet
 * filed for a candidate, the server returns only their own card — so this screen must say the view
 * is narrowed rather than report an empty list. "No scorecards yet" would be a plain lie to the one
 * person it is shown to, and it is the same shape as the orientation sessions dropdown that
 * rendered a 403 identically to an empty result. HR is never blinded.
 */
export function InterviewScoresPanel({
  interview,
  canManage = false,
}: {
  interview: JobInterviewDetail;
  /** True for HR, who see every card. A panelist sees a blinded view until they file. */
  canManage?: boolean;
}) {
  const { user } = useAuth();
  const results = useQueries({
    queries: interview.interviewees.map((ie) => ({
      queryKey: ['hr', 'interview-scores', ie.id],
      queryFn: () => jobInterviewService.getScoreSummaries(ie.id),
    })),
  });

  const loading = results.some((r) => r.isLoading);

  // The caller's own seat, if they sit on this panel. External assessors have no login, so only the
  // internal panel can ever be the viewer.
  const mySeatId = interview.panelists.find(
    (p) => p.employeeId === (user as any)?.employeeId,
  )?.id;

  if (interview.interviewees.length === 0) {
    return (
      <Card>
        <CardContent className="py-10">
          <EmptyState
            icon={ClipboardCheck}
            title="No candidates"
            description="Scorecards appear once candidates are booked into the session."
          />
        </CardContent>
      </Card>
    );
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {interview.interviewees.map((candidate, index) => {
        const cards: JobInterviewScoreSummary[] = results[index]?.data ?? [];
        const signed = cards.filter((c) => c.isFinalized).length;

        // Blinded when the viewer is a panelist on this interview who has not filed for THIS
        // candidate. The server has already narrowed the list; this only decides what to say about
        // it. ⚠ Never claim "nobody has scored" in this state — it may well be false.
        const blinded =
          !canManage && !!mySeatId && !cards.some((c) => c.internalPanelistId === mySeatId);

        return (
          <Card key={candidate.id}>
            <CardHeader>
              <CardTitle className="text-base">{candidate.candidateName}</CardTitle>
              <CardDescription>
                {blinded
                  ? 'Hidden until you file your own scorecard for this candidate.'
                  : cards.length === 0
                    ? 'No scorecards yet.'
                    : `${cards.length} scorecard${cards.length === 1 ? '' : 's'}, ${signed} signed off.`}
              </CardDescription>
            </CardHeader>
            <CardContent className="p-0">
              {blinded ? (
                <div className="px-6 pb-6">
                  <Alert>
                    <EyeOff className="h-4 w-4" />
                    <AlertDescription>
                      Scoring is blind. Whatever the rest of the panel have recorded stays hidden
                      until you file your own scorecard for {candidate.candidateName} — so your mark
                      is yours. The full set opens the moment you do, for the panel&rsquo;s own
                      comparison.
                    </AlertDescription>
                  </Alert>
                </div>
              ) : cards.length === 0 ? (
                <div className="px-6 pb-6 text-sm text-muted-foreground">
                  The panel has not scored this candidate.
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Panelist</TableHead>
                      <TableHead className="w-[120px]">Raw</TableHead>
                      <TableHead className="w-[140px]">Weighted</TableHead>
                      <TableHead className="w-[160px]">Recommendation</TableHead>
                      <TableHead className="w-[130px]">State</TableHead>
                      <TableHead>Comments</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {cards.map((card) => (
                      <TableRow key={card.id}>
                        <TableCell className="font-medium">
                          {card.internalPanelistName ?? card.externalPanelistName ?? 'Panelist'}
                          {card.externalPanelistId && (
                            <span className="ml-2 text-xs text-muted-foreground">external</span>
                          )}
                          {/* Round 4, lane F4. Shown only when the server says somebody else filed
                              it — a null source means the card predates this being tracked, which
                              is not the same claim as "the panelist typed it". */}
                          {card.scoreSource === 'PaperSheet' && (
                            <p className="mt-0.5 text-xs font-normal text-muted-foreground">
                              filed by HR on their behalf
                            </p>
                          )}
                        </TableCell>
                        <TableCell>{card.totalRawScore}</TableCell>
                        <TableCell>{card.totalWeightedScore}</TableCell>
                        <TableCell>
                          <StatusBadge status={card.recommendation} />
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={card.isFinalized ? 'Signed off' : 'Draft'} />
                        </TableCell>
                        <TableCell className="max-w-[320px] text-sm text-muted-foreground">
                          {card.comments || '—'}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        );
      })}
    </div>
  );
}
