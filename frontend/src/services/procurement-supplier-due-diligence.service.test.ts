import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { procurementSupplierDueDiligenceService as service } from './procurement-supplier-due-diligence.service';

describe('supplier due-diligence API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the dedicated tenant-safe lifecycle routes', async () => {
    await service.summary();
    await service.search({ status: 'PendingApproval', dueOnly: true });
    await service.current('supplier-1');
    await service.create({
      businessPartnerId: 'supplier-1',
      reviewType: 'Annual',
      workflowDefinitionId: 'workflow-1',
    });
    await service.update('review-1', {
      rowVersion: 'row-version',
      checks: [],
    });
    await service.submit('review-1', {
      rowVersion: 'row-version',
      comment: 'Ready for independent review.',
      evidence: [],
    });
    await service.approve('review-1', {
      rowVersion: 'row-version',
      comment: 'Workflow approved.',
      evidence: [],
    });
    await service.reject('review-1', {
      rowVersion: 'row-version',
      comment: 'Workflow rejected.',
      evidence: [],
    });

    expect(api.get).toHaveBeenCalledWith(
      '/procurement/supplier-due-diligence/summary'
    );
    expect(api.get).toHaveBeenCalledWith(
      '/procurement/supplier-due-diligence',
      expect.objectContaining({ status: 'PendingApproval', dueOnly: true })
    );
    expect(api.get).toHaveBeenCalledWith(
      '/procurement/supplier-due-diligence/suppliers/supplier-1/current'
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-due-diligence',
      expect.objectContaining({ businessPartnerId: 'supplier-1' })
    );
    expect(api.put).toHaveBeenCalledWith(
      '/procurement/supplier-due-diligence/review-1',
      expect.objectContaining({ rowVersion: 'row-version' })
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-due-diligence/review-1/submit',
      expect.any(Object)
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-due-diligence/review-1/approve',
      expect.any(Object)
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-due-diligence/review-1/reject',
      expect.any(Object)
    );
  });
});
