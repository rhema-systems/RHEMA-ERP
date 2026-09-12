'use client';

import Link from 'next/link';
import type { ReactNode } from 'react';
import { Loader2, type LucideIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate, formatPercent, humanizeEnum } from '@/lib/hr/attendance-format';
import type { TeamGoalFlat } from '@/types/hr/goals';

/**
 * The flat goal table behind the manager workspace's awaiting-approval, at-risk, overdue and
 * locked tabs. They return the same row shape, so the only per-tab differences are which
 * timing column is worth a header and what actions hang off each row.
 *
 * `daysRemaining`, `isOverdue` and the risk fields are all computed server-side after the
 * query, against a UTC clock — nothing here recomputes them from the dates.
 */
export type TeamGoalTiming = 'pending' | 'due' | 'overdue' | 'locked';

export function TeamGoalTable({
  rows,
  isLoading,
  timing,
  emptyTitle,
  emptyDescription,
  emptyIcon,
  actions,
  showRisk = false,
  showEmployee = true,
}: {
  rows: TeamGoalFlat[];
  isLoading: boolean;
  timing: TeamGoalTiming;
  emptyTitle: string;
  emptyDescription: string;
  emptyIcon?: LucideIcon;
  /** Per-row buttons, e.g. approve/reject on the awaiting-approval tab. */
  actions?: (row: TeamGoalFlat) => ReactNode;
  showRisk?: boolean;
  showEmployee?: boolean;
}) {
  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (rows.length === 0) {
    return <EmptyState icon={emptyIcon} title={emptyTitle} description={emptyDescription} />;
  }

  const timingHeader = {
    pending: 'Waiting',
    due: 'Due',
    overdue: 'Overdue by',
    locked: 'Locked',
  }[timing];

  return (
    <div className="overflow-x-auto">
      <Table>
        <TableHeader>
          <TableRow>
            {showEmployee && <TableHead>Employee</TableHead>}
            <TableHead>Goal</TableHead>
            <TableHead className="text-right">Weight</TableHead>
            <TableHead>Status</TableHead>
            <TableHead className="w-[150px]">Progress</TableHead>
            {showRisk && <TableHead>Risk</TableHead>}
            <TableHead>{timingHeader}</TableHead>
            {actions && <TableHead className="w-[180px]" />}
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((row) => (
            <TableRow key={row.goalId}>
              {showEmployee && <TableCell className="font-medium">{row.employeeName}</TableCell>}
              <TableCell>
                <Link
                  href={`/hr/performance/employee-goals/${row.goalId}`}
                  className="hover:underline"
                >
                  {row.title}
                </Link>
              </TableCell>
              <TableCell className="text-right tabular-nums">{row.weight}%</TableCell>
              <TableCell>
                <StatusBadge status={humanizeEnum(row.status)} />
              </TableCell>
              <TableCell>
                <div className="flex items-center gap-2">
                  <Progress value={Number(row.progressPercent)} className="h-2" />
                  <span className="w-12 shrink-0 text-right text-xs tabular-nums">
                    {formatPercent(row.progressPercent)}
                  </span>
                </div>
              </TableCell>
              {showRisk && (
                <TableCell>
                  {row.isAtRisk ? (
                    <span className="text-sm text-red-600 dark:text-red-500">
                      {row.riskReason || 'At risk'}
                    </span>
                  ) : (
                    <span className="text-sm text-muted-foreground">—</span>
                  )}
                </TableCell>
              )}
              <TableCell className="whitespace-nowrap">
                {timing === 'pending' ? (
                  <span>
                    {row.daysPendingApproval} day{row.daysPendingApproval === 1 ? '' : 's'}
                  </span>
                ) : timing === 'overdue' ? (
                  <Badge variant="destructive">
                    {row.daysOverdue ?? Math.abs(row.daysRemaining)} days
                  </Badge>
                ) : timing === 'locked' ? (
                  formatDate(row.lockedDate)
                ) : (
                  <span className={row.isOverdue ? 'text-red-600 dark:text-red-500' : undefined}>
                    {formatDate(row.dueDate)}
                    {!row.isOverdue && row.daysRemaining >= 0 && (
                      <span className="block text-xs text-muted-foreground">
                        {row.daysRemaining} day{row.daysRemaining === 1 ? '' : 's'} left
                      </span>
                    )}
                  </span>
                )}
              </TableCell>
              {actions && <TableCell>{actions(row)}</TableCell>}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
