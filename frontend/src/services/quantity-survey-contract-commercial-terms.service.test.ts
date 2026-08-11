import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { quantitySurveyContractCommercialTermsService as service } from './quantity-survey-contract-commercial-terms.service';

vi.mock('@/services/api.service', () => ({
  apiService: { get: vi.fn(), put: vi.fn() },
}));

describe('quantitySurveyContractCommercialTermsService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('loads the dedicated canonical-contract workspace', async () => {
    vi.mocked(apiService.get).mockResolvedValue({ readinessBlockers: [] });

    await service.workspace('contract-1');

    expect(apiService.get).toHaveBeenCalledWith(
      '/quantity-survey/contract-commercial-terms/contract-1'
    );
  });

  it('sends controlled identifiers and idempotency lineage without status or policy inputs', async () => {
    vi.mocked(apiService.put).mockResolvedValue({ readinessBlockers: [] });
    const request = {
      clientRequestId: 'request-1',
      rowVersion: 'AQ==',
      paymentTermId: 'payment-term-1',
      provisionalSumAmount: 100,
      contingencyAmount: 50,
      retentionPercentage: 5,
      defectsLiabilityDays: 180,
      retentionClause: 'Five percent retention.',
      allowSectionalTakeover: true,
      sectionalTakeoverClause: 'Certified sectional completion.',
      allowSubcontracting: true,
      subcontractPaymentTermId: 'payment-term-2',
      subcontractTerms: 'Controlled subcontract payment terms.',
      claimNoticePeriodDays: 28,
      claimClause: 'Written notice and evidence are required.',
      commercialTermsContractDocumentId: 'contract-document-1',
    };

    await service.configure('contract-1', request);

    expect(apiService.put).toHaveBeenCalledWith(
      '/quantity-survey/contract-commercial-terms/contract-1',
      request
    );
    expect(request).not.toHaveProperty('status');
    expect(request).not.toHaveProperty('configurationProfileId');
    expect(request).not.toHaveProperty('policyHash');
  });
});
