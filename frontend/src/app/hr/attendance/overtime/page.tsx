'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Plus, Timer, Loader2 } from 'lucide-react';
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
import { overtimeRequestService } from '@/services/hr/attendance.service';
import { formatDate, formatHours, humanizeEnum } from '@/lib/hr/attendance-format';
import { OVERTIME_REQUEST_STATUS_OPTIONS } from '@/types/hr/attendance';
import type { OvertimeRequestStatus, StaffOvertimeRequestSummary } from '@/types/hr/attendance';

const PENDING = '__pending__';
const AWAITING_CONFIRMATION = '__awaiting_confirmation__';
const ALL = '__all__';

/**
 * Overtime requests run a three-level lifecycle: raised → pre-approved through the workflow
 * → the supervisor confirms the hours actually worked. The "awaiting confirmation" filter
 * surfaces that last step, which is easy to lose track of because the request already looks
 * approved.
 */
export default function OvertimeRequestsPage() {
  const router = useRouter();
  const [filter, setFilter] = useState<string>(PENDING);

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'overtime-requests', 'list', filter],
    queryFn: () => {
      if (filter === PENDING) return overtimeRequestService.getPendingApproval();
      if (filter === AWAITING_CONFIRMATION)
        return overtimeRequestService.getPendingSupervisorConfirmation();
      if (filter === ALL) return overtimeRequestService.getPaged(1, 100).then((p) => p.items);
      return overtimeRequestService.getByStatus(filter as OvertimeRequestStatus);
    },
  });

  const rows: StaffOvertimeRequestSummary[] = data ?? [];

  const summaries = useWorkflowEntitySummaries(
    'StaffOvertimeRequest',
    rows.map((r) => r.id),
    rows.length > 0,
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Overtime Requests"
        description="Pre-approval of planned overtime, then confirmation of the hours actually worked."
        backHref="/hr/attendance"
        actions={
          <Button onClick={() => router.push('/hr/attendance/overtime/new')}>
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
                <SelectItem value={AWAITING_CONFIRMATION}>
                  Awaiting supervisor confirmation
                </SelectItem>
                <SelectItem value={ALL}>All requests</SelectItem>
                {OVERTIME_REQUEST_STATUS_OPTIONS.map((o) => (
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
                  <TableHead>Overtime date</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead className="text-right">Planned</TableHead>
                  <TableHead className="text-right">Actual</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Awaiting</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(8)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8}>
                      <EmptyState
                        icon={Timer}
                        title="No overtime requests"
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
                        onClick={() => router.push(`/hr/attendance/overtime/${r.id}`)}
                      >
                        <TableCell className="font-medium">{r.requestNumber}</TableCell>
                        <TableCell>{r.employeeName}</TableCell>
                        <TableCell>{formatDate(r.overtimeDate)}</TableCell>
                        <TableCell>{humanizeEnum(r.type)}</TableCell>
                        <TableCell className="text-right">
                          {formatHours(r.plannedOvertimeHours)}
                        </TableCell>
                        <TableCell className="text-right">
                          {formatHours(r.actualOvertimeHours)}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={r.status} />
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
