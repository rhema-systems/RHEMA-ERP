const MILLISECONDS_PER_DAY = 24 * 60 * 60 * 1000;

function dateOnlyUtc(value: string | Date): number {
  const date = typeof value === 'string' ? new Date(value) : value;
  return Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());
}

export function calendarDayDifference(left: string | Date, right: string | Date): number {
  return Math.abs(Math.round((dateOnlyUtc(left) - dateOnlyUtc(right)) / MILLISECONDS_PER_DAY));
}

export function isWithinStatementDateTolerance(
  bookDate: string | Date,
  statementDate: string | Date,
  toleranceDays: number,
): boolean {
  return calendarDayDifference(bookDate, statementDate) <= Math.max(0, toleranceDays);
}

export function expectedChequeClearingDate(depositDate: string, clearingPeriodDays: number): Date {
  const [year, month, day] = depositDate.split('-').map(Number);
  const result = new Date(year, month - 1, day);
  result.setDate(result.getDate() + Math.max(0, clearingPeriodDays));
  return result;
}
