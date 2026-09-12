'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { ClipboardList, Plus, TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { pipDashboardService, pipService } from '@/services/hr/pip.service';
import type { PipListItem } from '@/types/hr/pip';

/**
 * Performance improvement plans.
 *
 * Three scopes, because who may see what differs sharply here. The org-wide dashboard lists every
 * employee in the tenant who is on a plan and is **HR only** — a 403 on that tab is the system
 * working, not a fault. Managers and employees get their own slice from `/supervising` and
 * `/mine`, which take the employee from the token.
 *
 * The counters come from the dashboard endpoint, so they are only shown on that tab.
 */
type Scope = 'supervising' | 'mine' | 'all';

/** The manager and employee lists return plans, not dashboard rows; this squares them up. */
function toRow(plan: {
  id: string;
  pipNumber: string;
  employeeId: string;
  employeeName: string;
  supervisorName: string;
  hrOwnerName?: string | null;
  startDate: string;
  endDate: string;
  status: string;
  outcome?: string | null;
}): PipListItem {
  const start = new Date(plan.startDate);
  const end = new Date(plan.endDate);
  const now = new Date();
  const totalDays = Math.max(1, Math.round((end.getTime() - start.getTime()) / 86_400_000));
  const elapsed = Math.round((now.getTime() - start.getTime()) / 86_400_000);

  return {
    pipId: plan.id,
    pipNumber: plan.pipNumber,
    employeeId: plan.employeeId,
    employeeName: plan.employeeName,
    position: '',
    department: '',
    supervisorName: plan.supervisorName,
    hrOwnerName: plan.hrOwnerName ?? null,
    startDate: plan.startDate,
    endDate: plan.endDate,
    totalDays,
    daysRemaining: Math.max(0, Math.round((end.getTime() - now.getTime()) / 86_400_000)),
    periodProgressPercent: Math.min(100, Math.max(0, (elapsed / totalDays) * 100)),
    status: plan.status as PipListItem['status'],
    outcome: (plan.outcome as PipListItem['outcome']) ?? null,
    isOverdue: (plan.status === 'Active' || plan.status === 'InProgress') && end < now,
    totalGoals: 0,
    completedGoals: 0,
    goalProgressPercent: 0,
    totalMeetings: 0,
    completedMeetings: 0,
  };
}

export default function PipListPage() {
  const [scope, setScope] = useState<Scope>('supervising');

  const dashboard = useQuery({
    queryKey: ['hr', 'pip-dashboard'],
    queryFn: () => pipDashboardService.getDashboard(),
    enabled: scope === 'all',
    retry: false,
  });

  const scoped = useQuery({
    queryKey: ['hr', 'pips', scope],
    queryFn: () => (scope === 'mine' ? pipService.getMine() : pipService.getSupervising()),
    enabled: scope !== 'all',
    retry: false,
  });

  const isLoading = scope === 'all' ? dashboard.isLoading : scoped.isLoading;
  const isError = scope === 'all' ? dashboard.isError : scoped.isError;
  const error = (scope === 'all' ? dashboard.error : scoped.error) as Error | null;
  const rows: PipListItem[] =
    scope === 'all' ? (dashboard.data?.pips ?? []) : (scoped.data ?? []).map(toRow);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Improvement plans"
        description="Formal plans agreed where performance has fallen short of what the role needs, and the reviews that track them."
        backHref="/hr/performance"
        actions={
          <Button asChild>
            <Link href="/hr/performance/pip/new">
              <Plus className="mr-2 h-4 w-4" />
              New plan
            </Link>
          </Button>
        }
      />

      <Tabs value={scope} onValueChange={(v) => setScope(v as Scope)}>
        <TabsList>
          <TabsTrigger value="supervising">Plans I own</TabsTrigger>
          <TabsTrigger value="mine">My plans</TabsTrigger>
          <TabsTrigger value="all">All plans (HR)</TabsTrigger>
        </TabsList>
      </Tabs>

      {scope === 'all' && dashboard.data && (
        <MetricTiles
          tiles={[
            { label: 'Active', value: dashboard.data.activeCount, icon: ClipboardList },
            {
              label: 'Awaiting approval',
              value: dashboard.data.pendingApprovalCount,
              hint: `${dashboard.data.draftCount} still in draft`,
              tone: dashboard.data.pendingApprovalCount > 0 ? 'warning' : 'default',
            },
            {
              label: 'Overdue',
              value: dashboard.data.overdueCount,
              hint: 'Past the end date and still running',
              tone: dashboard.data.overdueCount > 0 ? 'danger' : 'default',
            },
            {
              label: 'Closed this year',
              value: dashboard.data.completedThisYearCount,
              hint: `${dashboard.data.passedCount} improved · ${dashboard.data.failedCount} not`,
              tone: 'success',
            },
          ]}
        />
      )}

      <Card>
        <CardContent className="p-0">
          {isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load improvement plans"
              description={
                scope === 'all'
                  ? 'The organisation-wide view is restricted to HR.'
                  : (error?.message ?? 'Your account may not be linked to an employee record.')
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
              title="No improvement plans"
              description={
                scope === 'mine'
                  ? 'Nothing here is good news.'
                  : 'A plan raised from an appraisal outcome also lands here, as a draft.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Plan</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Period</TableHead>
                  <TableHead className="w-44">Elapsed</TableHead>
                  {scope === 'all' && <TableHead>Goals</TableHead>}
                  <TableHead>Status</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => (
                  <TableRow key={row.pipId}>
                    <TableCell>
                      <div className="font-medium">{row.pipNumber}</div>
                      {row.supervisorName && (
                        <div className="text-xs text-muted-foreground">
                          Supervisor: {row.supervisorName}
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="text-sm">{row.employeeName}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {formatDate(row.startDate)} – {formatDate(row.endDate)}
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Progress
                          value={Number(row.periodProgressPercent) || 0}
                          className="h-2"
                        />
                        <span className="w-16 shrink-0 text-right text-xs tabular-nums text-muted-foreground">
                          {row.isOverdue ? 'overdue' : `${row.daysRemaining}d left`}
                        </span>
                      </div>
                    </TableCell>
                    {scope === 'all' && (
                      <TableCell className="text-sm tabular-nums">
                        {row.completedGoals}/{row.totalGoals}
                      </TableCell>
                    )}
                    <TableCell>
                      <StatusBadge
                        status={humanizeEnum(row.isOverdue ? 'Overdue' : row.status)}
                      />
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" asChild>
                        <Link href={`/hr/performance/pip/${row.pipId}`}>Open</Link>
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
