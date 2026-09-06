import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { accountsPayableService } from './accountsPayableService';
import { readFileSync } from 'node:fs';

vi.mock('./api.service', () => ({
  apiService: {
    get: vi.fn(),
  },
}));

describe('accounts payable supplier identity client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('loads the Finance-owned canonical Supplier projection', async () => {
    const response = [
      {
        id: 'canonical-supplier-id',
        code: 'TDC-DEMO-SUP-001',
        name: 'Akua Payables',
        paymentTermId: 'net-30',
      },
    ];
    vi.mocked(apiService.get).mockResolvedValueOnce(response);

    await expect(accountsPayableService.getInvoiceSuppliers()).resolves.toEqual(
      response
    );
    expect(apiService.get).toHaveBeenCalledWith('/ap/invoices/suppliers');
  });

  it('uses a separate entry lookup with the Procurement id for PO filtering', async () => {
    const response = [{ id: 'canonical-id', businessPartnerId: 'procurement-id', code: 'SUP-001', name: 'Supplier' }];
    vi.mocked(apiService.get).mockResolvedValueOnce(response);
    await expect(accountsPayableService.getInvoiceSupplierEntryOptions()).resolves.toEqual(response);
    expect(apiService.get).toHaveBeenCalledWith('/ap/invoices/supplier-entry-options');
  });

  it('keeps the reusable create/edit form on entry options and preserves the PO identity', () => {
    const source = readFileSync('src/app/finance/ap/invoices/create/page.tsx', 'utf8');
    expect(source).toContain('accountsPayableService.getInvoiceSupplierEntryOptions()');
    expect(source).toContain('supplierId: selectedSupplier.businessPartnerId || selectedSupplier.id');
    expect(source).toContain('suppressPurchaseOrderHydrationRef.current');
    expect(source).not.toContain('accountsPayableService.getInvoiceSuppliers()');
  });
});
