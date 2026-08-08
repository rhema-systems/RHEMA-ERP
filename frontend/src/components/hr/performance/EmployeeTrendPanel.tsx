'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { LineChart } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { PerformanceTrendChart } from '@/components/hr/performance/PerformanceCharts';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { performanceAnalyticsService } from '@/services/hr/analytics.service';

/**
 * One person's overall score across cycles.
 *
 * Two modes. With no `employeeId` it shows a picker and reads
 * `api/PerformanceAnalytics/employee/{id}/trend`, which HR, the employee themselves and their
 * line manager may call — anyone else gets a 403, and the panel says so rather than showing an
 * empty chart. With `mode="me"` it reads the caller's own history and needs no picker.
 *
 * ⚠ Only appraisals carrying an `overallScore` plot. Appraisals still in flight appear in the
 * table with no score, because "this year has not been scored yet" is worth seeing next to the
 * years that have.
 */
export function EmployeeTrendPanel({
  employeeId: fixedEmployeeId,
  employeeName,
  mode = 'pick',
}: {
  /** Skips the picker and pins the panel to one person. */
  employeeId?: string;
  employeeName?: string | null;
  /** `me` reads the signed-in employee's own trend. */
  mode?: 'pick' | 'me';
}) {
  const [pickedId, setPickedId] = useState<string | null>(null);
  const [pickedLabel, setPickedLabel] = useState<string | null>(null);

  const employeeId = fixedEmployeeId ?? pickedId;
  const enabled = mode === 'me' || !!employeeId;

  const trend = useQuery({
    queryKey: ['hr', 'performance-trend', mode === 'me' ? 'me' : employeeId],
    queryFn: () =>
      mode === 'me' || !employeeId
        ? performanceAnalyticsService.getMyTrend()
        : performanceAnalyticsService.getEmployeeTrend(employeeId),
    enabled,
  });

  const points = trend.data?.points ?? [];
  const scored = points.filter((p) => p.overallScore != null);

  // Two cycles in the same year would collide on a bare year axis, so the label falls back to
  // the cycle name when there is one.
  const chartData = scored.map((p) => ({
    label: p.cycleName ? `${p.year} · ${p.cycleName}` : String(p.year),
    score: Number(p.overallScore),
    hint: p.rating ? humanizeEnum(p.rating) : undefined,
  }));

  const heading =
    trend.data?.employeeName ?? employeeName ?? pickedLabel ?? (mode === 'me' ? 'Your' : null);

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <LineChart className="h-4 w-4" />
          {heading ? `${heading}${mode === 'me' ? '' : "'s"} performance trend` : 'Performance trend'}
        </CardTitle>
        <CardDescription>
          Overall score per cycle, on a fixed 0–100 scale so two years are comparable by eye.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {mode === 'pick' && !fixedEmployeeId && (
          <div className="max-w-md space-y-2">
            <Label>Employee</Label>
            <EmployeePicker
              value={pickedId}
              initialLabel={pickedLabel}
              onChange={(id, label) => {
                setPickedId(id);
                setPickedLabel(label);
              }}
            />
          </div>
        )}

        {!enabled && (
          <EmptyState
            title="Pick an employee"
            description="You can read your own trend and that of anyone who reports to you; HR can read anyone's."
          />
        )}

        {enabled && trend.isLoading && (
          <p className="py-8 text-center text-sm text-muted-foreground">Loading history…</p>
        )}

        {enabled && trend.isError && (
          <p className="py-8 text-center text-sm text-muted-foreground">
            {(trend.error as Error).message}
          </p>
        )}

        {enabled && trend.data && (
          <>
            <PerformanceTrendChart data={chartData} />

            {points.length > 0 && (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Year</TableHead>
                    <TableHead>Cycle</TableHead>
                    <TableHead className="text-right">Score</TableHead>
                    <TableHead>Rating</TableHead>
                    <TableHead>Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {points.map((p) => (
                    <TableRow key={p.appraisalId}>
                      <TableCell className="tabular-nums">{p.year || '—'}</TableCell>
                      <TableCell>{p.cycleName ?? '—'}</TableCell>
                      <TableCell className="text-right tabular-nums">
                        {p.overallScore != null ? p.overallScore.toFixed(1) : '—'}
                      </TableCell>
                      <TableCell>
                        {p.rating ? humanizeEnum(p.rating) : (
                          <span className="text-muted-foreground">not rated</span>
                        )}
                      </TableCell>
                      <TableCell>
                        {p.status ? <StatusBadge status={humanizeEnum(p.status)} /> : '—'}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
