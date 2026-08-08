'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Wand2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { regularizationService } from '@/services/hr/attendance.service';
import {
  formatDate,
  formatDateTime,
  formatTime,
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
 * One attendance regularization.
 *
 * The workflow engine owns approve/reject; the screen never sets a status itself, it just
 * refetches and lets `StaffAttendanceRegularizationWorkflowStatusAdapter` decide.
 *
 * "Apply" is deliberately separate from approval: approving accepts the request, applying
 * writes the requested times onto the underlying daily attendance record. Keeping them apart
 * means an approved correction can be reviewed before it changes reported hours.
 */
export default function RegularizationDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [applying, setApplying] = useState(false);
  const [confirmApply, setConfirmApply] = useState(false);

  const { data: r, isLoading, isError } = useQuery({
    queryKey: ['hr', 'regularizations', id],
    queryFn: () => regularizationService.getById(id),
    enabled: !!id,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'regularizations'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'daily-attendance'] });
  };

  const workflow = useWorkflowRecord({
    entityType: 'StaffAttendanceRegularization',
    entityId: id,
    entityLabel: 'Attendance Regularization',
    entityNumber: r?.regularizationNumber,
    status: r?.status ?? 'Pending',
    // There is no Draft state — the workflow starts when the request is created, so there
    // is never a submit action to offer here.
    canSubmit: false,
    canApproveReject: r?.status === 'Pending',
    enabled: !!r,
    commands: {
      approve: (ctx) => regularizationService.approve(id, ctx.comments || null),
      reject: (ctx) => regularizationService.reject(id, ctx.comments || 'Rejected'),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const applyCorrection = async () => {
    setApplying(true);
    try {
      await regularizationService.apply(id);
      await refresh();
      await workflow.refresh();
      toast({ title: 'Applied', description: 'The correction was written to the attendance record.' });
      setConfirmApply(false);
      return true;
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to apply the correction.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setApplying(false);
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
        <EmptyState title="Regularization not found" description="It may have been removed." />
      </div>
    );
  }

  const canApply = r.status === 'Approved' && !r.isApplied;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={r.regularizationNumber}
        description={`${r.employeeName} · ${humanizeEnum(r.type)} on ${formatDate(r.attendanceDate)}`}
        backHref="/hr/attendance/regularizations"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={r.isApplied ? 'Applied' : r.status} />
            <WorkflowApprovalActions {...workflow.actionProps} />
            {canApply && (
              <Button onClick={() => setConfirmApply(true)}>
                <Wand2 className="mr-2 h-4 w-4" /> Apply correction
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
              <CardTitle className="text-base">Request</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Employee" value={`${r.employeeName} (${r.employeeNumber})`} />
              <InfoRow label="Attendance date" value={formatDate(r.attendanceDate)} />
              <InfoRow label="Type" value={humanizeEnum(r.type)} />
              <InfoRow label="Raised on" value={formatDateTime(r.requestDate)} />
              <InfoRow
                label="Requested check-in"
                value={formatTime(r.requestedCheckInTime)}
              />
              <InfoRow
                label="Requested check-out"
                value={formatTime(r.requestedCheckOutTime)}
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Reason</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <p className="whitespace-pre-wrap">{r.reason || '—'}</p>
              {r.supportingDocuments && (
                <div>
                  <p className="text-xs text-muted-foreground">Supporting documents</p>
                  <p className="whitespace-pre-wrap">{r.supportingDocuments}</p>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Outcome</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Approved by" value={r.approvedByName} />
              <InfoRow label="Approved on" value={formatDateTime(r.approvalDate)} />
              <InfoRow label="Approval comments" value={r.approvalComments} />
              <InfoRow label="Rejected on" value={formatDateTime(r.rejectedDate)} />
              <InfoRow label="Rejection reason" value={r.rejectionReason} />
              <InfoRow
                label="Applied"
                value={r.isApplied ? formatDateTime(r.appliedDate) : 'Not yet applied'}
              />
            </CardContent>
          </Card>

          <Button
            variant="outline"
            onClick={() => router.push(`/hr/attendance/daily/${r.attendanceId}`)}
          >
            Open the attendance record
          </Button>
        </TabsContent>

        <WorkflowTabContent
          value="workflow"
          entityType="StaffAttendanceRegularization"
          entityId={id}
          entityLabel="Attendance Regularization"
          entityNumber={r.regularizationNumber}
          status={r.status}
          onAfterAction={async () => {
            await refresh();
            await workflow.refresh();
          }}
        />
      </Tabs>

      <ConfirmationDialog
        open={confirmApply}
        onOpenChange={setConfirmApply}
        title="Apply this correction?"
        description="Writes the requested times onto the daily attendance record. Reported hours will change."
        confirmText="Apply"
        isLoading={applying}
        onConfirm={applyCorrection}
      />
    </div>
  );
}
