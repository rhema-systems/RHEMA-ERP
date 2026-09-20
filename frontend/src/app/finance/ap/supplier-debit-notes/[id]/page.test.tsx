import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({ note: {} as any, refetch: vi.fn(), toast: vi.fn(), submit: vi.fn(), post: vi.fn(), decide: vi.fn(), cancel: vi.fn(), reverse: vi.fn(), header: vi.fn(), push: vi.fn() }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'credit-1' }), useRouter: () => ({ push: mocks.push }) }));
vi.mock('@tanstack/react-query', () => ({ useQuery: () => ({ data: mocks.note, refetch: mocks.refetch, isLoading: false }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => true }) }));
vi.mock('@/components/ui/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/components/finance/dimensions/source-document-dimension-panel', () => ({ SourceDocumentDimensionEvidence: () => null }));
vi.mock('@/services/accountsPayableService', () => ({ accountsPayableService: {
  submitSupplierDebitNote: mocks.submit, postSupplierDebitNote: mocks.post, decideSupplierDebitNote: mocks.decide,
  cancelSupplierDebitNote: mocks.cancel, reverseSupplierDebitNote: mocks.reverse, updateInventoryReturnCreditHeader: mocks.header,
} }));
import Page from './page';

beforeEach(() => {
  vi.stubGlobal('React', React); vi.clearAllMocks();
  mocks.note = { id: 'credit-1', debitNoteNumber: 'SDN-001', vendorName: 'Supplier', statusName: 'Draft', approvalRequired: false,
    inventoryPurchaseReturnId: 'return-1', originalVendorInvoiceId: 'invoice-1', originalVendorInvoiceNumber: 'VI-001',
    debitNoteDate: '2026-09-12', currencyCode: 'GHS', exchangeRate: 1, subTotal: 700, taxAmount: 0, totalAmount: 700,
    appliedAmount: 0, remainingAmount: 700, lineItems: [], applications: [], rowVersion: 'saved-version' };
  mocks.refetch.mockResolvedValue(undefined); mocks.submit.mockResolvedValue(undefined);
  mocks.post.mockReset(); mocks.post.mockResolvedValue(undefined); mocks.decide.mockResolvedValue(undefined);
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe('Supplier return credit detail', () => {
  it('corrects only draft header fields with the saved version and avoids an unusable cancellation', async () => {
    mocks.header.mockResolvedValue(undefined);
    render(<Page />);
    expect(screen.queryByRole('button', { name: 'Cancel' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Edit credit details' }));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('Supplier credit reference'), { target: { value: 'SCN-CORRECTED' } });
    fireEvent.change(within(dialog).getByLabelText('Credit date'), { target: { value: '2026-09-13' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(mocks.header).toHaveBeenCalledExactlyOnceWith('credit-1', {
      supplierCreditNoteReference: 'SCN-CORRECTED', creditDate: '2026-09-13', reason: undefined, rowVersion: 'saved-version',
    }));
    expect(mocks.post).not.toHaveBeenCalled(); expect(mocks.cancel).not.toHaveBeenCalled();
  });
  it('continues without approval when inactive and keeps invoice-derived lines immutable', async () => {
    render(<Page />);
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Approve|Submit for approval/ })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
    await waitFor(() => expect(mocks.submit).toHaveBeenCalledExactlyOnceWith('credit-1'));
    expect(mocks.post).not.toHaveBeenCalled();
  });

  it('keeps the configured approval route and uses optional comments in an app dialog', async () => {
    mocks.note.approvalRequired = true; mocks.note.statusName = 'PendingApproval';
    render(<Page />); fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    const dialog = screen.getByRole('dialog', { name: 'Approve supplier credit' });
    expect(within(dialog).getByLabelText('Comments (optional)')).toHaveValue('');
    expect(mocks.decide).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Approve' }));
    await waitFor(() => expect(mocks.decide).toHaveBeenCalledExactlyOnceWith('credit-1', true, undefined));
  });

  it('confirms the invoice and amount before posting without a fabricated approval label', async () => {
    mocks.note.statusName = 'Approved';
    render(<Page />);
    expect(screen.getByText('Ready to post')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Post' }));
    const dialog = screen.getByRole('dialog', { name: 'Post supplier credit' });
    expect(dialog).toHaveTextContent('VI-001'); expect(dialog).toHaveTextContent('Stock will not be changed again');
    expect(mocks.post).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Post' }));
    await waitFor(() => expect(mocks.post).toHaveBeenCalledExactlyOnceWith('credit-1'));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('keeps posting confirmation and problem detail on failure for a safe retry', async () => {
    mocks.note.statusName = 'Approved';
    mocks.post.mockRejectedValueOnce({ response: { data: { detail: 'Configure the supplier return clearing account.', code: 'RTV_CLEARING_REQUIRED' } } });
    render(<Page />); fireEvent.click(screen.getByRole('button', { name: 'Post' }));
    const dialog = screen.getByRole('dialog'); fireEvent.click(within(dialog).getByRole('button', { name: 'Post' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Configure the supplier return clearing account. (RTV_CLEARING_REQUIRED)');
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(mocks.refetch).not.toHaveBeenCalled();
  });

  it('shows the direct invoice application without offering payment creation or stock reversal', () => {
    Object.assign(mocks.note, { statusName: 'Posted', appliedAmount: 700, remainingAmount: 0, directInvoiceAppliedAmount: 700, directInvoiceAppliedAt: '2026-09-12T18:00:00Z' });
    render(<Page />);
    expect(screen.getByText('Credit only')).toBeInTheDocument();
    expect(screen.getByText('Applied')).toBeInTheDocument();
    expect(screen.queryByText('No applications have been recorded.')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reverse' })).not.toBeInTheDocument();
    expect(screen.queryByText(/Create applications from/)).not.toBeInTheDocument();
  });

  it('retains icon-only editing for an ordinary draft not derived from an inventory return', () => {
    mocks.note.inventoryPurchaseReturnId = undefined;
    render(<Page />);
    const edit = screen.getByRole('button', { name: 'Edit' });
    expect(edit).not.toHaveTextContent('Edit'); fireEvent.click(edit);
    expect(mocks.push).toHaveBeenCalledWith('/finance/ap/supplier-debit-notes/credit-1/edit');
  });
});
