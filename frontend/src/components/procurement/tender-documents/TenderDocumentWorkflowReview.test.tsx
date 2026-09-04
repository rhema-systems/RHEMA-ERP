import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type {
  WorkflowEntitySummaryDto,
  WorkflowEvidenceReviewInstanceDto,
} from '@/types/workflow';
import { WorkflowSignatureMethod, WorkflowStepType } from '@/types/workflow';

const api = vi.hoisted(() => ({
  getWorkflowEntitySummary: vi.fn(),
  processApproval: vi.fn(),
  getStepAttachments: vi.fn(),
  saveStepChecklistResponses: vi.fn(),
  stageWorkflowSignature: vi.fn(),
}));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: api }));
vi.mock('sonner', () => ({ toast }));
import { TenderDocumentWorkflowReview } from './TenderDocumentWorkflowReview';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';

const workflow: WorkflowEvidenceReviewInstanceDto = {
  id: 'exact-workflow',
  entityId: 'template-id',
  entityType: 'ConfiguredProcurementEntity',
  workflowDefinitionId: 'configured-workflow-definition',
  workflowName: 'TDC document review',
  createdDate: '2026-09-04T00:00:00Z',
  currentStepInstanceId: 'review-step',
  status: 1,
  steps: [],
};
const baseSummary: WorkflowEntitySummaryDto = {
  entityType: workflow.entityType,
  entityId: workflow.entityId,
  workflowInstanceId: workflow.id,
  hasActiveInstance: true,
  approvalRequired: true,
  currentStepInstanceId: 'review-step',
  currentStepType: WorkflowStepType.Approval,
  currentStepName: 'Independent review',
  canCurrentUserApprove: true,
  currentUserApprovalId: 'assigned-approval',
  pendingApprovers: [],
  currentStepChecklist: [],
  currentStepTaskAttachments: [],
};

const renderReview = (instance = workflow) => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const onUpdated = vi.fn().mockResolvedValue(undefined);
  render(
    <QueryClientProvider client={client}>
      <TenderDocumentWorkflowReview
        templateId="template-id"
        templateCode="TDC-TEMPLATE"
        templateStatus="PendingApproval"
        workflow={instance}
        onUpdated={onUpdated}
      />
    </QueryClientProvider>
  );
  return { onUpdated };
};
beforeEach(() => {
  vi.resetAllMocks();
  api.getWorkflowEntitySummary.mockResolvedValue(baseSummary);
  api.getStepAttachments.mockResolvedValue([]);
  api.saveStepChecklistResponses.mockResolvedValue(undefined);
  api.stageWorkflowSignature.mockResolvedValue(undefined);
  api.processApproval.mockResolvedValue({});
});
afterEach(cleanup);

describe('TenderDocumentWorkflowReview shared approval integration', () => {
  it('does not query active summaries or show a false error for a completed exact workflow', () => {
    renderReview({ ...workflow, status: 2 });
    expect(api.getWorkflowEntitySummary).not.toHaveBeenCalled();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(
      screen.queryByRole('region', { name: 'Workflow review' })
    ).not.toBeInTheDocument();
  });

  it('accepts canonical entity type aliases only when exact instance and record IDs still match', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue({
      ...baseSummary,
      entityType: 'PROCUREMENT_CONFIGURED_CODE',
    });
    renderReview();
    fireEvent.click(
      await screen.findByRole('button', { name: 'Approve workflow' })
    );
    fireEvent.click(
      screen.getByRole('button', { name: /^Approve$/ })
    );
    await waitFor(() =>
      expect(api.processApproval).toHaveBeenCalledWith(
        'assigned-approval',
        expect.objectContaining({ action: 0 })
      )
    );
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
  it('suppresses unsupported delegation and correction actions only in this template wrapper', async () => {
    renderReview();
    await screen.findByRole('button', { name: 'Approve workflow' });
    expect(
      screen.queryByRole('button', { name: /^Delegate$/ })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: /^Send back$/ })
    ).not.toBeInTheDocument();
  });

  it('preserves governance controls for existing shared component consumers by default', () => {
    render(
      <WorkflowApprovalActions
        entityType={workflow.entityType}
        entityId={workflow.entityId}
        entityLabel="Existing workflow"
        status="Pending Approval"
        workflowSummary={baseSummary}
        loadWorkflowSummary={false}
        onApprove={vi.fn()}
        onReject={vi.fn()}
      />
    );
    expect(
      screen.getByRole('button', { name: /^Delegate$/ })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /^Send back$/ })
    ).toBeInTheDocument();
  });
  it('uses entity type from the exact workflow and the shared confirmation', async () => {
    const { onUpdated } = renderReview();
    fireEvent.click(
      await screen.findByRole('button', { name: 'Approve workflow' })
    );
    expect(api.getWorkflowEntitySummary).toHaveBeenCalledWith(
      'ConfiguredProcurementEntity',
      'template-id'
    );
    expect(api.processApproval).not.toHaveBeenCalled();
    fireEvent.change(screen.getByLabelText('Comment (Optional)'), {
      target: { value: 'Reviewed current template' },
    });
    fireEvent.click(
      screen.getByRole('button', { name: /^Approve$/ })
    );
    await waitFor(() =>
      expect(api.processApproval).toHaveBeenCalledWith('assigned-approval', {
        action: 0,
        comments: 'Reviewed current template',
        checklistResponses: [],
      })
    );
    await waitFor(() => expect(onUpdated).toHaveBeenCalledTimes(1));
    expect(api.stageWorkflowSignature).not.toHaveBeenCalled();
  });

  it('retains required checklists and stages the configured signature once before processing', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue({
      ...baseSummary,
      currentStepChecklist: [
        {
          id: 'terms-check',
          name: 'Confirm terms',
          description: 'Review the current version',
          isRequired: true,
          requiresDocument: false,
        },
      ],
      currentStepSignaturePolicy: {
        isRequired: true,
        method: WorkflowSignatureMethod.Attestation,
        requireValidCertificateChain: false,
        attestationText: 'I confirm the approved tender terms.',
      },
    });
    renderReview();
    fireEvent.click(
      await screen.findByRole('button', { name: 'Approve workflow' })
    );
    expect(screen.getByText('Approval Checklist')).toBeInTheDocument();
    expect(screen.getByText('Electronic signature')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /^Approve$/ })
    ).toBeDisabled();
    const checks = screen.getAllByRole('checkbox');
    fireEvent.click(checks[0]);
    expect(
      screen.getByRole('button', { name: /^Approve$/ })
    ).toBeDisabled();
    fireEvent.click(checks[1]);
    fireEvent.click(
      screen.getByRole('button', { name: /^Approve$/ })
    );
    await waitFor(() => expect(api.processApproval).toHaveBeenCalledTimes(1));
    expect(api.saveStepChecklistResponses).toHaveBeenCalledWith('review-step', [
      expect.objectContaining({ id: 'terms-check', isSatisfied: true }),
    ]);
    expect(api.stageWorkflowSignature).toHaveBeenCalledWith(
      'assigned-approval',
      expect.objectContaining({
        method: 0,
        attestation: 'I confirm the approved tender terms.',
      })
    );
    expect(api.stageWorkflowSignature).toHaveBeenCalledTimes(1);
    expect(api.processApproval.mock.calls[0][1]).not.toHaveProperty(
      'signature'
    );
    expect(api.stageWorkflowSignature.mock.invocationCallOrder[0]).toBeLessThan(
      api.processApproval.mock.invocationCallOrder[0]
    );
  });

  it('does not hide required document checklist evidence', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue({
      ...baseSummary,
      currentStepChecklist: [
        {
          id: 'terms-file',
          name: 'Confirm supporting document',
          description: '',
          isRequired: true,
          requiresDocument: true,
          documentName: 'Signed terms',
        },
      ],
    });
    renderReview();
    const approveButton = await screen.findByRole('button', {
      name: 'Approve workflow',
    });
    await act(async () => {
      fireEvent.click(approveButton);
    });
    fireEvent.click(screen.getByRole('checkbox'));
    expect(
      screen.getByRole('button', { name: /^Approve$/ })
    ).toBeDisabled();
    expect(screen.getByText('Signed terms')).toBeInTheDocument();
    expect(api.processApproval).not.toHaveBeenCalled();
  });

  it('requires a comment for shared workflow rejection', async () => {
    renderReview();
    fireEvent.click(
      await screen.findByRole('button', { name: 'Reject workflow' })
    );
    expect(
      screen.getByRole('button', { name: /^Reject$/ })
    ).toBeDisabled();
    fireEvent.change(screen.getByLabelText('Comment *'), {
      target: { value: 'Needs corrected terms' },
    });
    fireEvent.click(
      screen.getByRole('button', { name: /^Reject$/ })
    );
    await waitFor(() =>
      expect(api.processApproval).toHaveBeenCalledWith('assigned-approval', {
        action: 1,
        comments: 'Needs corrected terms',
        checklistResponses: undefined,
      })
    );
  });

  it('does not call another record workflow when template and instance disagree', () => {
    renderReview({ ...workflow, entityId: 'another-template' });
    expect(screen.getByRole('alert')).toHaveTextContent(
      'exact template workflow could not be confirmed'
    );
    expect(api.getWorkflowEntitySummary).not.toHaveBeenCalled();
    expect(api.processApproval).not.toHaveBeenCalled();
  });

  it('fails closed for a different workflow instance returned by entity summary', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue({
      ...baseSummary,
      workflowInstanceId: 'another-instance',
    });
    renderReview();
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'exact template workflow could not be confirmed'
    );
    expect(
      screen.queryByRole('button', { name: 'Approve workflow' })
    ).not.toBeInTheDocument();
  });

  it('does not invent authority when the configured user is not an approver', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue({
      ...baseSummary,
      canCurrentUserApprove: false,
    });
    renderReview();
    expect(
      await screen.findByRole('button', { name: 'Approve workflow' })
    ).toBeDisabled();
    expect(
      screen.getByRole('button', { name: 'Reject workflow' })
    ).toBeDisabled();
  });

  it('does not expose unsupported template recall or resubmission lifecycle actions', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue({
      ...baseSummary,
      canCurrentUserApprove: false,
      canCurrentUserRecall: true,
      canCurrentUserResubmit: true,
    });
    renderReview();
    await screen.findByRole('region', { name: 'Workflow review' });
    expect(
      screen.queryByRole('button', { name: /Recall/ })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: /Resubmit/ })
    ).not.toBeInTheDocument();
  });

  it('cannot process approval if required signature staging fails', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue({
      ...baseSummary,
      currentStepSignaturePolicy: {
        isRequired: true,
        method: WorkflowSignatureMethod.Attestation,
        requireValidCertificateChain: false,
        attestationText: 'I attest to these terms.',
      },
    });
    api.stageWorkflowSignature.mockRejectedValue(
      new Error('Signature could not be staged.')
    );
    renderReview();
    fireEvent.click(
      await screen.findByRole('button', { name: 'Approve workflow' })
    );
    fireEvent.click(screen.getByRole('checkbox'));
    fireEvent.click(
      screen.getByRole('button', { name: /^Approve$/ })
    );
    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith(
        'Failed to approve tender document workflow',
        { description: 'Signature could not be staged.' }
      )
    );
    expect(api.processApproval).not.toHaveBeenCalled();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('refuses a changed approval assignment and keeps the entered comment', async () => {
    api.getWorkflowEntitySummary
      .mockResolvedValueOnce(baseSummary)
      .mockResolvedValue({
        ...baseSummary,
        currentUserApprovalId: 'next-approval',
      });
    renderReview();
    fireEvent.click(
      await screen.findByRole('button', { name: 'Approve workflow' })
    );
    fireEvent.change(screen.getByLabelText('Comment (Optional)'), {
      target: { value: 'Keep review note' },
    });
    fireEvent.click(
      screen.getByRole('button', { name: /^Approve$/ })
    );
    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith(
        'Failed to approve tender document workflow',
        expect.objectContaining({
          description: expect.stringContaining(
            'assignment or approval step changed'
          ),
        })
      )
    );
    expect(api.processApproval).not.toHaveBeenCalled();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByLabelText('Comment (Optional)')).toHaveValue(
      'Keep review note'
    );
  });
});
