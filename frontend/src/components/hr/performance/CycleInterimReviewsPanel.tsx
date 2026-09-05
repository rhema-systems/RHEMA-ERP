'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { CalendarCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { interimReviewService } from '@/services/hr/interim-reviews.service';

interface CycleInterimReviewsPanelProps {
  cycleId: string;
}

/**
 * HR's org-wide view of a cycle's interim checkpoints.
 *
 * The tiles answer the question HR actually has mid-cycle — how many checkpoints are still
 * outstanding, and how many are sitting on a manager's desk having already been submitted. The
 * `EmployeeSubmitted` count is the actionable one: those are reviews the employee has finished and
 * nobody has closed.
 *
 * ⚠ An empty panel is usually configuration, not a fault: a cycle whose settings profile has
 * `reviewFrequency = None` generates no checkpoints at all, and none exist until the cycle has
 * generated its appraisals.
 */
export function CycleInterimReviewsPanel({ cycleId }: CycleInterimReviewsPanelProps) {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'interim-reviews', 'by-cycle', cycleId],
    queryFn: () => interimReviewService.getByCycle(cycleId),
    enabled: !!cycleId,
    retry: false,
  });

  const rows = data ?? [];

  const tiles = useMemo(() => {
    const count = (status: string) => rows.filter((r) => r.status === status).length;
    return [
      { label: 'Checkpoints', value: rows.length },
      { label: 'Not started', value: count('Pending') },
      // The one somebody has to act on: the employee has finished and nobody has closed it.
      {
        label: 'Awaiting manager',
        value: count('EmployeeSubmitted'),
        tone: count('EmployeeSubmitted') > 0 ? ('warning' as const) : ('default' as const),
      },
      { label: 'Completed', value: count('Completed') },
    ];
  }, [rows]);

  if (isLoading) return <Skeleton className="h-64 w-full" />;

  if (isError) {
    return (
      <EmptyState
        title="Could not load interim reviews"
        description={(error as Error)?.message}
      />
    );
  }

  if (rows.length === 0) {
    return (
      <EmptyState
        icon={CalendarCheck}
        title="No interim checkpoints in this cycle"
        description="The settings profile may be set to year-end only, or appraisals have not been generated yet."
      />
    );
  }

  return (
    <div className="space-y-4">
      <MetricTiles tiles={tiles} />

      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Employee</TableHead>
                <TableHead>Checkpoint</TableHead>
                <TableHead>Date</TableHead>
                <TableHead>Depth</TableHead>
                <TableHead>Period score</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="w-24" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.id}>
                  <TableCell className="font-medium">{row.employeeName ?? '—'}</TableCell>
                  <TableCell>{humanizeEnum(row.type)}</TableCell>
                  <TableCell>{formatDate(row.eventDate)}</TableCell>
                  <TableCell>{row.isFullAppraisal ? 'Full appraisal' : 'Light touch'}</TableCell>
                  <TableCell className="tabular-nums">
                    {row.overallPeriodScore != null ? row.overallPeriodScore.toFixed(2) : '—'}
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={row.status} />
                  </TableCell>
                  <TableCell>
                    <Button variant="ghost" size="sm" asChild>
                      <Link href={`/hr/performance/interim-reviews/${row.id}`}>Open</Link>
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
