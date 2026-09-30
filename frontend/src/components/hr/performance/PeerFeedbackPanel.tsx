'use client';

import { useQuery } from '@tanstack/react-query';
import { MessagesSquare } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { formatDate } from '@/lib/hr/attendance-format';
import { formatActualAgainstTarget, formatCriterionScore } from '@/types/hr/appeals';
import { performanceAppraisalService } from '@/services/hr/appraisal-run.service';

/**
 * Every peer's feedback on one appraisal, for the manager.
 *
 * **Managers always see who said what**, whatever the cycle's anonymity setting — that setting
 * governs what the *appraisee* sees, and conflating the two would either hide information the
 * manager needs or leak it to the person being reviewed. The banner says which it is, so a
 * manager knows before quoting a comment back in a conversation.
 *
 * Unsubmitted peers are listed too, with no scores. Knowing who has not responded is the point
 * of the count at the top.
 *
 * When the cycle's `showPeerScoresToManager` is off, the server withholds every peer's scores and
 * comments until the manager has submitted their own evaluation (closure B2) — the names and the
 * submitted count stay, so the manager can still chase a missing response.
 */
export function PeerFeedbackPanel({ appraisalId }: { appraisalId: string }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'manager-peer-evaluations', appraisalId],
    queryFn: () => performanceAppraisalService.getPeerEvaluationReview(appraisalId),
    enabled: !!appraisalId,
    retry: false,
  });

  if (isLoading) return <Skeleton className="h-48 w-full" />;

  if (isError || !data) {
    return (
      <Card>
        <CardContent className="p-0">
          <EmptyState
            icon={MessagesSquare}
            title="No peer feedback"
            description="No peers have been approved for this appraisal yet."
          />
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardContent className="flex flex-wrap items-center justify-between gap-3 p-4 text-sm">
          <span>
            <span className="font-medium tabular-nums">
              {data.submittedEvaluations} of {data.totalPeerEvaluators}
            </span>{' '}
            peer evaluations submitted
          </span>
          <span className="text-muted-foreground">
            {data.isAnonymous
              ? 'Anonymous to the employee — you can see the names, they cannot.'
              : 'The employee will see this feedback attributed to each peer.'}
          </span>
          {data.scoresWithheld && (
            <span className="w-full text-muted-foreground">
              This cycle shows you the peers&apos; scores and comments once you have submitted your
              own evaluation.
            </span>
          )}
        </CardContent>
      </Card>

      {data.peerEvaluations.length === 0 ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={MessagesSquare}
              title="No peers assigned"
              description="Approve some peer nominations to have feedback collected."
            />
          </CardContent>
        </Card>
      ) : (
        data.peerEvaluations.map((peer) => (
          <Card key={peer.evaluationId}>
            <CardHeader>
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <CardTitle className="text-base">{peer.evaluatorName}</CardTitle>
                  <p className="mt-0.5 text-sm text-muted-foreground">
                    {peer.evaluatorPosition ?? '—'}
                    {peer.evaluatorEmployeeNumber ? ` · ${peer.evaluatorEmployeeNumber}` : ''}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  {peer.isSubmitted ? (
                    <>
                      <Badge variant="default">Submitted {formatDate(peer.submittedDate)}</Badge>
                      {peer.totalScore != null && (
                        <Badge variant="outline" className="tabular-nums">
                          {Number(peer.totalScore).toFixed(1)}
                        </Badge>
                      )}
                    </>
                  ) : (
                    <Badge variant="secondary">Not submitted</Badge>
                  )}
                </div>
              </div>
            </CardHeader>
            {peer.isSubmitted && peer.criterionScores.length > 0 && (
              <CardContent className="p-0">
                {/* Every criterion the peer scored — a KPI or goal row too, where the cycle lets
                    peers score them (performance closure lane D). This listed competencies only,
                    at weight 0, beside a KPI table that was always empty. */}
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Criterion</TableHead>
                      <TableHead className="w-20 text-right">Weight</TableHead>
                      <TableHead className="w-24 text-right">Score</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {peer.criterionScores.map((row) => {
                      const against =
                        row.scoringMethod === 'Measured'
                          ? formatActualAgainstTarget(row.actualValue, row.targetValue, row.unit)
                          : null;
                      return (
                        <TableRow key={row.criterionScoreId}>
                          <TableCell>
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="font-medium">{row.itemName || '—'}</span>
                              <Badge variant="secondary">{row.itemType}</Badge>
                            </div>
                            {row.sectionName && (
                              <div className="text-xs text-muted-foreground">{row.sectionName}</div>
                            )}
                            {against && (
                              <div className="text-xs text-muted-foreground tabular-nums">
                                Measured: {against}
                              </div>
                            )}
                            {row.comments && (
                              <div className="mt-1 whitespace-pre-wrap text-xs text-muted-foreground">
                                {row.comments}
                              </div>
                            )}
                          </TableCell>
                          <TableCell className="text-right tabular-nums">{row.weight}%</TableCell>
                          <TableCell className="text-right tabular-nums">
                            {formatCriterionScore(row.score, row.scoringMethod)}
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </CardContent>
            )}
          </Card>
        ))
      )}
    </div>
  );
}
