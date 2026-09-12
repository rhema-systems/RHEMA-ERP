'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { CheckCircle2, ClipboardCheck, Clock, TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { formatDate } from '@/lib/hr/attendance-format';
import { performanceAppraisalService } from '@/services/hr/appraisal-run.service';

/**
 * HR's sign-off queue: every appraisal in a cycle, and whether it is ready to be finalised.
 *
 * "Ready" is a real gate, not a hint — finalising an appraisal whose self, manager or minimum
 * peer evaluations are outstanding is refused with 422. The columns show each of those three
 * so the reason is visible from the list rather than only after opening a record.
 */
type Filter = 'ready' | 'waiting' | 'finalised' | 'all';

export default function HRReviewQueuePage() {
  const [cycleId, setCycleId] = useState('');
  const [filter, setFilter] = useState<Filter>('ready');

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'hr-review-list', cycleId],
    queryFn: () => performanceAppraisalService.getHRReviewList(cycleId || undefined),
  });

  const rows = data ?? [];

  const filtered = useMemo(() => {
    switch (filter) {
      case 'ready':
        return rows.filter((r) => r.isReadyForHRReview && !r.isFinalized);
      case 'waiting':
        return rows.filter((r) => !r.isReadyForHRReview && !r.isFinalized);
      case 'finalised':
        return rows.filter((r) => r.isFinalized);
      default:
        return rows;
    }
  }, [rows, filter]);

  const stats = useMemo(
    () => ({
      ready: rows.filter((r) => r.isReadyForHRReview && !r.isFinalized).length,
      waiting: rows.filter((r) => !r.isReadyForHRReview && !r.isFinalized).length,
      finalised: rows.filter((r) => r.isFinalized).length,
    }),
    [rows],
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="HR Review"
        description="Appraisals waiting on HR sign-off, and what is still outstanding on the ones that are not ready."
        backHref="/hr/performance"
      />

      <CycleSelect value={cycleId} onChange={setCycleId} allowAll />

      <MetricTiles
        tiles={[
          {
            label: 'Ready to finalise',
            value: stats.ready,
            icon: ClipboardCheck,
            tone: stats.ready > 0 ? 'warning' : 'default',
          },
          {
            label: 'Waiting on evaluations',
            value: stats.waiting,
            hint: 'Self, manager or peer feedback outstanding',
            icon: Clock,
          },
          {
            label: 'Finalised',
            value: stats.finalised,
            icon: CheckCircle2,
            tone: 'success',
          },
          { label: 'Total', value: rows.length },
        ]}
      />

      <Tabs value={filter} onValueChange={(v) => setFilter(v as Filter)}>
        <TabsList>
          <TabsTrigger value="ready">Ready ({stats.ready})</TabsTrigger>
          <TabsTrigger value="waiting">Waiting ({stats.waiting})</TabsTrigger>
          <TabsTrigger value="finalised">Finalised ({stats.finalised})</TabsTrigger>
          <TabsTrigger value="all">All ({rows.length})</TabsTrigger>
        </TabsList>
      </Tabs>

      <Card>
        <CardContent className="p-0">
          {isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load the review queue"
              description={(error as Error)?.message ?? 'This queue is restricted to HR roles.'}
            />
          ) : isLoading ? (
            <div className="space-y-2 p-4">
              {[0, 1, 2].map((i) => (
                <Skeleton key={i} className="h-12 w-full" />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={CheckCircle2}
              title="Nothing here"
              description={
                filter === 'ready'
                  ? 'No appraisal is currently waiting on you.'
                  : 'No appraisals match this filter.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Cycle</TableHead>
                  <TableHead>Self</TableHead>
                  <TableHead>Manager</TableHead>
                  <TableHead>Peers</TableHead>
                  <TableHead>HR review</TableHead>
                  <TableHead className="text-right">Score</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((row) => (
                  <TableRow key={row.appraisalId}>
                    <TableCell>
                      <div className="font-medium">{row.employeeName}</div>
                      <div className="text-xs text-muted-foreground">
                        {row.employeeNumber}
                        {row.position ? ` · ${row.position}` : ''}
                      </div>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {row.appraisalCycleName}
                      <div className="text-xs">{row.cycleYear}</div>
                    </TableCell>
                    <TableCell>
                      <DoneMark done={row.isSelfEvaluationComplete} />
                    </TableCell>
                    <TableCell>
                      <DoneMark done={row.isManagerEvaluationComplete} />
                    </TableCell>
                    <TableCell className="text-sm tabular-nums">
                      <span
                        className={
                          row.arePeerReviewsComplete
                            ? 'text-emerald-600 dark:text-emerald-500'
                            : 'text-amber-600 dark:text-amber-500'
                        }
                      >
                        {row.completedPeerReviews}/{row.requiredPeerReviews}
                      </span>
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={row.hrReviewStatus} />
                      {row.finalizedDate && (
                        <div className="mt-1 text-xs text-muted-foreground">
                          {formatDate(row.finalizedDate)}
                          {row.finalizedByName ? ` · ${row.finalizedByName}` : ''}
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {row.finalScore != null ? Number(row.finalScore).toFixed(1) : '—'}
                      {row.finalGrade && (
                        <div className="text-xs text-muted-foreground">{row.finalGrade}</div>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" asChild>
                        <Link href={`/hr/performance/hr-review/${row.appraisalId}`}>
                          {row.isFinalized ? 'View' : 'Review'}
                        </Link>
                      </Button>
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

function DoneMark({ done }: { done: boolean }) {
  return done ? (
    <CheckCircle2 className="h-4 w-4 text-emerald-600 dark:text-emerald-500" aria-label="Complete" />
  ) : (
    <Clock className="h-4 w-4 text-amber-600 dark:text-amber-500" aria-label="Outstanding" />
  );
}
