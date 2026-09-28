'use client';

/**
 * Round 5, lane H (decision A5): days beyond a leave type's limit, charged to annual leave.
 *
 * Three moments, one panel, on both request pages:
 * - undecided, asking for it: what approving it now would do, or why it could not;
 * - split at approval, on the first part: how many days went to annual leave, and where;
 * - on the annual part: the request it continues.
 *
 * While both parts are running, the return is reported and confirmed on the annual part, where
 * the absence ends; the first part says so rather than offering buttons that would go there.
 */

import Link from 'next/link';
import { Split } from 'lucide-react';
import type { LeaveRequest } from '@/types/hr/leave-request';

const running = (status?: string | null) => status === 'Approved' || status === 'InProgress';
const days = (n: number) => `${Math.round(n * 100) / 100} day${n === 1 ? '' : 's'}`;
const day = (d?: string | null) => (d ? d.slice(0, 10) : '—');

export function SplitAbsencePanel({
  request: r,
  hrefFor,
}: {
  request: LeaveRequest;
  /** Where a request opens on this screen: the desk and the portal have their own pages. */
  hrefFor: (id: string) => string;
}) {
  const box = 'rounded-md border border-primary/30 bg-primary/5 p-4 text-sm';
  const title = (text: string) => (
    <p className="flex items-center gap-2 font-medium">
      <Split className="h-4 w-4 shrink-0" /> {text}
    </p>
  );

  // The annual part: it continues another request.
  if (r.splitFromRequestId) {
    return (
      <div className={box}>
        {title('The rest of an absence beyond its limit')}
        <p className="mt-1">
          These are the days beyond the {r.splitFromLeaveTypeName ?? 'leave'} limit, charged to{' '}
          {r.leaveTypeName} when{' '}
          <Link className="font-medium underline" href={hrefFor(r.splitFromRequestId)}>
            {r.splitFromRequestNumber ?? 'the request'}
          </Link>{' '}
          was approved. The absence began on {day(r.splitFromStartDate)}.
        </p>
        {running(r.status) && running(r.splitFromStatus) && (
          <p className="mt-2 text-xs text-muted-foreground">
            The absence ends here, so the return is reported and confirmed on this request.
          </p>
        )}
      </div>
    );
  }

  // The first part, split at approval.
  if (r.chargedToAnnualRequestId) {
    const cancelled = r.chargedToAnnualStatus === 'Cancelled';
    return (
      <div className={box}>
        {title('Split at approval')}
        <p className="mt-1">
          {days(r.chargedToAnnualDays ?? 0)} of this absence went beyond the {r.leaveTypeName} limit
          and {cancelled ? 'were' : 'are'} charged to {r.chargedToAnnualLeaveTypeName ?? 'annual leave'}{' '}
          as{' '}
          <Link className="font-medium underline" href={hrefFor(r.chargedToAnnualRequestId)}>
            {r.chargedToAnnualRequestNumber ?? 'the annual part'}
          </Link>
          {cancelled ? ', since cancelled.' : `, to ${day(r.chargedToAnnualEndDate)}.`}
        </p>
        {running(r.status) && running(r.chargedToAnnualStatus) && (
          <p className="mt-2 text-xs text-muted-foreground">
            The absence ends on the annual part, so the return is reported and confirmed there.
            Cancelling this request cancels that one too.
          </p>
        )}
      </div>
    );
  }

  // Undecided, asking for it.
  if (!r.chargeExcessToAnnual) return null;
  if (!['Draft', 'Pending', 'ChangesSuggested'].includes(r.status)) return null;

  if (r.excessToAnnualRefusal) {
    return (
      <div className="rounded-md border border-amber-300/60 bg-amber-50 p-4 text-sm dark:border-amber-900/60 dark:bg-amber-950/40">
        {title('Asks for the days beyond the limit to go to annual leave')}
        <p className="mt-1">{r.excessToAnnualRefusal}</p>
      </div>
    );
  }

  return (
    <div className={box}>
      {title('Asks for the days beyond the limit to go to annual leave')}
      <p className="mt-1">
        {r.excessToAnnualDays
          ? `Approving it now would split it in two: ${days(r.totalDays - r.excessToAnnualDays)} of ${r.leaveTypeName}, then ${days(r.excessToAnnualDays)} of annual leave, approved together.`
          : `It fits within the ${r.leaveTypeName} limit now, so approving it would not split it.`}
      </p>
    </div>
  );
}
