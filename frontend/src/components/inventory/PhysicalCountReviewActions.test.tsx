import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { PhysicalCountReviewActions } from './PhysicalCountReviewActions';
import { inventoryManagementService as service, PhysicalCountDetailDto } from '@/services/inventoryManagementService';

const workflowState = vi.hoisted(() => ({ direct: false, known: true, loading: false, error: undefined as string | undefined }));
vi.mock('@/hooks/useWorkflowSummary', () => ({ useWorkflowSummary: () => ({
  visibility: { direct: workflowState.direct, known: workflowState.known },
  loading: workflowState.loading, error: workflowState.error, refresh: vi.fn(),
}) }));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('@/components/ui/confirmation-dialog', () => ({ ConfirmationDialog: ({ open, onConfirm }: any) => open ? <button onClick={onConfirm}>Confirm submit</button> : null }));
vi.mock('@/components/ui/select', () => ({
  Select: ({ children, value, onValueChange, disabled }: any) => <select aria-label="Decision" disabled={disabled} value={value} onChange={e => onValueChange(e.target.value)}><option value="">Choose</option>{children}</select>,
  SelectTrigger: () => null, SelectValue: () => null, SelectContent: ({ children }: any) => children,
  SelectItem: ({ value, children }: any) => <option value={value}>{children}</option>
}));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  reviewPhysicalCount: vi.fn(), submitReviewedPhysicalCount: vi.fn(), getPhysicalCountDecisions: vi.fn(),
  decidePhysicalCountStores: vi.fn(), decidePhysicalCountFinance: vi.fn(), attestPhysicalCountAudit: vi.fn()
} }));
let count: PhysicalCountDetailDto;
const changed = vi.fn().mockResolvedValue(undefined);
const saveCounts = vi.fn();
const uploadSheet = vi.fn();
beforeEach(() => {
  vi.clearAllMocks();
  workflowState.direct = false; workflowState.known = true; workflowState.loading = false; workflowState.error = undefined;
  count = { id: 'pc', rowVersion: 'v1', status: 'InProgress', canReview: true, canDecide: false,
    totalItems: 1, countedItems: 1, items: [], evidence: [{ isCurrentCountSheet: true, isImportedCountSheet: true }], actions: [] } as unknown as PhysicalCountDetailDto;
  vi.mocked(service.getPhysicalCountDecisions).mockResolvedValue({ revision: 'setup1', decisions: [
    { code: 'APPROVE', label: 'Approve adjustment', effect: 'ApproveAdjustment', isActive: true },
    { code: 'CHECK', label: 'Investigate discrepancy', effect: 'Investigate', isActive: true }
  ] });
});
const show = (unsaved = false) => render(<PhysicalCountReviewActions count={count} unsaved={unsaved} onSaveCounts={saveCounts} onUploadSheet={uploadSheet} onChanged={changed} />);

describe('count review and decisions', () => {
  it('offers Complete count without approval wording when no active workflow is configured', () => {
    workflowState.direct = true;
    count.status = 'UnderReview'; show();
    expect(screen.getByRole('button', { name: 'Complete count' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Submit for approval' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Decision')).not.toBeInTheDocument();
  });
  it('retains an existing approval cycle after the definition is deactivated', () => {
    workflowState.direct = true; count.status = 'UnderReview';
    count.actions = [{ actionType: 'Submitted' }] as never;
    show();
    expect(screen.getByRole('button', { name: 'Submit for approval' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Complete count' })).not.toBeInTheDocument();
  });
  it('keeps submission disabled when approval setup cannot be confirmed', () => {
    workflowState.known = false; workflowState.error = 'Unavailable'; count.status = 'UnderReview'; show();
    expect(screen.getByRole('button', { name: 'Submit for approval' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeEnabled();
  });
  it('hides all decision controls for a persisted no-approval count', () => {
    count.approvalRequired = false; count.canReview = false; count.canDecide = true; count.status = 'ReadyToPost'; show();
    expect(service.getPhysicalCountDecisions).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: 'Save decision' })).not.toBeInTheDocument();
  });
  it.each(['PendingStoresApproval', 'PendingFinanceApproval', 'PendingAuditAttestation'])('loads scoped decisions for %s without invoking an approval', async status => {
    count.status = status; count.canReview = false; count.canDecide = true; show();
    await screen.findByText('Approve adjustment');
    expect(service.getPhysicalCountDecisions).toHaveBeenCalledWith(false, 'pc');
    expect(service.decidePhysicalCountStores).not.toHaveBeenCalled();
    expect(service.decidePhysicalCountFinance).not.toHaveBeenCalled();
    expect(service.attestPhysicalCountAudit).not.toHaveBeenCalled();
  });
  it('shows a failed lookup inline and retries the same saved count without clearing comments', async () => {
    vi.mocked(service.getPhysicalCountDecisions).mockRejectedValueOnce(new Error('No warehouse responsibility.'));
    count.status = 'PendingStoresApproval'; count.canReview = false; count.canDecide = true; show();
    expect(await screen.findByRole('alert')).toHaveTextContent('No warehouse responsibility.');
    expect(screen.getByLabelText('Decision')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Save decision' })).toBeDisabled();
    fireEvent.change(screen.getByLabelText('Comments (optional)'), { target: { value: 'Review note retained.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Retry decisions' }));
    await screen.findByText('Approve adjustment');
    expect(service.getPhysicalCountDecisions).toHaveBeenNthCalledWith(2, false, 'pc');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Comments (optional)')).toHaveValue('Review note retained.');
  });
  it('keeps decisions disabled while loading and ignores an old count response after switching counts', async () => {
    let resolveOld!: (value: any) => void;
    vi.mocked(service.getPhysicalCountDecisions).mockImplementationOnce(() => new Promise(resolve => { resolveOld = resolve; }));
    count.status = 'PendingStoresApproval'; count.canReview = false; count.canDecide = true;
    const view = show();
    expect(screen.getByRole('status')).toHaveTextContent('Loading decisions');
    expect(screen.getByLabelText('Decision')).toBeDisabled();
    view.rerender(<PhysicalCountReviewActions count={{ ...count, id: 'other-count' }} unsaved={false} onSaveCounts={saveCounts} onUploadSheet={uploadSheet} onChanged={changed} />);
    await screen.findByText('Approve adjustment');
    await act(async () => resolveOld({ revision: 'old', decisions: [{ code: 'OLD', label: 'Old count only', effect: 'Investigate', isActive: true }] }));
    expect(screen.queryByText('Old count only')).not.toBeInTheDocument();
    expect(screen.getByText('Approve adjustment')).toBeInTheDocument();
    expect(service.getPhysicalCountDecisions).toHaveBeenLastCalledWith(false, 'other-count');
  });
  it('explains an empty configured decision list rather than presenting a silent blank selector', async () => {
    vi.mocked(service.getPhysicalCountDecisions).mockResolvedValueOnce({ revision: 'empty', decisions: [] });
    count.status = 'PendingStoresApproval'; count.canReview = false; count.canDecide = true; show();
    expect(await screen.findByRole('alert')).toHaveTextContent('No active count decisions are configured');
    expect(screen.getByRole('button', { name: 'Save decision' })).toBeDisabled();
  });
  it('keeps Save Counts and Submit together in one non-wrapping action row', () => {
    count.status = 'UnderReview'; show();
    const save = screen.getByRole('button', { name: 'Save Counts' });
    const submit = screen.getByRole('button', { name: 'Submit for approval' });
    expect(save.parentElement).toBe(submit.parentElement);
    expect(save.parentElement).toHaveClass('flex', 'flex-nowrap', 'items-center');
    expect(submit).toHaveClass('bg-destructive', 'text-white');
    expect(screen.queryByText(/Correct quantities in Items or replace/)).not.toBeInTheDocument();
  });
  it('reviews without submitting or posting', async () => {
    show(); fireEvent.click(screen.getByRole('button', { name: 'Review variance' }));
    await waitFor(() => expect(service.reviewPhysicalCount).toHaveBeenCalledWith('pc', expect.objectContaining({ rowVersion: 'v1' })));
    expect(service.submitReviewedPhysicalCount).not.toHaveBeenCalled();
  });
  it('prevents review while quantities are unsaved', () => { show(true); expect(screen.getByRole('button', { name: 'Review variance' })).toBeDisabled(); });
  it('confirms reviewed submission and keeps its source version', async () => {
    count.status = 'UnderReview'; show(); fireEvent.click(screen.getByRole('button', { name: 'Submit for approval' }));
    expect(service.submitReviewedPhysicalCount).not.toHaveBeenCalled();
    fireEvent.click(screen.getByText('Confirm submit'));
    await waitFor(() => expect(service.submitReviewedPhysicalCount).toHaveBeenCalledWith('pc', expect.objectContaining({ rowVersion: 'v1' })));
  });
  it('allows saved manual corrections without requiring the earlier Excel sheet to be reuploaded', () => {
    count.status = 'UnderReview'; count.evidence[0].isCurrentCountSheet = false; show();
    expect(screen.getByRole('button', { name: 'Submit for approval' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Save Counts' })).toBeDisabled();
    expect(screen.getByText('Quantities saved.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Upload revised count sheet' })).not.toBeInTheDocument();
    expect(uploadSheet).not.toHaveBeenCalled();
    expect(service.submitReviewedPhysicalCount).not.toHaveBeenCalled();
  });
  it('provides a save action for pending quantities and prevents an upload overwriting them', () => {
    count.status = 'UnderReview'; count.evidence[0].isCurrentCountSheet = false; show(true);
    fireEvent.click(screen.getByRole('button', { name: 'Save Counts' }));
    expect(saveCounts).toHaveBeenCalledOnce();
    expect(screen.queryByRole('button', { name: 'Upload revised count sheet' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Submit for approval' })).toBeDisabled();
  });
  it('offers Upload count sheet when no evidence has been saved', () => {
    count.status = 'UnderReview'; count.evidence = []; show();
    fireEvent.click(screen.getByRole('button', { name: 'Upload count sheet' }));
    expect(uploadSheet).toHaveBeenCalledOnce();
  });
  it('hides counter controls from another user', () => { count.canReview = false; show(); expect(screen.queryByRole('button')).not.toBeInTheDocument(); });
  it('requires findings when resuming an investigation', async () => {
    count.status = 'UnderInvestigation'; show(); const button = screen.getByRole('button', { name: 'Resume review' });
    expect(button).toBeDisabled(); fireEvent.change(screen.getByLabelText('Investigation findings'), { target: { value: 'One omitted entry found.' } });
    fireEvent.click(button); await waitFor(() => expect(service.reviewPhysicalCount).toHaveBeenCalledWith('pc', expect.objectContaining({ comment: 'One omitted entry found.' })));
  });
  it('uses the configured decision and requires a reason only for investigation', async () => {
    count.status = 'PendingStoresApproval'; count.canReview = false; count.canDecide = true; show();
    await screen.findByText('Investigate discrepancy'); fireEvent.change(screen.getByLabelText('Decision'), { target: { value: 'CHECK' } });
    expect(screen.getByRole('button', { name: 'Save decision' })).toBeDisabled();
    fireEvent.change(screen.getByLabelText('Reason for investigation'), { target: { value: 'Verify quantity.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save decision' }));
    await waitFor(() => expect(service.decidePhysicalCountStores).toHaveBeenCalledWith('pc', expect.objectContaining({ approved: false, decisionCode: 'CHECK', decisionRevision: 'setup1', reason: 'Verify quantity.' })));
  });
  it('allows approval without comments', async () => {
    count.status = 'PendingFinanceApproval'; count.canReview = false; count.canDecide = true; show();
    await screen.findByText('Approve adjustment'); fireEvent.change(screen.getByLabelText('Decision'), { target: { value: 'APPROVE' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save decision' }));
    await waitFor(() => expect(service.decidePhysicalCountFinance).toHaveBeenCalledWith('pc', expect.objectContaining({ approved: true, comment: undefined })));
  });
});
