import { describe, expect, it } from 'vitest';
import { buildRecurringJournalSchedule } from './recurring-journal-schedule';

describe('recurring journal schedule serialization', () => {
  it('serializes weekly choices with interval and weekday', () => {
    const result = buildRecurringJournalSchedule({ choice: 'weekly', interval: 2, weekday: 5, dayOfMonth: 1 });
    expect(result).toMatchObject({ frequency: 'Weekly', interval: 2, convention: 'NoAdjustment' });
    expect(JSON.parse(result.recurrenceRuleJson)).toEqual({ weekdays: [5] });
  });

  it('retains safe month-end and semi-monthly business rules', () => {
    expect(buildRecurringJournalSchedule({ choice: 'month-end', interval: 1, weekday: 1, dayOfMonth: 1 }))
      .toMatchObject({ frequency: 'Monthly', convention: 'PreviousBusinessDay' });
    expect(JSON.parse(buildRecurringJournalSchedule({ choice: 'semi-monthly', interval: 1, weekday: 1, dayOfMonth: 1 }).recurrenceRuleJson))
      .toEqual({ daysOfMonth: [15, 31] });
  });

  it('bounds monthly days and intervals before sending them to the server', () => {
    const result = buildRecurringJournalSchedule({ choice: 'monthly', interval: 0, weekday: 1, dayOfMonth: 40 });
    expect(result.interval).toBe(1);
    expect(JSON.parse(result.recurrenceRuleJson)).toEqual({ daysOfMonth: [31] });
  });
});
