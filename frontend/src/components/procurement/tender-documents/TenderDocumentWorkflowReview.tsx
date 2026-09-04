'use client';

import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { workflowApiService } from '@/services/workflow-api.service';
import { WorkflowApprovalAction, WorkflowStepType } from '@/types/workflow';
import type {
  ProcessApprovalRequest,
  WorkflowEntitySummaryDto,
  WorkflowEvidenceReviewInstanceDto,
} from '@/types/workflow';
import type { ProcurementTenderDocumentTemplateStatus } from '@/types/procurement-tender-document';
import { isTemplateWorkflowCompleted } from './TenderDocumentEvidenceReview';

interface Props {
  templateId: string;
  templateCode: string;
  templateStatus: ProcurementTenderDocumentTemplateStatus;
  workflow: WorkflowEvidenceReviewInstanceDto;
  disabled?: boolean;
  onUpdated: () => Promise<unknown>;
}

const sameRecord = (left: string | undefined, right: string) =>
  left?.toLowerCase() === right.toLowerCase();
const isApprovalStep = (
  stepType: WorkflowEntitySummaryDto['currentStepType']
) =>
  stepType === WorkflowStepType.Approval ||
  String(stepType).toLowerCase() === 'approval' ||
  String(stepType) === '2';

/** Reuses all shared checklist/signature controls against this exact workflow. */
export function TenderDocumentWorkflowReview({
  templateId,
  templateCode,
  templateStatus,
  workflow,
  disabled = false,
  onUpdated,
}: Props) {
  const recordMatches =
    sameRecord(workflow.entityId, templateId) &&
    Boolean(workflow.entityType?.trim());
  const completed = isTemplateWorkflowCompleted(workflow.status);
  const summary = useQuery({
    queryKey: [
      'tender-document-workflow-review',
      templateId,
      workflow.id,
      workflow.entityType,
      workflow.currentStepInstanceId,
    ],
    queryFn: () =>
      workflowApiService.getWorkflowEntitySummary(
        workflow.entityType,
        workflow.entityId
      ),
    enabled:
      recordMatches && templateStatus === 'PendingApproval' && !completed,
  });
  const exactSummary =
    summary.data &&
    sameRecord(summary.data.workflowInstanceId, workflow.id) &&
    sameRecord(summary.data.entityId, templateId) &&
    Boolean(summary.data.entityType?.trim());

  const process = async (request: ProcessApprovalRequest) => {
    const displayed = summary.data;
    if (
      !exactSummary ||
      !displayed?.currentUserApprovalId ||
      !displayed.canCurrentUserApprove ||
      !displayed.hasActiveInstance ||
      !isApprovalStep(displayed.currentStepType) ||
      templateStatus !== 'PendingApproval' ||
      completed ||
      disabled ||
      summary.isFetching
    ) {
      throw new Error(
        'The current workflow approval is unavailable. Refresh before reviewing.'
      );
    }
    try {
      const latest = await workflowApiService.getWorkflowEntitySummary(
        workflow.entityType,
        workflow.entityId
      );
      if (
        !sameRecord(latest.workflowInstanceId, workflow.id) ||
        !sameRecord(latest.entityId, templateId) ||
        !latest.entityType?.trim() ||
        !latest.hasActiveInstance ||
        latest.canCurrentUserApprove !== true ||
        latest.currentUserApprovalId !== displayed.currentUserApprovalId ||
        latest.currentStepInstanceId !== displayed.currentStepInstanceId ||
        !isApprovalStep(latest.currentStepType)
      ) {
        throw new Error(
          'The workflow assignment or approval step changed. Refresh and review the current requirements.'
        );
      }
      await workflowApiService.processApproval(
        displayed.currentUserApprovalId,
        request
      );
    } catch (failure) {
      throw new Error(
        getProcurementProblemMessage(failure, 'Workflow review failed.')
      );
    }
  };

  const refresh = async () => {
    await Promise.all([summary.refetch(), onUpdated()]);
  };

  if (templateStatus !== 'PendingApproval' || completed) return null;
  if (
    !recordMatches ||
    summary.isError ||
    (!summary.isLoading && !exactSummary)
  ) {
    return (
      <div
        role="alert"
        className="mb-4 space-y-2 rounded-md border p-3 text-sm text-destructive"
      >
        <p>
          {summary.isError
            ? getProcurementProblemMessage(
                summary.error,
                'Workflow review could not be loaded.'
              )
            : 'The exact template workflow could not be confirmed. Review actions are unavailable.'}
        </p>
        {recordMatches && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => void summary.refetch()}
          >
            Retry workflow review
          </Button>
        )}
      </div>
    );
  }
  if (summary.isLoading)
    return (
      <p className="mb-3 text-sm text-muted-foreground">
        Loading configured workflow review…
      </p>
    );
  if (!summary.data?.hasActiveInstance) return null;

  return (
    <section
      className="mb-4 space-y-2 rounded-md border p-3"
      aria-label="Workflow review"
    >
      <h3 className="text-sm font-medium">Workflow review</h3>
      <WorkflowApprovalActions
        entityType={workflow.entityType}
        entityId={workflow.entityId}
        entityLabel="Tender document workflow"
        entityNumber={templateCode}
        status="Pending Approval"
        workflowSummary={{
          ...summary.data,
          // Lifecycle/task transitions remain owned by the template workspace.
          canCurrentUserRecall: false,
          canCurrentUserResubmit: false,
          canCurrentUserComplete: false,
          canCurrentUserApprove:
            summary.data.canCurrentUserApprove &&
            !disabled &&
            !summary.isFetching,
        }}
        loadWorkflowSummary={false}
        showStepBadge
        hideGovernanceActions
        canSubmit={false}
        canApproveReject={
          Boolean(summary.data.currentUserApprovalId) &&
          isApprovalStep(summary.data.currentStepType)
        }
        forwardActionsDisabled={disabled || summary.isFetching}
        forwardActionsDisabledReason="Wait for the current document action to finish, then refresh workflow review."
        // Shared actions stage signatures; the engine validates and commits that exact staged record.
        onApprove={(comments, checklistResponses) =>
          process({
            action: WorkflowApprovalAction.Approve,
            comments,
            checklistResponses,
          })
        }
        onReject={(comments, checklistResponses) =>
          process({
            action: WorkflowApprovalAction.Reject,
            comments,
            checklistResponses,
          })
        }
        onAfterAction={refresh}
        approveLabel="Approve workflow"
        rejectLabel="Reject workflow"
      />
      <p className="text-xs text-muted-foreground">
        The configured approver, checklist and signature requirements apply
        here. Workflow approval does not publish the template.
      </p>
    </section>
  );
}
