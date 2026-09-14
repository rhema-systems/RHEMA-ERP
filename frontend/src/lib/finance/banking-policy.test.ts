import { describe, expect, it } from 'vitest';

import {
  calendarDayDifference,
  expectedChequeClearingDate,
  isWithinStatementDateTolerance,
} from './banking-policy';

describe('banking policy helpers', () => {
  it('compares transaction dates as calendar days', () => {
    expect(calendarDayDifference('2026-08-10T23:30:00Z', '2026-08-12T01:00:00Z')).toBe(2);
    expect(isWithinStatementDateTolerance('2026-08-10', '2026-08-12', 3)).toBe(true);
    expect(isWithinStatementDateTolerance('2026-08-10', '2026-08-16', 3)).toBe(false);
  });

  it('adds the configured calendar-day cheque clearing period', () => {
    const result = expectedChequeClearingDate('2026-08-31', 3);
    expect(result.getFullYear()).toBe(2026);
    expect(result.getMonth()).toBe(8);
    expect(result.getDate()).toBe(3);
  });
});
