import { beforeEach, describe, expect, it, vi } from 'vitest';

import { apiService } from '@/services/api.service';
import { civilEngineeringConfigurationService as service } from './civil-engineering-configuration.service';

vi.mock('@/services/api.service', () => ({
  apiService: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    request: vi.fn(),
  },
}));

describe('civilEngineeringConfigurationService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the dedicated tenant-scoped configuration routes', async () => {
    vi.mocked(apiService.get).mockResolvedValue({ items: [] });

    await service.schemas();
    await service.list({ status: 'Draft', pageSize: 20 });
    await service.history('profile-1');

    expect(apiService.get).toHaveBeenNthCalledWith(
      1,
      '/civil-engineering/configuration-profiles/schemas'
    );
    expect(apiService.get).toHaveBeenNthCalledWith(
      2,
      '/civil-engineering/configuration-profiles',
      { status: 'Draft', pageSize: 20 }
    );
    expect(apiService.get).toHaveBeenNthCalledWith(
      3,
      '/civil-engineering/configuration-profiles/profile-1/history'
    );
  });

  it('keeps DMS evidence, decision approval and publication as separate controls', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'profile-1' });
    const evidence = {
      evidenceType: 'Policy',
      centralDocumentRecordId: 'record-1',
      centralDocumentVersionId: 'version-1',
      decisionRowVersion: 'decision-row-version',
      reason: 'Approved policy evidence',
    };

    await service.linkEvidence('profile-1', 'CIV-CFG-001', evidence);
    await service.approveDecision('profile-1', 'CIV-CFG-001', {
      rowVersion: 'decision-row-version',
      approvalReference: 'MINUTE-001',
    });
    await service.publish('profile-1', {
      rowVersion: 'profile-row-version',
      reason: 'All controls independently approved',
    });

    expect(apiService.post).toHaveBeenNthCalledWith(
      1,
      '/civil-engineering/configuration-profiles/profile-1/decisions/CIV-CFG-001/evidence',
      evidence
    );
    expect(apiService.post).toHaveBeenNthCalledWith(
      2,
      '/civil-engineering/configuration-profiles/profile-1/decisions/CIV-CFG-001/approve',
      expect.objectContaining({ approvalReference: 'MINUTE-001' })
    );
    expect(apiService.post).toHaveBeenNthCalledWith(
      3,
      '/civil-engineering/configuration-profiles/profile-1/publish',
      expect.objectContaining({ reason: 'All controls independently approved' })
    );
  });
});
