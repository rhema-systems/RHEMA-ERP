'use client';

/**
 * Round 5, lane H (decision A5): a request for more days than its leave type has left.
 *
 * Where the type allows it, the days beyond the limit can be charged to annual leave, and HR
 * decides at the final approval, which splits the request in two. The figures come from
 * `GET api/Leaves/excess-preview`, which counts the days by the type's own rules and runs annual
 * leave's checks, so this says before Submit what the server would say after it.
 *
 * Shared by the desk form and the portal form; `forSelf` changes only the wording.
 */

import { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Checkbox } from '@/components/ui/checkbox';
import { leaveService } from '@/services/hr/leave.service';

interface ExcessToAnnualOfferProps {
  employeeId: string;
  leaveTypeId: string;
  leaveTypeName?: string;
  leaveSubTypeId?: string;
  startDate: string;
  endDate: string;
  checked: boolean;
  onCheckedChange: (value: boolean) => void;
  /** The employee's own form says "my annual leave"; the desk says "the employee's". */
  forSelf: boolean;
}

const days = (n: number) => `${Math.round(n * 100) / 100} day${n === 1 ? '' : 's'}`;

export function ExcessToAnnualOffer({
  employeeId,
  leaveTypeId,
  leaveTypeName,
  leaveSubTypeId,
  startDate,
  endDate,
  checked,
  onCheckedChange,
  forSelf,
}: ExcessToAnnualOfferProps) {
  const ready = !!employeeId && !!leaveTypeId && !!startDate && !!endDate && endDate >= startDate;

  const { data: preview } = useQuery({
    queryKey: ['hr', 'leave-excess-preview', employeeId, leaveTypeId, leaveSubTypeId ?? '', startDate, endDate],
    queryFn: () =>
      leaveService.getExcessPreview({ employeeId, leaveTypeId, leaveSubTypeId, startDate, endDate }),
    enabled: ready,
    retry: false,
  });

  const over = !!preview && preview.excessDays > 0;
  const offered = over && preview.allowsOffsetAgainstAnnual && !preview.refusal;

  // The tick only ever means "these extra days": once the dates or the type fit, it goes.
  useEffect(() => {
    if (ready && preview && !offered && checked) onCheckedChange(false);
  }, [ready, preview, offered, checked, onCheckedChange]);

  if (!ready || !preview || !over) return null;

  const typeName = leaveTypeName || 'This leave';
  const kept = preview.requestedDays - preview.excessDays;
  const heading = (
    <p className="font-medium">
      This is {days(preview.requestedDays)} of {typeName}, and {days(Math.max(0, preview.availableDays))}{' '}
      {preview.availableDays === 1 ? 'is' : 'are'} left.
    </p>
  );

  if (!offered) {
    return (
      <div className="rounded-md border border-amber-300/60 bg-amber-50 p-3 text-sm dark:border-amber-900/60 dark:bg-amber-950/40">
        {heading}
        <p className="mt-1">
          {preview.refusal ??
            `Shorten the request to ${days(kept)}, or raise the extra days as annual leave.`}
        </p>
      </div>
    );
  }

  const annualName = preview.annualLeaveTypeName ?? 'annual leave';
  return (
    <div className="space-y-2 rounded-md border border-primary/30 bg-primary/5 p-3 text-sm">
      {heading}
      <label className="flex cursor-pointer items-start gap-2">
        <Checkbox
          checked={checked}
          onCheckedChange={(v) => onCheckedChange(v === true)}
          className="mt-0.5"
        />
        <span>
          Charge the extra {days(preview.annualDays ?? preview.excessDays)} to{' '}
          {forSelf ? 'my' : "the employee's"} {annualName} (HR decides)
        </span>
      </label>
      <p className="text-xs text-muted-foreground">
        If it is approved, the request is split in two: {days(kept)} of {typeName}, then{' '}
        {days(preview.annualDays ?? preview.excessDays)} of {annualName}
        {preview.annualAvailableDays != null
          ? `, which has ${days(preview.annualAvailableDays)} that can be taken now`
          : ''}
        . Without the tick it cannot be submitted.
      </p>
    </div>
  );
}
