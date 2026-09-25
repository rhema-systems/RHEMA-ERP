'use client';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, UserCheck, X } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { leavePlanService } from '@/services/hr/leave.service';
import type { EmployeeReliever } from '@/types/hr/employee-reliever';
import type { LeavePlanRosterReliever, LeaveRelieverClash } from '@/types/hr/leave-request';

const fmt = (d: string) => d.slice(0, 10);

/** One clash, as a sentence. */
export function clashLine(c: LeaveRelieverClash) {
  return `${c.relieverName || 'Reliever'} ${c.description} (${fmt(c.fromDate)} – ${fmt(c.toDate)})`;
}

/** The roster endpoint's rows, in the shape a plan carries them. */
export function toRosterRelievers(rows: EmployeeReliever[] | undefined): LeavePlanRosterReliever[] {
  return (rows ?? [])
    .filter((r) => r.isActive)
    .sort((a, b) => a.priority - b.priority)
    .map((r) => ({
      employeeId: r.relieverEmployeeId,
      name: r.relieverName,
      positionName: r.relieverPositionName ?? null,
      priority: r.priority,
    }));
}

/**
 * Whether a chosen reliever is actually free over the plan's dates, asked as the form is filled in.
 *
 * Finish-plan lane 4: TDC's demo feedback was that reliever clashes were not visible on the plan.
 * The server answers from three sources — the reliever's own plans, their own live leave requests,
 * and other plans already naming them — and this is advisory: a plan with a clash can still be
 * saved, because leave gets cancelled and dates move.
 *
 * ⚠ Round 5 lane E1: this used to treat a failed check as "nothing in their diary". The request was
 * malformed, so it ALWAYS failed, and every reliever always read as free. A check that cannot answer
 * now says so.
 */
export function RelieverClashCheck({
  label,
  relieverId,
  startDate,
  endDate,
  excludePlanId,
}: {
  label: string;
  relieverId: string;
  startDate: string;
  endDate: string;
  excludePlanId?: string;
}) {
  const ready = !!relieverId && !!startDate && !!endDate && endDate >= startDate;
  const { data, isFetching, isError, error } = useQuery({
    queryKey: ['hr', 'leave-plans', 'reliever-clashes', relieverId, startDate, endDate, excludePlanId ?? ''],
    queryFn: () => leavePlanService.getRelieverClashes(relieverId, startDate, endDate, excludePlanId),
    enabled: ready,
  });

  if (!ready) return null;
  if (isFetching && !data) {
    return <p className="text-xs text-muted-foreground">Checking the {label.toLowerCase()}&apos;s diary…</p>;
  }
  if (isError) {
    return (
      <p className="text-xs text-amber-700 dark:text-amber-400">
        {label}: couldn&apos;t check their diary — {(error as any)?.message || 'the check failed'}. Don&apos;t
        rely on them being free until it has been checked.
      </p>
    );
  }
  const clashes = data ?? [];
  if (clashes.length === 0) {
    return (
      <p className="text-xs text-emerald-700 dark:text-emerald-400">
        {label}: nothing in their diary over these dates.
      </p>
    );
  }
  return (
    <Alert>
      <AlertTriangle className="h-4 w-4" />
      <AlertTitle>
        {label}: {clashes.length} clash{clashes.length === 1 ? '' : 'es'} over these dates
      </AlertTitle>
      <AlertDescription>
        <ul className="mt-1 list-disc space-y-1 pl-4 text-sm">
          {clashes.map((c, i) => (
            <li key={`${c.source}-${i}`}>{clashLine(c)}</li>
          ))}
        </ul>
        <p className="mt-2 text-xs text-muted-foreground">
          You can still save the plan — this is a warning, not a rule.
        </p>
      </AlertDescription>
    </Alert>
  );
}

/**
 * Pick a reliever: the employee's own reliever list first, a search as the fallback.
 *
 * Round 5 lane E2 — the stakeholders' point was that relievers are specified on the employee's
 * profile and the plan should read them, not ask for a name from scratch. The roster is offered as
 * one-click choices; when it is empty (or the right person is not on it) the search stays available,
 * which answers their "what if the employee has no relievers on their profile?".
 *
 * `allowSearch={false}` is the self-service portal: an employee chooses from their own list only, the
 * same rule as the portal request form; with an empty list, the approver or HR names someone.
 */
export function RelieverChooser({
  label,
  value,
  valueLabel,
  onChange,
  roster,
  rosterLoading,
  excludeIds = [],
  startDate,
  endDate,
  excludePlanId,
  allowSearch = true,
  disabled,
}: {
  label: string;
  value: string | null;
  valueLabel: string | null;
  onChange: (id: string | null, label: string | null) => void;
  roster: LeavePlanRosterReliever[] | undefined;
  rosterLoading?: boolean;
  /** Hide these people from the roster choices — the other slot's reliever, typically. */
  excludeIds?: (string | null | undefined)[];
  startDate: string;
  endDate: string;
  excludePlanId?: string;
  allowSearch?: boolean;
  disabled?: boolean;
}) {
  const offered = (roster ?? []).filter((r) => !excludeIds.includes(r.employeeId));
  const rosterEmpty = !rosterLoading && roster !== undefined && roster.length === 0;

  return (
    <div className="space-y-2">
      <Label>{label}</Label>

      {!disabled && offered.length > 0 && (
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="text-xs text-muted-foreground">From their reliever list:</span>
          {offered.map((r) => (
            <Button
              key={r.employeeId}
              type="button"
              size="sm"
              variant={value === r.employeeId ? 'default' : 'outline'}
              className="h-7 px-2 text-xs"
              onClick={() => onChange(r.employeeId, r.name)}
            >
              <UserCheck className="mr-1 h-3 w-3" />
              {r.name}
              {r.positionName ? ` · ${r.positionName}` : ''}
            </Button>
          ))}
        </div>
      )}

      {!disabled && rosterEmpty && (
        <p className="text-xs text-muted-foreground">
          {allowSearch
            ? 'No relievers are listed on their profile — search for someone.'
            : 'No relievers are listed on your profile. Your manager or HR can name one when they review the plan.'}
        </p>
      )}

      {allowSearch ? (
        <EmployeePicker
          value={value}
          initialLabel={valueLabel}
          onChange={(id, l) => onChange(id, l)}
          placeholder={offered.length > 0 ? 'Or search for someone else…' : 'Search employees…'}
          disabled={disabled}
        />
      ) : value ? (
        <div className="flex items-center justify-between rounded-md border px-3 py-2 text-sm">
          <span>{valueLabel ?? 'Selected'}</span>
          {!disabled && (
            <Button type="button" variant="ghost" size="icon" className="h-6 w-6" onClick={() => onChange(null, null)}>
              <X className="h-4 w-4" />
              <span className="sr-only">Clear</span>
            </Button>
          )}
        </div>
      ) : null}

      <RelieverClashCheck
        label={label}
        relieverId={value ?? ''}
        startDate={startDate}
        endDate={endDate}
        excludePlanId={excludePlanId}
      />
    </div>
  );
}
