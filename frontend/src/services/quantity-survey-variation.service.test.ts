import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { quantitySurveyVariationService as service } from './quantity-survey-variation.service';

vi.mock('@/services/api.service', () => ({
  apiService: { get: vi.fn(), put: vi.fn(), post: vi.fn() },
}));

describe('quantitySurveyVariationService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('loads the tenant-safe project variation workspace', async () => {
    vi.mocked(apiService.get).mockResolvedValue({ contracts: [], siteInstructions: [], changeRequests: [], boqLines: [], variations: [] });
    await service.workspace('project/one');
    expect(apiService.get).toHaveBeenCalledWith('/quantity-survey/variations', { projectId: 'project/one' });
  });

  it('saves controlled identifiers and server-calculated line inputs', async () => {
    vi.mocked(apiService.put).mockResolvedValue({ id: 'variation-1' });
    const request = {
      id: 'variation-1', clientRequestId: 'request-1', contractId: 'contract-1', approvedBoqVersionId: 'boq-1',
      sourceType: 'siteInstruction' as const, siteInstructionId: 'instruction-1', title: 'Drainage scope change',
      reason: 'Approved drainage alignment requires controlled remeasurement.', variationType: 'QuantityAdjustment',
      scheduleImpactDays: 2, rowVersion: 'AQ==',
      lines: [{ projectBoqVersionLineId: 'line-1', quantityChange: 5, unitRate: 12, valuationReason: 'Measured additional work.' }],
    };
    await service.save('project/one', request);
    expect(apiService.put).toHaveBeenCalledWith('/quantity-survey/variations?projectId=project%2Fone', request);
    expect(request).not.toHaveProperty('valuedAmount');
    expect(request).not.toHaveProperty('revisedContractSum');
  });

  it('keeps approval on the dedicated governed route', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'variation-1' });
    const request = { clientRequestId: 'decision-1', rowVersion: 'AQ==', reason: 'Technical and budget reviews completed.' };
    await service.approve('variation-1', request);
    expect(apiService.post).toHaveBeenCalledWith('/quantity-survey/variations/variation-1/approve', request);
  });

  it('retries downstream application through the protected variation route', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'variation-1', downstreamApplicationStatus: 'AppliedPendingBoqApproval' });
    const request = { clientRequestId: 'application-1', rowVersion: 'AQ==', reason: 'Retry the approved commercial application.' };
    await service.apply('variation-1', request);
    expect(apiService.post).toHaveBeenCalledWith('/quantity-survey/variations/variation-1/apply', request);
  });
});
