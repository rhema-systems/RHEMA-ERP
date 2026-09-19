import { describe, expect, it } from 'vitest';

import {
  calculateWithholdingBalanceReference,
  getReceiptAllocationDisposition,
  hasMaterialWithholdingVariance,
} from './ar-receipt-allocation';

describe('AR receipt allocation controls', () => {
  it('treats a receipt with no settlement components as a customer advance', () => {
    expect(getReceiptAllocationDisposition(1_000, 0, false)).toBe(
      'customer-advance'
    );
  });

  it('requires an allocated receipt to consume all receipt-currency cash', () => {
    expect(getReceiptAllocationDisposition(1_000, 750, true)).toBe(
      'partially-allocated'
    );
    expect(getReceiptAllocationDisposition(1_000, 1_000, true)).toBe(
      'fully-allocated'
    );
    expect(getReceiptAllocationDisposition(1_000, 1_001, true)).toBe(
      'over-allocated'
    );
  });

  it('calculates a visible configured-rate reference and identifies certificate variance', () => {
    const reference = calculateWithholdingBalanceReference(18_000, 5);
    expect(reference).toBe(900);
    expect(hasMaterialWithholdingVariance(900, reference)).toBe(false);
    expect(hasMaterialWithholdingVariance(850, reference)).toBe(true);
  });
});
