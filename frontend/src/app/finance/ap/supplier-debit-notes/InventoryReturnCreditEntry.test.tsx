import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const mocks = vi.hoisted(() => ({ finance: true, manage: true, candidates: vi.fn(), notes: vi.fn(), sources: vi.fn(), create: vi.fn(), toast: vi.fn() }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: (permission: string) => permission === 'Finance.Read' ? mocks.finance : permission === 'Finance.AP.SupplierDebitNotes.Manage' && mocks.manage }) }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/services/inventoryReturnCreditService', () => ({ inventoryReturnCreditService: { getCandidates: mocks.candidates, getNotes: mocks.notes, getSources: mocks.sources, create: mocks.create } }));
import { InventoryReturnCreditEntry } from './InventoryReturnCreditEntry';

const source = { returnId: 'return-1', returnNumber: 'RTV-001', supplierName: 'Harbourline', shippedDate: '2026-09-13T12:00:00Z', reason: 'Excess', totalQuantity: 1 };
function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}><InventoryReturnCreditEntry open onOpenChange={vi.fn()} /></QueryClientProvider>);
}
beforeEach(() => {
  vi.clearAllMocks(); mocks.finance = true; mocks.manage = true;
  mocks.candidates.mockResolvedValue([source]); mocks.notes.mockResolvedValue([]);
  mocks.sources.mockResolvedValue([{ invoiceId: 'invoice-1', invoiceNumber: 'VI-001', currencyCode: 'GHS', outstandingAmount: 52000 }]);
  mocks.create.mockResolvedValue({ id: 'credit-1', inventoryPurchaseReturnId: 'return-1', debitNoteNumber: 'DN-001', statusName: 'Draft', currencyCode: 'GHS', totalAmount: 700 });
});
afterEach(cleanup);

describe('AP supplier-return credit entry', () => {
  it('lets a Finance AP actor with no Inventory permission discover a return and create only its credit draft', async () => {
    show();
    fireEvent.click(await screen.findByRole('button', { name: 'Select RTV-001' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Create credit draft' }));
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Original invoice' })).toHaveTextContent('VI-001'));
    fireEvent.change(screen.getByLabelText('Supplier credit reference'), { target: { value: 'SCN-001' } });
    fireEvent.change(screen.getByLabelText('Credit date'), { target: { value: '2026-09-13' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create draft' }));
    expect(await screen.findByRole('link', { name: 'Open credit' })).toHaveAttribute('href', '/finance/ap/supplier-debit-notes/credit-1');
    expect(mocks.create).toHaveBeenCalledExactlyOnceWith('return-1', { originalVendorInvoiceId: 'invoice-1', supplierCreditNoteReference: 'SCN-001', creditDate: '2026-09-13', reason: 'Excess' });
    expect(screen.queryByRole('button', { name: /Dispatch|Post/ })).not.toBeInTheDocument();
  });
  it('does not expose or fetch financial sources without Finance.Read', () => {
    mocks.finance = false; show();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(mocks.candidates).not.toHaveBeenCalled();
  });
  it('keeps read-only Finance actors from creating a credit', async () => {
    mocks.manage = false; show();
    fireEvent.click(await screen.findByRole('button', { name: 'Select RTV-001' }));
    expect(await screen.findByText(/An AP officer can create it/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Create credit draft' })).not.toBeInTheDocument();
  });
  it('searches by readable return or supplier metadata and displays server errors', async () => {
    show(); await screen.findByText('Harbourline');
    mocks.candidates.mockRejectedValue({ response: { data: { detail: 'Finance access is unavailable.' } } });
    fireEvent.change(screen.getByLabelText('Search dispatched returns'), { target: { value: 'Harbour' } });
    expect(await screen.findByRole('alert')).toHaveTextContent('Finance access is unavailable.');
    expect(mocks.candidates).toHaveBeenLastCalledWith('Harbour');
  });
});
