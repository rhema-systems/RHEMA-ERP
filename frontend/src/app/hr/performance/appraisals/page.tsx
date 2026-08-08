'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { ClipboardList, TriangleAlert } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeeTrendPanel } from '@/components/hr/performance/EmployeeTrendPanel';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { performanceAppraisalService } from '@/services/hr/appraisal-run.service';

/**
 * My appraisals — every appraisal the signed-in employee is the subject of.
 *
 * Scoped to *your own* appraisals on purpose. Peer feedback you owe on other people's
 * appraisals is a separate screen (`/hr/performance/peer-reviews`), because those rows carry
 * a colleague's scores and do not belong in a list titled "mine".
 *
 * `actionText` and `dueDate` come from the server, which reads them off the appraisal's status
 * and the cycle's deadlines — so what the row asks you to do is always the current step, not a
 * guess made here.
 */
export default function MyAppraisalsPage() {
  const [filter, setFilter] = useState<'all' | 'open' | 'completed'>('all');

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'my-appraisals', filter],
    queryFn: () => performanceAppraisalService.getMine(filter === 'all' ? undefined : filter),
  });

  const rows = data ?? [];
  const actionable = rows.filter((r) => r.actionRequired);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="My Appraisals"
        description="Your own appraisals, what each one needs from you next, and the outcome once it is signed off."
        backHref="/hr/performance"
        actions={
          <Button variant="outline" asChild>
            <Link href="/hr/performance/peer-reviews">Peer reviews I owe</Link>
          </Button>
        }
      />

      {actionable.length > 0 && (
        <Card className="border-amber-500/40 bg-amber-50/60 dark:bg-amber-950/20">
          <CardContent className="flex items-center gap-3 p-4">
            <TriangleAlert className="h-4 w-4 shrink-0 text-amber-600" />
            <p className="text-sm">
              {actionable.length === 1
                ? 'One appraisal is waiting on you.'
                : `${actionable.length} appraisals are waiting on you.`}{' '}
              {actionable.some((r) => r.isOverdue) && (
                <span className="font-medium text-red-600 dark:text-red-500">
                  Some are past their deadline.
                </span>
              )}
            </p>
          </CardContent>
        </Card>
      )}

      <Tabs value={filter} onValueChange={(v) => setFilter(v as typeof filter)}>
        <TabsList>
          <TabsTrigger value="all">All</TabsTrigger>
          <TabsTrigger value="open">In progress</TabsTrigger>
          <TabsTrigger value="completed">Completed</TabsTrigger>
        </TabsList>
      </Tabs>

      <Card>
        <CardContent className="p-0">
          {isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load your appraisals"
              description={
                (error as Error)?.message ??
                'Your account may not be linked to an employee record.'
              }
            />
          ) : isLoading ? (
            <div className="space-y-2 p-4">
              {[0, 1, 2].map((i) => (
                <Skeleton key={i} className="h-12 w-full" />
              ))}
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={ClipboardList}
              title="No appraisals yet"
              description="An appraisal appears here once HR opens a cycle that covers you and generates its records."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Cycle</TableHead>
                  <TableHead>Period</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Next step</TableHead>
                  <TableHead className="text-right">Score</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => (
                  <TableRow key={row.appraisalId}>
                    <TableCell>
                      <div className="font-medium">{row.appraisalCycleName}</div>
                      <div className="text-xs text-muted-foreground">{row.appraisalNumber}</div>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {formatDate(row.periodStart)} – {formatDate(row.periodEnd)}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={humanizeEnum(row.status)} />
                    </TableCell>
                    <TableCell>
                      {row.actionRequired ? (
                        <div className="space-y-1">
                          <div className="text-sm font-medium">{row.actionText}</div>
                          {row.dueDate && (
                            <div
                              className={
                                row.isOverdue
                                  ? 'text-xs font-medium text-red-600 dark:text-red-500'
                                  : 'text-xs text-muted-foreground'
                              }
                            >
                              {row.isOverdue ? 'Overdue — due ' : 'Due '}
                              {formatDate(row.dueDate)}
                            </div>
                          )}
                        </div>
                      ) : (
                        <span className="text-sm text-muted-foreground">Nothing from you</span>
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {row.overallScore !== null && row.overallScore !== undefined
                        ? Number(row.overallScore).toFixed(1)
                        : '—'}
                      {row.appealFiled && (
                        <Badge variant="secondary" className="ml-2">
                          Appealed
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" asChild>
                        <Link href={`/hr/performance/appraisals/${row.appraisalId}`}>Open</Link>
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/*
        Your own score history, under your own appraisals. Reads `employee/me/trend`, so it needs
        no picker and cannot show anyone else's.
      */}
      <EmployeeTrendPanel mode="me" />
    </div>
  );
}
