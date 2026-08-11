import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { quantitySurveyPaymentCertificateService as service } from './quantity-survey-payment-certificate.service';

vi.mock('@/services/api.service', () => ({
  apiService: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    downloadBlob: vi.fn(),
  },
}));

describe('quantitySurveyPaymentCertificateService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the tenant-safe project query for controlled lookups and listing', async () => {
    vi.mocked(apiService.get).mockResolvedValue({ eligibleValuations: [] });
    await service.lookups('project-1');
    await service.list('project-1');
    expect(apiService.get).toHaveBeenNthCalledWith(
      1,
      '/quantity-survey/payment-certificates/lookups',
      { projectId: 'project-1' }
    );
    expect(apiService.get).toHaveBeenNthCalledWith(
      2,
      '/quantity-survey/payment-certificates',
      { projectId: 'project-1' }
    );
  });

  it('keeps generated certificate inputs separate from server-owned totals and policy IDs', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'certificate-1' });
    const request = {
      clientRequestId: 'request-1',
      valuationWorksheetId: 'worksheet-1',
      paymentDueDate: '2026-08-31',
      advanceRecoveryAmount: 10,
      materialDeductionAmount: 20,
      otherDeductionsAmount: 30,
      notes: 'Reviewed inputs',
    };
    await service.generate('project-1', request);
    expect(apiService.post).toHaveBeenCalledWith(
      '/quantity-survey/payment-certificates?projectId=project-1',
      request
    );
    expect(request).not.toHaveProperty('taxAmount');
    expect(request).not.toHaveProperty('expenseAccountId');
    expect(request).not.toHaveProperty('status');
  });

  it('routes approval, AP handoff, document and history through dedicated endpoints', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'certificate-1' });
    vi.mocked(apiService.get).mockResolvedValue([]);
    vi.mocked(apiService.downloadBlob).mockResolvedValue(new Blob());
    const action = {
      clientRequestId: 'request-2',
      rowVersion: 'AQ==',
      reason: 'Independent review completed',
    };
    await service.approve('certificate-1', action);
    await service.handoffToAp('certificate-1', action);
    await service.document('certificate-1');
    await service.history('certificate-1');
    expect(apiService.post).toHaveBeenCalledWith(
      '/quantity-survey/payment-certificates/certificate-1/approve',
      action
    );
    expect(apiService.post).toHaveBeenCalledWith(
      '/quantity-survey/payment-certificates/certificate-1/handoff-ap',
      action
    );
    expect(apiService.downloadBlob).toHaveBeenCalledWith(
      '/quantity-survey/payment-certificates/certificate-1/document'
    );
    expect(apiService.get).toHaveBeenCalledWith(
      '/quantity-survey/payment-certificates/certificate-1/history'
    );
  });
});
