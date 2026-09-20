import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ActualLandedCostSummary } from './ActualLandedCostSummary';
import { LandedCostInvoiceLink } from './LandedCostInvoiceLink';

const api = vi.hoisted(() => ({ source: vi.fn(), link: vi.fn(), invoices: vi.fn(), allowed: true }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: { getLandedCostsBySource: api.source, linkLandedCostInvoice: api.link } }));
vi.mock('@/services/accountsPayableService', () => ({ accountsPayableService: { getInvoices: api.invoices } }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasAnyPermission: () => api.allowed }) }));
vi.mock('next/link', () => ({ default: ({ children, ...props }: any) => <a {...props}>{children}</a> }));

const item: any = { id: 'charge', description: 'Freight', amount: 360, currency: 'GHS' };
const voucher: any = { id: 'voucher', receiptId: 'receipt', landedCostNumber: 'LC-1', status: 'Allocated', totalCostAmount: 360, currency: 'GHS', costItems: [item] };
afterEach(cleanup);
beforeEach(() => { vi.clearAllMocks(); api.allowed = true; api.source.mockResolvedValue([voucher]); });

describe('Actual receipt landed costs', () => {
  it('separates unposted costs from the approved PO amount', async () => {
    render(<ActualLandedCostSummary purchaseOrderId="po" poTotal={52000} currency="GHS" />);
    await screen.findByText('GHS 360.00 recorded');
    fireEvent.click(screen.getByRole('button', { name: /Actual receipt landed costs/ }));
    expect(screen.getByText(/approved PO amount remains GHS 52,000.00/)).toBeTruthy();
    expect(screen.queryByText('GHS 52,360.00')).toBeNull();
    expect(screen.getByRole('link', { name: 'Open receipt' }).getAttribute('href')).toBe('/procurement/purchase-receipts/receipt');
  });
  it('adds posted charges once for the cost comparison', async () => {
    api.source.mockResolvedValue([{ ...voucher, status: 'Posted' }]);
    render(<ActualLandedCostSummary purchaseOrderId="po" poTotal={52000} currency="GHS" />);
    await screen.findByText('GHS 360.00 recorded');
    expect(screen.getByText('GHS 52,360.00')).toBeTruthy();
  });
  it('does not sum unlike currencies', async () => {
    api.source.mockResolvedValue([voucher, { ...voucher, id: 'other', currency: 'EUR', totalCostAmount: 10 }]);
    render(<ActualLandedCostSummary purchaseOrderId="po" poTotal={52000} currency="GHS" />);
    await screen.findByText(/EUR 10.00 recorded/);
    expect(screen.queryByText(/PO value plus posted receipt charges/)).toBeNull();
  });
  it('distinguishes a related receipt cost from a linked invoice charge', async () => {
    render(<ActualLandedCostSummary invoiceId="invoice" purchaseOrderId="po" />);
    await screen.findByText('Related receipt cost — not linked to this invoice');
    expect(api.source).toHaveBeenCalledWith('invoice', 'invoice');
    expect(api.link).not.toHaveBeenCalled();
  });
  it('shows load failure rather than claiming zero costs', async () => {
    api.source.mockRejectedValue(new Error('offline'));
    render(<ActualLandedCostSummary purchaseOrderId="po" />);
    await screen.findByText('Receipt costs unavailable');
    expect(screen.queryByText('No receipt landed costs recorded')).toBeNull();
  });
});

describe('Supplier invoice link', () => {
  it('does not offer a mutation without the AP permission', () => {
    api.allowed = false;
    render(<LandedCostInvoiceLink voucherId="voucher" item={item} onChanged={vi.fn()} />);
    expect(screen.getByText('Not linked')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Link invoice' })).toBeNull();
  });
  it('links only after selecting a saved invoice and preserves errors', async () => {
    api.invoices.mockResolvedValue({ items: [{ id: 'invoice', invoiceNumber: 'VIN-1', supplierName: 'Carrier' }] });
    api.link.mockRejectedValue(new Error('different supplier'));
    const changed = vi.fn();
    render(<LandedCostInvoiceLink voucherId="voucher" item={item} onChanged={changed} />);
    fireEvent.click(screen.getByRole('button', { name: 'Link invoice' }));
    fireEvent.change(screen.getByLabelText('Invoice number or supplier reference'), { target: { value: 'VIN-1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Find invoice' }));
    fireEvent.change(await screen.findByRole('combobox', { name: 'Saved invoice' }), { target: { value: 'invoice' } });
    fireEvent.click(screen.getAllByRole('button', { name: 'Link invoice' }).at(-1)!);
    await waitFor(() => expect(api.link).toHaveBeenCalledWith('voucher', 'charge', 'invoice'));
    expect(changed).not.toHaveBeenCalled();
    expect(await screen.findByRole('alert')).toBeTruthy();
    expect(screen.getByRole('combobox', { name: 'Saved invoice' })).toHaveProperty('value', 'invoice');
  });
});
