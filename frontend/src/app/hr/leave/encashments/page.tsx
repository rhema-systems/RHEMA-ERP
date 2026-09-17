'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Banknote, MoreHorizontal, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
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
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { leaveEncashmentService } from '@/services/hr/leave.service';
import type { LeaveEncashment } from '@/types/hr/leave-request';

const ALL = '__all__';
const currentYear = new Date().getFullYear();
const years = [currentYear + 1, currentYear, currentYear - 1, currentYear - 2];
const statuses = ['Draft', 'Submitted', 'PendingApproval', 'Approved', 'Rejected', 'Processed', 'Cancelled'];

type Pending =
  | { kind: 'approve'; row: LeaveEncashment }
  | { kind: 'reject'; row: LeaveEncashment }
  | { kind: 'process'; row: LeaveEncashment };

/**
 * Encashment requests. Approve/reject route through the workflow engine
 * (LeaveEncashmentWorkflowStatusAdapter decides the resulting status); marking as
 * processed is the separate payment step that follows approval.
 */
export default function LeaveEncashmentsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [year, setYear] = useState(String(currentYear));
  const [status, setStatus] = useState(ALL);
  const [pending, setPending] = useState<Pending | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [paymentReference, setPaymentReference] = useState('');

  const queryKey = ['hr', 'leave-encashments', year, status];

  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () => leaveEncashmentService.getAll(Number(year), status === ALL ? undefined : status),
  });

  const mutation = useMutation({
    mutationFn: async (p: Pending) => {
      if (p.kind === 'approve') return leaveEncashmentService.approve(p.row.id);
      if (p.kind === 'reject') {
        if (!rejectReason.trim()) throw new Error('A rejection reason is required.');
        return leaveEncashmentService.reject(p.row.id, rejectReason.trim());
      }
      if (!paymentReference.trim()) throw new Error('A payment reference is required.');
      return leaveEncashmentService.markAsProcessed(p.row.id, {
        paymentReference: paymentReference.trim(),
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-encashments'] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-balances'] });
      toast({ title: 'Done', description: 'Encashment updated.' });
      setPending(null);
      setRejectReason('');
      setPaymentReference('');
    },
    onError: (e: any) =>
      toast({ title: 'Error', description: e?.message || 'Action failed.', variant: 'destructive' }),
  });

  const rows = data ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave Encashments"
        description="Requests to convert unused leave days into cash."
      />

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 md:grid-cols-2 lg:max-w-xl">
            <div className="space-y-2">
              <Label>Year</Label>
              <Select value={year} onValueChange={setYear}>
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
              <Label>Status</Label>
              <Select value={status} onValueChange={setStatus}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All statuses</SelectItem>
                  {statuses.map((s) => (
                    <SelectItem key={s} value={s}>
                      {s}
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
          <CardTitle>Encashments</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Leave type</TableHead>
                  <TableHead>Year</TableHead>
                  <TableHead className="text-right">Days</TableHead>
                  <TableHead className="text-right">Amount</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Payment ref</TableHead>
                  <TableHead className="w-[60px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(4)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(8)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[70px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8}>
                      <EmptyState
                        icon={Banknote}
                        title="No encashments"
                        description="Encashment requests appear here once employees convert unused days."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((e) => (
                    <TableRow key={e.id}>
                      <TableCell className="font-medium">{e.employeeName}</TableCell>
                      <TableCell>{e.leaveTypeName}</TableCell>
                      <TableCell>{e.year}</TableCell>
                      <TableCell className="text-right">{e.daysEncashed}</TableCell>
                      <TableCell className="text-right">
                        {e.amountPaid?.toLocaleString() ?? '—'}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={e.status} />
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {e.paymentReference || '—'}
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon" className="h-8 w-8">
                              <MoreHorizontal className="h-4 w-4" />
                              <span className="sr-only">Actions</span>
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            {['Submitted', 'PendingApproval'].includes(e.status) && (
                              <>
                                <DropdownMenuItem
                                  onClick={() => setPending({ kind: 'approve', row: e })}
                                >
                                  Approve
                                </DropdownMenuItem>
                                <DropdownMenuItem
                                  className="text-red-600"
                                  onClick={() => setPending({ kind: 'reject', row: e })}
                                >
                                  Reject
                                </DropdownMenuItem>
                              </>
                            )}
                            {e.status === 'Approved' && (
                              <DropdownMenuItem
                                onClick={() => setPending({ kind: 'process', row: e })}
                              >
                                Mark as paid
                              </DropdownMenuItem>
                            )}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={pending !== null}
        onOpenChange={(open) => !open && setPending(null)}
        title={
          pending?.kind === 'approve'
            ? 'Approve encashment?'
            : pending?.kind === 'reject'
              ? 'Reject encashment?'
              : 'Mark encashment as paid?'
        }
        description={
          pending?.kind === 'process'
            ? 'Records the payment against this approved encashment.'
            : undefined
        }
        confirmText={
          pending?.kind === 'approve' ? 'Approve' : pending?.kind === 'reject' ? 'Reject' : 'Mark paid'
        }
        variant={pending?.kind === 'reject' ? 'destructive' : 'default'}
        isLoading={mutation.isPending}
        onConfirm={async () => {
          if (!pending) return false;
          try {
            await mutation.mutateAsync(pending);
            return true;
          } catch {
            return false;
          }
        }}
      >
        {pending?.kind === 'reject' && (
          <div className="space-y-2">
            <Label htmlFor="rejectReason">Reason</Label>
            <Textarea
              id="rejectReason"
              rows={3}
              value={rejectReason}
              onChange={(ev) => setRejectReason(ev.target.value)}
            />
          </div>
        )}
        {pending?.kind === 'process' && (
          <div className="space-y-2">
            <Label htmlFor="paymentReference">Payment reference</Label>
            <Input
              id="paymentReference"
              value={paymentReference}
              onChange={(ev) => setPaymentReference(ev.target.value)}
              placeholder="Payment voucher or transaction id"
            />
          </div>
        )}
      </ConfirmationDialog>
    </div>
  );
}
