import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({ existing: {} as any, save: vi.fn(), toast: vi.fn(), push: vi.fn() }));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mocks.push, back: vi.fn() }) }));
vi.mock('@/components/ui/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/components/finance/dimensions/source-document-dimension-panel', () => ({ SourceDocumentDimensionPanel: () => null }));
vi.mock('@/services/accountsPayableService', () => ({ accountsPayableService: { updateSupplierDebitNote: mocks.save } }));
vi.mock('@/services/finance.service', () => ({ financeService: {} }));
vi.mock('@/services/finance/finance-data.service', () => ({ financeDataService: {} }));
vi.mock('@/services/finance/tax-data.service', () => ({ taxDataService: {} }));
vi.mock('@/lib/finance/invoice-exchange-rate', () => ({ loadApprovedInvoiceRate: async () => ({ rate: 1 }) }));
vi.mock('@tanstack/react-query', () => ({ useQuery: ({ queryKey }: any) => ({
  isLoading: false, isPending: false, data: queryKey[0] === 'supplier-debit-note' ? mocks.existing
    : queryKey[0] === 'finance-settings' ? { baseCurrency: 'GHS' }
    : queryKey[0] === 'supplier-debit-note-posting-accounts' ? [
      { id: 'expense', accountCode: '5000', accountName: 'Expense', accountType: 'Expense', allowDirectPosting: true, status: 'Active' },
      { id: 'asset', accountCode: '1200', accountName: 'Inventory', accountType: 'Asset', allowDirectPosting: true, status: 'Active' },
    ] : queryKey[0] === 'posted-supplier-invoices' ? { items: [] } : [],
}) }));

import { SupplierDebitNoteForm } from './SupplierDebitNoteForm';

const originalScrollIntoView = HTMLElement.prototype.scrollIntoView;

beforeEach(() => {
  vi.stubGlobal('React', React); vi.clearAllMocks();
  HTMLElement.prototype.scrollIntoView = vi.fn();
  mocks.save.mockResolvedValue({ id: 'note' });
  mocks.existing = {
    id: 'note', vendorId: 'supplier', debitNoteDate: '2026-09-25', reason: 'Agreed supplier reduction',
    currencyCode: 'GHS', exchangeRate: 1, rowVersion: 'version', lineItems: [{
      id: 'line', lineItemType: 'Expense', glAccountId: 'expense', description: 'Credit',
      quantity: 1, unitPrice: 100, taxGroupId: 'vat', taxRate: 10, taxAmount: 9.5,
      discountPercentage: 5, discountAmount: 5, lineTotal: 104.5,
    }],
  };
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); HTMLElement.prototype.scrollIntoView = originalScrollIntoView; });

describe('Supplier debit note write-off entry', () => {
  it('removes tax and discount when selecting a write-off and saves the supplier account fallback', async () => {
    render(<SupplierDebitNoteForm noteId="note" />);
    fireEvent.click(screen.getByRole('combobox', { name: 'Credit type *' }));
    fireEvent.click(await screen.findByRole('option', { name: 'Supplier liability write-off' }));
    expect(screen.getByText('Use supplier Writeoffs account')).toBeInTheDocument();
    expect(screen.getByText(/applying the credit to outstanding invoices is a separate step/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(mocks.save).toHaveBeenCalledWith('note', expect.objectContaining({
      rowVersion: 'version', originalVendorInvoiceId: undefined,
      lines: [expect.objectContaining({ lineItemType: 'Writeoff', glAccountId: undefined,
        taxGroupId: undefined, taxRate: 0, taxAmount: 0, discountPercentage: 0, discountAmount: 0, lineTotal: 100 })],
    })));
  });

  it('retains the captured write-off classification and account when reopening a saved draft', async () => {
    Object.assign(mocks.existing.lineItems[0], { lineItemType: 'Writeoff', taxGroupId: undefined,
      taxRate: 0, taxAmount: 0, discountPercentage: 0, discountAmount: 0, lineTotal: 100 });
    render(<SupplierDebitNoteForm noteId="note" />);
    expect(screen.getByRole('combobox', { name: 'Credit type *' })).toHaveTextContent('Supplier liability write-off');
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(mocks.save).toHaveBeenCalledWith('note', expect.objectContaining({
      lines: [expect.objectContaining({ lineItemType: 'Writeoff', glAccountId: 'expense' })],
    })));
  });

  it('retains standard credit classification and amounts while saving', async () => {
    render(<SupplierDebitNoteForm noteId="note" />);
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(mocks.save).toHaveBeenCalledWith('note', expect.objectContaining({
      lines: [expect.objectContaining({ lineItemType: 'Expense', glAccountId: 'expense',
        taxGroupId: 'vat', taxAmount: 9.5, discountAmount: 5, lineTotal: 104.5 })],
    })));
  });
});
