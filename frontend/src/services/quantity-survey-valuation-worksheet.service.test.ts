import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { quantitySurveyValuationWorksheetService as service } from './quantity-survey-valuation-worksheet.service';

vi.mock('@/services/api.service', () => ({ apiService: {
  get: vi.fn(), put: vi.fn(), post: vi.fn(), downloadBlob: vi.fn(),
} }));

describe('quantitySurveyValuationWorksheetService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('loads only controlled project and interim-valuation routes', async () => {
    vi.mocked(apiService.get).mockResolvedValue({});
    await service.lookups('project-1');
    await service.get('valuation-1', 'boq-version-1');
    expect(apiService.get).toHaveBeenNthCalledWith(1, '/quantity-survey/valuation-worksheets/lookups', { projectId: 'project-1' });
    expect(apiService.get).toHaveBeenNthCalledWith(2, '/quantity-survey/valuation-worksheets/valuation-1', { projectBoqVersionId: 'boq-version-1' });
  });

  it('preserves the server idempotency and concurrency contract when saving', async () => {
    vi.mocked(apiService.put).mockResolvedValue({});
    const request = {
      clientRequestId: 'request-1', projectBoqVersionId: 'boq-version-1', rowVersion: 'row-version',
      retentionPercentage: 10,
      lines: [{ projectBoqVersionLineId: 'line-1', currentClaimedQuantity: 8, currentCertifiedQuantity: 7, reviewNote: 'One unit disputed.' }],
    };
    await service.save('valuation-1', request);
    expect(apiService.put).toHaveBeenCalledWith('/quantity-survey/valuation-worksheets/valuation-1', request);
  });

  it('uses the audit-protected history route', async () => {
    vi.mocked(apiService.get).mockResolvedValue([]);
    await service.history('valuation-1');
    expect(apiService.get).toHaveBeenCalledWith('/quantity-survey/valuation-worksheets/valuation-1/history');
  });

  it('uses the shared governed lifecycle routes for QS approval actions', async () => {
    vi.mocked(apiService.post).mockResolvedValue({});
    const request = { clientRequestId: 'request-2', rowVersion: 'row-version', reason: 'Reviewed independently.' };
    await service.vet('worksheet-1', request);
    await service.submitApproval('worksheet-1', request);
    await service.approve('worksheet-1', request);
    await service.reject('worksheet-1', request);
    expect(apiService.post).toHaveBeenNthCalledWith(1, '/quantity-survey/valuation-worksheets/worksheets/worksheet-1/vet', request);
    expect(apiService.post).toHaveBeenNthCalledWith(2, '/quantity-survey/valuation-worksheets/worksheets/worksheet-1/submit-approval', request);
    expect(apiService.post).toHaveBeenNthCalledWith(3, '/quantity-survey/valuation-worksheets/worksheets/worksheet-1/approve', request);
    expect(apiService.post).toHaveBeenNthCalledWith(4, '/quantity-survey/valuation-worksheets/worksheets/worksheet-1/reject', request);
  });

  it('keeps contractor and consultant portal actions on tenant-scoped project routes', async () => {
    vi.mocked(apiService.get).mockResolvedValue([]);
    vi.mocked(apiService.put).mockResolvedValue({});
    vi.mocked(apiService.post).mockResolvedValue({});
    await service.externalList('project-1');
    await service.saveContractorClaim('project-1', 'worksheet-1', { clientRequestId: 'request-3' });
    await service.submitContractorClaim('project-1', 'worksheet-1', { clientRequestId: 'request-4' });
    await service.endorseConsultant('project-1', 'worksheet-1', { clientRequestId: 'request-5' });
    expect(apiService.get).toHaveBeenCalledWith('/projects/external/my-projects/project-1/valuation-worksheets');
    expect(apiService.put).toHaveBeenCalledWith('/projects/external/my-projects/project-1/valuation-worksheets/worksheet-1/claim', { clientRequestId: 'request-3' });
    expect(apiService.post).toHaveBeenNthCalledWith(1, '/projects/external/my-projects/project-1/valuation-worksheets/worksheet-1/submit', { clientRequestId: 'request-4' });
    expect(apiService.post).toHaveBeenNthCalledWith(2, '/projects/external/my-projects/project-1/valuation-worksheets/worksheet-1/endorse', { clientRequestId: 'request-5' });
  });
});
