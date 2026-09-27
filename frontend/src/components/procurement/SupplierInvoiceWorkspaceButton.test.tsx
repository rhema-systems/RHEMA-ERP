import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SupplierInvoiceWorkspaceButton } from './SupplierInvoiceWorkspaceButton';
import { accountsPayableService } from '@/services/accountsPayableService';

const { push, toast } = vi.hoisted(() => ({ push: vi.fn(), toast: vi.fn() }));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push }) }));
vi.mock('@/components/ui/use-toast', () => ({ useToast: () => ({ toast }) }));
vi.mock('@/services/accountsPayableService', () => ({ accountsPayableService: { getInvoice: vi.fn() } }));
Object.assign(globalThis, { React });

beforeEach(() => vi.clearAllMocks());

describe('supplier invoice workspace navigation', () => {
  it.each([
    { estateAcquisitionId: 'estate' },
    { purchaseOrderId: 'po' },
    { acceptedSupplyKind: 'WorksPaymentCertificate' },
    { isProcurementAutoInvoice: true },
    { lineItems: [{ landedCostItemId: 'charge' }] },
  ])('opens source-owned invoices in Procurement: %j', async (source) => {
    vi.mocked(accountsPayableService.getInvoice).mockResolvedValue({ id: 'persisted-invoice', lineItems: [], ...source } as never);
    render(<SupplierInvoiceWorkspaceButton invoiceId="invoice-reference" />);
    fireEvent.click(screen.getByRole('button', { name: 'Open supplier invoice' }));
    await waitFor(() => expect(push).toHaveBeenCalledWith('/procurement/supplier-invoices/persisted-invoice'));
    expect(accountsPayableService.getInvoice).toHaveBeenCalledWith('invoice-reference');
    expect(toast).not.toHaveBeenCalled();
  });

  it.each([
    { lineItems: [] },
    { isOpeningBalance: true, estateAcquisitionId: 'estate', lineItems: [] },
  ])('keeps legacy and opening invoices on Finance: %j', async (invoice) => {
    vi.mocked(accountsPayableService.getInvoice).mockResolvedValue({ id: 'legacy-invoice', ...invoice } as never);
    render(<SupplierInvoiceWorkspaceButton invoiceId="legacy-invoice" />);
    fireEvent.click(screen.getByRole('button', { name: 'Open supplier invoice' }));
    await waitFor(() => expect(push).toHaveBeenCalledWith('/finance/ap/invoices/legacy-invoice'));
  });

  it('retains the page, reports server detail and code, and allows retry after a failed lookup', async () => {
    vi.mocked(accountsPayableService.getInvoice).mockRejectedValueOnce({ response: { data: { detail: 'The invoice is unavailable in this company.', extensions: { code: 'INVOICE_SCOPE' } } } });
    render(<SupplierInvoiceWorkspaceButton invoiceId="unavailable" />);
    const button = screen.getByRole('button', { name: 'Open supplier invoice' });
    fireEvent.click(button);
    await waitFor(() => expect(toast).toHaveBeenCalledWith({ title: 'Unable to open supplier invoice', description: 'The invoice is unavailable in this company. (INVOICE_SCOPE)', variant: 'destructive' }));
    expect(push).not.toHaveBeenCalled();
    expect(button).toBeEnabled();
    vi.mocked(accountsPayableService.getInvoice).mockResolvedValueOnce({ id: 'available', estateAcquisitionId: 'estate', lineItems: [] } as never);
    fireEvent.click(button);
    await waitFor(() => expect(push).toHaveBeenCalledWith('/procurement/supplier-invoices/available'));
  });
});
