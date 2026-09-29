'use client';

import { useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Plus, CalendarDays, Loader2 } from 'lucide-react';
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
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useLeaveYear } from '@/components/hr/leave/use-leave-year';
import { useAuth } from '@/hooks/use-auth';
import { useWorkflowEntitySummaries, formatPendingApprovers } from '@/hooks/useWorkflowEntitySummaries';
import { leaveService } from '@/services/hr/leave.service';
import { LEAVE_STATUS_OPTIONS } from '@/types/hr/leave-request';

const ALL = '__all__';

/**
 * Leave requests are listed per employee — the backend exposes history by employee rather
 * than a tenant-wide paged search, so an employee must be chosen first.
 */
export default function LeaveRequestsPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { user } = useAuth();
  // The employee profile's Leave tab links here as `?employeeId=…`. The parameter used to be
  // ignored, so "open in Leave" landed on whoever the picker happened to default to — a different
  // person's leave, presented as if it were theirs (closure plan L-8). It only seeds the initial
  // value; the picker stays free afterwards.
  //
  // 2026-09-03: without one, open the caller's own history first. The API answers
  // self-or-HR.Leave.Read, so a plain employee who lands here (the leaf is open to all staff) sees
  // their requests instead of an empty picker and a 403 on whoever they choose.
  const linkedEmployeeId = searchParams?.get('employeeId') || null;
  const [employeeId, setEmployeeId] = useState<string | null>(
    linkedEmployeeId ?? user?.employeeId ?? null,
  );
  const selfLabel = user?.employeeId ? [user.firstName, user.lastName].filter(Boolean).join(' ') || 'Me' : null;
  // ⚠ Leave settings audit 2, L-95: the current leave year, not the calendar year; the choice is
  // kept apart so a late answer moves the default and never overrides it (see useLeaveYear).
  const { currentYear } = useLeaveYear();
  const years = [currentYear + 1, currentYear, currentYear - 1, currentYear - 2];
  const [chosenYear, setYear] = useState<string | null>(null);
  const year = chosenYear ?? String(currentYear);
  const [status, setStatus] = useState<string>(ALL);
  const [page, setPage] = useState(1);

  // The status filter belongs in the query, not in the browser: filtering the fetched page meant
  // "the rejected ones that happen to be on page 1", with the unfiltered total beside it (L-7).
  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'leave-requests', 'history', employeeId, year, status, page],
    queryFn: () =>
      leaveService.getEmployeeHistory(
        employeeId ?? '',
        Number(year),
        page,
        20,
        status === ALL ? undefined : status,
      ),
    enabled: !!employeeId,
  });

  const rows = useMemo(() => data?.items ?? [], [data]);

  // Name whoever the picker is currently pointed at. Arriving by deep link there is no label to
  // show, so it comes from the rows themselves once they load; without it the filter reads as an
  // empty search box above somebody else's leave.
  const pickerLabel =
    employeeId === user?.employeeId ? selfLabel : rows[0]?.employeeName ?? null;

  // Batch workflow summaries so each row can show where it sits in its approval chain.
  const summaries = useWorkflowEntitySummaries(
    'LeaveRequest',
    rows.map((r) => r.id),
    rows.length > 0,
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave Requests"
        description="Requests and their position in the approval workflow."
        actions={
          <Button onClick={() => router.push('/hr/leave/requests/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Request
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <label className="text-sm font-medium">Employee</label>
              <EmployeePicker
                value={employeeId}
                initialLabel={pickerLabel}
                onChange={(v) => {
                  setEmployeeId(v);
                  setPage(1);
                }}
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Year</label>
              <Select
                value={year}
                onValueChange={(v) => {
                  setYear(v);
                  setPage(1);
                }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {years.map((y) => (
                    <SelectItem key={y} value={String(y)}>
                      {y}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Status</label>
              <Select
                value={status}
                onValueChange={(v) => {
                  setStatus(v);
                  setPage(1);
                }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All statuses</SelectItem>
                  {LEAVE_STATUS_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Requests</CardTitle>
            {isFetching && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
          </div>
        </CardHeader>
        <CardContent>
          {!employeeId ? (
            <EmptyState
              icon={CalendarDays}
              title="Choose an employee"
              description="Leave history is listed per employee."
            />
          ) : (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Request</TableHead>
                    <TableHead>Leave type</TableHead>
                    <TableHead>From</TableHead>
                    <TableHead>To</TableHead>
                    <TableHead className="text-right">Days</TableHead>
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
                          icon={CalendarDays}
                          title="No leave requests"
                          description="Nothing matches these filters."
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
                          onClick={() => router.push(`/hr/leave/requests/${r.id}`)}
                        >
                          <TableCell className="font-medium">{r.requestNumber}</TableCell>
                          <TableCell>
                            {r.leaveTypeName}
                            {r.leaveSubTypeName ? ` · ${r.leaveSubTypeName}` : ''}
                          </TableCell>
                          <TableCell>{r.startDate?.slice(0, 10)}</TableCell>
                          <TableCell>{r.endDate?.slice(0, 10)}</TableCell>
                          <TableCell className="text-right">{r.totalDays}</TableCell>
                          <TableCell>
                            <StatusBadge status={r.status} />
                          </TableCell>
                          <TableCell className="text-muted-foreground">
                            {(() => {
                              if (!summary?.currentStepName) return '—';
                              const approvers = formatPendingApprovers(
                                summary.pendingApprovers ?? [],
                              );
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
          )}

          {employeeId && data && data.totalPages > 1 && (
            <div className="flex items-center justify-between pt-4">
              <p className="text-sm text-muted-foreground">
                Page {data.page} of {data.totalPages} · {data.totalCount} requests
              </p>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasPrevious}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasNext}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
