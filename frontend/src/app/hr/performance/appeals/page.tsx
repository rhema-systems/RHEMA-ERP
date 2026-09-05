'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { CheckCircle2, Gavel, Inbox, RotateCcw, Scale, TriangleAlert } from 'lucide-react';
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
import { appraisalAppealService } from '@/services/hr/appeals.service';
import type { AppraisalAppealStatus } from '@/types/hr/appeals';

/**
 * HR's appeals queue.
 *
 * The four tabs are the four things that can be owed, and they are not all owed by HR:
 *   • **New** — submitted, nobody has picked it up.
 *   • **In review** — HR has it open.
 *   • **With the manager** — remanded. The ball is in the manager's court until they re-submit;
 *     HR's next move is the *final* decision on the post-remand screen, not another resolution.
 *   • **Decided** — upheld or rejected. Final; kept for the record.
 *
 * The whole list is HR-only — it carries every appellant's name and employee number.
 */
type Filter = 'new' | 'reviewing' | 'remanded' | 'decided' | 'all';

const DECIDED: AppraisalAppealStatus[] = ['Upheld', 'Rejected'];

export default function AppealsQueuePage() {
  const [cycleId, setCycleId] = useState('');
  const [filter, setFilter] = useState<Filter>('new');

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'appeals', cycleId],
    queryFn: () => appraisalAppealService.getAppealsList(cycleId || undefined),
  });

  const rows = useMemo(() => data ?? [], [data]);

  const stats = useMemo(
    () => ({
      fresh: rows.filter((r) => r.status === 'Submitted').length,
      reviewing: rows.filter((r) => r.status === 'UnderReview').length,
      remanded: rows.filter((r) => r.status === 'Remanded').length,
      decided: rows.filter((r) => DECIDED.includes(r.status)).length,
    }),
    [rows],
  );

  const filtered = useMemo(() => {
    switch (filter) {
      case 'new':
        return rows.filter((r) => r.status === 'Submitted');
      case 'reviewing':
        return rows.filter((r) => r.status === 'UnderReview');
      case 'remanded':
        return rows.filter((r) => r.status === 'Remanded');
      case 'decided':
        return rows.filter((r) => DECIDED.includes(r.status));
      default:
        return rows;
    }
  }, [rows, filter]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Appeals"
        description="Employees contesting a finalised appraisal, and where each appeal has got to."
        backHref="/hr/performance"
      />

      <CycleSelect value={cycleId} onChange={setCycleId} allowAll />

      <MetricTiles
        tiles={[
          {
            label: 'Waiting to be picked up',
            value: stats.fresh,
            icon: Inbox,
            tone: stats.fresh > 0 ? 'warning' : 'default',
          },
          { label: 'In review', value: stats.reviewing, icon: Scale },
          {
            label: 'With the manager',
            value: stats.remanded,
            hint: 'Remanded — awaiting re-evaluation',
            icon: RotateCcw,
            tone: stats.remanded > 0 ? 'warning' : 'default',
          },
          { label: 'Decided', value: stats.decided, icon: CheckCircle2, tone: 'success' },
        ]}
      />

      <Tabs value={filter} onValueChange={(v) => setFilter(v as Filter)}>
        <TabsList>
          <TabsTrigger value="new">New ({stats.fresh})</TabsTrigger>
          <TabsTrigger value="reviewing">In review ({stats.reviewing})</TabsTrigger>
          <TabsTrigger value="remanded">With the manager ({stats.remanded})</TabsTrigger>
          <TabsTrigger value="decided">Decided ({stats.decided})</TabsTrigger>
          <TabsTrigger value="all">All ({rows.length})</TabsTrigger>
        </TabsList>
      </Tabs>

      <Card>
        <CardContent className="p-0">
          {isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load the appeals queue"
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
              icon={Gavel}
              title="Nothing here"
              description={
                filter === 'new'
                  ? 'No appeal is waiting to be picked up.'
                  : 'No appeal matches this filter.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Cycle</TableHead>
                  <TableHead>Submitted</TableHead>
                  <TableHead className="text-right">Items</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-32" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((row) => (
                  <TableRow key={row.appealId}>
                    <TableCell>
                      <div className="font-medium">{row.employeeName}</div>
                      <div className="text-xs text-muted-foreground">
                        {row.employeeNumber} · {row.appraisalNumber}
                      </div>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">{row.cycleName}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {formatDate(row.submittedDate)}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {row.appealedItemsCount}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={row.status} />
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" asChild>
                        <Link href={`/hr/performance/appeals/${row.appraisalId}`}>
                          {DECIDED.includes(row.status)
                            ? 'View'
                            : row.status === 'Remanded'
                              ? 'Final decision'
                              : 'Review'}
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
