import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { quantitySurveyEscalationCalculationService as service } from './quantity-survey-escalation-calculation.service';

describe('quantity survey escalation calculation API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses controlled formula and impact-target lookup endpoints', async () => {
    await service.lookups();
    await service.impactTargets('formula-1');

    expect(api.get).toHaveBeenNthCalledWith(
      1,
      '/quantity-survey/escalation-calculations/lookups'
    );
    expect(api.get).toHaveBeenNthCalledWith(
      2,
      '/quantity-survey/escalation-calculations/impact-targets',
      { formulaId: 'formula-1' }
    );
  });

  it('retains the client request id and controlled impact target', async () => {
    const request = {
      clientRequestId: 'request-1',
      formulaId: 'formula-1',
      impactTargetType: 'PaymentCertificate' as const,
      impactTargetId: 'certificate-1',
      currentIndexPeriod: '2026-08-01',
      reason: 'August escalation calculation.',
    };

    await service.calculate(request);

    expect(api.post).toHaveBeenCalledWith(
      '/quantity-survey/escalation-calculations',
      request
    );
  });

  it('separates reviewer adjustment from lifecycle actions and history', async () => {
    await service.reviewAdjustment(
      'run-1',
      'row-version',
      -125.5,
      'Independent measurement review.'
    );
    await service.lifecycle(
      'run-1',
      'approve',
      'row-version-2',
      'Approved against controlled indices.'
    );
    await service.history('run-1');

    expect(api.post).toHaveBeenNthCalledWith(
      1,
      '/quantity-survey/escalation-calculations/run-1/review-adjustment',
      {
        rowVersion: 'row-version',
        adjustmentAmount: -125.5,
        reason: 'Independent measurement review.',
      }
    );
    expect(api.post).toHaveBeenNthCalledWith(
      2,
      '/quantity-survey/escalation-calculations/run-1/approve',
      {
        rowVersion: 'row-version-2',
        reason: 'Approved against controlled indices.',
      }
    );
    expect(api.get).toHaveBeenCalledWith(
      '/quantity-survey/escalation-calculations/run-1/history'
    );
  });
});
