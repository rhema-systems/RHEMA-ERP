import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { quantitySurveyDayworkService as service } from './quantity-survey-daywork.service';

vi.mock('@/services/api.service', () => ({ apiService: { get: vi.fn(), put: vi.fn(), post: vi.fn(), downloadBlob: vi.fn() } }));

describe('quantity survey daywork service', () => {
  beforeEach(() => vi.clearAllMocks());
  it('uses the internal project-scoped workspace route', async () => {
    vi.mocked(apiService.get).mockResolvedValue({ variations: [], rates: [], sheets: [] });
    await service.workspace('project-1');
    expect(apiService.get).toHaveBeenCalledWith('/quantity-survey/dayworks', { projectId: 'project-1' });
  });
  it('uses the external contractor route for save and sign', async () => {
    vi.mocked(apiService.put).mockResolvedValue({}); vi.mocked(apiService.post).mockResolvedValue({});
    await service.saveExternal('project-1', { variationOrderId: 'variation-1' });
    await service.signExternal('project-1', 'sheet-1', { clientRequestId: 'request-1', rowVersion: 'row', reason: 'Signed on site.' });
    expect(apiService.put).toHaveBeenCalledWith('/projects/external/my-projects/project-1/dayworks', expect.any(Object));
    expect(apiService.post).toHaveBeenCalledWith('/projects/external/my-projects/project-1/dayworks/sheet-1/sign', expect.any(Object));
  });
  it('uses the permission-protected internal verification route', async () => {
    vi.mocked(apiService.post).mockResolvedValue({});
    await service.verify('sheet-1', true, { clientRequestId: 'request-1', rowVersion: 'row', reason: 'Rates and evidence verified.' });
    expect(apiService.post).toHaveBeenCalledWith('/quantity-survey/dayworks/sheet-1/verify', expect.any(Object));
  });
});
