import { describe, expect, it } from 'vitest';
import { calculateBidValidity } from './procurement-bid-validity';

describe('bid validity from explicit terms', () => {
  it('retains UTC time and precision across a leap day', () => {
    expect(calculateBidValidity('2028-02-28T16:50:27.321Z', 2)).toBe(
      '2028-03-01T16:50:27.321Z'
    );
  });
  it('uses submission rather than the opening date and has no default', () => {
    expect(calculateBidValidity('2026-09-23T16:50:00Z', 30)).toBe(
      '2026-10-23T16:50:00.000Z'
    );
    expect(calculateBidValidity('2026-09-23T16:50:00Z')).toBeUndefined();
  });
  it.each([0, -1, 1.5, Number.MAX_VALUE, NaN])(
    'rejects invalid days %s',
    (days) => {
      expect(
        calculateBidValidity('2026-09-23T16:50:00Z', days)
      ).toBeUndefined();
    }
  );
  it('rejects missing or malformed closing dates', () => {
    expect(calculateBidValidity('', 30)).toBeUndefined();
    expect(calculateBidValidity('invalid', 30)).toBeUndefined();
  });
});
