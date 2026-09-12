'use client';

import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Building2, Eye, EyeOff, Gauge, Layers, Loader2, Target, Users } from 'lucide-react';
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
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { companyGoalService, unitGoalService } from '@/services/hr/goals.service';
import { formatDate, formatPercent } from '@/lib/hr/attendance-format';
import type { GoalPriority } from '@/types/hr/goals';

/**
 * One company goal and everything hanging off it.
 *
 * The cascade counts come from a dedicated aggregate endpoint rather than from the unit-goal
 * list below, because employee goals can point straight at a company goal without going
 * through a unit — so the two numbers are genuinely different things, not the same list
 * counted twice.
 */
const PRIORITY_VARIANT: Record<GoalPriority, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Critical: 'destructive',
  High: 'default',
  Medium: 'secondary',
  Low: 'outline',
};

export default function CompanyGoalDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;

  const { data: goal, isLoading } = useQuery({
    queryKey: ['hr', 'company-goals', id, 'detail'],
    queryFn: () => companyGoalService.getById(id),
    enabled: !!id,
  });

  const { data: stats } = useQuery({
    queryKey: ['hr', 'company-goals', id, 'cascade-stats'],
    queryFn: () => companyGoalService.getCascadeStats(id),
    enabled: !!id,
  });

  const { data: unitGoals, isLoading: unitGoalsLoading } = useQuery({
    queryKey: ['hr', 'unit-goals', 'by-company-goal', id],
    queryFn: () => unitGoalService.getByCompanyGoal(id),
    enabled: !!id,
  });

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
          icon={Building2}
          title="Company goal not found"
          description="It may have been deleted."
        />
      </div>
    );
  }

  const rows = unitGoals ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={goal.title}
        description={`Company goal${goal.cycleCode ? ` · ${goal.cycleCode}` : ''}`}
        backHref="/hr/performance/company-goals"
        actions={
          <div className="flex items-center gap-2">
            <Badge variant={PRIORITY_VARIANT[goal.priority]}>{goal.priority}</Badge>
            {goal.isVisible ? (
              <Badge variant="outline" className="gap-1">
                <Eye className="h-3 w-3" /> Visible
              </Badge>
            ) : (
              <Badge variant="secondary" className="gap-1">
                <EyeOff className="h-3 w-3" /> Hidden
              </Badge>
            )}
          </div>
        }
      />

      <MetricTiles
        tiles={[
          {
            label: 'Unit goals',
            value: stats?.unitGoalsCount ?? '—',
            hint: 'Units that took this up',
            icon: Layers,
          },
          {
            label: 'Employee goals',
            value: stats?.employeeGoalsCount ?? '—',
            hint: 'Aligned directly, not via a unit',
            icon: Users,
          },
          {
            label: 'Total aligned',
            value: stats?.totalGoalsCount ?? '—',
            hint: 'Unit and employee goals together',
            icon: Target,
          },
          {
            label: 'Average progress',
            value:
              stats?.averageEmployeeProgress == null
                ? '—'
                : formatPercent(stats.averageEmployeeProgress),
            hint: 'Across live employee goals',
            icon: Gauge,
          },
        ]}
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
              <span className="text-muted-foreground">Cycle</span>
              <span>{goal.cycleCode || '—'}</span>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-muted-foreground">Strategic goal</span>
              <span className="text-right">{goal.strategicGoalTitle || 'Not linked'}</span>
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
            {stats?.averageEmployeeProgress != null && (
              <div className="space-y-1 pt-2">
                <div className="flex justify-between gap-2">
                  <span className="text-muted-foreground">Employee progress</span>
                  <span className="tabular-nums">
                    {formatPercent(stats.averageEmployeeProgress)}
                  </span>
                </div>
                <Progress value={Number(stats.averageEmployeeProgress)} />
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Unit goals cascaded from this goal</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {unitGoalsLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Layers}
              title="No unit goals yet"
              description={
                goal.isVisible
                  ? 'No org unit has taken this goal up. Managers link to it from the unit goals screen.'
                  : 'This goal is hidden, so it does not appear when managers pick an alignment.'
              }
            />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Unit goal</TableHead>
                    <TableHead>Org unit</TableHead>
                    <TableHead>Owner</TableHead>
                    <TableHead>Priority</TableHead>
                    <TableHead className="text-right">Target</TableHead>
                    <TableHead>Due</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((r) => (
                    <TableRow key={r.id}>
                      <TableCell>
                        <Link
                          href={`/hr/performance/unit-goals/${r.id}`}
                          className="font-medium hover:underline"
                        >
                          {r.title}
                        </Link>
                      </TableCell>
                      <TableCell>{r.organizationUnitName}</TableCell>
                      <TableCell>{r.managerName}</TableCell>
                      <TableCell>
                        <Badge variant={PRIORITY_VARIANT[r.priority]}>{r.priority}</Badge>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {r.targetValue == null
                          ? '—'
                          : `${r.targetValue}${r.unit ? ` ${r.unit}` : ''}`}
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
