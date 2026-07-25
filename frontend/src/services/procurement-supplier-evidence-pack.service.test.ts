import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { procurementSupplierEvidencePackService as service } from './procurement-supplier-evidence-pack.service';

describe('supplier evidence-pack API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the dedicated tenant-safe administration and applicant readiness routes', async () => {
    await service.summary();
    await service.search({ category: 'Works', page: 2 });
    await service.workflowOptions();
    await service.configurationProfileOptions();
    await service.get('pack-1');
    await service.registrationReadiness('registration-1');

    expect(api.get.mock.calls.map(([path]) => path)).toEqual([
      '/procurement/supplier-evidence-packs/summary',
      '/procurement/supplier-evidence-packs',
      '/procurement/supplier-evidence-packs/workflow-options',
      '/procurement/supplier-evidence-packs/configuration-profile-options',
      '/procurement/supplier-evidence-packs/pack-1',
      '/procurement/supplier-evidence-packs/registrations/registration-1/readiness',
    ]);
  });

  it('targets the complete version lifecycle without a parallel workflow or evidence API', async () => {
    const lifecycle = {
      rowVersion: 'AAAA',
      evidence: [
        {
          referenceKind: 'ExternalReference' as const,
          reference: 'MINUTE-001',
        },
      ],
    };
    await service.submit('pack-1', lifecycle);
    await service.publish('pack-1', lifecycle);
    await service.reject('pack-1', lifecycle);
    await service.retire('pack-1', lifecycle);
    await service.deleteDraft('pack-1', lifecycle);
    await service.clone('pack-1', {
      rowVersion: 'AAAA',
      effectiveFromUtc: '2026-08-01T00:00:00Z',
      changeSummary: 'Annual update',
    });

    expect(api.post.mock.calls.map(([path]) => path)).toEqual([
      '/procurement/supplier-evidence-packs/pack-1/submit',
      '/procurement/supplier-evidence-packs/pack-1/publish',
      '/procurement/supplier-evidence-packs/pack-1/reject',
      '/procurement/supplier-evidence-packs/pack-1/retire',
      '/procurement/supplier-evidence-packs/pack-1/delete-draft',
      '/procurement/supplier-evidence-packs/pack-1/clone',
    ]);
  });
});
