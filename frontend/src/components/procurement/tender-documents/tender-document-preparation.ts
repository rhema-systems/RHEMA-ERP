import type { ProcurementTenderDocumentTemplate } from '@/types/procurement-tender-document';
import type {
  WorkflowEntitySummaryDto,
  WorkflowEvidenceReviewInstanceDto,
} from '@/types/workflow';

export const documentPreparationStages = [
  'Prepare',
  'Upload',
  'Review',
  'Publish document',
] as const;

/** Presentation only: never grants actions or substitutes for server readiness. */
export function getDocumentPreparationState(
  template: ProcurementTenderDocumentTemplate,
  workflow?: WorkflowEvidenceReviewInstanceDto,
  summary?: WorkflowEntitySummaryDto,
  workflowUnavailable = false
) {
  const state = (
    label: string,
    stage: number | null,
    next: string,
    owner?: string
  ) => ({ label, stage, next, owner });
  if (template.status === 'Draft')
    return state(
      'Preparation not started',
      0,
      'Save the setup, then select Start document preparation. Uploading becomes available afterward.'
    );
  if (template.status === 'Published')
    return state(
      'Document published',
      3,
      'Bind this version to a compatible tender. Publishing this document does not publish the tender.'
    );
  if (template.status === 'Retired')
    return state(
      'Document retired',
      null,
      'Clone a new Draft to prepare a replacement document.'
    );
  const exactWorkflow =
    workflow &&
    workflow.id.toLowerCase() === template.workflowInstanceId?.toLowerCase() &&
    workflow.entityId.toLowerCase() === template.id.toLowerCase();
  if (workflowUnavailable || !exactWorkflow)
    return state(
      'Workflow status unavailable',
      null,
      'Refresh to confirm the current task. Do not submit again.'
    );
  const status = String(workflow.status).toLowerCase();
  if (['3', '4', '5', 'cancelled', 'failed', 'suspended'].includes(status))
    return state(
      'Workflow needs attention',
      null,
      'Review the existing workflow before continuing; do not start another request.'
    );
  if (status === '2' || status === 'completed') {
    if (!template.contentWorkflowEvidenceDocumentId)
      return state(
        'Awaiting document attachment',
        3,
        'Complete any required file review, select the eligible document below, then Attach eligible content.'
      );
    return state(
      'Awaiting document publication',
      3,
      'The authorized publisher can select Publish approved version once all readiness checks pass.'
    );
  }
  const step = workflow.steps.find(
    (item) => item.stepInstanceId === workflow.currentStepInstanceId
  );
  if (!step)
    return state(
      'Workflow status unavailable',
      null,
      'Refresh to confirm the current task. Do not submit again.'
    );
  if (step.stepName.trim().toLowerCase() === 'submitted')
    return step.evidence.total > 0
      ? state(
          'Awaiting send for approval',
          1,
          'The file is uploaded. Select Send for approval to hand it to the configured reviewer.',
          template.submittedByName || 'Document preparer'
        )
      : state(
          'Awaiting document upload',
          1,
          'Choose the tender document below, Upload controlled content, then Send for approval.',
          template.submittedByName || 'Document preparer'
        );
  const exactSummary =
    summary?.workflowInstanceId?.toLowerCase() === workflow.id.toLowerCase() &&
    summary?.entityId.toLowerCase() === template.id.toLowerCase() &&
    summary?.currentStepInstanceId === workflow.currentStepInstanceId;
  const reviewers = exactSummary
    ? [
        ...new Set(
          summary.pendingApprovers
            .map(
              (item) => item.approverName?.trim() || item.approverRole?.trim()
            )
            .filter(Boolean)
        ),
      ].join(', ')
    : '';
  return state(
    'Awaiting workflow review',
    2,
    `Current step: ${step.stepName}. Complete the configured file review and workflow approval before attaching and publishing the document.`,
    reviewers || 'Reviewer assignment unavailable — refresh to confirm'
  );
}
