'use client';

import React, { useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { workflowApiService } from '@/services/workflow-api.service';
import { WorkflowApprovalAction, WorkflowStepAction, WorkflowStepType } from '@/types/workflow';
import type { ProcessApprovalRequest, WorkflowEntitySummaryDto, WorkflowStatusDto } from '@/types/workflow';
import type { ProcurementEvaluationScoreRecall } from '@/types/procurement-evaluation-committee';

interface Props {
  recall: ProcurementEvaluationScoreRecall;
  currentUserId?: string;
  onUpdated?: () => Promise<unknown>;
}

const sameId = (left?: string, right?: string) => Boolean(left && right && left.toLowerCase() === right.toLowerCase());
const normalized = (value: unknown) => String(value ?? '').toLowerCase();
const active = (status: WorkflowStatusDto['status']) => ['1', '6', 'inprogress', 'waiting'].includes(normalized(status));
const stepIs = (type: WorkflowEntitySummaryDto['currentStepType'], expected: WorkflowStepType) =>
  normalized(type) === String(expected) || normalized(type) === WorkflowStepType[expected].toLowerCase();
const unavailable = 'The exact recall workflow could not be confirmed. Refresh before continuing.';

async function readSnapshot(recallId: string, workflowId: string, pending: boolean) {
  const instance = await workflowApiService.getWorkflowInstance(workflowId);
  if (!sameId(instance.workflowInstanceId, workflowId) || !sameId(instance.entityId, recallId) || !instance.entityType?.trim()) {
    throw new Error(unavailable);
  }
  // Never substitute a newer entity workflow for this retained recall instance.
  if (!pending || !active(instance.status)) return { instance, summary: undefined };
  const summary = await workflowApiService.getWorkflowEntitySummary(instance.entityType, instance.entityId);
  if (
    !sameId(summary.workflowInstanceId, workflowId) ||
    !sameId(summary.entityId, recallId) ||
    !sameId(summary.entityType, instance.entityType) ||
    !sameId(summary.currentStepInstanceId, instance.currentStepInstanceId) ||
    !summary.hasActiveInstance ||
    (summary.status !== undefined && !active(summary.status))
  ) throw new Error(unavailable);
  return { instance, summary };
}

/** Lazy, exact-instance handoff to the central workflow engine. */
export function EvaluationScoreRecallWorkflowReview(props: Props) {
  const [open, setOpen] = useState(false);
  return (
    <details className="mt-3 rounded-md border p-3" onToggle={(event) => setOpen(event.currentTarget.open)}>
      <summary className="cursor-pointer text-sm font-medium">Review recall workflow</summary>
      {open && <RecallWorkflowActions {...props} />}
    </details>
  );
}

function RecallWorkflowActions({ recall, currentUserId, onUpdated }: Props) {
  const pending = recall.status === 'PendingApproval';
  const workflowId = recall.workflowInstanceId;
  const isRequester = sameId(currentUserId, recall.requestedByUserId);
  const isIndependentApprover = Boolean(currentUserId && recall.requestedByUserId && !isRequester);
  const [reviewError, setReviewError] = useState<string>();
  const [recorded, setRecorded] = useState(false);
  const [processing, setProcessing] = useState(false);
  const mutationLocked = useRef(false);
  const snapshot = useQuery({
    queryKey: ['evaluation-score-recall-workflow', recall.id, workflowId, recall.status],
    queryFn: () => {
      if (!workflowId) throw new Error(unavailable);
      return readSnapshot(recall.id, workflowId, pending);
    },
    enabled: Boolean(workflowId),
    retry: false,
    staleTime: 0,
  });
  const summary = snapshot.data?.summary;
  const actionsDisabled = !pending || processing || snapshot.isFetching || snapshot.isError || recorded || Boolean(reviewError);

  const refresh = async () => {
    try {
      await Promise.all([snapshot.refetch({ throwOnError: true }), onUpdated?.()]);
      setReviewError(undefined);
      setRecorded(false);
      mutationLocked.current = false;
    } catch (failure) {
      setReviewError(getProcurementProblemMessage(failure, 'The latest workflow state could not be refreshed.'));
      // The action may be saved already: recovery re-reads, never repeats it.
    }
  };

  const validateCurrent = async (kind: 'task' | 'approval', stepId?: string) => {
    if (!pending || !workflowId || actionsDisabled || mutationLocked.current || !summary) throw new Error(unavailable);
    mutationLocked.current = true;
    setProcessing(true);
    const current = (await readSnapshot(recall.id, workflowId, pending)).summary;
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
        !isIndependentApprover ||
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

  const error = reviewError || (snapshot.isError ? getProcurementProblemMessage(snapshot.error, unavailable) : undefined);
  if (!workflowId || snapshot.isError || (recorded && error)) return (
    <div role="alert" className="mt-3 space-y-2 text-sm">
      {recorded && <p className="font-medium">Workflow action saved. Refresh the latest state before continuing.</p>}
      <p className="text-destructive">{error || unavailable}</p>
      {workflowId && <Button type="button" size="sm" variant="outline" disabled={snapshot.isFetching} onClick={() => void refresh()}>Refresh workflow</Button>}
    </div>
  );
  if (snapshot.isLoading) return <p className="mt-3 text-sm text-muted-foreground">Loading current workflow step…</p>;
  if (!summary) {
    const status = normalized(snapshot.data?.instance.status);
    const outcome = ['2', 'completed'].includes(status) ? 'Approve' : ['3', '4', 'cancelled', 'failed'].includes(status) ? 'Reject' : undefined;
    return (
      <div className="mt-3 space-y-2 text-sm text-muted-foreground">
        <p>{!pending ? 'The recall decision is already recorded. No workflow task or approval can be processed here.' :
          outcome === 'Approve' ? 'Workflow complete. The independent reviewer can now use Approve recall to record the separate recall decision.' :
          outcome === 'Reject' ? 'Workflow cancelled or failed. The independent reviewer can record the rejected recall outcome.' :
          'This workflow is not active. No task or approval can be processed here.'}</p>
        <Button type="button" variant="outline" size="sm" disabled={snapshot.isFetching} onClick={() => void refresh()}>Refresh workflow</Button>
      </div>
    );
  }

  return (
    <section aria-label="Recall workflow" className="mt-3 space-y-2">
      <div className="flex items-center justify-between gap-2">
        <h3 className="text-sm font-medium">Current workflow step</h3>
        <Button type="button" variant="ghost" size="sm" disabled={snapshot.isFetching || processing} onClick={() => void refresh()}>Refresh workflow</Button>
      </div>
      {reviewError && <p role="alert" className="text-sm text-destructive">{reviewError}</p>}
      {isRequester && stepIs(summary.currentStepType, WorkflowStepType.Approval) && (
        <p className="text-sm text-muted-foreground">An independent reviewer must approve or reject this workflow; the recall requester cannot make that decision.</p>
      )}
      <WorkflowApprovalActions
        key={`${workflowId}:${summary.currentStepInstanceId}:${summary.currentUserApprovalId ?? ''}`}
        entityType={summary.entityType}
        entityId={recall.id}
        entityLabel="Recall workflow"
        entityNumber="Controlled score recall"
        status="Pending Approval"
        workflowSummary={{
          ...summary,
          canCurrentUserRecall: false,
          canCurrentUserResubmit: false,
          currentUserCorrectionId: undefined,
          canCurrentUserApprove: summary.canCurrentUserApprove && isIndependentApprover && !actionsDisabled,
          canCurrentUserComplete: summary.canCurrentUserComplete && !actionsDisabled,
        }}
        loadWorkflowSummary={false}
        showStepBadge
        hideGovernanceActions
        canSubmit={false}
        canApproveReject={isIndependentApprover && stepIs(summary.currentStepType, WorkflowStepType.Approval) && Boolean(summary.currentUserApprovalId)}
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
        // Shared actions stage the signature first; the workflow engine consumes
        // that exact approval's pending signature without a second evidence write.
        onApprove={(comments, checklistResponses) => processApproval({ action: WorkflowApprovalAction.Approve, comments, checklistResponses })}
        onReject={(comments, checklistResponses) => processApproval({ action: WorkflowApprovalAction.Reject, comments, checklistResponses })}
        onAfterAction={refresh}
        approveLabel="Approve workflow"
        rejectLabel="Reject workflow"
      />
      <p className="text-xs text-muted-foreground">Only the assigned user can act. Completing this workflow does not approve the recall or unlock a score; the independent recall decision remains separate.</p>
    </section>
  );
}
