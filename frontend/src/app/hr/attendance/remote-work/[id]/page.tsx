'use client';

import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { remoteWorkRequestService } from '@/services/hr/attendance.service';
import { formatDate, formatDateTime } from '@/lib/hr/attendance-format';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

export default function RemoteWorkRequestDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();

  const { data: r, isLoading, isError } = useQuery({
    queryKey: ['hr', 'remote-work-requests', id],
    queryFn: () => remoteWorkRequestService.getById(id),
    enabled: !!id,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'remote-work-requests'] });
  };

  const workflow = useWorkflowRecord({
    recallPrompt: 'reason',
    entityType: 'RemoteWorkRequest',
    entityId: id,
    entityLabel: 'Remote Work Request',
    entityNumber: r?.requestNumber,
    status: r?.status ?? 'Pending',
    // No Draft state — the workflow starts at creation, so there is nothing to submit.
    canSubmit: false,
    canApproveReject: r?.status === 'Pending',
    enabled: !!r,
    commands: {
      approve: (ctx) => remoteWorkRequestService.approve(id, ctx.comments || null),
      reject: (ctx) => remoteWorkRequestService.reject(id, ctx.comments || 'Rejected'),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

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
        <EmptyState title="Remote work request not found" description="It may have been removed." />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={r.requestNumber}
        description={`${r.employeeName} · ${formatDate(r.startDate)} – ${formatDate(r.endDate)} (${r.requestedDays} days)`}
        backHref="/hr/attendance/remote-work"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={r.status} />
            <WorkflowApprovalActions {...workflow.actionProps} />
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
              <InfoRow label="From" value={formatDate(r.startDate)} />
              <InfoRow label="To" value={formatDate(r.endDate)} />
              <InfoRow label="Working days" value={r.requestedDays} />
              <InfoRow label="Remote location" value={r.remoteLocation} />
              <InfoRow
                label="Equipment confirmed"
                value={r.equipmentConfirmed ? 'Yes' : 'No'}
              />
              <InfoRow label="Raised on" value={formatDateTime(r.requestDate)} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Reason</CardTitle>
            </CardHeader>
            <CardContent className="text-sm">
              <p className="whitespace-pre-wrap">{r.reason || '—'}</p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Outcome</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Approved by" value={r.approvedByName} />
              <InfoRow label="Approved on" value={formatDateTime(r.approvalDate)} />
              <InfoRow label="Comments" value={r.approvalComments} />
              <InfoRow label="Rejected on" value={formatDateTime(r.rejectedDate)} />
              <InfoRow label="Rejection reason" value={r.rejectionReason} />
            </CardContent>
          </Card>
        </TabsContent>

        <WorkflowTabContent
          value="workflow"
          entityType="RemoteWorkRequest"
          entityId={id}
          entityLabel="Remote Work Request"
          entityNumber={r.requestNumber}
          status={r.status}
          onAfterAction={async () => {
            await refresh();
            await workflow.refresh();
          }}
        />
      </Tabs>
    </div>
  );
}
