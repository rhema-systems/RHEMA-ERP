'use client';

import React, { useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { workflowApiService } from '@/services/workflow-api.service';
import { WorkflowApprovalAction, WorkflowStepAction, WorkflowStepType } from '@/types/workflow';
import type { ProcessApprovalRequest, WorkflowEntitySummaryDto, WorkflowStatusDto } from '@/types/workflow';
import type { ProcurementTenderDocumentChange } from '@/types/procurement-tender-document';

interface Props {
  change: ProcurementTenderDocumentChange;
  disabled?: boolean;
  onUpdated: () => Promise<unknown>;
  renderDecisionAction?: (outcome: 'Approve' | 'Reject') => React.ReactNode;
}

const sameId = (left?: string, right?: string) => Boolean(left && right && left.toLowerCase() === right.toLowerCase());
const normalized = (value: unknown) => String(value ?? '').toLowerCase();
const active = (status: WorkflowStatusDto['status']) => ['1', '6', 'inprogress', 'waiting'].includes(normalized(status));
const stepIs = (type: WorkflowEntitySummaryDto['currentStepType'], expected: WorkflowStepType) =>
  normalized(type) === String(expected) || normalized(type) === WorkflowStepType[expected].toLowerCase();
const unavailable = 'The exact change workflow could not be confirmed. Refresh before continuing.';

async function readSnapshot(changeId: string, workflowId: string) {
  const instance = await workflowApiService.getWorkflowInstance(workflowId);
  if (!sameId(instance.workflowInstanceId, workflowId) || !sameId(instance.entityId, changeId) || !instance.entityType?.trim()) {
    throw new Error(unavailable);
  }
  // Completed/paused instances must never fall through to a newer entity workflow.
  if (!active(instance.status)) return { instance, summary: undefined };
  const summary = await workflowApiService.getWorkflowEntitySummary(instance.entityType, instance.entityId);
  if (
    !sameId(summary.workflowInstanceId, workflowId) ||
    !sameId(summary.entityId, changeId) ||
    !sameId(summary.entityType, instance.entityType) ||
    !sameId(summary.currentStepInstanceId, instance.currentStepInstanceId) ||
    !summary.hasActiveInstance ||
    (summary.status !== undefined && !active(summary.status))
  ) throw new Error(unavailable);
  return { instance, summary };
}

/** Shared task/checklist/signature controls, anchored to this exact change instance. */
export function TenderDocumentChangeWorkflowReview({ change, disabled = false, onUpdated, renderDecisionAction }: Props) {
  const pending = change.status === 'PendingApproval' && change.changeType === 'UnpublishedScheduleReschedule';
  const workflowId = change.workflowInstanceId;
  const [reviewError, setReviewError] = useState<string>();
  const [recorded, setRecorded] = useState(false);
  const [processing, setProcessing] = useState(false);
  const mutationLocked = useRef(false);
  const snapshot = useQuery({
    queryKey: ['tender-document-change-workflow', change.id, workflowId, change.status],
    queryFn: () => {
      if (!workflowId) throw new Error(unavailable);
      return readSnapshot(change.id, workflowId);
    },
    enabled: pending && Boolean(workflowId),
    retry: false,
  });
  const summary = snapshot.data?.summary;
  const actionsDisabled = disabled || processing || snapshot.isFetching || snapshot.isError || recorded || Boolean(reviewError);

  const refresh = async () => {
    try {
      await Promise.all([snapshot.refetch({ throwOnError: true }), onUpdated()]);
      setReviewError(undefined);
      setRecorded(false);
      mutationLocked.current = false;
    } catch (failure) {
      setReviewError(getProcurementProblemMessage(failure, 'The latest workflow state could not be refreshed.'));
      // The mutation may already be saved: refresh is a read-only retry, never re-process it.
    }
  };

  const validateCurrent = async (kind: 'task' | 'approval', stepId?: string) => {
    if (!pending || !workflowId || actionsDisabled || mutationLocked.current || !summary) throw new Error(unavailable);
    mutationLocked.current = true;
    setProcessing(true);
    const latest = await readSnapshot(change.id, workflowId);
    const current = latest.summary;
    if (
      !current ||
      !sameId(current.entityType, summary.entityType) ||
      !sameId(current.currentStepInstanceId, summary.currentStepInstanceId) ||
      (kind === 'task' && (
        !sameId(stepId, current.currentStepInstanceId) ||
        !stepIs(summary.currentStepType, WorkflowStepType.Manual) ||
        !stepIs(current.currentStepType, WorkflowStepType.Manual) ||
        summary.canCurrentUserComplete !== true || current.canCurrentUserComplete !== true
      )) ||
      (kind === 'approval' && (
        !stepIs(summary.currentStepType, WorkflowStepType.Approval) ||
        !stepIs(current.currentStepType, WorkflowStepType.Approval) ||
        summary.canCurrentUserApprove !== true || current.canCurrentUserApprove !== true ||
        !sameId(current.currentUserApprovalId, summary.currentUserApprovalId)
      ))
    ) throw new Error('The workflow step or assignment changed. Refresh and review the current requirements.');
    return current;
  };

  const fail = (failure: unknown): never => {
    const message = getProcurementProblemMessage(failure, 'Workflow action failed.');
    setReviewError(message);
    mutationLocked.current = false;
    throw new Error(message);
  };
  const processApproval = async (request: ProcessApprovalRequest) => {
    try {
      const current = await validateCurrent('approval');
      if (!current.currentUserApprovalId) throw new Error(unavailable);
      await workflowApiService.processApproval(current.currentUserApprovalId, request);
      setRecorded(true);
    } catch (failure) { fail(failure); }
    finally { setProcessing(false); }
  };

  if (!pending) return null;
  const error = reviewError || (snapshot.isError ? getProcurementProblemMessage(snapshot.error, unavailable) : undefined);
  if (!workflowId || snapshot.isError || (recorded && error)) return (
    <div role="alert" className="space-y-2 rounded-md border p-3 text-sm">
      {recorded && <p className="font-medium">Workflow action saved. Refresh the latest state before continuing.</p>}
      <p className="text-destructive">{error || unavailable}</p>
      {workflowId && <Button type="button" size="sm" variant="outline" disabled={snapshot.isFetching} onClick={() => void refresh()}>Refresh workflow</Button>}
    </div>
  );
  if (snapshot.isLoading) return <p className="text-sm text-muted-foreground">Loading current workflow step…</p>;
  if (!summary) {
    const status = normalized(snapshot.data?.instance.status);
    const outcome = ['2', 'completed'].includes(status) ? 'Approve' : ['3', '4', 'cancelled', 'failed'].includes(status) ? 'Reject' : undefined;
    return outcome ? (
      <div className="space-y-2">
        <p className="text-sm text-muted-foreground">{outcome === 'Approve' ? 'Workflow complete. The independent reviewer can apply the approved outcome below.' : 'Workflow cancelled or failed. The independent reviewer can record the rejected outcome below.'}</p>
        {!disabled && !snapshot.isFetching && !recorded && renderDecisionAction?.(outcome)}
      </div>
    ) : <p className="text-sm text-muted-foreground">This workflow is not active. No task or approval can be processed here.</p>;
  }

  return (
    <section aria-label="Schedule change workflow" className="space-y-2 rounded-md border p-3">
      <div className="flex items-center justify-between gap-2">
        <h3 className="text-sm font-medium">Current workflow step</h3>
        <Button type="button" variant="ghost" size="sm" disabled={snapshot.isFetching || processing} onClick={() => void refresh()}>Refresh workflow</Button>
      </div>
      {reviewError && <p role="alert" className="text-sm text-destructive">{reviewError}</p>}
      <WorkflowApprovalActions
        key={`${workflowId}:${summary.currentStepInstanceId}:${summary.currentUserApprovalId ?? ''}`}
        entityType={summary.entityType}
        entityId={change.id}
        entityLabel="Schedule change workflow"
        entityNumber={`Schedule change #${change.sequence}`}
        status="Pending Approval"
        workflowSummary={{
          ...summary,
          canCurrentUserRecall: false,
          canCurrentUserResubmit: false,
          canCurrentUserApprove: summary.canCurrentUserApprove && !actionsDisabled,
          canCurrentUserComplete: summary.canCurrentUserComplete && !actionsDisabled,
        }}
        loadWorkflowSummary={false}
        showStepBadge
        hideGovernanceActions
        canSubmit={false}
        canApproveReject={stepIs(summary.currentStepType, WorkflowStepType.Approval) && Boolean(summary.currentUserApprovalId)}
        forwardActionsDisabled={actionsDisabled}
        forwardActionsDisabledReason="Refresh the current workflow and register before continuing."
        onCompleteTask={async ({ stepInstanceId, comments }) => {
          try {
            const current = await validateCurrent('task', stepInstanceId);
            if (!current.currentStepInstanceId) throw new Error(unavailable);
            const result = await workflowApiService.processStep(current.currentStepInstanceId, { action: WorkflowStepAction.Complete, comments });
            if (!result.success) throw new Error(result.message || result.errors?.map((item) => item.message).filter(Boolean).join(' ') || 'The workflow task could not be completed.');
            setRecorded(true);
            return result;
          } catch (failure) { return fail(failure); }
          finally { setProcessing(false); }
        }}
        // Shared actions save checklist/attachments and stage signatures before these central engine calls.
        onApprove={(comments, checklistResponses) => processApproval({ action: WorkflowApprovalAction.Approve, comments, checklistResponses })}
        onReject={(comments, checklistResponses) => processApproval({ action: WorkflowApprovalAction.Reject, comments, checklistResponses })}
        onAfterAction={refresh}
        approveLabel="Approve workflow"
        rejectLabel="Reject workflow"
      />
      <p className="text-xs text-muted-foreground">Only the assigned user can act. Completing a task routes to the next configured step; it does not publish the tender or apply the schedule.</p>
    </section>
  );
}
