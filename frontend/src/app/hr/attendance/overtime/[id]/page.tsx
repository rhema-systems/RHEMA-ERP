'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, CheckCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { overtimeRequestService } from '@/services/hr/attendance.service';
import {
  formatDate,
  formatDateTime,
  formatTime,
  formatHours,
  humanizeEnum,
} from '@/lib/hr/attendance-format';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

/**
 * One overtime request.
 *
 * Two distinct decisions live on this page and they are not the same thing:
 *  - **Pre-approval** goes through the workflow engine (`WorkflowApprovalActions`), and
 *    authorises the overtime *before* it is worked.
 *  - **Supervisor confirmation** records the hours actually worked afterwards, and is what
 *    payroll pays against. It is not a workflow step — it happens once, after approval.
 */
export default function OvertimeRequestDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [actualHours, setActualHours] = useState('');
  const [supervisorNotes, setSupervisorNotes] = useState('');

  const { data: r, isLoading, isError } = useQuery({
    queryKey: ['hr', 'overtime-requests', id],
    queryFn: () => overtimeRequestService.getById(id),
    enabled: !!id,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'overtime-requests'] });
  };

  const workflow = useWorkflowRecord({
    entityType: 'StaffOvertimeRequest',
    entityId: id,
    entityLabel: 'Overtime Request',
    entityNumber: r?.requestNumber,
    status: r?.status ?? 'Pending',
    // No Draft state — the workflow starts at creation, so there is nothing to submit.
    canSubmit: false,
    canApproveReject: r?.status === 'Pending',
    enabled: !!r,
    commands: {
      approve: (ctx) => overtimeRequestService.approve(id, ctx.comments || null),
      reject: (ctx) => overtimeRequestService.reject(id, ctx.comments || 'Rejected'),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const openConfirm = () => {
    // Pre-fill with what was planned — usually right, and it makes the common case one click.
    setActualHours(String(r?.plannedOvertimeHours ?? ''));
    setSupervisorNotes('');
    setConfirming(true);
  };

  const confirmHours = async () => {
    const hours = Number(actualHours);
    if (!Number.isFinite(hours) || hours < 0.5 || hours > 24) {
      toast({
        title: 'Enter the hours worked',
        description: 'Actual overtime must be between 0.5 and 24 hours.',
        variant: 'destructive',
      });
      return false;
    }
    setBusy(true);
    try {
      await overtimeRequestService.confirmActualHours(id, hours, supervisorNotes.trim() || null);
      await refresh();
      await workflow.refresh();
      toast({ title: 'Confirmed', description: 'The hours worked were recorded.' });
      setConfirming(false);
      return true;
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to confirm the hours.',
        variant: 'destructive',
      });
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
        <EmptyState title="Overtime request not found" description="It may have been removed." />
      </div>
    );
  }

  const canConfirm = r.status === 'Approved' && !r.supervisorConfirmedDate;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={r.requestNumber}
        description={`${r.employeeName} · ${formatHours(r.plannedOvertimeHours)} on ${formatDate(r.overtimeDate)}`}
        backHref="/hr/attendance/overtime"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={r.status} />
            <WorkflowApprovalActions {...workflow.actionProps} />
            {canConfirm && (
              <Button onClick={openConfirm}>
                <CheckCheck className="mr-2 h-4 w-4" /> Confirm hours worked
              </Button>
            )}
          </div>
        }
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <WorkflowTabTrigger value="workflow" />
        </TabsList>

        <TabsContent value="overview" className="space-y-4 pt-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Planned overtime</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Employee" value={`${r.employeeName} (${r.employeeNumber})`} />
              <InfoRow label="Overtime date" value={formatDate(r.overtimeDate)} />
              <InfoRow label="Type" value={humanizeEnum(r.type)} />
              <InfoRow label="Planned start" value={formatTime(r.plannedStartTime)} />
              <InfoRow label="Planned end" value={formatTime(r.plannedEndTime)} />
              <InfoRow label="Planned hours" value={formatHours(r.plannedOvertimeHours)} />
              <InfoRow label="Raised on" value={formatDateTime(r.requestDate)} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Purpose</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <p className="whitespace-pre-wrap">{r.purpose || '—'}</p>
              {r.taskDetails && (
                <div>
                  <p className="text-xs text-muted-foreground">Task details</p>
                  <p className="whitespace-pre-wrap">{r.taskDetails}</p>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Pre-approval</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Approved by" value={r.approvedByName} />
              <InfoRow label="Approved on" value={formatDateTime(r.approvalDate)} />
              <InfoRow label="Comments" value={r.approvalComments} />
              <InfoRow label="Rejected on" value={formatDateTime(r.rejectedDate)} />
              <InfoRow label="Rejection reason" value={r.rejectionReason} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Hours actually worked</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Actual hours" value={formatHours(r.actualOvertimeHours)} />
              <InfoRow label="Confirmed by" value={r.supervisorConfirmedByName} />
              <InfoRow label="Confirmed on" value={formatDateTime(r.supervisorConfirmedDate)} />
              <InfoRow label="Supervisor notes" value={r.supervisorNotes} />
            </CardContent>
          </Card>

          {r.attendanceId && (
            <Button
              variant="outline"
              onClick={() => router.push(`/hr/attendance/daily/${r.attendanceId}`)}
            >
              Open the attendance record
            </Button>
          )}
        </TabsContent>

        <WorkflowTabContent
          value="workflow"
          entityType="StaffOvertimeRequest"
          entityId={id}
          entityLabel="Overtime Request"
          entityNumber={r.requestNumber}
          status={r.status}
          onAfterAction={async () => {
            await refresh();
            await workflow.refresh();
          }}
        />
      </Tabs>

      <ConfirmationDialog
        open={confirming}
        onOpenChange={setConfirming}
        title="Confirm hours worked"
        description="This is the figure payroll pays against — it replaces the planned hours."
        confirmText="Confirm"
        isLoading={busy}
        onConfirm={confirmHours}
      >
        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="actualHours">Actual overtime hours</Label>
            <Input
              id="actualHours"
              type="number"
              step="0.25"
              min="0.5"
              max="24"
              value={actualHours}
              onChange={(e) => setActualHours(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              Planned: {formatHours(r.plannedOvertimeHours)}
            </p>
          </div>
          <div className="space-y-2">
            <Label htmlFor="supervisorNotes">Notes</Label>
            <Textarea
              id="supervisorNotes"
              rows={3}
              value={supervisorNotes}
              onChange={(e) => setSupervisorNotes(e.target.value)}
              placeholder="Optional — explain any difference from the plan"
            />
          </div>
        </div>
      </ConfirmationDialog>
    </div>
  );
}
