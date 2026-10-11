import { describe, expect, it } from 'vitest';
import {
  buildCustomerAdvanceApplicationUrl,
  isEligibleCustomerAdvance,
} from './ar-customer-advance';

const eligible = {
  businessPartnerId: 'customer-1',
  isCreditNote: false,
  isCustomerAdvance: true,
  status: 'Posted' as const,
  unallocatedAmount: 75,
};

describe('AR customer advance application', () => {
  it('admits only a posted, unapplied advance belonging to the selected customer', () => {
    expect(isEligibleCustomerAdvance(eligible, 'customer-1')).toBe(true);
    expect(
      isEligibleCustomerAdvance(
        { ...eligible, businessPartnerId: 'customer-2' },
        'customer-1'
      )
    ).toBe(false);
    expect(
      isEligibleCustomerAdvance(
        { ...eligible, isCustomerAdvance: false },
        'customer-1'
      )
    ).toBe(false);
    expect(
      isEligibleCustomerAdvance(
        { ...eligible, isCreditNote: true },
        'customer-1'
      )
    ).toBe(false);
    expect(
      isEligibleCustomerAdvance(
        { ...eligible, unallocatedAmount: 0 },
        'customer-1'
      )
    ).toBe(false);
    expect(
      isEligibleCustomerAdvance(
        { ...eligible, status: 'Reversed' },
        'customer-1'
      )
    ).toBe(false);
  });

  it('preserves the selected invoice while opening the governed advance application path', () => {
    const url = buildCustomerAdvanceApplicationUrl(
      'customer-1',
      'advance-1',
      'invoice-1'
    );
    expect(url).toContain('businessPartnerId=customer-1');
    expect(url).toContain('paymentId=advance-1');
    expect(url).toContain('invoiceId=invoice-1');
    expect(url).toContain('mode=apply-account');
  });
});
