'use client';

import { useQuery } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { CheckCircle2, Clock, RotateCcw, Scale, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate } from '@/lib/hr/attendance-format';
import { appraisalAppealService } from '@/services/hr/appeals.service';
import {
  formatActualAgainstTarget,
  formatCriterionScore,
  type AppraisalAppealStatus,
} from '@/types/hr/appeals';

/**
 * The employee's own view of their appeal, live from submission through to the verdict.
 *
 * The distinction the copy has to carry is that **Remanded is not a decision**. It means HR
 * found enough in the appeal to send it back to the manager, and the real outcome is still
 * coming — an employee reading "Remanded" with no explanation would reasonably assume they had
 * won, or lost.
 *
 * **Each contested item shows what it scored when the appeal was filed** (closure D-38) and, once
 * decided, what it scores now. While a remand is open the scores as they stand are withheld: the
 * manager's re-scoring is provisional until HR decides (the page showed it, drafts included).
 */
export default function AppealStatusPage() {
  const params = useParams<{ id: string }>();
  const appraisalId = params.id;

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'appeal-status', appraisalId],
    queryFn: () => appraisalAppealService.getAppealStatus(appraisalId),
    enabled: !!appraisalId,
    retry: false,
  });

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="space-y-6">
        <PageHeader title="Your appeal" backHref={`/me/performance/appraisals/${appraisalId}`} />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="No appeal to show"
              description={
                (error as Error)?.message ?? 'You have not filed an appeal on this appraisal.'
              }
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  const isFinal = data.status === 'Upheld' || data.status === 'Rejected';
  const changed =
    data.originalScore != null &&
    data.currentOverallScore != null &&
    Number(data.originalScore) !== Number(data.currentOverallScore);

  // The overall now: after the decision it is the outcome, changed or not; while HR has it, it is the
  // score appealed; while the manager re-evaluates it is withheld.
  const scoreNowTile = isFinal
    ? {
        label: 'Score now',
        value: data.currentOverallScore != null ? Number(data.currentOverallScore).toFixed(1) : '—',
        tone: changed ? ('success' as const) : ('default' as const),
        hint: changed ? 'Changed by the appeal' : 'Unchanged — the score you appealed stands',
      }
    : {
        label: 'Score now',
        value: data.outcomeReleased && data.currentOverallScore != null
          ? Number(data.currentOverallScore).toFixed(1)
          : '—',
        hint: data.outcomeReleased
          ? 'Not decided yet'
          : 'Withheld while your manager re-evaluates',
      };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Your appeal"
        description={`${data.appraisalNumber} · ${data.cycleName}`}
        backHref={`/me/performance/appraisals/${appraisalId}`}
        actions={
          isFinal ? (
            <Button asChild>
              <Link href={`/me/performance/appraisals/${appraisalId}/appeal-outcome`}>
                See the full outcome
              </Link>
            </Button>
          ) : undefined
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={data.status} />
        <span className="text-sm text-muted-foreground">
          Submitted {formatDate(data.submittedDate)}
        </span>
        {data.resolvedDate && (
          <span className="text-sm text-muted-foreground">
            Decided {formatDate(data.resolvedDate)}
            {data.reviewedByName ? ` by ${data.reviewedByName}` : ''}
          </span>
        )}
      </div>

      <StageNote status={data.status} />

      <MetricTiles
        tiles={[
          {
            label: 'Score when you appealed',
            value: data.originalScore != null ? Number(data.originalScore).toFixed(1) : '—',
          },
          scoreNowTile,
          { label: 'Items contested', value: data.appealedItems.length },
        ]}
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">What you said</CardTitle>
        </CardHeader>
        <CardContent className="whitespace-pre-wrap text-sm">
          {data.appealReason || 'No overall reason was recorded.'}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Items you contested</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {data.appealedItems.length === 0 ? (
            <EmptyState
              icon={Scale}
              title="No itemised claims"
              description="This appeal was filed against the appraisal as a whole."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Item</TableHead>
                  <TableHead>Your reason</TableHead>
                  {/* Not shown when the cycle shows the overall only (closure B2). */}
                  {data.scoreBreakdownShown && (
                    <TableHead className="text-right">When you appealed</TableHead>
                  )}
                  {data.scoreBreakdownShown && isFinal && (
                    <TableHead className="text-right">Now</TableHead>
                  )}
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.appealedItems.map((item) => {
                  const measured = item.scoringMethod === 'Measured';
                  const actual = measured
                    ? formatActualAgainstTarget(item.actualValue, item.targetValue, item.unit)
                    : null;
                  return (
                    <TableRow key={item.itemId}>
                      <TableCell>
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-medium">{item.itemName}</span>
                          <Badge variant="secondary">{item.itemType}</Badge>
                        </div>
                        {item.sectionName && (
                          <div className="text-xs text-muted-foreground">{item.sectionName}</div>
                        )}
                      </TableCell>
                      <TableCell className="max-w-md text-sm text-muted-foreground">
                        {item.reason}
                      </TableCell>
                      {data.scoreBreakdownShown && (
                        <TableCell className="text-right tabular-nums">
                          {formatCriterionScore(item.originalScore, item.scoringMethod)}
                        </TableCell>
                      )}
                      {data.scoreBreakdownShown && isFinal && (
                        <TableCell className="text-right tabular-nums">
                          <div className="font-medium">
                            {formatCriterionScore(item.currentScore, item.scoringMethod)}
                          </div>
                          {actual && <div className="text-xs text-muted-foreground">{actual}</div>}
                          {item.scoreChanged === true && (
                            <div className="text-xs text-emerald-700 dark:text-emerald-400">
                              Changed by the appeal
                            </div>
                          )}
                          {item.scoreChanged === false && (
                            <div className="text-xs text-muted-foreground">Unchanged</div>
                          )}
                        </TableCell>
                      )}
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
          {data.scoreBreakdownShown && !data.outcomeReleased && (
            <p className="border-t px-4 py-3 text-xs text-muted-foreground">
              The scores as they stand are withheld while your manager re-evaluates; you will see
              them once HR has decided.
            </p>
          )}
        </CardContent>
      </Card>

      {data.resolutionNotes && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">HR&apos;s response</CardTitle>
          </CardHeader>
          <CardContent className="whitespace-pre-wrap text-sm">{data.resolutionNotes}</CardContent>
        </Card>
      )}
    </div>
  );
}

function StageNote({ status }: { status: AppraisalAppealStatus }) {
  switch (status) {
    case 'Submitted':
      return (
        <Alert>
          <Clock className="h-4 w-4" />
          <AlertTitle>With HR</AlertTitle>
          <AlertDescription>
            Your appeal has been received. HR and your manager have been notified; nobody has
            started reviewing it yet.
          </AlertDescription>
        </Alert>
      );
    case 'UnderReview':
      return (
        <Alert>
          <Scale className="h-4 w-4" />
          <AlertTitle>Being reviewed</AlertTitle>
          <AlertDescription>
            Someone in HR has picked your appeal up and is working through it.
          </AlertDescription>
        </Alert>
      );
    case 'Remanded':
      return (
        <Alert>
          <RotateCcw className="h-4 w-4" />
          <AlertTitle>Sent back for re-evaluation — not yet decided</AlertTitle>
          <AlertDescription>
            HR found enough in your appeal to ask your manager to look at the scores again. This
            is not the outcome: once they have re-submitted, HR makes the final decision and you
            will be told what it is.
          </AlertDescription>
        </Alert>
      );
    case 'Upheld':
      return (
        <Alert>
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>Upheld</AlertTitle>
          <AlertDescription>
            HR agreed with your appeal. The full outcome says whether any score changed, and HR&apos;s
            reasoning.
          </AlertDescription>
        </Alert>
      );
    case 'Rejected':
      return (
        <Alert>
          <TriangleAlert className="h-4 w-4" />
          <AlertTitle>Not upheld</AlertTitle>
          <AlertDescription>
            HR has confirmed the original scores. Open the full outcome for their reasoning.
          </AlertDescription>
        </Alert>
      );
    default:
      return null;
  }
}
