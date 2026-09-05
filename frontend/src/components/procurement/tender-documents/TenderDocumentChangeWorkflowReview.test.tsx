import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { WorkflowSignatureMethod, WorkflowStepType } from '@/types/workflow';
import type { WorkflowEntitySummaryDto, WorkflowStatusDto } from '@/types/workflow';
import type { ProcurementTenderDocumentChange } from '@/types/procurement-tender-document';

const api = vi.hoisted(() => ({
  getWorkflowInstance: vi.fn(), getWorkflowEntitySummary: vi.fn(), processStep: vi.fn(),
  processApproval: vi.fn(), getStepAttachments: vi.fn(), saveStepChecklistResponses: vi.fn(),
  stageWorkflowSignature: vi.fn(),
}));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: api }));
vi.mock('sonner', () => ({ toast }));
import { TenderDocumentChangeWorkflowReview } from './TenderDocumentChangeWorkflowReview';

const change = {
  id: 'change-1', workflowInstanceId: 'workflow-1', status: 'PendingApproval',
  changeType: 'UnpublishedScheduleReschedule', sequence: 1,
} as ProcurementTenderDocumentChange;
const instance: WorkflowStatusDto = {
  workflowInstanceId: 'workflow-1', entityId: 'change-1', entityType: 'Procurement Sourcing',
  workflowName: 'Configured schedule review', status: 1, startedDate: new Date('2026-09-05T00:00:00Z'),
  currentStepInstanceId: 'maker-step', currentStepName: 'Submitted', steps: [], pendingApprovals: [],
  progress: { totalSteps: 2, completedSteps: 0, percentComplete: 0 },
};
const summary: WorkflowEntitySummaryDto = {
  entityId: instance.entityId, entityType: instance.entityType, workflowInstanceId: instance.workflowInstanceId,
  hasActiveInstance: true, approvalRequired: true, currentStepInstanceId: instance.currentStepInstanceId,
  currentStepName: 'Submitted', currentStepType: WorkflowStepType.Manual, canCurrentUserComplete: true,
  canCurrentUserApprove: false, canCurrentUserRecall: true, canCurrentUserResubmit: true,
  pendingApprovers: [], currentStepChecklist: [], currentStepTaskAttachments: [],
};
const approvalSummary: WorkflowEntitySummaryDto = {
  ...summary, currentStepType: WorkflowStepType.Approval, currentStepName: 'Independent review',
  canCurrentUserComplete: false, canCurrentUserApprove: true, currentUserApprovalId: 'assigned-approval',
};
function renderReview(props: Partial<React.ComponentProps<typeof TenderDocumentChangeWorkflowReview>> = {}) {
  const onUpdated = vi.fn().mockResolvedValue(undefined);
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <TenderDocumentChangeWorkflowReview change={change} onUpdated={onUpdated} renderDecisionAction={(outcome) => <button>{outcome === 'Approve' ? 'Apply recorded outcome' : 'Record rejected outcome'}</button>} {...props} />
  </QueryClientProvider>);
  return { onUpdated };
}
async function openTask() {
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

describe('exact schedule change workflow handoff', () => {
  it.each([
    { workflowInstanceId: 'other-workflow' }, { entityId: 'other-change' }, { entityType: '' },
  ])('rejects mismatched instance metadata %j before summary lookup', async (invalid) => {
    api.getWorkflowInstance.mockResolvedValue({ ...instance, ...invalid });
    renderReview();
    await screen.findByRole('alert');
    expect(api.getWorkflowEntitySummary).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: 'Complete Task' })).not.toBeInTheDocument();
  });
  it.each([
    { workflowInstanceId: 'other-workflow' }, { entityId: 'other-change' },
    { entityType: 'Other Entity' }, { currentStepInstanceId: 'other-step' },
    { hasActiveInstance: false },
  ])('rejects mismatched summary %j', async (invalid) => {
    api.getWorkflowEntitySummary.mockResolvedValue({ ...summary, ...invalid });
    renderReview();
    await screen.findByRole('alert');
    expect(screen.queryByRole('button', { name: 'Complete Task' })).not.toBeInTheDocument();
    expect(api.processStep).not.toHaveBeenCalled();
  });
  it('uses the actual maker task and shared checklist, then refreshes workflow and register', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue({ ...summary, currentStepChecklist: [{ id: 'schedule-check', name: 'Confirm schedule request', isRequired: true, requiresDocument: false }] });
    const { onUpdated } = renderReview();
    const dialog = await openTask();
    expect(screen.queryByRole('button', { name: 'Apply recorded outcome' })).not.toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Complete Task' })).toBeDisabled();
    fireEvent.click(within(dialog).getByRole('checkbox'));
    fireEvent.change(within(dialog).getByLabelText('Comments'), { target: { value: 'Schedule verified' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Complete Task' }));
    await waitFor(() => expect(api.processStep).toHaveBeenCalledWith('maker-step', { action: 0, comments: 'Schedule verified' }));
    expect(api.getWorkflowInstance).toHaveBeenCalledWith('workflow-1');
    expect(api.getWorkflowEntitySummary).toHaveBeenCalledWith('Procurement Sourcing', 'change-1');
    expect(api.saveStepChecklistResponses).toHaveBeenCalledWith('maker-step', [expect.objectContaining({ id: 'schedule-check', isSatisfied: true })]);
    await waitFor(() => expect(onUpdated).toHaveBeenCalledTimes(1));
    expect(api.getWorkflowInstance.mock.calls.length).toBeGreaterThanOrEqual(3);
    expect(api.processApproval).not.toHaveBeenCalled();
  });
  it('does not offer task completion, recall, resubmit or outcome application to an unassigned user', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue({ ...summary, canCurrentUserComplete: false });
    renderReview();
    await screen.findByRole('region', { name: 'Schedule change workflow' });
    expect(screen.queryByRole('button', { name: /Complete Task|Recall|Resubmit|Submit|Delegate|Send back|Apply recorded outcome/i })).not.toBeInTheDocument();
  });
  it('uses independent approval and preserves configured checklist/signature requirements', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue({ ...approvalSummary,
      currentStepChecklist: [{ id: 'review-check', name: 'Confirm dates', isRequired: true, requiresDocument: false }],
      currentStepSignaturePolicy: { isRequired: true, method: WorkflowSignatureMethod.Attestation, requireValidCertificateChain: false, attestationText: 'I reviewed this schedule.' },
    });
    const { onUpdated } = renderReview();
    fireEvent.click(await screen.findByRole('button', { name: 'Approve workflow' }));
    expect(screen.getByRole('button', { name: /^Approve$/ })).toBeDisabled();
    expect(screen.queryByRole('button', { name: /Recall|Resubmit|Delegate|Send back/ })).not.toBeInTheDocument();
    for (const checkbox of screen.getAllByRole('checkbox')) fireEvent.click(checkbox);
    fireEvent.click(screen.getByRole('button', { name: /^Approve$/ }));
    await waitFor(() => expect(api.processApproval).toHaveBeenCalledWith('assigned-approval', expect.objectContaining({ action: 0 })));
    expect(api.stageWorkflowSignature).toHaveBeenCalledTimes(1);
    expect(api.stageWorkflowSignature).toHaveBeenCalledWith('assigned-approval', expect.objectContaining({ method: 0 }));
    expect(api.processApproval.mock.calls[0][1]).not.toHaveProperty('signature');
    await waitFor(() => expect(onUpdated).toHaveBeenCalledTimes(1));
    expect(api.processStep).not.toHaveBeenCalled();
  });
  it('refuses a stale maker task before central processing', async () => {
    renderReview();
    const dialog = await openTask();
    api.getWorkflowInstance.mockResolvedValue({ ...instance, currentStepInstanceId: 'new-step' });
    api.getWorkflowEntitySummary.mockResolvedValue({ ...summary, currentStepInstanceId: 'new-step' });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Complete Task' }));
    await screen.findByText('The workflow step or assignment changed. Refresh and review the current requirements.');
    expect(api.processStep).not.toHaveBeenCalled();
  });
  it('refuses a changed approval assignment before central processing', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue(approvalSummary);
    renderReview();
    fireEvent.click(await screen.findByRole('button', { name: 'Approve workflow' }));
    api.getWorkflowEntitySummary.mockResolvedValue({ ...approvalSummary, currentUserApprovalId: 'different-approval' });
    fireEvent.click(screen.getByRole('button', { name: /^Approve$/ }));
    await screen.findByText('The workflow step or assignment changed. Refresh and review the current requirements.');
    expect(api.processApproval).not.toHaveBeenCalled();
  });
  it('retains entered task comments when central processing fails', async () => {
    api.processStep.mockRejectedValue(new Error('Workflow step could not be saved'));
    renderReview();
    const dialog = await openTask();
    fireEvent.change(within(dialog).getByLabelText('Comments'), { target: { value: 'Keep my schedule review notes' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Complete Task' }));
    await screen.findByText('Workflow step could not be saved');
    expect(screen.getByRole('dialog', { name: 'Complete Workflow Task' })).toBeInTheDocument();
    expect(screen.getByLabelText('Comments')).toHaveValue('Keep my schedule review notes');
    expect(screen.getByRole('button', { name: 'Complete Task' })).toBeDisabled();
    expect(api.processStep).toHaveBeenCalledTimes(1);
  });
  it('displays read failure and permits read-only retry without mutation', async () => {
    api.getWorkflowInstance.mockRejectedValueOnce(new Error('Workflow read unavailable'));
    renderReview();
    expect(await screen.findByRole('alert')).toHaveTextContent('Workflow read unavailable');
    fireEvent.click(screen.getByRole('button', { name: 'Refresh workflow' }));
    await screen.findByRole('button', { name: 'Complete Task' });
    expect(api.processStep).not.toHaveBeenCalled();
    expect(api.processApproval).not.toHaveBeenCalled();
  });
  it('shows outcome application only after the exact workflow completes', async () => {
    api.getWorkflowInstance.mockResolvedValue({ ...instance, status: 2 });
    renderReview();
    await screen.findByRole('button', { name: 'Apply recorded outcome' });
    expect(api.getWorkflowEntitySummary).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: /Complete Task|Approve workflow|Reject workflow/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Record rejected outcome' })).not.toBeInTheDocument();
  });
  it.each([3, 4])('shows only rejection for terminal workflow status %s', async (status) => {
    api.getWorkflowInstance.mockResolvedValue({ ...instance, status });
    renderReview();
    await screen.findByRole('button', { name: 'Record rejected outcome' });
    expect(screen.queryByRole('button', { name: 'Apply recorded outcome' })).not.toBeInTheDocument();
    expect(api.getWorkflowEntitySummary).not.toHaveBeenCalled();
  });
  it('does not treat a suspended workflow as completed', async () => {
    api.getWorkflowInstance.mockResolvedValue({ ...instance, status: 5 });
    renderReview();
    await screen.findByText(/This workflow is not active/);
    expect(screen.queryByRole('button', { name: 'Apply recorded outcome' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Record rejected outcome' })).not.toBeInTheDocument();
  });
  it('recovers an unknown approval response with a read-only exact-instance refresh, never repeat approval', async () => {
    api.getWorkflowEntitySummary.mockResolvedValue(approvalSummary);
    api.processApproval.mockImplementation(async () => {
      api.getWorkflowInstance.mockResolvedValue({ ...instance, status: 2 });
      throw new SyntaxError('Approval response could not be parsed');
    });
    renderReview();
    fireEvent.click(await screen.findByRole('button', { name: 'Approve workflow' }));
    fireEvent.click(screen.getByRole('button', { name: /^Approve$/ }));
    await screen.findByText('Approval response could not be parsed');
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    fireEvent.click(screen.getByRole('button', { name: 'Refresh workflow' }));
    await screen.findByRole('button', { name: 'Apply recorded outcome' });
    expect(api.processApproval).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('button', { name: 'Record rejected outcome' })).not.toBeInTheDocument();
  });
  it('separates a successful task from a failed refresh and prevents a duplicate retry', async () => {
    const onUpdated = vi.fn().mockRejectedValue(new Error('Register refresh unavailable'));
    renderReview({ onUpdated });
    const dialog = await openTask();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Complete Task' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Workflow action saved');
    expect(api.processStep).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('button', { name: 'Complete Task' })).not.toBeInTheDocument();
    expect(toast.error).not.toHaveBeenCalledWith('Failed to complete workflow task', expect.anything());
    onUpdated.mockResolvedValue(undefined);
    fireEvent.click(screen.getByRole('button', { name: 'Refresh workflow' }));
    await screen.findByRole('button', { name: 'Complete Task' });
    expect(api.processStep).toHaveBeenCalledTimes(1);
  });
});
