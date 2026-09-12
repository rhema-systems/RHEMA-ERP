import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { toast } from 'sonner';
import type { UnitJournalEntry } from '@/types/unit-accounts';

const mocks = vi.hoisted(() => ({
  get: vi.fn(), submit: vi.fn(), post: vi.fn(), approve: vi.fn(), reject: vi.fn(), reverse: vi.fn(), remove: vi.fn(),
  permission: vi.fn(), push: vi.fn(),
}));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'unit-1' }), useRouter: () => ({ push: mocks.push }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: mocks.permission }) }));
vi.mock('@/services/finance/unit-accounts-data.service', () => ({ unitAccountsDataService: {
  getUnitJournalEntryById: mocks.get, submitUnitJournalEntry: mocks.submit, postUnitJournalEntry: mocks.post,
  approveUnitJournalEntry: mocks.approve, rejectUnitJournalEntry: mocks.reject, reverseUnitJournalEntry: mocks.reverse,
  deleteUnitJournalEntry: mocks.remove,
} }));
import Page from './page';

const entry: UnitJournalEntry = {
  id: 'unit-1', entryNumber: 'UJE-2026-0001', entryDate: '2026-09-12', status: 'Draft',
  createdAt: '2026-09-12T10:00:00Z', createdBy: 'Maker', approvalRequired: true,
  lines: [{ id: 'line-1', lineNumber: 1, unitAccountId: 'account-1', unitAccountName: 'Staff count', quantity: 10 }],
};
beforeEach(() => { vi.clearAllMocks(); mocks.permission.mockReturnValue(true); mocks.get.mockResolvedValue(entry); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

describe('Unit journal entry optional approval', () => {
  it('offers Post, not human approval, for a submitted direct entry', async () => {
    mocks.get.mockResolvedValue({ ...entry, status: 'ReadyToPost', approvalRequired: false });
    render(<Page />);
    expect(await screen.findByText('Ready to Post')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Post' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reject' })).not.toBeInTheDocument();
  });

  it('still requires the posting permission for direct entries', async () => {
    mocks.get.mockResolvedValue({ ...entry, status: 'ReadyToPost', approvalRequired: false });
    mocks.permission.mockImplementation((permission: string) => permission !== 'Finance.JournalEntries.Post');
    render(<Page />);
    await screen.findByText('Ready to Post');
    expect(screen.queryByRole('button', { name: 'Post' })).not.toBeInTheDocument();
  });

  it('does not expose Post while a draft is unsubmitted', async () => {
    mocks.get.mockResolvedValue({ ...entry, approvalRequired: false });
    render(<Page />);
    expect(await screen.findByRole('button', { name: 'Submit' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Post' })).not.toBeInTheDocument();
  });

  it('uses the actual submission result and preserves lines when the API returns a summary', async () => {
    mocks.submit.mockResolvedValue({ ...entry, lines: undefined, status: 'ReadyToPost', approvalRequired: false });
    const success = vi.spyOn(toast, 'success');
    render(<Page />);
    fireEvent.click(await screen.findByRole('button', { name: 'Submit' }));
    await waitFor(() => expect(success).toHaveBeenCalledWith('Unit journal entry is ready to post.'));
    expect(screen.getByText('Staff count')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Post' })).toBeEnabled();
    expect(mocks.post).not.toHaveBeenCalled();
  });

  it('retains configured approval controls and keeps posting unavailable until approval', async () => {
    mocks.get.mockResolvedValue({ ...entry, status: 'PendingApproval' });
    render(<Page />);
    expect(await screen.findByRole('button', { name: 'Approve' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Post' })).not.toBeInTheDocument();
  });

  it('keeps rejection reason and dialog open on failure without native prompts', async () => {
    mocks.get.mockResolvedValue({ ...entry, status: 'PendingApproval' });
    mocks.reject.mockRejectedValue(Object.assign(new Error('Request failed.'), { response: {
      detail: 'The workflow assignment changed.', code: 'WORKFLOW_CHANGED',
    } }));
    const errorToast = vi.spyOn(toast, 'error');
    render(<Page />);
    fireEvent.click(await screen.findByRole('button', { name: 'Reject' }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('Reason'), { target: { value: 'Quantity was incorrect.' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Reject' }));
    await waitFor(() => expect(mocks.reject).toHaveBeenCalledWith('unit-1', 'Quantity was incorrect.'));
    await waitFor(() => expect(within(dialog).getByRole('button', { name: 'Reject' })).toBeEnabled());
    expect(within(dialog).getByLabelText('Reason')).toHaveValue('Quantity was incorrect.');
    expect(dialog).toBeVisible();
    expect(errorToast).toHaveBeenCalledWith('The workflow assignment changed. (WORKFLOW_CHANGED)');
  });
});
