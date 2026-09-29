import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import type { InventoryReturnCreditNote } from '@/services/inventoryReturnCreditService';
import type { SupplierReturnAccountingGroup } from '@/services/supplierReturnService';

const mocks = vi.hoisted(() => ({ read: true, manage: true, notes: vi.fn(), sources: vi.fn(), create: vi.fn(), toast: vi.fn() }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: (permission: string) => permission === 'Finance.Read' ? mocks.read : mocks.manage }) }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/services/inventoryReturnCreditService', () => ({ inventoryReturnCreditService: { getNotes: mocks.notes, getSources: mocks.sources, create: mocks.create } }));
import { SupplierReturnCreditPanel, isReturnCreditResolved } from './SupplierReturnCreditPanel';

const note = (extra: Partial<InventoryReturnCreditNote> = {}): InventoryReturnCreditNote => ({
  id: 'credit-1', inventoryPurchaseReturnId: 'return-1', debitNoteNumber: 'SDN-001', status: 'Draft', statusName: 'Draft', currencyCode: 'GHS', totalAmount: 700, ...extra,
});
const posted = () => note({ status: 'Posted', statusName: 'Posted', journalEntryId: 'credit-journal', postingEventId: 'credit-event',
  returnDispatchJournalEntryId: 'dispatch-journal', returnDispatchPostingEventId: 'dispatch-event', directInvoiceAppliedAmount: 700, directInvoiceAppliedAt: '2026-09-12T18:00:00Z' });
const props = { returnId: 'return-1', returnNumber: 'SRT-001', reason: 'Excess' };
const group = (extra: Partial<SupplierReturnAccountingGroup> = {}): SupplierReturnAccountingGroup => ({
  id: 'group-1', originalVendorInvoiceId: 'invoice-1', originalInvoiceNumber: 'VI-001', baseQuantity: 10,
  carryingAmount: 700, originalAccrualAmount: 0, functionalCurrency: 'GHS', financeResolutionCompleted: false, ...extra,
});

beforeEach(() => {
  vi.clearAllMocks(); mocks.read = true; mocks.manage = true;
  mocks.notes.mockResolvedValue([]);
  mocks.sources.mockResolvedValue([{ invoiceId: 'invoice-1', invoiceNumber: 'VI-001', supplierInvoiceNumber: 'SUP-001', currencyCode: 'GHS', outstandingAmount: 52000 }]);
  mocks.create.mockResolvedValue(note());
});
afterEach(cleanup);

async function openDraft() {
  render(<SupplierReturnCreditPanel {...props} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Create credit draft' }));
  await waitFor(() => expect(screen.getByRole('combobox', { name: 'Original invoice' })).toHaveTextContent('VI-001'));
}

describe('Supplier return credit', () => {
  it('keeps the return pending until every original invoice group is settled', async () => {
    mocks.notes.mockResolvedValue([{ ...posted(), inventorySupplierReturnAccountingGroupId: 'group-1' }]);
    const resolved = vi.fn();
    render(<SupplierReturnCreditPanel {...props} onResolved={resolved} accountingGroups={[
      group({ supplierDebitNoteId: 'credit-1' }),
      group({ id: 'group-2', originalVendorInvoiceId: 'invoice-2', originalInvoiceNumber: 'VI-002' }),
    ]} />);
    expect(await screen.findByRole('button', { name: 'Create credit for VI-002' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Completed accounting groups' })).toHaveTextContent('VI-001');
    expect(screen.getByRole('region', { name: 'Pending accounting groups' })).toHaveTextContent('VI-002');
    await waitFor(() => expect(resolved).toHaveBeenCalledWith('return-1', false));
    expect(resolved).not.toHaveBeenCalledWith('return-1', true);
  });

  it('creates the remaining invoice group without losing the first credit link', async () => {
    mocks.notes.mockResolvedValue([{ ...posted(), inventorySupplierReturnAccountingGroupId: 'group-1' }]);
    mocks.sources.mockResolvedValue([{ invoiceId: 'invoice-2', invoiceNumber: 'VI-002', currencyCode: 'GHS', outstandingAmount: 200 }]);
    mocks.create.mockResolvedValue(note({ id: 'credit-2', debitNoteNumber: 'SDN-002', inventorySupplierReturnAccountingGroupId: 'group-2' }));
    render(<SupplierReturnCreditPanel {...props} accountingGroups={[
      group({ supplierDebitNoteId: 'credit-1' }),
      group({ id: 'group-2', originalVendorInvoiceId: 'invoice-2', originalInvoiceNumber: 'VI-002' }),
    ]} />);
    fireEvent.click(await screen.findByRole('button', { name: 'Create credit for VI-002' }));
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Original invoice' })).toHaveTextContent('VI-002'));
    fireEvent.change(screen.getByLabelText('Supplier credit reference'), { target: { value: 'SCN-002' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create draft' }));
    expect(await screen.findByRole('link', { name: 'Open SDN-002' })).toHaveAttribute('href', '/finance/ap/supplier-debit-notes/credit-2');
    expect(screen.getByRole('link', { name: 'Open SDN-001' })).toHaveAttribute('href', '/finance/ap/supplier-debit-notes/credit-1');
    expect(mocks.create.mock.calls[0][1].originalVendorInvoiceId).toBe('invoice-2');
  });

  it('shows cleared uninvoiced goods without asking for an AP credit', async () => {
    const resolved = vi.fn();
    render(<SupplierReturnCreditPanel {...props} onResolved={resolved} accountingGroups={[
      group({ originalVendorInvoiceId: null, originalInvoiceNumber: null, originalAccrualAmount: 400, financeResolutionCompleted: true }),
    ]} />);
    expect(await screen.findByText('Receipt accrual cleared')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Create credit/ })).not.toBeInTheDocument();
    await waitFor(() => expect(resolved).toHaveBeenCalledWith('return-1', true));
  });

  it('does not fetch financial data or offer credit creation without Finance access', () => {
    mocks.read = false;
    render(<SupplierReturnCreditPanel {...props} />);
    expect(screen.getByText(/Finance can link/)).toBeInTheDocument();
    expect(mocks.notes).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: 'Create credit draft' })).not.toBeInTheDocument();
  });

  it('offers an existing linked draft instead of creating a second credit', async () => {
    mocks.notes.mockResolvedValue([note()]);
    render(<SupplierReturnCreditPanel {...props} />);
    expect(await screen.findByRole('link', { name: 'Open credit' })).toHaveAttribute('href', '/finance/ap/supplier-debit-notes/credit-1');
    expect(screen.getByText('Credit saved · Finance resolution pending')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Create credit draft' })).not.toBeInTheDocument();
    expect(mocks.create).not.toHaveBeenCalled();
  });

  it('requires posted dispatch, posted credit and full original-invoice application for completion', async () => {
    const value = posted();
    expect(isReturnCreditResolved(value)).toBe(true);
    for (const patch of [{ directInvoiceAppliedAmount: 699 }, { directInvoiceAppliedAt: null }, { postingEventId: null },
      { returnDispatchJournalEntryId: null }, { journalEntryId: '00000000-0000-0000-0000-000000000000' }, { status: 'Reversed', statusName: 'Reversed' }]) {
      expect(isReturnCreditResolved({ ...value, ...patch })).toBe(false);
    }
    mocks.notes.mockResolvedValue([value]);
    const resolved = vi.fn();
    render(<SupplierReturnCreditPanel {...props} onResolved={resolved} />);
    expect(await screen.findByText('Credit posted and applied to original invoice')).toBeInTheDocument();
    await waitFor(() => expect(resolved).toHaveBeenCalledWith('return-1', true));
  });

  it('creates a draft from the exact invoice and retained reason without posting anything', async () => {
    await openDraft();
    expect(screen.getByRole('dialog')).toHaveStyle({ width: '560px' });
    expect(screen.getByRole('button', { name: 'Create draft' })).toBeDisabled();
    fireEvent.change(screen.getByLabelText('Supplier credit reference'), { target: { value: ' SCN-001 ' } });
    fireEvent.change(screen.getByLabelText('Credit date'), { target: { value: '2026-09-12' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create draft' }));
    await waitFor(() => expect(mocks.create).toHaveBeenCalledWith('return-1', {
      originalVendorInvoiceId: 'invoice-1', supplierCreditNoteReference: 'SCN-001', creditDate: '2026-09-12', reason: 'Excess',
    }));
    expect(await screen.findByRole('link', { name: 'Open credit' })).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Post' })).not.toBeInTheDocument();
  });

  it('keeps entered values after server rejection and supports a safe same-form retry', async () => {
    mocks.create.mockRejectedValueOnce({ response: { data: { error: 'Source invoice is no longer eligible.', code: 'RETURN_SOURCE' } } });
    await openDraft();
    fireEvent.change(screen.getByLabelText('Supplier credit reference'), { target: { value: 'SCN-RETRY' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create draft' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Source invoice is no longer eligible. (RETURN_SOURCE)');
    expect(screen.getByLabelText('Supplier credit reference')).toHaveValue('SCN-RETRY');
    fireEvent.click(screen.getByRole('button', { name: 'Create draft' }));
    await screen.findByRole('link', { name: 'Open credit' });
    expect(mocks.create).toHaveBeenCalledTimes(2);
    expect(mocks.create.mock.calls[1]).toEqual(mocks.create.mock.calls[0]);
  });

  it('shows an actionable empty-source message without manual quantity or amount entry', async () => {
    mocks.sources.mockResolvedValue([]);
    render(<SupplierReturnCreditPanel {...props} />);
    fireEvent.click(await screen.findByRole('button', { name: 'Create credit draft' }));
    expect(await screen.findByText(/No eligible posted invoice/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Create draft' })).toBeDisabled();
    expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument();
  });

  it('does not offer creation when checking the linked credit failed or returned another return', async () => {
    mocks.notes.mockResolvedValue([note({ inventoryPurchaseReturnId: 'other-return' })]);
    render(<SupplierReturnCreditPanel {...props} />);
    expect(await screen.findByRole('alert')).toHaveTextContent('does not belong');
    expect(screen.queryByRole('button', { name: 'Create credit draft' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Open credit' })).not.toBeInTheDocument();
  });

  it('does not allow a Finance read-only actor to create credits', async () => {
    mocks.manage = false;
    render(<SupplierReturnCreditPanel {...props} />);
    expect(await screen.findByText(/An AP officer can create it/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Create credit draft' })).not.toBeInTheDocument();
  });
});
