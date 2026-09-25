import React from 'react';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { LandedCostSupplierInvoices } from './LandedCostSupplierInvoices';
import type { LandedCostDetailDto } from '@/services/inventoryManagementService';

const api = vi.hoisted(() => ({ suppliers: vi.fn(), post: vi.fn(), allowed: true }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasAnyPermission: () => api.allowed }) }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getAllPartnersForDropdown: api.suppliers } }));
vi.mock('@/services/landedCostInvoiceService', () => ({ landedCostInvoiceService: { post: api.post } }));
vi.mock('next/link', () => ({ default: ({ children, ...props }: any) => <a {...props}>{children}</a> }));
const voucher = { id: 'costs', landedCostNumber: 'LC-1', status: 'Allocated', currency: 'GHS', costItems: [
  { id: 'freight', description: 'Freight', amount: 310, currency: 'GHS', supplierId: 'carrier', referenceNumber: 'CARRIER-1' },
  { id: 'handling', description: 'Handling', amount: 50, currency: 'GHS', supplierId: 'carrier', referenceNumber: 'CARRIER-1' },
] } as LandedCostDetailDto;
beforeEach(() => {
  vi.clearAllMocks(); api.allowed = true;
  HTMLElement.prototype.scrollIntoView = vi.fn();
  api.suppliers.mockResolvedValue([{ id: 'carrier', partnerName: 'Carrier', partnerCode: 'C1', partnerType: 'Supplier', isActive: true, approvalStatus: 'Approved' }]);
  api.post.mockResolvedValue({ inventoryPosted: true, invoicesPending: false, invoices: [
    { id: 'invoice', invoiceNumber: 'INV-1', supplierName: 'Carrier', currencyCode: 'GHS', totalAmount: 360, status: 'Draft' },
  ] });
});
afterEach(cleanup);
async function open(posted = false) {
  const changed = vi.fn(); render(<LandedCostSupplierInvoices voucher={{ ...voucher, status: posted ? 'Posted' : 'Allocated' }} onCreated={changed} />);
  await act(async () => fireEvent.click(screen.getByRole('button', { name: posted ? 'Retry Post' : 'Post', exact: true })));
  return changed;
}
describe('combined landed-cost Post', () => {
  it('requires receiving permission and prior allocation', () => {
    api.allowed = false; const { unmount } = render(<LandedCostSupplierInvoices voucher={voucher} onCreated={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Post', exact: true })).toBeDisabled(); unmount(); api.allowed = true;
    render(<LandedCostSupplierInvoices voucher={{ ...voucher, status: 'Draft' }} onCreated={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Post', exact: true })).toBeDisabled(); expect(api.post).not.toHaveBeenCalled();
  });
  it('opens a pre-post review with no tax fields and makes no mutation', async () => {
    await open(); expect(screen.getByText('Carrier · CARRIER-1 · GHS 360.00')).toBeInTheDocument();
    expect(screen.queryByLabelText('Invoice charge 1 tax treatment')).not.toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Cancel', exact: true })); expect(api.post).not.toHaveBeenCalled();
  });
  it('one confirmation posts and creates drafts without assigning tax', async () => {
    const changed = await open();
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Post' }));
    await screen.findByRole('link', { name: 'INV-1 · Carrier' });
    expect(api.post).toHaveBeenCalledTimes(1);
    const request = api.post.mock.calls[0][1]; expect(request.charges).toHaveLength(2);
    expect(request.charges[0]).toEqual({ costItemId: 'freight', businessPartnerId: 'carrier', supplierInvoiceNumber: 'CARRIER-1' });
    expect(changed).toHaveBeenCalledOnce(); expect(screen.getByText(/No invoice approval or financial posting/)).toBeInTheDocument();
  });
  it('requires billing identities but does not require tax before creating a draft', async () => {
    await open(); fireEvent.change(screen.getByLabelText('Invoice charge 1 reference'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Post' }));
    expect(screen.getByRole('alert')).toHaveTextContent('enter its invoice reference'); expect(api.post).not.toHaveBeenCalled();
  });
  it('retains inputs after a network error for an idempotent retry', async () => {
    api.post.mockRejectedValueOnce(new Error('Connection lost.'));
    const changed = await open();
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Post' }));
    await screen.findByRole('alert'); expect(screen.getByLabelText('Invoice charge 1 reference')).toHaveValue('CARRIER-1');
    expect(changed).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Post' }));
    await screen.findByRole('link', { name: 'INV-1 · Carrier' });
  });
  it('shows partial success and retries only the pending invoice stage', async () => {
    api.post.mockResolvedValueOnce({ inventoryPosted: true, invoicesPending: true, invoices: [], message: 'Inventory posted; invoice creation is pending.' });
    const changed = await open();
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Post' }));
    await screen.findByRole('alert'); expect(screen.queryByRole('link', { name: 'INV-1 · Carrier' })).not.toBeInTheDocument();
    expect(changed).toHaveBeenCalledOnce();
    fireEvent.click(screen.getByRole('button', { name: 'Finish invoice drafts' }));
    await screen.findByRole('link', { name: 'INV-1 · Carrier' });
  });
  it('provides invoice-only recovery for an existing posted voucher', async () => {
    await open(true); expect(screen.getByText(/Inventory is already posted/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Finish invoice drafts' }));
    await screen.findByRole('link', { name: 'INV-1 · Carrier' });
  });
  it('does not offer another Post when all invoices are linked', () => {
    render(<LandedCostSupplierInvoices voucher={{ ...voucher, status: 'Posted', costItems: voucher.costItems.map(i => ({ ...i, invoiceNumber: 'INV-1' })) }} onCreated={vi.fn()} />);
    expect(screen.queryByRole('button', { name: /Post/ })).not.toBeInTheDocument();
    expect(screen.getByText('Posted · Supplier invoices linked')).toBeInTheDocument();
  });
});
