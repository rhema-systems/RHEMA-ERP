import { describe, expect, it } from 'vitest';

import { hasSaleableNumber } from './saleableItemDisplay';

describe('saleable item numeric display', () => {
  it('treats a Project Unit null stock quantity as unavailable instead of a number', () => {
    expect(hasSaleableNumber(null)).toBe(false);
    expect(hasSaleableNumber(undefined)).toBe(false);
  });

  it('retains valid quantities, including zero', () => {
    expect(hasSaleableNumber(0)).toBe(true);
    expect(hasSaleableNumber(12.5)).toBe(true);
    expect(hasSaleableNumber(Number.NaN)).toBe(false);
  });
});
