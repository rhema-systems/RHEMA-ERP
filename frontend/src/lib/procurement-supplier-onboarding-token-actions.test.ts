import { describe, expect, it } from 'vitest';

import {
  getVerifiableSupplierOnboardingPayment,
  isSupplierOnboardingDeliveryRecovery,
} from './procurement-supplier-onboarding-token-actions';
import type { SupplierOnboardingPayment } from '@/types/procurement-supplier-onboarding-token';

const payment = (
  status: SupplierOnboardingPayment['status']
): SupplierOnboardingPayment => ({
  id: `${status.toLowerCase()}-payment`,
  paymentMethodId: 'method-1',
  paymentMethodCode: 'TEST',
  paymentMethodName: 'Test method',
  feeAmount: 100,
  taxAmount: 0,
  totalAmount: 100,
  currencyCode: 'GHS',
  status,
  paidAtUtc: '2026-09-03T00:00:00Z',
  rowVersion: 'row-version',
  integrityHash: 'integrity-hash',
});

describe('supplier onboarding payment actions', () => {
  it('exposes a reconciled payment only for server-approved delivery recovery', () => {
    const reconciled = payment('Reconciled');

    expect(getVerifiableSupplierOnboardingPayment([reconciled], false)).toBeUndefined();
    expect(getVerifiableSupplierOnboardingPayment([reconciled], true)).toBe(reconciled);
    expect(isSupplierOnboardingDeliveryRecovery(reconciled, true)).toBe(true);
  });

  it('keeps ordinary pending and posted verification available', () => {
    const pending = payment('Pending');
    const posted = payment('Posted');

    expect(getVerifiableSupplierOnboardingPayment([pending], false)).toBe(pending);
    expect(getVerifiableSupplierOnboardingPayment([posted], false)).toBe(posted);
  });
});
