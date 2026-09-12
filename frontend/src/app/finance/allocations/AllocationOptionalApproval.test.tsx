import * as React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import Page from './runs/[id]/page';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import { workflowApiService } from '@/services/workflow-api.service';
const state = vi.hoisted(() => ({ manage: true }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'batch' }), useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => state.manage }) }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: { getWorkflowEntitySummary: vi.fn() } }));
vi.mock('@/services/finance/unit-accounts-data.service', () => ({ unitAccountsDataService: {
  getAllocationRunBatchById: vi.fn(), submitAllocationRunBatch: vi.fn(), postAllocationRunBatch: vi.fn(),
  approveAllocationRunBatch: vi.fn(), rejectAllocationRunBatch: vi.fn(),
} }));
const draft = { id: 'batch', batchNumber: 'AB-TEST', status: 'Draft', approvalRequired: true,
  functionalCurrencyCode: 'GHS', sourcePeriodBalance: 100, totalAllocated: 100, lines: [] };
beforeEach(() => {
  vi.clearAllMocks(); state.manage = true;
  vi.mocked(unitAccountsDataService.getAllocationRunBatchById).mockResolvedValue(draft as never);
  vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue({
    entityType: 'AllocationRunBatch', entityId: 'batch', approvalRequired: false, hasActiveInstance: false,
    canCurrentUserApprove: false, pendingApprovers: [],
  });
});
afterEach(cleanup);
describe('allocation optional approval', () => {
  it('finalizes first and then offers normal posting without approval actions', async () => {
    vi.mocked(unitAccountsDataService.submitAllocationRunBatch).mockResolvedValue({ ...draft, status: 'ReadyToPost', approvalRequired: false } as never);
    render(<Page />);
    fireEvent.click(await screen.findByRole('button', { name: 'Finalize' }));
    await waitFor(() => expect(unitAccountsDataService.submitAllocationRunBatch).toHaveBeenCalledExactlyOnceWith('batch', undefined));
    expect(await screen.findByRole('button', { name: 'Post' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: /Submit for Approval|Approve Step|^Reject$/ })).not.toBeInTheDocument();
    expect(unitAccountsDataService.postAllocationRunBatch).not.toHaveBeenCalled();
  });
  it('keeps the active approval route', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue({
      entityType: 'AllocationRunBatch', entityId: 'batch', approvalRequired: true, hasActiveInstance: false,
      canCurrentUserApprove: false, pendingApprovers: [],
    });
    render(<Page />);
    expect(await screen.findByRole('button', { name: 'Submit for Approval' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Finalize' })).not.toBeInTheDocument();
  });
  it('retains reviewer controls for an in-flight workflow even after deactivation', async () => {
    vi.mocked(unitAccountsDataService.getAllocationRunBatchById).mockResolvedValue({ ...draft, status: 'PendingApproval' } as never);
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue({
      entityType: 'AllocationRunBatch', entityId: 'batch', approvalRequired: false, hasActiveInstance: true,
      canCurrentUserApprove: true, pendingApprovers: [],
    });
    render(<Page />);
    expect(await screen.findByRole('button', { name: 'Approve Step' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: /Finalize|^Post$/ })).not.toBeInTheDocument();
  });
  it('does not invent a direct route when lookup fails', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockRejectedValue(new Error('Policy unavailable'));
    render(<Page />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Policy unavailable');
    expect(screen.queryByRole('button', { name: 'Finalize' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Checking approval status/ })).toBeDisabled();
  });
  it('preserves Finance permission for direct posting', async () => {
    state.manage = false;
    vi.mocked(unitAccountsDataService.getAllocationRunBatchById).mockResolvedValue({ ...draft, status: 'ReadyToPost', approvalRequired: false } as never);
    render(<Page />);
    await screen.findByText('Ready to post');
    expect(screen.queryByRole('button', { name: 'Post' })).not.toBeInTheDocument();
  });
});
