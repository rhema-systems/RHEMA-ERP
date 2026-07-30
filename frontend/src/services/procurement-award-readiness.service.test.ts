import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  silentGet: vi.fn(),
  post: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { procurementAwardReadinessService as service } from './procurement-award-readiness.service';

describe('procurement award-readiness API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('loads latest and retained history through exact tenant-safe source queries', async () => {
    await service.sodStatus('ExceptionalSourcing', 'tender-2');
    await service.latest('Tender', 'tender-1');
    await service.history('RequestForQuotation', 'rfq-1', 25);

    expect(api.get).toHaveBeenCalledWith(
      '/procurement/award-readiness/sod-status',
      { sourceType: 'ExceptionalSourcing', sourceId: 'tender-2' }
    );
    expect(api.silentGet).toHaveBeenCalledWith(
      '/procurement/award-readiness/latest',
      { sourceType: 'Tender', sourceId: 'tender-1' }
    );
    expect(api.get).toHaveBeenCalledWith(
      '/procurement/award-readiness/history',
      {
        sourceType: 'RequestForQuotation',
        sourceId: 'rfq-1',
        take: 25,
      }
    );
  });

  it('uses the explicit server evaluation endpoint without a readiness flag', async () => {
    const request = {
      idempotencyKey: 'tdc0209-evaluate-1',
      expectedRecommendedSubjectIds: ['bid-1'],
      expectedBusinessPartnerIds: ['supplier-1'],
      expectedSourceIntegrityHash: 'source-hash',
    };
    await service.evaluate('ExceptionalSourcing', 'tender-2', request);

    expect(api.post).toHaveBeenCalledWith(
      '/procurement/award-readiness/evaluate?sourceType=ExceptionalSourcing&sourceId=tender-2',
      request
    );
    expect(request).not.toHaveProperty('isReady');
    expect(request).not.toHaveProperty('approved');
  });
});
