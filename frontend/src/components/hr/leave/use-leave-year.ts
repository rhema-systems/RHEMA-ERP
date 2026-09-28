'use client';

import { useQuery } from '@tanstack/react-query';
import { leaveService } from '@/services/hr/leave.service';

/**
 * The tenant's current leave year (round 5, lane C4).
 *
 * The leave screens opened on `new Date().getFullYear()`, which is the leave year only while it
 * starts in January: from January until the start month, a tenant whose year starts in April is
 * still in the leave year named after the previous calendar year. The server knows the start month;
 * this asks it.
 *
 * Until the answer arrives it returns the calendar year — the right answer for every January tenant,
 * TDC included — so nothing waits on it. A page that lets the user pick a year should keep the
 * user's choice separately and fall back to `currentYear` (`chosen ?? currentYear`), so a late
 * answer still moves the default and never overrides a choice.
 */
export function useLeaveYear() {
  const { data } = useQuery({
    queryKey: ['hr', 'leave-year'],
    queryFn: () => leaveService.getLeaveYear(),
    // The start month is set once, at setup (the server refuses to change it once leave exists).
    staleTime: 60 * 60 * 1000,
  });

  const calendarYear = new Date().getFullYear();
  return {
    currentYear: data?.currentYear ?? calendarYear,
    startMonth: data?.startMonth ?? 1,
    /** First and last day of the current leave year, as `YYYY-MM-DD`. */
    startDate: data?.startDate?.slice(0, 10) ?? `${calendarYear}-01-01`,
    endDate: data?.endDate?.slice(0, 10) ?? `${calendarYear}-12-31`,
    loaded: !!data,
  };
}
