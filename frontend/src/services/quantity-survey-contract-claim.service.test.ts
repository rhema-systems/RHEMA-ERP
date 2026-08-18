import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { quantitySurveyContractClaimService as service } from './quantity-survey-contract-claim.service';

vi.mock('@/services/api.service', () => ({ apiService: { get: vi.fn(), put: vi.fn(), post: vi.fn(), downloadBlob: vi.fn() } }));

describe('quantitySurveyContractClaimService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('loads the internal tenant and project scoped workspace', async () => {
    vi.mocked(apiService.get).mockResolvedValue({ contracts: [], variations: [], extensionsOfTime: [], approvedBoqVersions: [], claims: [] });
    await service.workspace('project/one');
    expect(apiService.get).toHaveBeenCalledWith('/quantity-survey/contract-claims', { projectId: 'project/one' });
  });

  it('uses the dedicated contractor portal route and controlled identifiers', async () => {
    vi.mocked(apiService.put).mockResolvedValue({ id: 'claim-1' });
    const request = { clientRequestId: 'request-1', contractId: 'contract-1', approvedBoqVersionId: 'boq-1',
      variationOrderId: 'variation-1', extensionOfTimeId: null, claimType: 'Variation', title: 'Drainage claim',
      basis: 'Controlled contractual basis and supporting particulars.', claimedAmount: 125, rowVersion: null };
    await service.saveExternal('project/one', request);
    expect(apiService.put).toHaveBeenCalledWith('/projects/external/my-projects/project/one/contract-claims', request);
    expect(request).not.toHaveProperty('approvedAmount');
    expect(request).not.toHaveProperty('rejectedAmount');
  });

  it('keeps QS vetting and independent approval on dedicated routes', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'claim-1' });
    const request = { clientRequestId: 'decision-1', rowVersion: 'AQ==', reason: 'Reviewed against the contract.', assessedAmount: 100 };
    await service.vet('claim-1', request);
    expect(apiService.post).toHaveBeenCalledWith('/quantity-survey/contract-claims/claim-1/vet', request);
    await service.approve('claim-1', request);
    expect(apiService.post).toHaveBeenLastCalledWith('/quantity-survey/contract-claims/claim-1/approve', request);
  });
});
