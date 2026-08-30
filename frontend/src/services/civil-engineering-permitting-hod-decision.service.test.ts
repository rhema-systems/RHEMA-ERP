import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringPermittingHodDecisionService } from './civil-engineering-permitting-hod-decision.service';

describe('civilEngineeringPermittingHodDecisionService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the constrained HOD queue and decision routes', () => {
    const request = { clientRequestId: 'request', outcome: 'ReturnForCorrection' as const, reason: 'Correct the structural drawing detail.' };
    civilEngineeringPermittingHodDecisionService.pending();
    civilEngineeringPermittingHodDecisionService.decide('review', request);
    expect(api.get).toHaveBeenCalledWith('/projects/civil-engineering/permitting-engineering-reviews/pending-hod-decisions');
    expect(api.post).toHaveBeenCalledWith('/projects/civil-engineering/permitting-engineering-reviews/review/hod-decision', request);
  });
});
