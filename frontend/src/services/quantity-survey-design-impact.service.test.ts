import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { quantitySurveyDesignImpactService as service } from './quantity-survey-design-impact.service';

vi.mock('@/services/api.service', () => ({ apiService: { get: vi.fn(), post: vi.fn() } }));

describe('quantitySurveyDesignImpactService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses project-scoped lookup and list routes', async () => {
    vi.mocked(apiService.get).mockResolvedValue({});
    await service.lookups('project-1');
    await service.list('project-1');
    expect(apiService.get).toHaveBeenNthCalledWith(1, '/quantity-survey/design-revision-impacts/lookups', { projectId: 'project-1' });
    expect(apiService.get).toHaveBeenNthCalledWith(2, '/quantity-survey/design-revision-impacts', { projectId: 'project-1' });
  });

  it('uses the governed create and lifecycle routes', async () => {
    vi.mocked(apiService.post).mockResolvedValue({});
    const lifecycle = { clientRequestId: 'request-2', reason: 'Independent review', rowVersion: 'row-version' };
    await service.create({ clientRequestId: 'request-1', projectId: 'project-1', previousDrawingId: 'drawing-1', revisedDrawingId: 'drawing-2', route: 'Measurement', title: 'Impact', changeSummary: 'Drawing changed significantly.', lines: [] });
    await service.submit('impact-1', lifecycle);
    await service.approve('impact-1', lifecycle);
    await service.reject('impact-1', lifecycle);
    expect(apiService.post).toHaveBeenNthCalledWith(1, '/quantity-survey/design-revision-impacts', expect.objectContaining({ projectId: 'project-1' }));
    expect(apiService.post).toHaveBeenNthCalledWith(2, '/quantity-survey/design-revision-impacts/impact-1/submit', lifecycle);
    expect(apiService.post).toHaveBeenNthCalledWith(3, '/quantity-survey/design-revision-impacts/impact-1/approve', lifecycle);
    expect(apiService.post).toHaveBeenNthCalledWith(4, '/quantity-survey/design-revision-impacts/impact-1/reject', lifecycle);
  });
});
