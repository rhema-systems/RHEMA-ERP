import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { procurementSupplierPerformanceService as service } from './procurement-supplier-performance.service';

describe('supplier performance scorecard API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the dedicated tenant-safe immutable scorecard routes', async () => {
    await service.summary();
    await service.search({ search: 'SUP', belowMinimum: true, page: 2 });
    await service.get('scorecard-1');
    await service.current('supplier-1');
    await service.supplierOptions();
    await service.calculate({
      businessPartnerId: 'supplier-1',
      idempotencyKey: 'performance-1',
    });

    expect(api.get).toHaveBeenCalledWith(
      '/procurement/supplier-performance-scorecards/summary'
    );
    expect(api.get).toHaveBeenCalledWith(
      '/procurement/supplier-performance-scorecards',
      expect.objectContaining({ search: 'SUP', belowMinimum: true, page: 2 })
    );
    expect(api.get).toHaveBeenCalledWith(
      '/procurement/supplier-performance-scorecards/suppliers/supplier-1/current'
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-performance-scorecards/calculate',
      expect.objectContaining({
        businessPartnerId: 'supplier-1',
        idempotencyKey: 'performance-1',
      })
    );
  });
});
