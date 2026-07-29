import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  delete: vi.fn(),
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { procurementSupplierAvlService as service } from './procurement-supplier-avl.service';

describe('supplier AVL API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the dedicated tenant-safe lifecycle routes', async () => {
    await service.summary();
    await service.search({ status: 'PendingApproval', page: 2 });
    await service.current('supplier-1');
    await service.create({
      reviewYear: 2026,
      effectiveFromUtc: '2026-08-01T00:00:00Z',
      workflowDefinitionId: 'workflow-1',
    });
    await service.update('register-1', {
      rowVersion: 'register-version',
      effectiveFromUtc: '2026-08-01T00:00:00Z',
    });
    await service.addEntry('register-1', {
      businessPartnerId: 'supplier-1',
      registerRowVersion: 'register-version',
    });
    await service.removeEntry('register-1', 'entry-1', 'row+/=');
    await service.submit('register-1', lifecycle());
    await service.approve('register-1', lifecycle());
    await service.reject('register-1', lifecycle());
    await service.publish('register-1', lifecycle());
    await service.suspendEntry('register-1', 'entry-1', entryLifecycle());
    await service.reinstateEntry('register-1', 'entry-1', entryLifecycle());
    await service.processExpiry();

    expect(api.get).toHaveBeenCalledWith('/procurement/supplier-avl/summary');
    expect(api.get).toHaveBeenCalledWith(
      '/procurement/supplier-avl',
      expect.objectContaining({ status: 'PendingApproval', page: 2 })
    );
    expect(api.get).toHaveBeenCalledWith(
      '/procurement/supplier-avl/suppliers/supplier-1/current'
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-avl',
      expect.objectContaining({ reviewYear: 2026 })
    );
    expect(api.put).toHaveBeenCalledWith(
      '/procurement/supplier-avl/register-1',
      expect.objectContaining({ rowVersion: 'register-version' })
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-avl/register-1/entries',
      expect.objectContaining({ businessPartnerId: 'supplier-1' })
    );
    expect(api.delete).toHaveBeenCalledWith(
      '/procurement/supplier-avl/register-1/entries/entry-1?rowVersion=row%2B%2F%3D'
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-avl/register-1/submit',
      expect.any(Object)
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-avl/register-1/approve',
      expect.any(Object)
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-avl/register-1/publish',
      expect.any(Object)
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-avl/register-1/entries/entry-1/suspend',
      expect.any(Object)
    );
    expect(api.post).toHaveBeenCalledWith(
      '/procurement/supplier-avl/process-expiry',
      {}
    );
  });
});

const lifecycle = () => ({
  rowVersion: 'register-version',
  comment: 'Retained lifecycle evidence.',
  evidence: [
    {
      referenceKind: 'ExternalReference' as const,
      reference: 'AVL-MINUTE-001',
      label: 'AVL minute',
      requirementKey: 'SupplierAVL.Lifecycle',
    },
  ],
});

const entryLifecycle = () => ({
  rowVersion: 'entry-version',
  reason: 'Evidenced supplier status review.',
  evidence: lifecycle().evidence,
});
