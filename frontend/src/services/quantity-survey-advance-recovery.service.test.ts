import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { quantitySurveyAdvanceRecoveryService as service } from './quantity-survey-advance-recovery.service';

vi.mock('@/services/api.service', () => ({
  apiService: { get: vi.fn(), post: vi.fn() },
}));

describe('quantitySurveyAdvanceRecoveryService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('loads only the tenant-safe project workspace', async () => {
    vi.mocked(apiService.get).mockResolvedValue({
      eligibleAdvances: [],
      agreements: [],
    });
    await service.workspace('project-1');
    expect(apiService.get).toHaveBeenCalledWith(
      '/quantity-survey/advance-recoveries',
      {
        projectId: 'project-1',
      }
    );
  });

  it('submits controlled Finance payment and Works contract identifiers without typed snapshots', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'agreement-1' });
    const request = {
      clientRequestId: 'request-1',
      contractId: 'contract-1',
      vendorPaymentId: 'payment-1',
      recoveryPercentage: 10,
      reason: 'Approved advance terms captured from the signed Works contract.',
    };
    await service.create('project/with space', request);
    expect(apiService.post).toHaveBeenCalledWith(
      '/quantity-survey/advance-recoveries?projectId=project%2Fwith%20space',
      request
    );
    expect(request).not.toHaveProperty('originalAdvanceAmount');
    expect(request).not.toHaveProperty('contractorName');
    expect(request).not.toHaveProperty('status');
  });

  it('uses dedicated lifecycle and immutable-history endpoints', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'agreement-1' });
    vi.mocked(apiService.get).mockResolvedValue([]);
    const action = {
      clientRequestId: 'request-2',
      rowVersion: 'AQ==',
      reason: 'Independent commercial review completed with source evidence.',
    };
    await service.submit('agreement-1', action);
    await service.approve('agreement-1', action);
    await service.reject('agreement-1', action);
    await service.history('agreement-1');
    expect(apiService.post).toHaveBeenCalledWith(
      '/quantity-survey/advance-recoveries/agreement-1/submit',
      action
    );
    expect(apiService.post).toHaveBeenCalledWith(
      '/quantity-survey/advance-recoveries/agreement-1/approve',
      action
    );
    expect(apiService.post).toHaveBeenCalledWith(
      '/quantity-survey/advance-recoveries/agreement-1/reject',
      action
    );
    expect(apiService.get).toHaveBeenCalledWith(
      '/quantity-survey/advance-recoveries/agreement-1/history'
    );
  });
});
