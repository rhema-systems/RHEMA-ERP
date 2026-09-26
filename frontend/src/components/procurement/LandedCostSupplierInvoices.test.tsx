import React from 'react';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { LandedCostSupplierInvoices } from './LandedCostSupplierInvoices';
import type { LandedCostDetailDto } from '@/services/inventoryManagementService';

const api = vi.hoisted(() => ({ suppliers: vi.fn(), prepare: vi.fn(), allowed: true }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasAnyPermission: () => api.allowed }) }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getAllPartnersForDropdown: api.suppliers } }));
vi.mock('@/services/landedCostInvoiceService', () => ({ landedCostInvoiceService: { prepare: api.prepare } }));
vi.mock('next/link', () => ({ default: ({ children, ...props }: any) => <a {...props}>{children}</a> }));
const voucher = { id: 'costs', landedCostNumber: 'LC-1', status: 'Allocated', currency: 'GHS', costItems: [
  { id: 'freight', description: 'Freight', amount: 310, currency: 'GHS', supplierId: 'carrier', referenceNumber: 'CARRIER-1' },
  { id: 'handling', description: 'Handling', amount: 50, currency: 'GHS', supplierId: 'carrier', referenceNumber: 'CARRIER-1' },
] } as LandedCostDetailDto;
beforeEach(() => {
  vi.clearAllMocks(); api.allowed = true;
  HTMLElement.prototype.scrollIntoView = vi.fn();
  api.suppliers.mockResolvedValue([{ id: 'carrier', partnerName: 'Carrier', partnerCode: 'C1', partnerType: 'Supplier', isActive: true, approvalStatus: 'Approved' }]);
  api.prepare.mockResolvedValue({ inventoryPosted: false, invoicesPending: false, invoices: [
    { id: 'invoice', invoiceNumber: 'INV-1', supplierName: 'Carrier', currencyCode: 'GHS', totalAmount: 360, status: 'Draft' },
  ] });
});
afterEach(cleanup);
async function open(posted = false) {
  const changed = vi.fn(); render(<LandedCostSupplierInvoices voucher={{ ...voucher, status: posted ? 'Posted' : 'Allocated' }} onCreated={changed} />);
  await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Prepare supplier invoices', exact: true })));
  return changed;
}
describe('landed-cost invoice preparation', () => {
  it('requires receiving permission and prior allocation', () => {
    api.allowed = false; const { unmount } = render(<LandedCostSupplierInvoices voucher={voucher} onCreated={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Prepare supplier invoices', exact: true })).toBeDisabled(); unmount(); api.allowed = true;
    render(<LandedCostSupplierInvoices voucher={{ ...voucher, status: 'Draft' }} onCreated={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Prepare supplier invoices', exact: true })).toBeDisabled(); expect(api.prepare).not.toHaveBeenCalled();
  });
  it('opens a draft preparation review with no tax fields and makes no mutation', async () => {
    await open(); expect(screen.getByText('Carrier · CARRIER-1 · GHS 360.00')).toBeInTheDocument();
    expect(screen.queryByLabelText('Invoice charge 1 tax treatment')).not.toBeInTheDocument();
    expect(api.prepare).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Cancel', exact: true })); expect(api.prepare).not.toHaveBeenCalled();
  });
  it('prepares drafts without posting or assigning tax', async () => {
    const changed = await open();
    fireEvent.click(screen.getByRole('button', { name: 'Prepare drafts' }));
    await screen.findByRole('link', { name: 'INV-1 · Carrier' });
    expect(api.prepare).toHaveBeenCalledTimes(1);
    const request = api.prepare.mock.calls[0][1]; expect(request.charges).toHaveLength(2);
    expect(request.charges[0]).toEqual({ costItemId: 'freight', supplierId: 'carrier', supplierInvoiceNumber: 'CARRIER-1' });
    expect(changed).toHaveBeenCalledOnce(); expect(screen.getByText(/review taxes, approve and post/)).toBeInTheDocument();
  });
  it('requires billing identities but does not require tax before creating a draft', async () => {
    await open(); fireEvent.change(screen.getByLabelText('Invoice charge 1 reference'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: 'Prepare drafts' }));
    expect(screen.getByRole('alert')).toHaveTextContent('enter its invoice reference'); expect(api.prepare).not.toHaveBeenCalled();
  });
  it('retains inputs after a network error for an idempotent retry', async () => {
    api.prepare.mockRejectedValueOnce(new Error('Connection lost.'));
    const changed = await open();
    fireEvent.click(screen.getByRole('button', { name: 'Prepare drafts' }));
    await screen.findByRole('alert'); expect(screen.getByLabelText('Invoice charge 1 reference')).toHaveValue('CARRIER-1');
    expect(changed).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Prepare drafts' }));
    await screen.findByRole('link', { name: 'INV-1 · Carrier' });
  });
  it('shows server failure and preserves the billing inputs without reporting a partial posting', async () => {
    api.prepare.mockRejectedValueOnce({ response: { data: { detail: 'Invoice preparation rolled back.' } } });
    const changed = await open();
    fireEvent.click(screen.getByRole('button', { name: 'Prepare drafts' }));
    await screen.findByRole('alert');
    expect(screen.getByRole('alert')).toHaveTextContent('Invoice preparation rolled back.');
    expect(screen.getByLabelText('Invoice charge 1 reference')).toHaveValue('CARRIER-1');
    expect(changed).not.toHaveBeenCalled();
  });
  it('provides invoice-only recovery for an existing posted voucher', async () => {
    await open(true); expect(screen.getByText(/voucher was already posted/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Prepare drafts' }));
    await screen.findByRole('link', { name: 'INV-1 · Carrier' });
  });
  it('directs linked invoices to their posting process', () => {
    render(<LandedCostSupplierInvoices voucher={{ ...voucher, status: 'Posted', costItems: voucher.costItems.map(i => ({ ...i, invoiceNumber: 'INV-1' })) }} onCreated={vi.fn()} />);
    expect(screen.queryByRole('button', { name: /Prepare/ })).not.toBeInTheDocument();
    expect(screen.getByText('Supplier invoices linked · Post from Invoices')).toBeInTheDocument();
  });
});
