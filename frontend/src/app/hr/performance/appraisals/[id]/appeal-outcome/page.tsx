'use client';

import { useQuery } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import { CheckCircle2, ScrollText, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate } from '@/lib/hr/attendance-format';
import { appraisalAppealService } from '@/services/hr/appeals.service';

/**
 * The employee's final outcome — the record of what happened to their appeal.
 *
 * Only reachable once the appeal is Upheld or Rejected; a remand is still in flight and the
 * server answers 400, so the empty state says to check the status page instead.
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
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Appeal outcome" backHref={`/hr/performance/appraisals/${appraisalId}`} />
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
    <div className="space-y-6 p-6">
      <PageHeader
        title="Appeal outcome"
        description={`${data.appraisalNumber} · ${data.cycleName} (${formatDate(data.cycleStartDate)} – ${formatDate(data.cycleEndDate)})`}
        backHref={`/hr/performance/appraisals/${appraisalId}`}
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
          {
            label: 'Scores changed',
            value: data.scoresChangedAfterAppeal ? 'Yes' : 'No',
            tone: data.scoresChangedAfterAppeal ? 'success' : 'default',
            hint: data.scoresChangedAfterAppeal
              ? 'At least one score moved as a result'
              : 'The original scores stand',
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
              description="This appraisal has no scored criteria on record."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Criterion</TableHead>
                  <TableHead className="text-right">Weight</TableHead>
                  <TableHead className="text-right">Score</TableHead>
                  <TableHead className="text-right">Weighted</TableHead>
                  <TableHead>Contested</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.finalCriteriaScores.map((c) => (
                  <TableRow key={c.templateItemId}>
                    <TableCell>
                      <div className="font-medium">{c.itemName}</div>
                      {c.managerComments && (
                        <div className="mt-1 text-xs text-muted-foreground">
                          {c.managerComments}
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{c.weight}</TableCell>
                    <TableCell className="text-right tabular-nums font-medium">
                      {c.finalScore ?? '—'}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {c.finalWeightedScore.toFixed(1)}
                    </TableCell>
                    <TableCell>
                      {c.wasAppealed ? (
                        <StatusBadge status="Appealed" />
                      ) : (
                        <span className="text-xs text-muted-foreground">—</span>
                      )}
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
