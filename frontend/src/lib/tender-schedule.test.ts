import { describe, expect, it } from 'vitest';

import {
  getTenderScheduleError,
  TENDER_OPENING_SEQUENCE_ERROR,
} from './tender-schedule';

describe('tender schedule validation', () => {
  it('rejects an opening date before the submission deadline', () => {
    expect(
      getTenderScheduleError('2026-09-10T12:00', '2026-09-10T11:59')
    ).toBe(TENDER_OPENING_SEQUENCE_ERROR);
  });

  it('allows opening at or after the submission deadline', () => {
    expect(
      getTenderScheduleError('2026-09-10T12:00', '2026-09-10T12:00')
    ).toBeNull();
    expect(
      getTenderScheduleError('2026-09-10T12:00', '2026-09-10T12:01')
    ).toBeNull();
  });

  it('does not require an optional opening date', () => {
    expect(getTenderScheduleError('2026-09-10T12:00', '')).toBeNull();
  });
});
