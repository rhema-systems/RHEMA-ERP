'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import {
  CheckCircle2,
  Lightbulb,
  RefreshCw,
  TriangleAlert,
  XCircle,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { TargetLink } from '@/components/hr/performance/OutcomeRecommendationsPanel';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { appraisalOutcomeRecommendationService } from '@/services/hr/outcomes.service';

/**
 * HR's queue of appraisal outcome recommendations.
 *
 * **Three tabs, and the middle one is the important one.** "Proposed" is the ordinary inbox.
 * "Needs dispatch" is recommendations that were approved but whose downstream record was never
 * created — the handler failed. They are not finished work, and nothing else in the system will
 * pick them up, so they sit here until someone retries them or actions the outcome in its own
 * module. "Closed" is everything settled: actioned, rejected or dismissed.
 */
type Filter = 'proposed' | 'stalled' | 'closed';

export default function RecommendationWorklistPage() {
  const [filter, setFilter] = useState<Filter>('proposed');
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'recommendation-worklist'],
    queryFn: () => appraisalOutcomeRecommendationService.getWorklist(),
  });

  const rows = useMemo(() => data ?? [], [data]);

  const stats = useMemo(
    () => ({
      proposed: rows.filter((r) => r.status === 'Proposed').length,
      stalled: rows.filter((r) => r.status === 'Approved').length,
      actioned: rows.filter((r) => r.status === 'Actioned').length,
      closed: rows.filter((r) => ['Actioned', 'Rejected', 'Dismissed'].includes(r.status)).length,
    }),
    [rows],
  );

  const filtered = useMemo(() => {
    switch (filter) {
      case 'proposed':
        return rows.filter((r) => r.status === 'Proposed');
      case 'stalled':
        return rows.filter((r) => r.status === 'Approved');
      default:
        return rows.filter((r) => ['Actioned', 'Rejected', 'Dismissed'].includes(r.status));
    }
  }, [rows, filter]);

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'recommendation-worklist'] });

  const fail = (title: string) => (e: Error) =>
    toast({ title, description: e.message, variant: 'destructive' });

  const approve = useMutation({
    mutationFn: (id: string) => appraisalOutcomeRecommendationService.approve(id),
    onSuccess: (result) => {
      if (result.status === 'Actioned') {
        toast({
          title: 'Approved and actioned',
          description: `A ${humanizeEnum(result.targetEntityType)} record has been created.`,
        });
      } else {
        toast({
          title: 'Approved, but not actioned',
          description: 'It has moved to "Needs dispatch". Retry it from there.',
          variant: 'destructive',
        });
      }
      refresh();
    },
    onError: fail('Could not approve'),
  });

  const retry = useMutation({
    mutationFn: (id: string) => appraisalOutcomeRecommendationService.retryDispatch(id),
    onSuccess: (result) => {
      toast({
        title: 'Dispatched',
        description: `A ${humanizeEnum(result.targetEntityType)} record has been created.`,
      });
      refresh();
    },
    onError: fail('Could not dispatch'),
  });

  const dismiss = useMutation({
    mutationFn: (id: string) => appraisalOutcomeRecommendationService.dismiss(id),
    onSuccess: () => {
      toast({ title: 'Dismissed' });
      refresh();
    },
    onError: fail('Could not dismiss'),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Outcome recommendations"
        description="What appraisals across the organisation say should happen next — and whether it has actually happened."
        backHref="/hr/performance"
      />

      <MetricTiles
        tiles={[
          {
            label: 'Awaiting your decision',
            value: stats.proposed,
            icon: Lightbulb,
            tone: stats.proposed > 0 ? 'warning' : 'default',
          },
          {
            label: 'Needs dispatch',
            value: stats.stalled,
            hint: 'Approved, but the record was never created',
            icon: TriangleAlert,
            tone: stats.stalled > 0 ? 'danger' : 'default',
          },
          { label: 'Actioned', value: stats.actioned, icon: CheckCircle2, tone: 'success' },
          { label: 'Total', value: rows.length },
        ]}
      />

      {stats.stalled > 0 && (
        <Alert variant="destructive">
          <TriangleAlert className="h-4 w-4" />
          <AlertTitle>{stats.stalled} approved recommendation(s) never reached their module</AlertTitle>
          <AlertDescription>
            Approving is supposed to create a real record — a pay proposal, a PIP, a training
            request. These did not. Retry the dispatch, or action the outcome in its own module
            and dismiss the recommendation.
          </AlertDescription>
        </Alert>
      )}

      <Tabs value={filter} onValueChange={(v) => setFilter(v as Filter)}>
        <TabsList>
          <TabsTrigger value="proposed">Proposed ({stats.proposed})</TabsTrigger>
          <TabsTrigger value="stalled">Needs dispatch ({stats.stalled})</TabsTrigger>
          <TabsTrigger value="closed">Closed ({stats.closed})</TabsTrigger>
        </TabsList>
      </Tabs>

      <Card>
        <CardContent className="p-0">
          {isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load the worklist"
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
              icon={filter === 'stalled' ? CheckCircle2 : Lightbulb}
              title="Nothing here"
              description={
                filter === 'stalled'
                  ? 'Every approved recommendation reached its module.'
                  : 'No recommendation matches this filter.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Outcome</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Proposed</TableHead>
                  <TableHead>Result</TableHead>
                  <TableHead className="w-56" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((row) => (
                  <TableRow key={row.id}>
                    <TableCell>
                      <div className="font-medium">{row.employeeName ?? '—'}</div>
                      <div className="text-xs text-muted-foreground">{row.appraisalNumber}</div>
                    </TableCell>
                    <TableCell>
                      <div className="font-medium">{humanizeEnum(row.recommendationType)}</div>
                      {row.notes && (
                        <div className="max-w-xs text-xs text-muted-foreground">{row.notes}</div>
                      )}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={row.status} />
                      {row.status === 'Approved' && (
                        <div className="mt-1 text-xs text-amber-600 dark:text-amber-500">
                          Not dispatched
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {formatDate(row.recommendedDate)}
                    </TableCell>
                    <TableCell className="text-sm">
                      <TargetLink recommendation={row} />
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        {row.status === 'Proposed' && (
                          <>
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => approve.mutate(row.id)}
                              disabled={approve.isPending}
                            >
                              <CheckCircle2 className="mr-1 h-4 w-4" />
                              Approve
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => dismiss.mutate(row.id)}
                              disabled={dismiss.isPending}
                            >
                              <XCircle className="mr-1 h-4 w-4" />
                              Dismiss
                            </Button>
                          </>
                        )}
                        {row.status === 'Approved' && (
                          <>
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => retry.mutate(row.id)}
                              disabled={retry.isPending}
                            >
                              <RefreshCw className="mr-1 h-4 w-4" />
                              Retry
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => dismiss.mutate(row.id)}
                              disabled={dismiss.isPending}
                            >
                              Dismiss
                            </Button>
                          </>
                        )}
                        <Button size="sm" variant="ghost" asChild>
                          <Link href={`/hr/performance/hr-review/${row.performanceAppraisalId}`}>
                            Appraisal
                          </Link>
                        </Button>
                      </div>
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
