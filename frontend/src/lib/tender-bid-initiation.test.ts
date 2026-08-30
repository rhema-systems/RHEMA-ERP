import { describe, expect, it } from 'vitest';

import {
  isPositiveMandatoryFee,
  nextTenderBidInitiationStep,
} from './tender-bid-initiation';

describe('tender bid initiation routing', () => {
  it('completes a free tender without a declaration after assignment', () => {
    expect(
      nextTenderBidInitiationStep({
        hasAssignment: true,
        requiresAcceptanceDeclaration: false,
        declarationSatisfied: true,
        paymentRequired: false,
        paymentSatisfied: true,
      })
    ).toBe('complete');
  });

  it('keeps a required declaration on the declaration step', () => {
    expect(
      nextTenderBidInitiationStep({
        hasAssignment: true,
        requiresAcceptanceDeclaration: true,
        declarationSatisfied: false,
        paymentRequired: false,
        paymentSatisfied: true,
      })
    ).toBe(2);
  });

  it('keeps an unpaid positive mandatory fee on payment verification', () => {
    expect(
      nextTenderBidInitiationStep({
        hasAssignment: true,
        requiresAcceptanceDeclaration: false,
        declarationSatisfied: true,
        paymentRequired: true,
        paymentSatisfied: false,
      })
    ).toBe(3);
  });

  it('does not treat a zero or optional fee as a payment gate', () => {
    expect(isPositiveMandatoryFee({ isMandatory: true, amount: 0 })).toBe(
      false
    );
    expect(isPositiveMandatoryFee({ isMandatory: false, amount: 100 })).toBe(
      false
    );
    expect(isPositiveMandatoryFee({ isMandatory: true, amount: 100 })).toBe(
      true
    );
  });
});
