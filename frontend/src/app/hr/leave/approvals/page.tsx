'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, Info } from 'lucide-react';
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
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { leaveService } from '@/services/hr/leave.service';

/**
 * Leave requests awaiting a given manager's decision.
 *
 * The decision itself is taken on the request's detail page, where the shared workflow
 * action component enforces the engine's rules (step order, delegation, checklists,
 * e-signature). This screen is the queue, not a second approval implementation — the
 * cross-module inbox at /workflow/inbox lists the same items alongside every other type.
 */
export default function LeaveApprovalsPage() {
  const router = useRouter();
  const [managerId, setManagerId] = useState<string | null>(null);
  const [page, setPage] = useState(1);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'leave-requests', 'pending-approvals', managerId, page],
    queryFn: () => leaveService.getPendingApprovals(managerId ?? '', page, 20),
    enabled: !!managerId,
  });

  const rows = data?.items ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave Approvals"
        description="Requests waiting on a manager's decision."
        actions={
          <Button variant="outline" onClick={() => router.push('/workflow/inbox')}>
            Open approval inbox
          </Button>
        }
      />

      <div className="flex items-start gap-2 rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">
        <Info className="mt-0.5 h-4 w-4 shrink-0" />
        <p>
          Approve or reject from a request&apos;s own page — the workflow engine applies step
          order, delegation and any required checklists or signatures there.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Manager</CardTitle>
        </CardHeader>
        <CardContent className="max-w-md">
          <EmployeePicker
            value={managerId}
            onChange={(v) => {
              setManagerId(v);
              setPage(1);
            }}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Pending requests</CardTitle>
        </CardHeader>
        <CardContent>
          {!managerId ? (
            <EmptyState
              icon={CheckCircle2}
              title="Choose a manager"
              description="Pending approvals are listed per approving manager."
            />
          ) : (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Request</TableHead>
                    <TableHead>Employee</TableHead>
                    <TableHead>Leave type</TableHead>
                    <TableHead>From</TableHead>
                    <TableHead>To</TableHead>
                    <TableHead className="text-right">Days</TableHead>
                    <TableHead>Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {isLoading ? (
                    [...Array(4)].map((_, i) => (
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
                          icon={CheckCircle2}
                          title="Nothing pending"
                          description="This manager has no leave requests awaiting a decision."
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    rows.map((r) => (
                      <TableRow
                        key={r.id}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => router.push(`/hr/leave/requests/${r.id}`)}
                      >
                        <TableCell className="font-medium">{r.requestNumber}</TableCell>
                        <TableCell>{r.employeeName}</TableCell>
                        <TableCell>{r.leaveTypeName}</TableCell>
                        <TableCell>{r.startDate?.slice(0, 10)}</TableCell>
                        <TableCell>{r.endDate?.slice(0, 10)}</TableCell>
                        <TableCell className="text-right">{r.totalDays}</TableCell>
                        <TableCell>
                          <StatusBadge status={r.status} />
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          )}

          {managerId && data && data.totalPages > 1 && (
            <div className="flex items-center justify-between pt-4">
              <p className="text-sm text-muted-foreground">
                Page {data.page} of {data.totalPages} · {data.totalCount} pending
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
