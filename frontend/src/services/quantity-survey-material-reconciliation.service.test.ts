import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { quantitySurveyMaterialReconciliationService as service } from './quantity-survey-material-reconciliation.service';

vi.mock('@/services/api.service', () => ({
  apiService: { get: vi.fn(), put: vi.fn(), post: vi.fn() },
}));

describe('quantitySurveyMaterialReconciliationService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses separate tenant-safe internal and contractor workspaces', async () => {
    vi.mocked(apiService.get).mockResolvedValue({
      valuations: [], materialSources: [], inventoryIssues: [], evidence: [], reconciliations: [],
    });
    await service.workspace('project-1');
    await service.externalWorkspace('project-1');
    expect(apiService.get).toHaveBeenCalledWith(
      '/quantity-survey/material-reconciliations', { projectId: 'project-1' }
    );
    expect(apiService.get).toHaveBeenCalledWith(
      '/projects/external/my-projects/project-1/material-reconciliations'
    );
  });

  it('saves only controlled source identifiers and never accepts calculated totals', async () => {
    vi.mocked(apiService.put).mockResolvedValue({ id: 'material-1' });
    const request = {
      clientRequestId: 'request-1',
      valuationWorksheetId: 'valuation-1',
      reason: 'QS material reconciliation prepared from governed source records.',
      lines: [{
        lineType: 'tdcSuppliedMaterial' as const,
        inventoryItemId: 'item-1',
        quantity: 1,
        inventoryIssueVoucherLineId: 'issue-1',
        approvedRateId: 'rate-1',
      }],
    };
    await service.save('project/with space', request);
    expect(apiService.put).toHaveBeenCalledWith(
      '/quantity-survey/material-reconciliations?projectId=project%2Fwith%20space', request
    );
    expect(request).not.toHaveProperty('materialOnSiteAmount');
    expect(request).not.toHaveProperty('materialOffSiteAmount');
    expect(request).not.toHaveProperty('tdcSuppliedDeductionAmount');
  });

  it('keeps contractor confirmation on the external project route', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'material-1' });
    const request = {
      clientRequestId: 'request-2', rowVersion: 'AQ==',
      reason: 'Contractor review completed.',
      attestation: 'I confirm the controlled material schedule for this valuation.',
    };
    await service.confirm('project-1', 'material-1', request);
    expect(apiService.post).toHaveBeenCalledWith(
      '/projects/external/my-projects/project-1/material-reconciliations/material-1/confirm',
      request
    );
  });
});
