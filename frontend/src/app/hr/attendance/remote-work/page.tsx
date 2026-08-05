'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Plus, House, Loader2 } from 'lucide-react';
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
import { remoteWorkRequestService } from '@/services/hr/attendance.service';
import { formatDate, formatDateTime } from '@/lib/hr/attendance-format';
import { REMOTE_WORK_STATUS_OPTIONS } from '@/types/hr/attendance';
import type { RemoteWorkRequestStatus, RemoteWorkRequestSummary } from '@/types/hr/attendance';

const PENDING = '__pending__';
const ALL = '__all__';

export default function RemoteWorkRequestsPage() {
  const router = useRouter();
  const [filter, setFilter] = useState<string>(PENDING);

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'remote-work-requests', 'list', filter],
    queryFn: () => {
      if (filter === PENDING) return remoteWorkRequestService.getPendingApproval();
      if (filter === ALL) return remoteWorkRequestService.getPaged(1, 100).then((p) => p.items);
      return remoteWorkRequestService.getByStatus(filter as RemoteWorkRequestStatus);
    },
  });

  const rows: RemoteWorkRequestSummary[] = data ?? [];

  const summaries = useWorkflowEntitySummaries(
    'RemoteWorkRequest',
    rows.map((r) => r.id),
    rows.length > 0,
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Remote Work Requests"
        description="Work-from-home and off-site requests, approved through the workflow."
        backHref="/hr/attendance"
        actions={
          <Button onClick={() => router.push('/hr/attendance/remote-work/new')}>
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
                {REMOTE_WORK_STATUS_OPTIONS.map((o) => (
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
                  <TableHead>From</TableHead>
                  <TableHead>To</TableHead>
                  <TableHead className="text-right">Days</TableHead>
                  <TableHead>Raised</TableHead>
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
                        icon={House}
                        title="No remote work requests"
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
                        onClick={() => router.push(`/hr/attendance/remote-work/${r.id}`)}
                      >
                        <TableCell className="font-medium">{r.requestNumber}</TableCell>
                        <TableCell>{r.employeeName}</TableCell>
                        <TableCell>{formatDate(r.startDate)}</TableCell>
                        <TableCell>{formatDate(r.endDate)}</TableCell>
                        <TableCell className="text-right">{r.requestedDays}</TableCell>
                        <TableCell>{formatDateTime(r.requestDate)}</TableCell>
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
