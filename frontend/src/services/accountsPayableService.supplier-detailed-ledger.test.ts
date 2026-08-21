import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { accountsPayableService } from './accountsPayableService';

vi.mock('./api.service', () => ({
  apiService: {
    get: vi.fn(),
  },
}));

describe('accounts payable supplier detailed ledger client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('submits canonical Supplier.Id values and omits the filter for All Suppliers', async () => {
    vi.mocked(apiService.get).mockResolvedValue({ suppliers: [] });

    await accountsPayableService.getSupplierDetailedLedger({
      fromDate: '2025-01-01',
      toDate: '2025-01-31',
      supplierIds: ['canonical-supplier-id'],
      showSupplierCurrency: false,
    });
    expect(apiService.get).toHaveBeenLastCalledWith(
      expect.stringContaining('supplierIds=canonical-supplier-id')
    );

    await accountsPayableService.getSupplierDetailedLedger({
      fromDate: '2025-01-01',
      toDate: '2025-01-31',
      supplierIds: [],
      showSupplierCurrency: false,
    });
    expect(apiService.get).toHaveBeenLastCalledWith(
      expect.not.stringContaining('supplierIds=')
    );
  });
});
