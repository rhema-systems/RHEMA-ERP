'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Ban, CheckCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { useAuth } from '@/hooks/use-auth';
import { leaveService } from '@/services/hr/leave.service';
import { LeaveAttachmentsPanel } from '@/components/hr/leave/LeaveAttachmentsPanel';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

function InfoCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">{title}</CardTitle>
      </CardHeader>
      <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">{children}</CardContent>
    </Card>
  );
}

export default function LeaveRequestDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { user } = useAuth();

  const [action, setAction] = useState<null | 'cancel' | 'close'>(null);
  const [busy, setBusy] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [closureNotes, setClosureNotes] = useState('');

  const { data: r, isLoading, isError } = useQuery({
    queryKey: ['hr', 'leave-requests', id],
    queryFn: () => leaveService.getById(id),
    enabled: !!id,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-requests'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-balances'] });
  };

  /**
   * The generic workflow engine owns submit/approve/reject. The backend endpoints are thin
   * pass-throughs to LeaveService, which drives the engine and then applies the resulting
   * status via LeaveRequestWorkflowStatusAdapter — so this page never sets a status itself,
   * it just refetches.
   */
  const workflow = useWorkflowRecord({
    entityType: 'LeaveRequest',
    entityId: id,
    entityLabel: 'Leave Request',
    entityNumber: r?.requestNumber,
    status: r?.status ?? 'Draft',
    canSubmit: r?.status === 'Draft',
    canApproveReject: r?.status === 'Pending',
    enabled: !!r,
    commands: {
      submit: () => leaveService.submit(id),
      approve: (ctx) =>
        leaveService.approve(id, {
          approvedBy: (user?.id as string) ?? '',
          approvalNotes: ctx.comments || null,
          comments: ctx.comments || null,
        }),
      reject: (ctx) => leaveService.reject(id, { rejectionReason: ctx.comments || 'Rejected' }),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const runAction = async () => {
    if (!action) return false;
    setBusy(true);
    try {
      if (action === 'cancel') {
        if (!cancelReason.trim()) {
          toast({ title: 'A reason is required', variant: 'destructive' });
          return false;
        }
        await leaveService.cancel(id, cancelReason.trim());
      } else {
        await leaveService.close(id, { closureNotes: closureNotes.trim() || null });
      }
      await refresh();
      await workflow.refresh();
      toast({ title: 'Done', description: `Leave request ${action === 'cancel' ? 'cancelled' : 'closed'}.` });
      setAction(null);
      setCancelReason('');
      setClosureNotes('');
      return true;
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'Action failed.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !r) {
    return (
      <div className="p-6">
        <EmptyState title="Leave request not found" description="It may have been removed." />
      </div>
    );
  }

  const isDraft = r.status === 'Draft';
  const canCancel = ['Draft', 'Pending', 'Approved'].includes(r.status);
  const canClose = r.status === 'InProgress' || r.status === 'Approved';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={r.requestNumber}
        description={`${r.employeeName} · ${r.leaveTypeName}${r.leaveSubTypeName ? ` (${r.leaveSubTypeName})` : ''}`}
        backHref="/hr/leave/requests"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={r.status} />
            {isDraft && (
              <Button
                variant="outline"
                onClick={() => router.push(`/hr/leave/requests/${id}/edit`)}
              >
                <Pencil className="mr-2 h-4 w-4" /> Edit
              </Button>
            )}

            {/* Submit / approve / reject / recall all come from the workflow engine. */}
            <WorkflowApprovalActions {...workflow.actionProps} />

            {canClose && (
              <Button variant="outline" onClick={() => setAction('close')}>
                <CheckCheck className="mr-2 h-4 w-4" /> Close
              </Button>
            )}
            {canCancel && (
              <Button variant="destructive" onClick={() => setAction('cancel')}>
                <Ban className="mr-2 h-4 w-4" /> Cancel
              </Button>
            )}
          </div>
        }
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="attachments">Attachments</TabsTrigger>
          <WorkflowTabTrigger value="workflow" />
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
          <InfoCard title="Leave">
            <InfoRow label="Leave type" value={r.leaveTypeName} />
            <InfoRow label="Sub-type" value={r.leaveSubTypeName} />
            <InfoRow label="Paid" value={r.isPaidLeave ? 'Yes' : 'No'} />
            <InfoRow label="Start date" value={r.startDate?.slice(0, 10)} />
            <InfoRow label="End date" value={r.endDate?.slice(0, 10)} />
            <InfoRow label="Total days" value={r.totalDays} />
            <InfoRow label="Requested on" value={r.requestDate?.slice(0, 10)} />
            <InfoRow label="From a plan" value={r.leavePlanId ? 'Yes' : 'No'} />
          </InfoCard>

          <InfoCard title="Employee & cover">
            <InfoRow label="Employee" value={`${r.employeeName} (${r.employeeNumber})`} />
            <InfoRow label="Reliever" value={r.relieverEmployeeName} />
            <InfoRow label="Second reliever" value={r.secondRelieverEmployeeName} />
          </InfoCard>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Reason & notes</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <div>
                <p className="text-xs text-muted-foreground">Reason</p>
                <p className="whitespace-pre-wrap">{r.reason || '—'}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Handover notes</p>
                <p className="whitespace-pre-wrap">{r.handoverNotes || '—'}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Reliever notes</p>
                <p className="whitespace-pre-wrap">{r.relieverNotes || '—'}</p>
              </div>
            </CardContent>
          </Card>

          {(r.cancellationDate || r.closureDate) && (
            <InfoCard title="Outcome">
              <InfoRow label="Cancelled on" value={r.cancellationDate?.slice(0, 10)} />
              <InfoRow label="Cancellation reason" value={r.cancellationReason} />
              <InfoRow label="Closed on" value={r.closureDate?.slice(0, 10)} />
              <InfoRow label="Closure notes" value={r.closureNotes} />
            </InfoCard>
          )}
        </TabsContent>

        <TabsContent value="attachments" className="pt-4">
          <LeaveAttachmentsPanel leaveRequestId={id} canUpload={isDraft || r.status === 'Pending'} />
        </TabsContent>

        <WorkflowTabContent
          value="workflow"
          entityType="LeaveRequest"
          entityId={id}
          entityLabel="Leave Request"
          entityNumber={r.requestNumber}
          status={r.status}
          onAfterAction={async () => {
            await refresh();
            await workflow.refresh();
          }}
        />
      </Tabs>

      <ConfirmationDialog
        open={action !== null}
        onOpenChange={(open) => !open && setAction(null)}
        title={action === 'cancel' ? 'Cancel leave request?' : 'Close leave request?'}
        description={
          action === 'cancel'
            ? 'The request is withdrawn and any pending days are released.'
            : 'Marks the leave as taken and complete.'
        }
        confirmText={action === 'cancel' ? 'Cancel request' : 'Close'}
        variant={action === 'cancel' ? 'destructive' : 'default'}
        isLoading={busy}
        onConfirm={runAction}
      >
        {action === 'cancel' ? (
          <div className="space-y-2">
            <Label htmlFor="cancelReason">Reason</Label>
            <Textarea
              id="cancelReason"
              rows={3}
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              placeholder="Why is this being cancelled?"
            />
          </div>
        ) : (
          <div className="space-y-2">
            <Label htmlFor="closureNotes">Closure notes</Label>
            <Textarea
              id="closureNotes"
              rows={3}
              value={closureNotes}
              onChange={(e) => setClosureNotes(e.target.value)}
              placeholder="Optional"
            />
          </div>
        )}
      </ConfirmationDialog>
    </div>
  );
}
