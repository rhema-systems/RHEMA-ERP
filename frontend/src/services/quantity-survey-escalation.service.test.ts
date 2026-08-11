import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { quantitySurveyEscalationService as service } from './quantity-survey-escalation.service';

describe('quantity survey escalation API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the tenant formula query and controlled index-family routes', async () => {
    await service.list({ projectId: 'project-1', status: 'Approved', page: 1, pageSize: 25 });
    await service.indexFamilies('GSS', true);
    await service.createIndexFamily({
      code: 'GSS-PBCI-MAT',
      name: 'GSS material index',
      source: 'GssPbci',
      publisher: 'Ghana Statistical Service',
      isActive: true,
      reason: 'Create controlled family.',
    });

    expect(api.get).toHaveBeenNthCalledWith(1, '/quantity-survey/escalation-formulas', {
      projectId: 'project-1',
      status: 'Approved',
      page: 1,
      pageSize: 25,
    });
    expect(api.get).toHaveBeenNthCalledWith(
      2,
      '/quantity-survey/escalation-formulas/index-families',
      { search: 'GSS', includeInactive: true }
    );
    expect(api.post).toHaveBeenCalledWith(
      '/quantity-survey/escalation-formulas/index-families',
      expect.objectContaining({ source: 'GssPbci', reason: 'Create controlled family.' })
    );
  });

  it('posts row-version and reason to the governed lifecycle route', async () => {
    await service.lifecycle('formula-1', 'approve', 'row-version', 'Independent review complete.');

    expect(api.post).toHaveBeenCalledWith(
      '/quantity-survey/escalation-formulas/formula-1/approve',
      { rowVersion: 'row-version', reason: 'Independent review complete.' }
    );
  });
});
