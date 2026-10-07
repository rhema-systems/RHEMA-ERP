import type { BusinessDayConvention, RecurrenceFrequency } from '@/services/finance/recurring-journal-data.service';

export type ScheduleChoice = 'daily' | 'weekly' | 'semi-monthly' | 'monthly' | 'month-end' | 'quarterly' | 'annually';

export interface ScheduleEditorValue {
  choice: ScheduleChoice;
  interval: number;
  weekday: number;
  dayOfMonth: number;
}

export interface ScheduleDefinition {
  frequency: RecurrenceFrequency;
  interval: number;
  recurrenceRuleJson: string;
  convention: BusinessDayConvention;
  label: string;
}

const bounded = (value: number, minimum: number, maximum: number) =>
  Math.min(maximum, Math.max(minimum, Number.isFinite(value) ? Math.trunc(value) : minimum));

export const buildRecurringJournalSchedule = (value: ScheduleEditorValue): ScheduleDefinition => {
  const interval = bounded(value.interval, 1, 366);
  const day = bounded(value.dayOfMonth, 1, 31);
  const weekday = bounded(value.weekday, 0, 6);
  switch (value.choice) {
    case 'daily':
      return { frequency: 'Daily', interval, recurrenceRuleJson: '{}', convention: 'NoAdjustment', label: `Every ${interval === 1 ? 'day' : `${interval} days`}.` };
    case 'weekly':
      return { frequency: 'Weekly', interval, recurrenceRuleJson: JSON.stringify({ weekdays: [weekday] }), convention: 'NoAdjustment', label: `Every ${interval === 1 ? 'week' : `${interval} weeks`} on the selected weekday.` };
    case 'semi-monthly':
      return { frequency: 'SemiMonthly', interval, recurrenceRuleJson: JSON.stringify({ daysOfMonth: [15, 31] }), convention: 'PreviousBusinessDay', label: 'The 15th and month-end, moved backward to a business day.' };
    case 'monthly':
      return { frequency: 'Monthly', interval, recurrenceRuleJson: JSON.stringify({ daysOfMonth: [day] }), convention: 'PreviousBusinessDay', label: `Day ${day} every ${interval === 1 ? 'month' : `${interval} months`}, moved backward when required.` };
    case 'quarterly':
      return { frequency: 'Quarterly', interval, recurrenceRuleJson: JSON.stringify({ daysOfMonth: [day] }), convention: 'PreviousBusinessDay', label: `Day ${day} every ${interval === 1 ? 'quarter' : `${interval} quarters`}, anchored to the effective month.` };
    case 'annually':
      return { frequency: 'Annually', interval, recurrenceRuleJson: JSON.stringify({ daysOfMonth: [day] }), convention: 'PreviousBusinessDay', label: `Day ${day} every ${interval === 1 ? 'year' : `${interval} years`}, in the effective-from month.` };
    default:
      return { frequency: 'Monthly', interval, recurrenceRuleJson: JSON.stringify({ lastCalendarDay: true }), convention: 'PreviousBusinessDay', label: 'Last business day of each month.' };
  }
};

export const recurringJournalWeekdays = [
  'Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday',
] as const;
