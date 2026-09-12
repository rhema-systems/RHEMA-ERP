import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';

const mocks = vi.hoisted(() => ({ get: vi.fn(), submit: vi.fn(), post: vi.fn(), summary: vi.fn(), permission: vi.fn(), toast: vi.fn() }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'batch-1' }), useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: mocks.permission }) }));
vi.mock('@/components/ui/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/services/finance/finance-data.service', () => ({ financeDataService: { getAccounts: vi.fn(async () => []) } }));
vi.mock('@/services/finance/journal-batch-data.service', () => ({ journalBatchDataService: { getBatch: mocks.get, submit: mocks.submit, post: mocks.post } }));
vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: { getWorkflowEntitySummary: mocks.summary } }));
import Page from './page';

const item = { id: 'item-1', journalEntryId: 'journal-1', journalEntryNumber: 'JE-1', entryDate: '2026-09-12',
  description: 'Balanced journal', totalDebit: 100, lineCount: 2, reviewStatus: 'Pending', postingStatus: 'NotEligible', reviews: [] };
const draft = { id: 'batch-1', batchNumber: 'JB-1', description: 'Batch test', displayStatus: 'Draft', approvalStatus: 'Draft',
  approvalRequired: true, batchType: 'Standard', controlCurrencyCode: 'GHS', expectedDebitTotal: 100, actualDebitTotal: 100,
  variance: 0, entryCount: 1, canSubmit: true, canEdit: false, canReview: false, canPostAny: false, items: [item], postingRuns: [] };
const direct = { entityType: 'JournalBatch', entityId: 'batch-1', approvalRequired: false, hasActiveInstance: false, hasWorkflowHistory: false };
const ready = { ...draft, approvalRequired: false, approvalStatus: 'ReadyToPost', displayStatus: 'Ready to Post', canSubmit: false,
  canPostAny: true, items: [{ ...item, reviewStatus: 'NotRequired', postingStatus: 'Ready' }] };
beforeEach(() => { vi.stubGlobal('React', React); vi.clearAllMocks(); mocks.get.mockResolvedValue(draft); mocks.summary.mockResolvedValue(direct); mocks.permission.mockReturnValue(true); });
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe('journal batch optional approval', () => {
  it('prepares a direct batch without posting or displaying human review', async () => {
    mocks.submit.mockResolvedValue(ready);
    render(<Page />);
    fireEvent.click(await screen.findByRole('button', { name: 'Prepare to post' }));
    fireEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Prepare to post' }));
    await waitFor(() => expect(mocks.submit).toHaveBeenCalledWith('batch-1'));
    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ title: 'Ready to post' })));
    expect(screen.queryByRole('columnheader', { name: 'Review' })).not.toBeInTheDocument();
    expect(screen.queryByText(/Submit all .* decisions/)).not.toBeInTheDocument();
    expect(mocks.post).not.toHaveBeenCalled();
  });

  it('selects direct-ready items and posts only after explicit confirmation', async () => {
    mocks.get.mockResolvedValue(ready);
    mocks.post.mockResolvedValue({ id: 'run-1' });
    render(<Page />);
    fireEvent.click(await screen.findByRole('button', { name: 'Select all ready' }));
    fireEvent.click(screen.getByRole('button', { name: 'Post selected' }));
    expect(mocks.post).not.toHaveBeenCalled();
    fireEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Post selected' }));
    await waitFor(() => expect(mocks.post).toHaveBeenCalledWith('batch-1', ['item-1']));
    expect(screen.queryByText('NotRequired')).not.toBeInTheDocument();
  });

  it('does not substitute submission permission for posting permission', async () => {
    mocks.get.mockResolvedValue(ready);
    mocks.permission.mockImplementation((permission: string) => permission !== 'Finance.JournalBatches.Post');
    render(<Page />);
    await screen.findByRole('heading', { name: 'JB-1' });
    expect(screen.queryByRole('button', { name: 'Post selected' })).not.toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Select JE-1 for posting' })).toBeDisabled();
  });

  it('retains review for an in-flight batch even when new approvals are disabled', async () => {
    mocks.get.mockResolvedValue({ ...draft, approvalStatus: 'PendingApproval', displayStatus: 'Pending Approval', canSubmit: false, canReview: true });
    mocks.summary.mockResolvedValue({ ...direct, hasActiveInstance: true, hasWorkflowHistory: true });
    render(<Page />);
    expect(await screen.findByRole('button', { name: 'Submit all 1 decisions' })).toBeVisible();
    expect(screen.getByRole('columnheader', { name: 'Review' })).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Prepare to post' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Post selected' })).not.toBeInTheDocument();
  });

  it('fails closed on workflow lookup failure and allows retry', async () => {
    mocks.summary.mockRejectedValue(new Error('Policy check unavailable'));
    render(<Page />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Policy check unavailable');
    expect(screen.getByRole('button', { name: 'Submit for approval' })).toBeDisabled();
    mocks.summary.mockResolvedValue(direct);
    fireEvent.click(screen.getByRole('button', { name: 'Retry workflow check' }));
    expect(await screen.findByRole('button', { name: 'Prepare to post' })).toBeEnabled();
  });

  it('preserves the posting selection and dialog when the canonical posting guard fails', async () => {
    mocks.get.mockResolvedValue(ready);
    mocks.post.mockRejectedValue({ response: { detail: 'The accounting period is closed.', code: 'PERIOD_CLOSED' } });
    render(<Page />);
    fireEvent.click(await screen.findByRole('button', { name: 'Select all ready' }));
    fireEvent.click(screen.getByRole('button', { name: 'Post selected' }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Post selected' }));
    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ description: 'The accounting period is closed. (PERIOD_CLOSED)' })));
    expect(dialog).toBeVisible();
    expect(screen.getByRole('checkbox', { name: 'Select JE-1 for posting', hidden: true })).toBeChecked();
  });

  it('retains active-process submission text', async () => {
    mocks.summary.mockResolvedValue({ ...direct, approvalRequired: true });
    render(<Page />);
    expect(await screen.findByRole('button', { name: 'Submit for approval' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Prepare to post' })).not.toBeInTheDocument();
  });
});
