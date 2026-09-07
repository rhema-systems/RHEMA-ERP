import { describe, expect, it } from 'vitest';
import type { ProcurementTenderDocumentTemplate } from '@/types/procurement-tender-document';
import type {
  WorkflowEntitySummaryDto,
  WorkflowEvidenceReviewInstanceDto,
} from '@/types/workflow';
import { getDocumentPreparationState } from './tender-document-preparation';

const template = {
  id: 'template',
  status: 'PendingApproval',
  workflowInstanceId: 'flow',
  submittedByName: 'Actual preparer',
} as ProcurementTenderDocumentTemplate;
const workflow: WorkflowEvidenceReviewInstanceDto = {
  id: 'flow',
  entityId: 'template',
  entityType: 'ConfiguredDocument',
  workflowDefinitionId: 'definition',
  workflowName: 'Configured approval',
  status: 1,
  createdDate: '2026-09-07',
  currentStepInstanceId: 'step',
  steps: [
    {
      stepInstanceId: 'step',
      workflowStepId: 'definition-step',
      stepName: 'Submitted',
      status: 0,
      evidence: {
        total: 0,
        pending: 0,
        verified: 0,
        rejected: 0,
        legalHold: 0,
      },
    },
  ],
};

describe('document preparation progress is not the generic lifecycle badge', () => {
  it('explains that starting a draft enables upload, not reviewer submission', () => {
    expect(
      getDocumentPreparationState({ ...template, status: 'Draft' })
    ).toMatchObject({ stage: 0, label: 'Preparation not started' });
  });
  it('shows the recorded preparer and upload task after workflow start', () => {
    expect(getDocumentPreparationState(template, workflow)).toMatchObject({
      stage: 1,
      label: 'Awaiting document upload',
      owner: 'Actual preparer',
    });
  });
  it('does not claim an uploaded file has been sent to the reviewer', () => {
    const uploaded = {
      ...workflow,
      steps: [
        {
          ...workflow.steps[0],
          evidence: { ...workflow.steps[0].evidence, total: 1 },
        },
      ],
    };
    expect(getDocumentPreparationState(template, uploaded).label).toBe(
      'Awaiting send for approval'
    );
  });
  it('displays all real pending reviewers, with role fallback, from this exact step', () => {
    const review = {
      ...workflow,
      steps: [{ ...workflow.steps[0], stepName: 'Independent review' }],
    };
    const summary = {
      entityId: 'template',
      workflowInstanceId: 'flow',
      currentStepInstanceId: 'step',
      pendingApprovers: [
        { approverName: 'Ama Reviewer' },
        { approverRole: 'Configured review role' },
      ],
    } as WorkflowEntitySummaryDto;
    expect(
      getDocumentPreparationState(template, review, summary)
    ).toMatchObject({
      stage: 2,
      owner: 'Ama Reviewer, Configured review role',
    });
    for (const mismatch of [
      { entityId: 'other' },
      { workflowInstanceId: 'old-flow' },
      { currentStepInstanceId: 'old-step' },
    ]) {
      expect(
        getDocumentPreparationState(template, review, {
          ...summary,
          ...mismatch,
        }).owner
      ).toContain('assignment unavailable');
    }
  });
  it.each([
    undefined,
    { ...workflow, id: 'other-flow' },
    { ...workflow, entityId: 'other-template' },
    { ...workflow, currentStepInstanceId: 'absent' },
  ])(
    'does not invent a stage from absent or mismatched workflow evidence',
    (value) => {
      expect(getDocumentPreparationState(template, value).stage).toBeNull();
    }
  );
  it('does not show stale success after a failed workflow refresh', () => {
    expect(
      getDocumentPreparationState(template, workflow, undefined, true).label
    ).toBe('Workflow status unavailable');
  });
  it.each([3, 4, 5])(
    'keeps stopped workflow state %s distinct from review',
    (status) => {
      expect(
        getDocumentPreparationState(template, { ...workflow, status }).label
      ).toBe('Workflow needs attention');
    }
  );
  it('separates completed approval, attachment and publication', () => {
    const completed = { ...workflow, status: 2 };
    expect(getDocumentPreparationState(template, completed).label).toBe(
      'Awaiting document attachment'
    );
    expect(
      getDocumentPreparationState(
        { ...template, contentWorkflowEvidenceDocumentId: 'file' },
        completed
      ).label
    ).toBe('Awaiting document publication');
    expect(
      getDocumentPreparationState({ ...template, status: 'Published' }).next
    ).toContain('does not publish the tender');
    expect(
      getDocumentPreparationState({ ...template, status: 'Retired' }).stage
    ).toBeNull();
  });
});
