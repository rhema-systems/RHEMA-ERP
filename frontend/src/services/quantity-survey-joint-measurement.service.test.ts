import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { quantitySurveyJointMeasurementService as service } from './quantity-survey-joint-measurement.service';

vi.mock('@/services/api.service', () => ({
  apiService: { get: vi.fn(), post: vi.fn(), downloadBlob: vi.fn() },
}));

describe('quantitySurveyJointMeasurementService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses tenant internal and project-scoped external lookup and list routes', async () => {
    vi.mocked(apiService.get).mockResolvedValue({});
    await service.lookups('project-1');
    await service.list('project-1');
    await service.lookups('project-1', true);
    await service.list('project-1', true);
    expect(apiService.get).toHaveBeenNthCalledWith(1, '/quantity-survey/joint-measurements/lookups', { projectId: 'project-1' });
    expect(apiService.get).toHaveBeenNthCalledWith(2, '/quantity-survey/joint-measurements', { projectId: 'project-1', page: 1, pageSize: 100 });
    expect(apiService.get).toHaveBeenNthCalledWith(3, '/projects/external/my-projects/project-1/joint-measurements/lookups', undefined);
    expect(apiService.get).toHaveBeenNthCalledWith(4, '/projects/external/my-projects/project-1/joint-measurements', undefined);
  });

  it('uses explicit internal workflow and external attendance and endorsement routes', async () => {
    vi.mocked(apiService.post).mockResolvedValue({});
    const lifecycle = { clientRequestId: 'request-1', rowVersion: 'row-version' };
    await service.schedule('joint-1', lifecycle);
    await service.review('joint-1', lifecycle);
    await service.approve('joint-1', lifecycle);
    await service.attend('project-1', 'joint-1', 'participant-1', lifecycle, true);
    await service.endorse('project-1', 'joint-1', 'participant-1', lifecycle);
    expect(apiService.post).toHaveBeenNthCalledWith(1, '/quantity-survey/joint-measurements/joint-1/schedule', lifecycle);
    expect(apiService.post).toHaveBeenNthCalledWith(2, '/quantity-survey/joint-measurements/joint-1/review', lifecycle);
    expect(apiService.post).toHaveBeenNthCalledWith(3, '/quantity-survey/joint-measurements/joint-1/approve', lifecycle);
    expect(apiService.post).toHaveBeenNthCalledWith(4, '/projects/external/my-projects/project-1/joint-measurements/joint-1/participants/participant-1/attendance', lifecycle);
    expect(apiService.post).toHaveBeenNthCalledWith(5, '/projects/external/my-projects/project-1/joint-measurements/joint-1/participants/participant-1/endorse', lifecycle);
  });

  it('uploads governed evidence and opens it only through scoped routes', async () => {
    vi.mocked(apiService.post).mockResolvedValue({});
    vi.mocked(apiService.downloadBlob).mockResolvedValue(new Blob());
    const file = new File(['photo'], 'site.jpg', { type: 'image/jpeg' });
    await service.addEvidence('project-1', 'joint-1', {
      clientRequestId: 'request-2', evidenceType: 'SitePhoto', title: 'Joint site photo', file,
    }, true);
    await service.evidenceContent('project-1', 'joint-1', 'evidence-1', true);
    expect(apiService.post).toHaveBeenCalledWith(
      '/projects/external/my-projects/project-1/joint-measurements/joint-1/evidence',
      expect.any(FormData)
    );
    expect(apiService.downloadBlob).toHaveBeenCalledWith(
      '/projects/external/my-projects/project-1/joint-measurements/joint-1/evidence/evidence-1/content'
    );
  });
});
