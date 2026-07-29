import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { procurementSupplierEligibilityService as service } from './procurement-supplier-eligibility.service';

describe('supplier eligibility API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the dedicated read-only tenant-safe eligibility routes', async () => {
    await service.boundaries();
    await service.evaluate({
      businessPartnerId: 'partner-1',
      boundary: 'FrameworkCallOff',
      categoryIds: ['category-1'],
      requiresPrequalification: true,
    });

    expect(api.get).toHaveBeenCalledWith(
      '/procurement/supplier-validation/boundaries'
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-validation/eligibility',
      expect.objectContaining({
        businessPartnerId: 'partner-1',
        boundary: 'FrameworkCallOff',
      })
    );
  });
});
