import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ProcurementAutoInvoice } from './ProcurementAutoInvoice';
import { procurementAutoInvoiceService, type AutoInvoiceReceipt } from '@/services/procurementAutoInvoiceService';
import { accountsPayableService } from '@/services/procurementSupplierInvoiceService';
import type { VendorInvoice } from '@/types/ap';

const auth = vi.hoisted(() => ({ allowed: true }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasAnyPermission: () => auth.allowed }) }));
vi.mock('@/services/procurementAutoInvoiceService', () => ({ procurementAutoInvoiceService: { receipts: vi.fn(), create: vi.fn() } }));
vi.mock('@/services/procurementSupplierInvoiceService', () => ({ accountsPayableService: { getInvoiceSupplierEntryOptions: vi.fn() } }));
vi.mock('@/services/finance.service', () => ({ financeService: { getSettings: vi.fn().mockResolvedValue({ baseCurrency: 'GHS' }), getCurrentExchangeRate: vi.fn() } }));
vi.mock('@/lib/finance/invoice-exchange-rate', () => ({ loadApprovedInvoiceRate: vi.fn().mockResolvedValue({ rate: 1 }) }));
vi.mock('@/components/ui/select', () => ({
  Select: ({ children, value, onValueChange, disabled }: any) => <select aria-label="Supplier" value={value} disabled={disabled} onChange={e => onValueChange(e.target.value)}><option value="">Select supplier</option>{children}</select>,
  SelectTrigger: () => null, SelectValue: () => null, SelectContent: ({ children }: any) => <>{children}</>,
  SelectItem: ({ value, children, disabled }: any) => <option value={value} disabled={disabled}>{children}</option>,
}));

const receipt = (id: string, currency = 'GHS'): AutoInvoiceReceipt => ({
  goodsReceiptNoteId: id, receiptNumber: id, receiptDate: '2026-09-24', purchaseOrderId: `po-${id}`, orderNumber: `PO-${id}`,
  currencyCode: currency, warehouseId: 'warehouse', lines: [{ goodsReceiptNoteItemId: `line-${id}`, purchaseOrderItemId: `po-line-${id}`,
    description: 'Stock', unit: 'EA', acceptedQuantity: 10, returnedQuantity: 1, invoicedQuantity: 3, availableQuantity: 6, unitPrice: 20 }],
});
const invoice = { id: 'invoice' } as VendorInvoice;
beforeEach(() => {
  vi.clearAllMocks(); auth.allowed = true;
  vi.mocked(accountsPayableService.getInvoiceSupplierEntryOptions).mockResolvedValue([
    { id: 'supplier', businessPartnerId: 'supplier', businessPartnerRoleId: 'role-1', code: 'SUP', name: 'Supplier', roleType: 'Supplier', isTransactionReady: true },
    { id: 'customer', businessPartnerId: 'customer', businessPartnerRoleId: 'role-2', code: 'CUS', name: 'Incomplete supplier', roleType: 'Supplier', isTransactionReady: false, readinessMessage: 'AP profile required' },
  ] as any);
  vi.mocked(procurementAutoInvoiceService.receipts).mockResolvedValue([receipt('GRN-1'), receipt('GRN-2')]);
  vi.mocked(procurementAutoInvoiceService.create).mockResolvedValue(invoice);
});
async function open() {
  fireEvent.click(screen.getByRole('button', { name: 'Auto Invoice' }));
  await screen.findByRole('option', { name: 'SUP · Supplier (Supplier)' });
  fireEvent.change(screen.getByRole('combobox', { name: 'Supplier' }), { target: { value: 'role-1' } });
  await screen.findByLabelText('Select GRN-1 Stock');
  fireEvent.change(screen.getByLabelText('Supplier invoice reference'), { target: { value: 'BILL-100' } });
}
describe('procurement Auto Invoice', () => {
  it('consolidates selected GRNs with independently entered partial quantities', async () => {
    const created = vi.fn(); render(<ProcurementAutoInvoice onCreated={created} />); await open();
    expect(screen.queryByRole('option', { name: 'CUS · Customer' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByLabelText('Select GRN-1 Stock')); fireEvent.click(screen.getByLabelText('Select GRN-2 Stock'));
    fireEvent.change(screen.getByLabelText('Invoice quantity GRN-1 Stock'), { target: { value: '2.5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Generate one draft invoice' }));
    await waitFor(() => expect(created).toHaveBeenCalledWith(invoice));
    expect(procurementAutoInvoiceService.create).toHaveBeenCalledWith(expect.objectContaining({ businessPartnerId: 'supplier', businessPartnerRoleId: 'role-1', supplierInvoiceNumber: 'BILL-100',
      lines: [{ goodsReceiptNoteItemId: 'line-GRN-1', quantity: 2.5 }, { goodsReceiptNoteItemId: 'line-GRN-2', quantity: 6 }] }));
  });
  it('retains failed selection and retries using the same request identity', async () => {
    vi.mocked(procurementAutoInvoiceService.create).mockRejectedValueOnce({ response: { data: { detail: 'Connection interrupted.', code: 'RETRY' } } });
    render(<ProcurementAutoInvoice onCreated={vi.fn()} />); await open(); fireEvent.click(screen.getByLabelText('Select GRN-1 Stock'));
    fireEvent.click(screen.getByRole('button', { name: 'Generate one draft invoice' }));
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Connection interrupted.'));
    expect(screen.getByLabelText('Invoice quantity GRN-1 Stock')).toHaveValue(6);
    fireEvent.click(screen.getByRole('button', { name: 'Generate one draft invoice' }));
    await waitFor(() => expect(procurementAutoInvoiceService.create).toHaveBeenCalledTimes(2));
    expect(vi.mocked(procurementAutoInvoiceService.create).mock.calls[0][0]).toEqual(vi.mocked(procurementAutoInvoiceService.create).mock.calls[1][0]);
  });
  it('selects the Contractor role explicitly when a partner has both AP roles', async () => {
    vi.mocked(accountsPayableService.getInvoiceSupplierEntryOptions).mockResolvedValue([
      { id: 'partner', businessPartnerId: 'partner', businessPartnerRoleId: 'supplier-role', code: 'BP', name: 'Dual role partner', roleType: 'Supplier', isTransactionReady: true },
      { id: 'partner', businessPartnerId: 'partner', businessPartnerRoleId: 'contractor-role', code: 'BP', name: 'Dual role partner', roleType: 'Contractor', isTransactionReady: true },
    ] as any);
    render(<ProcurementAutoInvoice onCreated={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Auto Invoice' }));
    await screen.findByRole('option', { name: 'BP · Dual role partner (Contractor)' });
    fireEvent.change(screen.getByRole('combobox', { name: 'Supplier' }), { target: { value: 'contractor-role' } });
    await screen.findByLabelText('Select GRN-1 Stock');
    expect(procurementAutoInvoiceService.receipts).toHaveBeenCalledWith('partner');
    fireEvent.change(screen.getByLabelText('Supplier invoice reference'), { target: { value: 'CON-100' } });
    fireEvent.click(screen.getByLabelText('Select GRN-1 Stock'));
    fireEvent.click(screen.getByRole('button', { name: 'Generate one draft invoice' }));
    await waitFor(() => expect(procurementAutoInvoiceService.create).toHaveBeenCalledWith(expect.objectContaining({ businessPartnerId: 'partner', businessPartnerRoleId: 'contractor-role' })));
  });
  it('blocks quantities above availability and mixed currencies', async () => {
    vi.mocked(procurementAutoInvoiceService.receipts).mockResolvedValue([receipt('GRN-1'), receipt('GRN-2', 'USD')]);
    render(<ProcurementAutoInvoice onCreated={vi.fn()} />); await open(); fireEvent.click(screen.getByLabelText('Select GRN-1 Stock'));
    fireEvent.change(screen.getByLabelText('Invoice quantity GRN-1 Stock'), { target: { value: '7' } });
    expect(screen.getByRole('button', { name: 'Generate one draft invoice' })).toBeDisabled();
    fireEvent.change(screen.getByLabelText('Invoice quantity GRN-1 Stock'), { target: { value: '2' } });
    fireEvent.click(screen.getByLabelText('Select GRN-2 Stock'));
    expect(screen.getByRole('alert')).toHaveTextContent('one currency');
    expect(screen.getByRole('button', { name: 'Generate one draft invoice' })).toBeDisabled();
    expect(procurementAutoInvoiceService.create).not.toHaveBeenCalled();
  });
  it('requires invoice creation permission', () => {
    auth.allowed = false; render(<ProcurementAutoInvoice onCreated={vi.fn()} />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
