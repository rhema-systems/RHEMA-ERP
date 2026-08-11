import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { quantitySurveySubcontractService as service } from './quantity-survey-subcontract.service';

vi.mock('@/services/api.service', () => ({
  apiService: {
    get: vi.fn(),
    put: vi.fn(),
    post: vi.fn(),
    downloadBlob: vi.fn(),
  },
}));

describe('quantitySurveySubcontractService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the internal project-scoped workspace and controlled save routes', async () => {
    vi.mocked(apiService.get).mockResolvedValue({});
    vi.mocked(apiService.put).mockResolvedValue({});
    await service.workspace('project-1');
    await service.save('project-1', { clientRequestId: 'request-1' });
    expect(apiService.get).toHaveBeenCalledWith(
      '/quantity-survey/subcontracts',
      { projectId: 'project-1' }
    );
    expect(apiService.put).toHaveBeenCalledWith(
      '/quantity-survey/subcontracts?projectId=project-1',
      { clientRequestId: 'request-1' }
    );
  });

  it('keeps external valuation and evidence routes under the assigned project and subcontract', async () => {
    vi.mocked(apiService.put).mockResolvedValue({});
    vi.mocked(apiService.post).mockResolvedValue({});
    await service.saveValuation(
      'project-1',
      'sub-1',
      { clientRequestId: 'request-1' },
      true
    );
    const file = new File(['proof'], 'proof.pdf', { type: 'application/pdf' });
    await service.uploadEvidence(
      'project-1',
      'sub-1',
      'val-1',
      'request-2',
      'Progress evidence',
      file,
      true
    );
    expect(apiService.put).toHaveBeenCalledWith(
      '/projects/external/my-projects/project-1/subcontracts/sub-1/valuations',
      { clientRequestId: 'request-1' }
    );
    expect(apiService.post).toHaveBeenCalledWith(
      '/projects/external/my-projects/project-1/subcontracts/sub-1/valuations/val-1/evidence',
      expect.any(FormData)
    );
  });

  it('uses Finance handoff and payment refresh endpoints without a local payment API', async () => {
    vi.mocked(apiService.post).mockResolvedValue({});
    const request = {
      clientRequestId: 'request-3',
      rowVersion: 'AA==',
      reason: 'Finance handoff.',
    };
    await service.handoff('val-1', request);
    await service.refreshPayment('val-1');
    expect(apiService.post).toHaveBeenNthCalledWith(
      1,
      '/quantity-survey/subcontracts/valuations/val-1/handoff-ap',
      request
    );
    expect(apiService.post).toHaveBeenNthCalledWith(
      2,
      '/quantity-survey/subcontracts/valuations/val-1/refresh-payment',
      {}
    );
  });
});
