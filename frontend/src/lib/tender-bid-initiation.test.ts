import { describe, expect, it } from 'vitest';

import {
  isPositiveMandatoryFee,
  nextTenderBidInitiationStep,
} from './tender-bid-initiation';

describe('tender bid initiation routing', () => {
  it('starts a new supplier at user-access assignment', () => {
    expect(
      nextTenderBidInitiationStep({
        hasAssignment: false,
        requiresAcceptanceDeclaration: false,
        declarationSatisfied: false,
        paymentRequired: true,
        paymentSatisfied: false,
      })
    ).toBe(1);
  });

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
        paymentRequired: true,
        paymentSatisfied: false,
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

  it.each([
    ['manual evidence pending verification', true],
    ['payment provider confirmation pending', false],
    ['payment rejected', false],
  ])('keeps %s on the payment step', (_scenario, paymentEvidenceAccepted) => {
    const status = {
      hasAssignment: true,
      requiresAcceptanceDeclaration: false,
      declarationSatisfied: true,
      paymentRequired: true,
      paymentSatisfied: false,
      paymentEvidenceAccepted,
    };

    expect(nextTenderBidInitiationStep(status)).toBe(3);
  });

  it('completes initiation after the required payment is verified', () => {
    expect(
      nextTenderBidInitiationStep({
        hasAssignment: true,
        requiresAcceptanceDeclaration: false,
        declarationSatisfied: true,
        paymentRequired: true,
        paymentSatisfied: true,
      })
    ).toBe('complete');
  });

  it('completes initiation when no tender fee is required', () => {
    expect(
      nextTenderBidInitiationStep({
        hasAssignment: true,
        requiresAcceptanceDeclaration: false,
        declarationSatisfied: true,
        paymentRequired: false,
        paymentSatisfied: false,
      })
    ).toBe('complete');
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
