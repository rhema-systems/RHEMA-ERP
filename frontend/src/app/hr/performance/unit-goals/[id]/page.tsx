'use client';

import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Layers, Link2Off, Loader2, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { AttachmentsPanel } from '@/components/hr/common/AttachmentsPanel';
import { unitGoalService } from '@/services/hr/goals.service';
import { formatDate, formatPercent, humanizeEnum } from '@/lib/hr/attendance-format';
import type { GoalPriority } from '@/types/hr/goals';

/**
 * One unit goal, and the employee goals cascaded from it.
 *
 * The employee list is a projection built for exactly this view — it carries each goal's
 * status and progress but not its full body, so nothing here needs a per-row fetch.
 *
 * The goal itself is the tenant's to read; the per-employee rows are the HR desk's and the
 * unit line's only (performance closure P11). Everyone else is refused the rows and sees the
 * cascade in numbers, which names nobody.
 */
const PRIORITY_VARIANT: Record<GoalPriority, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Critical: 'destructive',
  High: 'default',
  Medium: 'secondary',
  Low: 'outline',
};

export default function UnitGoalDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;

  const { data: goal, isLoading } = useQuery({
    queryKey: ['hr', 'unit-goals', id, 'detail'],
    queryFn: () => unitGoalService.getById(id),
    enabled: !!id,
  });

  const {
    data: employeeGoals,
    isLoading: cascadeLoading,
    error: cascadeError,
  } = useQuery({
    queryKey: ['hr', 'unit-goals', id, 'employee-goals'],
    queryFn: () => unitGoalService.getEmployeeGoalSummaries(id),
    enabled: !!id,
  });

  // The counts everyone may see (P11) — what the page shows when the rows are refused.
  const { data: cascadeStats } = useQuery({
    queryKey: ['hr', 'unit-goals', id, 'cascade-stats'],
    queryFn: () => unitGoalService.getCascadeStats(id),
    enabled: !!id,
  });

  const rowsRefused = (cascadeError as { status?: number } | null)?.status === 403;

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!goal) {
    return (
      <div className="p-6">
        <EmptyState
          icon={Layers}
          title="Unit goal not found"
          description="It may have been deleted."
        />
      </div>
    );
  }

  const rows = employeeGoals ?? [];
  const alignedCount = cascadeStats?.employeeGoalsCount ?? rows.length;
  const averageProgress =
    cascadeStats?.averageProgressPercent != null
      ? Number(cascadeStats.averageProgressPercent)
      : rows.length === 0
        ? null
        : rows.reduce((sum, r) => sum + Number(r.progressPercent ?? 0), 0) / rows.length;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={goal.title}
        description={`Unit goal · ${goal.organizationUnitName}${
          goal.cycleCode ? ` · ${goal.cycleCode}` : ''
        }`}
        backHref="/hr/performance/unit-goals"
        actions={<Badge variant={PRIORITY_VARIANT[goal.priority]}>{goal.priority}</Badge>}
      />

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Goal</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4 text-sm">
            <div>
              <p className="text-muted-foreground">Description</p>
              <p className="mt-1 whitespace-pre-wrap">{goal.description || 'Not provided.'}</p>
            </div>
            <div>
              <p className="text-muted-foreground">Success criteria</p>
              <p className="mt-1 whitespace-pre-wrap">{goal.successCriteria || 'Not provided.'}</p>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">At a glance</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div className="flex justify-between gap-2">
              <span className="text-muted-foreground">Org unit</span>
              <span className="text-right">
                {goal.organizationUnitName}
                <span className="block text-xs text-muted-foreground">
                  {goal.organizationLevelName}
                </span>
              </span>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-muted-foreground">Owner</span>
              <span>{goal.managerName || '—'}</span>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-muted-foreground">Aligned to</span>
              <span className="text-right">
                {goal.parentCompanyGoalId ? (
                  <Link
                    href={`/hr/performance/company-goals/${goal.parentCompanyGoalId}`}
                    className="hover:underline"
                  >
                    {goal.parentGoalTitle}
                  </Link>
                ) : (
                  <Badge variant="secondary" className="gap-1">
                    <Link2Off className="h-3 w-3" /> Unlinked
                  </Badge>
                )}
              </span>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-muted-foreground">Target</span>
              <span>
                {goal.targetValue == null
                  ? '—'
                  : `${goal.targetValue}${goal.unit ? ` ${goal.unit}` : ''}`}
              </span>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-muted-foreground">Due</span>
              <span>{formatDate(goal.dueDate)}</span>
            </div>
            {averageProgress !== null && (
              <div className="space-y-1 pt-2">
                <div className="flex justify-between gap-2">
                  <span className="text-muted-foreground">Cascade progress</span>
                  <span className="tabular-nums">{formatPercent(averageProgress)}</span>
                </div>
                <Progress value={averageProgress} />
                <p className="text-xs text-muted-foreground">
                  Mean across the {alignedCount} employee goal{alignedCount === 1 ? '' : 's'}{' '}
                  aligned to it.
                </p>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      <AttachmentsPanel
        title="Supporting evidence"
        note="Readable by anyone in the tenant — a unit goal is a departmental target, so do not attach anything personal."
        queryKey={['hr', 'unit-goal-attachments', id]}
        list={() => unitGoalService.getAttachments(id)}
        upload={(file, description) => unitGoalService.uploadAttachment(id, file, description)}
        download={(attachment) => unitGoalService.downloadAttachment(id, attachment)}
        remove={(attachmentId) => unitGoalService.deleteAttachment(id, attachmentId)}
        emptyDescription="Attach the plans, dashboards or papers that back this target up."
      />

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Employee goals cascaded from this goal</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {cascadeLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rowsRefused ? (
            <EmptyState
              icon={Users}
              title={
                alignedCount === 0
                  ? 'No employee goals yet'
                  : `${alignedCount} employee goal${alignedCount === 1 ? '' : 's'} aligned to this goal`
              }
              description="Who they belong to, and how each is going, is visible to HR and to the managers in this unit's line."
            />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Users}
              title="No employee goals yet"
              description="Nobody has aligned a goal to this unit goal. Employees pick it when writing their own."
            />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Employee</TableHead>
                    <TableHead>Goal</TableHead>
                    <TableHead>Priority</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="w-[160px]">Progress</TableHead>
                    <TableHead>Due</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((r) => (
                    <TableRow key={r.id}>
                      <TableCell className="font-medium">{r.employeeName}</TableCell>
                      <TableCell>{r.title}</TableCell>
                      <TableCell>
                        <Badge variant={PRIORITY_VARIANT[r.priority]}>{r.priority}</Badge>
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={humanizeEnum(r.status)} />
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <Progress value={Number(r.progressPercent)} className="h-2" />
                          <span className="w-12 shrink-0 text-right text-xs tabular-nums">
                            {formatPercent(r.progressPercent)}
                          </span>
                        </div>
                      </TableCell>
                      <TableCell>{formatDate(r.dueDate)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
