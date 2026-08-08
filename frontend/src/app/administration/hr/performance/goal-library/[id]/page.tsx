'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { CalendarRange, Library, Loader2, Target, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { goalLibraryService } from '@/services/hr/goals.service';
import { formatPercent, humanizeEnum } from '@/lib/hr/attendance-format';

/**
 * One goal library template and what has been made from it.
 *
 * The usage list is the point of this page. Because copying a template takes a snapshot,
 * editing the wording here changes nothing on the goals below — this is the list of goals
 * an edit will *not* reach, and the argument for creating a new template rather than
 * rewriting an old one mid-cycle.
 */
export default function GoalLibraryDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const [page, setPage] = useState(1);

  const { data: item, isLoading } = useQuery({
    queryKey: ['hr', 'goal-library', id, 'details'],
    queryFn: () => goalLibraryService.getDetails(id),
    enabled: !!id,
  });

  const { data: usage, isLoading: usageLoading } = useQuery({
    queryKey: ['hr', 'goal-library', id, 'usage', page],
    queryFn: () => goalLibraryService.getUsage(id, page, 10),
    enabled: !!id,
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!item) {
    return (
      <div className="p-6">
        <EmptyState
          icon={Library}
          title="Template not found"
          description="It may have been deleted."
        />
      </div>
    );
  }

  const scope = item.positionTitle
    ? `Position: ${item.positionTitle}`
    : item.organizationUnitName
      ? `Unit: ${item.organizationUnitName}`
      : item.organizationLevelName
        ? `Level: ${item.organizationLevelName}`
        : 'Global';

  const rows = usage?.items ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={item.title}
        description="Goal library template"
        backHref="/administration/hr/performance/goal-library"
        actions={
          <div className="flex items-center gap-2">
            <Badge variant="outline">{scope}</Badge>
            <StatusBadge active={item.isActive} />
          </div>
        }
      />

      <MetricTiles
        className="lg:grid-cols-3"
        tiles={[
          {
            label: 'Goals created',
            value: item.totalGoals,
            hint: 'Copies made from this template',
            icon: Target,
          },
          {
            label: 'Employees',
            value: item.uniqueEmployees,
            hint: 'Distinct people carrying one',
            icon: Users,
          },
          {
            label: 'Cycles',
            value: item.cycleCount,
            hint: 'Appraisal cycles it has appeared in',
            icon: CalendarRange,
          },
        ]}
      />

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Template wording</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4 text-sm">
          <div>
            <p className="text-muted-foreground">Description</p>
            <p className="mt-1 whitespace-pre-wrap">{item.description || 'Not provided.'}</p>
          </div>
          <div>
            <p className="text-muted-foreground">Success criteria</p>
            <p className="mt-1 whitespace-pre-wrap">{item.successCriteria || 'Not provided.'}</p>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Goals made from this template</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {usageLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Target}
              title="Not used yet"
              description="No employee goal has been created from this template."
            />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Employee</TableHead>
                    <TableHead>Goal</TableHead>
                    <TableHead>Cycle</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Progress</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((r) => (
                    <TableRow key={r.employeeGoalId}>
                      <TableCell className="font-medium">{r.employeeName}</TableCell>
                      <TableCell>{r.goalTitle}</TableCell>
                      <TableCell>{r.appraisalCycleName || '—'}</TableCell>
                      <TableCell>
                        <StatusBadge status={humanizeEnum(r.status)} />
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatPercent(r.progressPercent)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      {usage && usage.totalPages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Page {usage.page} of {usage.totalPages} · {usage.totalCount} goals
          </p>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={!usage.hasPrevious}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={!usage.hasNext}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
