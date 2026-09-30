'use client';

import { useQuery } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import { CheckCircle2, ScrollText, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate } from '@/lib/hr/attendance-format';
import { appraisalAppealService } from '@/services/hr/appeals.service';
import { formatActualAgainstTarget, formatCriterionScore } from '@/types/hr/appeals';

/**
 * The employee's final outcome — the record of what happened to their appeal.
 *
 * Only reachable once the appeal is Upheld or Rejected; a remand is still in flight and the
 * server answers 400, so the empty state says to check the status page instead.
 *
 * **It says whether anything moved** (closure C-b): an upheld appeal can leave every score as it
 * was — HR agreed, on a profile that does not let HR change scores — and the message said "your
 * scores were adjusted" regardless. A contested row shows what it scored when the appeal was filed
 * beside the final score (D-38); a KPI or goal with a target shows its actual (it read "—").
 */
export default function AppealOutcomePage() {
  const params = useParams<{ id: string }>();
  const appraisalId = params.id;

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'appeal-outcome', appraisalId],
    queryFn: () => appraisalAppealService.getAppealOutcome(appraisalId),
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
        <PageHeader title="Appeal outcome" backHref={`/me/performance/appraisals/${appraisalId}`} />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={ScrollText}
              title="No final outcome yet"
              description={
                (error as Error)?.message ??
                'Your appeal has not been decided. The status page shows where it has got to.'
              }
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Appeal outcome"
        description={`${data.appraisalNumber} · ${data.cycleName} (${formatDate(data.cycleStartDate)} – ${formatDate(data.cycleEndDate)})`}
        backHref={`/me/performance/appraisals/${appraisalId}`}
      />

      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={data.appealStatus} />
        <span className="text-sm text-muted-foreground">
          Decided {formatDate(data.appealResolvedDate)}
        </span>
        <span className="text-sm text-muted-foreground">
          {data.positionTitle}
          {data.organizationUnitName ? ` · ${data.organizationUnitName}` : ''}
        </span>
      </div>

      <Alert>
        {data.isUpheld ? (
          <CheckCircle2 className="h-4 w-4" />
        ) : (
          <TriangleAlert className="h-4 w-4" />
        )}
        <AlertTitle>{data.isUpheld ? 'Your appeal was upheld' : 'Your appeal was not upheld'}</AlertTitle>
        <AlertDescription>{data.outcomeMessage}</AlertDescription>
      </Alert>

      <MetricTiles
        tiles={[
          { label: 'Final overall score', value: data.finalOverallScore.toFixed(1) },
          ...(data.originalOverallScore != null
            ? [{ label: 'Score when you appealed', value: Number(data.originalOverallScore).toFixed(1) }]
            : []),
          {
            label: 'Scores changed',
            value: data.scoresChangedAfterAppeal ? 'Yes' : 'No',
            tone: data.scoresChangedAfterAppeal ? 'success' : 'default',
            hint: data.scoresChangedAfterAppeal
              ? 'The appeal moved a score'
              : 'The scores you appealed stand',
          },
          { label: 'Items contested', value: data.appealedItems.length },
          { label: 'Appeal filed', value: formatDate(data.appealSubmittedDate) },
        ]}
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">HR&apos;s decision</CardTitle>
        </CardHeader>
        <CardContent className="whitespace-pre-wrap text-sm">
          {data.hrFinalNotes || 'No further notes were recorded.'}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">What you said</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="whitespace-pre-wrap text-sm">
            {data.employeeAppealReason || 'No overall reason was recorded.'}
          </p>
          {data.appealedItems.length > 0 && (
            <ul className="list-inside list-disc text-sm text-muted-foreground">
              {data.appealedItems.map((item, i) => (
                <li key={`${item}-${i}`}>{item}</li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Final scores</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {data.finalCriteriaScores.length === 0 ? (
            <EmptyState
              icon={ScrollText}
              title="No itemised scores"
              description={
                // The server sends none when the cycle shows the overall only (closure B2).
                data.scoreBreakdownShown
                  ? 'This appraisal has no scored criteria on record.'
                  : 'This cycle shows you the final result, not each criterion’s score.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Criterion</TableHead>
                  <TableHead className="text-right">Weight</TableHead>
                  <TableHead className="text-right">When you appealed</TableHead>
                  <TableHead className="text-right">Final score</TableHead>
                  <TableHead className="text-right">Weighted</TableHead>
                  <TableHead>Contested</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.finalCriteriaScores.map((c) => {
                  const actual =
                    c.scoringMethod === 'Measured'
                      ? formatActualAgainstTarget(c.finalActualValue, c.targetValue, c.unit)
                      : null;
                  return (
                    <TableRow key={c.criterionKey}>
                      <TableCell>
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-medium">{c.itemName}</span>
                          <Badge variant="secondary">{c.itemType}</Badge>
                        </div>
                        {c.sectionName && (
                          <div className="text-xs text-muted-foreground">{c.sectionName}</div>
                        )}
                        {c.managerComments && (
                          <div className="mt-1 text-xs text-muted-foreground">
                            {c.managerComments}
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">{c.weight}%</TableCell>
                      <TableCell className="text-right tabular-nums text-muted-foreground">
                        {c.wasAppealed ? formatCriterionScore(c.scoreWhenAppealed, c.scoringMethod) : ''}
                      </TableCell>
                      <TableCell className="text-right tabular-nums font-medium">
                        {formatCriterionScore(c.finalScore, c.scoringMethod)}
                        {actual && (
                          <div className="text-xs font-normal text-muted-foreground">{actual}</div>
                        )}
                        {c.achievementOverridden && (
                          <div className="text-xs font-normal text-amber-700 dark:text-amber-400">
                            Achievement restated by calibration or appeal
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {c.finalWeightedScore.toFixed(1)}
                      </TableCell>
                      <TableCell>
                        {c.wasAppealed ? (
                          <div className="space-y-1">
                            <StatusBadge status="Appealed" />
                            {c.changedOnAppeal === true && (
                              <div className="text-xs text-emerald-700 dark:text-emerald-400">
                                Changed
                              </div>
                            )}
                            {c.changedOnAppeal === false && (
                              <div className="text-xs text-muted-foreground">Unchanged</div>
                            )}
                          </div>
                        ) : (
                          <span className="text-xs text-muted-foreground">—</span>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
