import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { accountsPayableService } from './accountsPayableService';

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

  it('loads unified invoice-entry options without changing the canonical report lookup', async () => {
    const response = [
      {
        id: 'business-partner-id',
        businessPartnerId: 'business-partner-id',
        supplierId: null,
        code: 'SUP260001',
        name: 'USD Supplier',
        currency: 'USD',
      },
    ];
    vi.mocked(apiService.get).mockResolvedValueOnce(response);

    await expect(
      accountsPayableService.getInvoiceSupplierEntryOptions()
    ).resolves.toEqual(response);
    expect(apiService.get).toHaveBeenCalledWith(
      '/ap/invoices/entry-suppliers'
    );
  });
});
