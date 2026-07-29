import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { procurementSupplierRiskService as service } from './procurement-supplier-risk.service';

describe('supplier risk API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses dedicated tenant-safe assessment and alert lifecycle routes', async () => {
    await service.summary();
    await service.search({ search: 'SUP', hasOpenAlerts: true, page: 2 });
    await service.get('assessment-1');
    await service.current('supplier-1');
    await service.supplierOptions();
    await service.workflowOptions();
    await service.evaluate({
      businessPartnerId: 'supplier-1',
      idempotencyKey: 'risk-1',
    });
    await service.escalate('alert-1', {
      workflowDefinitionId: 'workflow-1',
      reason: 'Approved risk escalation.',
      rowVersion: 'alert-version',
      evidence: [],
    });
    await service.resolve('alert-1', {
      reason: 'Completed independent resolution.',
      rowVersion: 'alert-version-2',
      evidence: [],
    });

    expect(api.get).toHaveBeenCalledWith('/procurement/supplier-risk/summary');
    expect(api.get).toHaveBeenCalledWith(
      '/procurement/supplier-risk',
      expect.objectContaining({ search: 'SUP', hasOpenAlerts: true, page: 2 })
    );
    expect(api.get).toHaveBeenCalledWith(
      '/procurement/supplier-risk/suppliers/supplier-1/current'
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-risk/assessments',
      expect.objectContaining({
        businessPartnerId: 'supplier-1',
        idempotencyKey: 'risk-1',
      })
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-risk/alerts/alert-1/escalate',
      expect.objectContaining({ workflowDefinitionId: 'workflow-1' })
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-risk/alerts/alert-1/resolve',
      expect.objectContaining({ rowVersion: 'alert-version-2' })
    );
  });
});
