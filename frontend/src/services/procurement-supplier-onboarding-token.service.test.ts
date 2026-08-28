import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { procurementSupplierOnboardingTokenService as service } from './procurement-supplier-onboarding-token.service';

describe('supplier-onboarding token API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the dedicated tenant-safe register and applicant-owned routes', async () => {
    await service.summary();
    await service.issueOptions();
    await service.search({ status: 'Active', page: 2 });
    await service.get('token-1');
    await service.getForRegistration('registration-1');
    await service.paymentMethods('token-1');

    expect(api.get.mock.calls.map(([path]) => path)).toEqual([
      '/procurement/supplier-onboarding-tokens/summary',
      '/procurement/supplier-onboarding-tokens/issue-options',
      '/procurement/supplier-onboarding-tokens',
      '/procurement/supplier-onboarding-tokens/token-1',
      '/procurement/supplier-onboarding-tokens/registrations/registration-1',
      '/procurement/supplier-onboarding-tokens/token-1/payment-methods',
    ]);
  });

  it('maps issue, rotation, Finance payment, reconciliation, and shared-workflow exemption actions', async () => {
    const evidence = [
      {
        referenceKind: 'ExternalReference' as const,
        reference: 'EXEMPTION-MINUTE-001',
      },
    ];

    await service.issue('registration-1');
    await service.reissue('token-1', 'Applicant reported compromise.', 'AAAA');
    await service.recordPayment('token-1', {
      paymentMethodId: 'method-1',
      paymentReference: 'MOMO-001',
      rowVersion: 'AAAA',
    });
    await service.reconcile(
      'token-1',
      'payment-1',
      'BANK-001',
      undefined,
      'BBBB'
    );
    await service.requestExemption('token-1', {
      reason: 'Approved public-interest exemption.',
      evidence,
      rowVersion: 'AAAA',
    });
    await service.decideExemption('token-1', 'exemption-1', {
      approve: true,
      comment: 'Workflow approved with complete evidence.',
      evidence,
      rowVersion: 'CCCC',
    });

    expect(api.post.mock.calls.map(([path]) => path)).toEqual([
      '/procurement/supplier-onboarding-tokens',
      '/procurement/supplier-onboarding-tokens/token-1/reissue',
      '/procurement/supplier-onboarding-tokens/token-1/payments',
      '/procurement/supplier-onboarding-tokens/token-1/payments/payment-1/reconcile',
      '/procurement/supplier-onboarding-tokens/token-1/exemptions',
      '/procurement/supplier-onboarding-tokens/token-1/exemptions/exemption-1/decision',
    ]);
    expect(api.post.mock.calls[3][1]).toEqual({
      reconciliationReference: 'BANK-001',
      rowVersion: 'BBBB',
    });
  });
});
