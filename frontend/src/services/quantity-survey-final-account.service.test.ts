import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { quantitySurveyFinalAccountService as service } from './quantity-survey-final-account.service';

vi.mock('@/services/api.service', () => ({
  apiService: { get: vi.fn(), post: vi.fn() },
}));

describe('quantitySurveyFinalAccountService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('loads the tenant-safe project workspace with a controlled project identifier', async () => {
    vi.mocked(apiService.get).mockResolvedValue({ contracts: [] });

    await service.workspace('project-1');

    expect(apiService.get).toHaveBeenCalledWith(
      '/quantity-survey/final-accounts',
      { projectId: 'project-1' }
    );
  });

  it('prepares from selected source identifiers without client-controlled totals or status', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'final-account-1' });
    const request = {
      clientRequestId: 'request-1',
      contractId: 'contract-1',
      settlementDate: '2026-08-10',
      rowVersion: 'AQ==',
      reason: 'Final reconciliation prepared from approved commercial sources.',
      notes: 'Reviewed by the QS team.',
    };

    await service.prepare('project/with space', request);

    expect(apiService.post).toHaveBeenCalledWith(
      '/quantity-survey/final-accounts?projectId=project%2Fwith%20space',
      request
    );
    expect(request).not.toHaveProperty('status');
    expect(request).not.toHaveProperty('finalAccountValue');
    expect(request).not.toHaveProperty('paidToDateAmount');
  });

  it('uses dedicated lifecycle and immutable-history routes', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'final-account-1' });
    vi.mocked(apiService.get).mockResolvedValue([]);
    const action = {
      clientRequestId: 'request-2',
      rowVersion: 'Ag==',
      reason: 'Independent final-account review completed.',
    };

    await service.submit('final-account-1', action);
    await service.approve('final-account-1', action);
    await service.reject('final-account-1', action);
    await service.close('final-account-1', action);
    await service.history('final-account-1');

    for (const operation of ['submit', 'approve', 'reject', 'close']) {
      expect(apiService.post).toHaveBeenCalledWith(
        `/quantity-survey/final-accounts/final-account-1/${operation}`,
        action
      );
    }
    expect(apiService.get).toHaveBeenCalledWith(
      '/quantity-survey/final-accounts/final-account-1/history'
    );
  });
});
