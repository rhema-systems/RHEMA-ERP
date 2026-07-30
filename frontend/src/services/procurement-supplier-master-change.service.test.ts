import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { procurementSupplierMasterChangeService } from './procurement-supplier-master-change.service';

vi.mock('@/services/api.service', () => ({
  apiService: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}));

describe('procurement supplier master change client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('keeps customer-only partners out of supplier options', async () => {
    vi.mocked(apiService.get).mockResolvedValueOnce({
      items: [
        {
          id: 'supplier',
          partnerCode: 'SUP-1',
          partnerName: 'Supplier',
          partnerType: 'Supplier',
          status: 'Approved',
        },
        {
          id: 'customer',
          partnerCode: 'CUS-1',
          partnerName: 'Customer',
          partnerType: 'Customer',
          status: 'Active',
        },
      ],
    });

    await expect(
      procurementSupplierMasterChangeService.suppliers()
    ).resolves.toEqual([expect.objectContaining({ id: 'supplier' })]);
  });

  it('uses the tenant-authenticated active category endpoint', async () => {
    vi.mocked(apiService.get).mockResolvedValueOnce([]);
    await procurementSupplierMasterChangeService.categories();
    expect(apiService.get).toHaveBeenCalledWith(
      '/procurement/partner-categories/active'
    );
  });
});
