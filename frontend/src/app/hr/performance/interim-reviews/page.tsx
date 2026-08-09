'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { CalendarCheck, ClipboardList, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { interimReviewService } from '@/services/hr/interim-reviews.service';
import type { AppraisalReviewEvent } from '@/types/hr/interim-reviews';

/**
 * Interim reviews — the quarterly and mid-year checkpoints inside an appraisal cycle.
 *
 * Two lists rather than one, matching the check-ins screen: "my checkpoints" is a record of where
 * I am against my goals, "my team's" is a queue of reviews I owe someone. Both come from `/mine`
 * and `/team`, so neither needs an employee id.
 *
 * ⚠ **These are generated, not created.** How many exist, and whether each is a light-touch
 * conversation or a scored appraisal of the period, comes from the cycle's `reviewFrequency` and
 * `interimReviewDepth` settings and is written when the cycle generates its appraisals. An empty
 * list usually means the cycle is set to `None`, or that appraisals have not been generated yet —
 * not that anything is broken. Only `Custom` expects HR to add them by hand.
 */
export default function InterimReviewsPage() {
  const [scope, setScope] = useState<'mine' | 'team'>('mine');
  const [cycleId, setCycleId] = useState('');

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'interim-reviews', scope, cycleId],
    queryFn: () =>
      scope === 'mine'
        ? interimReviewService.getMine(cycleId || undefined)
        : interimReviewService.getTeam(cycleId || undefined),
  });

  const rows = data ?? [];
  const awaitingMe = rows.filter((r) =>
    scope === 'mine' ? r.status === 'Pending' : r.status === 'EmployeeSubmitted',
  ).length;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Interim reviews"
        description="Quarterly and mid-year checkpoints between goal setting and the year-end appraisal."
      />

      <CycleSelect value={cycleId} onChange={setCycleId} allowAll />

      <Tabs value={scope} onValueChange={(v) => setScope(v as typeof scope)}>
        <TabsList>
          <TabsTrigger value="mine">
            <ClipboardList className="mr-2 h-4 w-4" />
            My checkpoints
          </TabsTrigger>
          <TabsTrigger value="team">
            <Users className="mr-2 h-4 w-4" />
            My team
          </TabsTrigger>
        </TabsList>
      </Tabs>

      {awaitingMe > 0 && (
        <Card>
          <CardContent className="flex items-center gap-3 py-4">
            <CalendarCheck className="h-5 w-5 text-muted-foreground" />
            <p className="text-sm">
              <span className="font-medium">{awaitingMe}</span>{' '}
              {scope === 'mine'
                ? `checkpoint${awaitingMe === 1 ? '' : 's'} still need${awaitingMe === 1 ? 's' : ''} your self-assessment.`
                : `review${awaitingMe === 1 ? '' : 's'} ${awaitingMe === 1 ? 'is' : 'are'} waiting for you to close ${awaitingMe === 1 ? 'it' : 'them'}.`}
            </p>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="space-y-2 p-6">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : isError ? (
            <EmptyState
              title="Could not load interim reviews"
              description={(error as Error)?.message}
            />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={CalendarCheck}
              title="No interim reviews"
              description={
                scope === 'mine'
                  ? 'Your cycle may be set to year-end only, or its appraisals have not been generated yet.'
                  : 'Nobody reporting to you has an interim checkpoint in this cycle.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  {scope === 'team' && <TableHead>Employee</TableHead>}
                  <TableHead>Checkpoint</TableHead>
                  <TableHead>Cycle</TableHead>
                  <TableHead>Date</TableHead>
                  <TableHead>Depth</TableHead>
                  <TableHead>Period score</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row: AppraisalReviewEvent) => (
                  <TableRow key={row.id}>
                    {scope === 'team' && (
                      <TableCell className="font-medium">{row.employeeName ?? '—'}</TableCell>
                    )}
                    <TableCell>{humanizeEnum(row.type)}</TableCell>
                    <TableCell className="text-muted-foreground">{row.cycleCode ?? '—'}</TableCell>
                    <TableCell>{formatDate(row.eventDate)}</TableCell>
                    <TableCell>
                      {row.isFullAppraisal ? 'Full appraisal' : 'Light touch'}
                    </TableCell>
                    <TableCell>
                      {/* Null on a light-touch event by design, not a missing value. */}
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
          )}
        </CardContent>
      </Card>
    </div>
  );
}
