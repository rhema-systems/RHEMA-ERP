import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { WorkflowSignatureMethod, WorkflowStepType } from '@/types/workflow';
import type { WorkflowEntitySummaryDto, WorkflowStatusDto } from '@/types/workflow';
import type { ProcurementEvaluationScoreRecall } from '@/types/procurement-evaluation-committee';

const api = vi.hoisted(() => ({
  getWorkflowInstance: vi.fn(), getWorkflowEntitySummary: vi.fn(), processStep: vi.fn(),
  processApproval: vi.fn(), getStepAttachments: vi.fn(), saveStepChecklistResponses: vi.fn(),
  stageWorkflowSignature: vi.fn(),
}));
vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: api }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
import { EvaluationScoreRecallWorkflowReview } from './EvaluationScoreRecallWorkflowReview';

const recall = { id: 'recall-1', requestedByUserId: 'requester-1', workflowInstanceId: 'workflow-1', status: 'PendingApproval' } as ProcurementEvaluationScoreRecall;
const instance: WorkflowStatusDto = {
  workflowInstanceId: 'workflow-1', entityId: 'recall-1', entityType: 'Configured Recall',
  workflowName: 'Independent recall review', status: 1, startedDate: new Date('2026-09-06T00:00:00Z'),
  currentStepInstanceId: 'maker-step', currentStepName: 'Submitted', steps: [], pendingApprovals: [],
  progress: { totalSteps: 2, completedSteps: 0, percentComplete: 0 },
};
const summary: WorkflowEntitySummaryDto = {
  entityId: instance.entityId, entityType: instance.entityType, workflowInstanceId: instance.workflowInstanceId,
  hasActiveInstance: true, approvalRequired: true, currentStepInstanceId: instance.currentStepInstanceId,
  currentStepName: 'Submitted', currentStepType: WorkflowStepType.Manual, canCurrentUserComplete: true,
  canCurrentUserApprove: false, canCurrentUserRecall: true, canCurrentUserResubmit: true,
  currentUserCorrectionId: 'must-not-use', pendingApprovers: [], currentStepChecklist: [], currentStepTaskAttachments: [],
};
const approval = {
  ...summary, currentStepType: WorkflowStepType.Approval, currentStepName: 'Independent review',
  canCurrentUserComplete: false, canCurrentUserApprove: true, currentUserApprovalId: 'approval-1',
};
function renderReview(overrides: Partial<React.ComponentProps<typeof EvaluationScoreRecallWorkflowReview>> = {}) {
  const onUpdated = vi.fn().mockResolvedValue(undefined);
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <EvaluationScoreRecallWorkflowReview recall={recall} currentUserId="reviewer-1" onUpdated={onUpdated} {...overrides} />
  </QueryClientProvider>);
  return { onUpdated };
}
function expand() { fireEvent.click(screen.getByText('Review recall workflow')); }
async function openTask() {
  expand();
  fireEvent.click(await screen.findByRole('button', { name: 'Complete Task' }));
  return screen.getByRole('dialog', { name: 'Complete Workflow Task' });
}
beforeEach(() => {
  vi.resetAllMocks();
  api.getWorkflowInstance.mockResolvedValue(instance);
  api.getWorkflowEntitySummary.mockResolvedValue(summary);
  api.getStepAttachments.mockResolvedValue([]);
  api.saveStepChecklistResponses.mockResolvedValue(undefined);
  api.stageWorkflowSignature.mockResolvedValue(undefined);
  api.processStep.mockResolvedValue({ success: true, status: 1 });
  api.processApproval.mockResolvedValue({});
});
afterEach(cleanup);

describe('exact recall workflow handoff', () => {
  it('stays collapsed and does not fetch workflow history until requested', () => {
    renderReview();
    expect(screen.getByText('Review recall workflow').closest('details')).not.toHaveAttribute('open');
    expect(api.getWorkflowInstance).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: 'Complete Task' })).not.toBeInTheDocument();
  });
  it('does not invent a workflow when its retained ID is absent', async () => {
    renderReview({ recall: { ...recall, workflowInstanceId: undefined } });
    expand();
    expect(await screen.findByRole('alert')).toHaveTextContent('exact recall workflow could not be confirmed');
    expect(api.getWorkflowInstance).not.toHaveBeenCalled();
  });
  it.each([{ workflowInstanceId: 'other' }, { entityId: 'other' }, { entityType: '' }])(
    'rejects mismatched retained instance %j', async (invalid) => {
      api.getWorkflowInstance.mockResolvedValue({ ...instance, ...invalid });
      renderReview(); expand();
      await screen.findByRole('alert');
      expect(api.getWorkflowEntitySummary).not.toHaveBeenCalled();
      expect(api.processStep).not.toHaveBeenCalled();
    }
  );
  it('processes only the assigned task through shared checklist controls and refreshes the register', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue({ ...summary, currentStepChecklist: [{ id: 'review-check', name: 'Confirm recall reason', isRequired: true, requiresDocument: false }] });
    const { onUpdated } = renderReview({ currentUserId: 'requester-1' });
    const dialog = await openTask();
    expect(within(dialog).getByRole('button', { name: 'Complete Task' })).toBeDisabled();
    fireEvent.click(within(dialog).getByRole('checkbox'));
    fireEvent.change(within(dialog).getByLabelText('Comments'), { target: { value: 'Recall request reviewed' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Complete Task' }));
    await waitFor(() => expect(api.processStep).toHaveBeenCalledWith('maker-step', { action: 0, comments: 'Recall request reviewed' }));
    expect(api.getWorkflowEntitySummary).toHaveBeenCalledWith('Configured Recall', 'recall-1');
    expect(api.saveStepChecklistResponses).toHaveBeenCalledWith('maker-step', [expect.objectContaining({ id: 'review-check', isSatisfied: true })]);
    await waitFor(() => expect(onUpdated).toHaveBeenCalledTimes(1));
    expect(api.processApproval).not.toHaveBeenCalled();
  });
  it('offers no task, approval, recall, resubmit or domain decision to an unassigned reader', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue({ ...summary, canCurrentUserComplete: false });
    renderReview(); expand();
    await screen.findByRole('region', { name: 'Recall workflow' });
    expect(screen.queryByRole('button', { name: /Complete Task|Approve|Reject|Recall|Resubmit|Delegate|Send back/i })).not.toBeInTheDocument();
  });
  it('preserves independent approval checklist and signature requirements', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue({ ...approval,
      currentStepChecklist: [{ id: 'approve-check', name: 'Verify independent review', isRequired: true, requiresDocument: false }],
      currentStepSignaturePolicy: { isRequired: true, method: WorkflowSignatureMethod.Attestation, requireValidCertificateChain: false, attestationText: 'I independently reviewed this recall.' },
    });
    const { onUpdated } = renderReview(); expand();
    fireEvent.click(await screen.findByRole('button', { name: 'Approve workflow' }));
    expect(screen.getByRole('button', { name: /^Approve$/ })).toBeDisabled();
    for (const checkbox of screen.getAllByRole('checkbox')) fireEvent.click(checkbox);
    fireEvent.click(screen.getByRole('button', { name: /^Approve$/ }));
    await waitFor(() => expect(api.processApproval).toHaveBeenCalledWith('approval-1', expect.objectContaining({ action: 0 })));
    expect(api.stageWorkflowSignature).toHaveBeenCalledWith('approval-1', expect.objectContaining({ method: 0 }));
    expect(api.stageWorkflowSignature).toHaveBeenCalledTimes(1);
    expect(api.processApproval.mock.calls[0][1]).not.toHaveProperty('signature');
    await waitFor(() => expect(onUpdated).toHaveBeenCalledTimes(1));
    expect(api.processStep).not.toHaveBeenCalled();
  });
  it('prevents the requester from approving or rejecting even if a generic workflow assigns them approval', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue(approval);
    renderReview({ currentUserId: 'REQUESTER-1' }); expand();
    await screen.findByText(/An independent reviewer must approve or reject/);
    expect(screen.queryByRole('button', { name: /Approve workflow|Reject workflow|Complete Task/ })).not.toBeInTheDocument();
    expect(api.processApproval).not.toHaveBeenCalled();
  });
  it('requires refresh when the task changes before central processing', async () => {
    renderReview(); const dialog = await openTask();
    api.getWorkflowInstance.mockResolvedValue({ ...instance, currentStepInstanceId: 'other-step' });
    api.getWorkflowEntitySummary.mockResolvedValue({ ...summary, currentStepInstanceId: 'other-step' });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Complete Task' }));
    await screen.findByText('The workflow step or assignment changed. Refresh and review the current requirements.');
    expect(api.processStep).not.toHaveBeenCalled();
  });
  it('preserves comments on action failure and permits only read-only recovery', async () => {
    api.processStep.mockRejectedValue(new Error('Task write unavailable'));
    renderReview(); const dialog = await openTask();
    fireEvent.change(within(dialog).getByLabelText('Comments'), { target: { value: 'Keep review evidence' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Complete Task' }));
    await screen.findByText('Task write unavailable');
    expect(screen.getByLabelText('Comments')).toHaveValue('Keep review evidence');
    expect(screen.getByRole('button', { name: 'Complete Task' })).toBeDisabled();
    expect(api.processStep).toHaveBeenCalledTimes(1);
  });
  it('retries an unavailable workflow read without a mutation', async () => {
    api.getWorkflowInstance.mockRejectedValueOnce(new Error('Workflow read unavailable'));
    renderReview(); expand();
    expect(await screen.findByRole('alert')).toHaveTextContent('Workflow read unavailable');
    fireEvent.click(screen.getByRole('button', { name: 'Refresh workflow' }));
    await screen.findByRole('button', { name: 'Complete Task' });
    expect(api.processStep).not.toHaveBeenCalled();
    expect(api.processApproval).not.toHaveBeenCalled();
  });
  it('explains the separate decision after completion without approving or unlocking recall', async () => {
    api.getWorkflowInstance.mockResolvedValue({ ...instance, status: 2 });
    renderReview(); expand();
    await screen.findByText(/Workflow complete. The independent reviewer/);
    expect(api.getWorkflowEntitySummary).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: /Complete Task|Approve workflow|Approve recall|Reject workflow/ })).not.toBeInTheDocument();
  });
  it('does not reopen workflow actions for an already decided recall', async () => {
    renderReview({ recall: { ...recall, status: 'Approved' } }); expand();
    await screen.findByText(/recall decision is already recorded/);
    expect(api.getWorkflowEntitySummary).not.toHaveBeenCalled();
    expect(api.processStep).not.toHaveBeenCalled();
  });
});
