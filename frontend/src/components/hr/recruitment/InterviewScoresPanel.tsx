'use client';

import { useQueries } from '@tanstack/react-query';
import { ClipboardCheck, Loader2 } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { humanizeEnum } from '@/lib/hr/attendance-format';
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
 */
export function InterviewScoresPanel({ interview }: { interview: JobInterviewDetail }) {
  const results = useQueries({
    queries: interview.interviewees.map((ie) => ({
      queryKey: ['hr', 'interview-scores', ie.id],
      queryFn: () => jobInterviewService.getScoreSummaries(ie.id),
    })),
  });

  const loading = results.some((r) => r.isLoading);

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

        return (
          <Card key={candidate.id}>
            <CardHeader>
              <CardTitle className="text-base">{candidate.candidateName}</CardTitle>
              <CardDescription>
                {cards.length === 0
                  ? 'No scorecards yet.'
                  : `${cards.length} scorecard${cards.length === 1 ? '' : 's'}, ${signed} signed off.`}
              </CardDescription>
            </CardHeader>
            <CardContent className="p-0">
              {cards.length === 0 ? (
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
