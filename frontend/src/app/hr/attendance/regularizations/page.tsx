'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Plus, FileClock, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  useWorkflowEntitySummaries,
  formatPendingApprovers,
} from '@/hooks/useWorkflowEntitySummaries';
import { regularizationService } from '@/services/hr/attendance.service';
import { formatDate, formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { REGULARIZATION_STATUS_OPTIONS } from '@/types/hr/attendance';
import type {
  AttendanceRegularizationStatus,
  StaffAttendanceRegularizationSummary,
} from '@/types/hr/attendance';

const PENDING = '__pending__';
const ALL = '__all__';

/**
 * Attendance regularizations — requests to correct a recorded day.
 *
 * Approval runs through the generic workflow engine, so each row shows where it sits in its
 * chain via the batched entity summaries rather than inferring anything from the status.
 */
export default function RegularizationsPage() {
  const router = useRouter();
  const [filter, setFilter] = useState<string>(PENDING);

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'regularizations', 'list', filter],
    queryFn: () => {
      if (filter === PENDING) return regularizationService.getPendingApproval();
      if (filter === ALL) {
        return regularizationService.getPaged(1, 100).then((p) => p.items);
      }
      return regularizationService.getByStatus(filter as AttendanceRegularizationStatus);
    },
  });

  const rows: StaffAttendanceRegularizationSummary[] = data ?? [];

  const summaries = useWorkflowEntitySummaries(
    'StaffAttendanceRegularization',
    rows.map((r) => r.id),
    rows.length > 0,
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Attendance Regularizations"
        description="Requests to correct a recorded day, and where each sits in its approval chain."
        backHref="/hr/attendance"
        actions={
          <Button onClick={() => router.push('/hr/attendance/regularizations/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Request
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Filter</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="max-w-xs space-y-2">
            <label className="text-sm font-medium">Show</label>
            <Select value={filter} onValueChange={setFilter}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={PENDING}>Awaiting approval</SelectItem>
                <SelectItem value={ALL}>All requests</SelectItem>
                {REGULARIZATION_STATUS_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>
              {rows.length} {rows.length === 1 ? 'request' : 'requests'}
            </CardTitle>
            {isFetching && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
          </div>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Attendance date</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Raised</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Awaiting</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[90px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={FileClock}
                        title="No regularizations"
                        description="Nothing matches this filter."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((r) => {
                    const summary = summaries.summariesById?.[r.id];
                    return (
                      <TableRow
                        key={r.id}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => router.push(`/hr/attendance/regularizations/${r.id}`)}
                      >
                        <TableCell className="font-medium">{r.regularizationNumber}</TableCell>
                        <TableCell>{r.employeeName}</TableCell>
                        <TableCell>{formatDate(r.attendanceDate)}</TableCell>
                        <TableCell>{humanizeEnum(r.type)}</TableCell>
                        <TableCell>{formatDateTime(r.requestDate)}</TableCell>
                        <TableCell>
                          <StatusBadge status={r.isApplied ? 'Applied' : r.status} />
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {(() => {
                            if (!summary?.currentStepName) return '—';
                            const approvers = formatPendingApprovers(summary.pendingApprovers ?? []);
                            return approvers.short
                              ? `${summary.currentStepName} · ${approvers.short}`
                              : summary.currentStepName;
                          })()}
                        </TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
