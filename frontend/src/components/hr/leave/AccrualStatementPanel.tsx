'use client';

import { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { AlertTriangle, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { leaveService } from '@/services/hr/leave.service';
import type { LeaveAccrualStatement } from '@/types/hr/leave-request';

/** `2026-09-25` → `25 Sep 2026`, read as a calendar day rather than a UTC midnight. */
export function fmtDay(value?: string | null) {
  if (!value) return '—';
  const [y, m, d] = value.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
}

const days = (n: number) => `${Math.round(n * 100) / 100} day${n === 1 ? '' : 's'}`;

const FREQUENCY_WORD: Record<string, string> = {
  Monthly: 'monthly',
  PerPayPeriod: 'monthly',
  Quarterly: 'quarterly',
  SemiAnnual: 'half-yearly',
  Annual: 'once a year',
};

const PERIOD_WORD: Record<string, string> = {
  Monthly: 'a month',
  PerPayPeriod: 'a month',
  Quarterly: 'a quarter',
  SemiAnnual: 'a half-year',
  Annual: 'a year',
};

/** Where the year's entitlement came from, in a sentence. */
function entitlementSource(s: LeaveAccrualStatement) {
  const from =
    s.entitlementSource === 'StaffLevelAllocation'
      ? `the ${s.staffLevelName ?? 'staff level'} allocation${
          s.allocationEffectiveFrom ? ` (in force from ${fmtDay(s.allocationEffectiveFrom)})` : ''
        }`
      : "the leave type's own days a year";
  const parts = [`${days(s.entitlementBaseDays)} a year — ${from}`];
  if (s.ceilingDays != null)
    parts.push(`limited to ${days(s.ceilingDays)}, the highest allocation annual leave allows`);
  if (s.firstYearMonthsPresent != null)
    parts.push(
      `scaled to ${s.firstYearMonthsPresent} of 12 months for the year of joining: ${days(s.annualEntitledDays)}`,
    );
  return parts.join('; ');
}

/** The one-line headline: what has built up, as at when, and why that date if it is not the one asked. */
function Headline({ s }: { s: LeaveAccrualStatement }) {
  const stoppedAt =
    s.asOfLimit === 'YearEnd'
      ? ` — the ${s.year} leave year ended on ${fmtDay(s.yearEnd)}`
      : s.asOfLimit === 'LastDayOfService'
        ? ` — their last day of service; nothing builds up after it`
        : '';

  if (s.state === 'NoPolicy') {
    return (
      <p className="text-sm">
        This leave does not build up: the whole <strong>{days(s.annualEntitledDays)}</strong> is there
        from the start of the leave year, {fmtDay(s.yearStart)}.
      </p>
    );
  }

  return (
    <p className="text-sm">
      Built up as at <strong>{fmtDay(s.asOf)}</strong>
      {stoppedAt}: <strong className="text-base">{days(s.accruedDays)}</strong> of{' '}
      {days(s.annualEntitledDays)}
      {s.capReached && s.state === 'Accruing' ? ' — the whole year’s entitlement.' : '.'}
    </p>
  );
}

/**
 * How a balance's accrual is worked out as at a date — round 5, lane C2.
 *
 * The stakeholders asked whether HR "might run a kind of utility that accrues the leave days for
 * all". Nothing needs running: the system works out what has built up whenever it is asked, so the
 * figure is always current. What it never did was show its working. This does, for any date — the
 * rule, the entitlement and where it came from, the rate, one line per completed period with a
 * running total — and every figure comes from the same arithmetic the request check enforces.
 */
export function AccrualStatementPanel({
  balanceId,
  audience,
}: {
  balanceId: string;
  /** `desk` adds the notes only HR can act on (the stored entitlement disagreeing with the rules). */
  audience: 'desk' | 'self';
}) {
  // '' = today, which the server fills in; anything else is a date the user chose.
  const [asOf, setAsOf] = useState('');

  const { data: s, isLoading, isError, error, isFetching } = useQuery({
    queryKey: ['leave', 'accrual-statement', balanceId, asOf],
    queryFn: () => leaveService.getAccrualStatement(balanceId, asOf || undefined),
    // The last statement stays up while another date is worked out, so the panel does not blank.
    placeholderData: keepPreviousData,
  });

  const self = audience === 'self';
  // The balance's own leave year, from the server — not assumed to end in December.
  const yearEnd = s?.yearEnd?.slice(0, 10);

  return (
    <div className="space-y-3 rounded-md border p-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h3 className="text-sm font-semibold">How {self ? 'your' : 'this'} leave builds up</h3>
          <p className="text-xs text-muted-foreground">
            Worked out whenever it is asked for — pick any date to see what will have built up by
            then.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Input
            type="date"
            aria-label="Worked out as at"
            value={asOf}
            onChange={(e) => setAsOf(e.target.value)}
            className="h-8 w-[150px]"
          />
          <Button size="sm" variant="ghost" onClick={() => setAsOf('')} disabled={!asOf}>
            Today
          </Button>
          {yearEnd && (
            <Button size="sm" variant="ghost" onClick={() => setAsOf(yearEnd)} disabled={asOf === yearEnd}>
              Year end
            </Button>
          )}
          {isFetching && !isLoading && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
        </div>
      </div>

      {isLoading ? (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" /> Working it out…
        </div>
      ) : isError || !s ? (
        <p className="text-sm text-destructive">
          {(error as Error | undefined)?.message || 'The statement could not be worked out.'}
        </p>
      ) : (
        <>
          <Headline s={s} />

          <ul className="list-disc space-y-1 pl-5 text-sm text-muted-foreground">
            <li>
              <span className="text-foreground">Entitlement:</span> {entitlementSource(s)}.
            </li>
            {s.hasPolicy && s.mode === 'FullGrantOnEligibility' && (
              <li>
                <span className="text-foreground">Granted in full</span> on becoming eligible
                {s.minServiceMonths ? `, after ${s.minServiceMonths} months' service` : ''}.
              </li>
            )}
            {s.hasPolicy && s.mode !== 'FullGrantOnEligibility' && s.frequency && (
              <li>
                <span className="text-foreground">Builds up {FREQUENCY_WORD[s.frequency]}:</span>{' '}
                {days(s.ratePerPeriod)} {PERIOD_WORD[s.frequency]}
                {s.rateIsDerived
                  ? ` — ${days(s.annualEntitledDays)} ÷ ${s.periodsPerYear}`
                  : ' — the fixed rate the leave type sets'}
                . A period counts on its last day.
              </li>
            )}
            {s.hasPolicy && (
              <li>
                <span className="text-foreground">Starts:</span>{' '}
                {s.minServiceMonths
                  ? `after ${s.minServiceMonths} months' service${
                      s.eligibleFrom ? ` — from ${fmtDay(s.eligibleFrom)}` : ''
                    }`
                  : 'from the first day'}
                {s.hiredOn ? ` (hired ${fmtDay(s.hiredOn)})` : ' (no hire date on record, so from the start of the year)'}
                {s.proRateOnJoin
                  ? '. In the year of qualifying, only the months after it count.'
                  : '. Once qualified, the whole leave year counts.'}
              </li>
            )}
            {s.hasPolicy && s.proRateOnExit && s.leftOn && (
              <li>
                <span className="text-foreground">Stops</span> on the last day of service,{' '}
                {fmtDay(s.leftOn)}.
              </li>
            )}
          </ul>

          {s.state === 'YearNotStarted' && s.asOfLimit !== 'LastDayOfService' && (
            <p className="text-sm">
              The {s.year} leave year starts on {fmtDay(s.yearStart)}; nothing has built up yet.
            </p>
          )}
          {s.state === 'YearNotStarted' && s.asOfLimit === 'LastDayOfService' && (
            <p className="text-sm">They left before the {s.year} leave year began.</p>
          )}
          {s.state === 'NotYetEligible' && (
            <p className="text-sm">
              Nothing builds up until {fmtDay(s.eligibleFrom)}
              {s.minServiceMonths ? `, after ${s.minServiceMonths} months' service` : ''}.
            </p>
          )}
          {s.state === 'FullGrant' && (
            <p className="text-sm">
              The whole entitlement, {days(s.annualEntitledDays)}, was granted on becoming eligible
              on {fmtDay(s.eligibleFrom)}.
            </p>
          )}

          {s.state === 'Accruing' && (
            <>
              {s.periods.length > 0 ? (
                <div className="max-h-72 overflow-y-auto rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Period</TableHead>
                        <TableHead className="text-right">Days</TableHead>
                        <TableHead className="text-right">Total</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {s.periods.map((p) => (
                        <TableRow key={p.start}>
                          <TableCell className="text-sm">
                            {fmtDay(p.start)} – {fmtDay(p.end)}
                          </TableCell>
                          <TableCell className="text-right text-sm">
                            {p.days}
                            {p.capped && (
                              <span className="ml-1 text-xs text-muted-foreground">(cap)</span>
                            )}
                          </TableCell>
                          <TableCell className="text-right text-sm font-medium">
                            {p.runningTotal}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">
                  No period has completed yet — the first one opened on {fmtDay(s.windowStart)}.
                </p>
              )}

              {s.nextPeriodStart && s.nextPeriodEnd && !s.tailNotCredited && (
                <p className="text-xs text-muted-foreground">
                  Next: {fmtDay(s.nextPeriodStart)} – {fmtDay(s.nextPeriodEnd)}, credited on{' '}
                  {fmtDay(s.nextPeriodEnd)}.
                </p>
              )}
              {s.tailNotCredited && s.nextPeriodStart && (
                <p className="text-xs text-muted-foreground">
                  {fmtDay(s.nextPeriodStart)} – {fmtDay(s.yearEnd)} is part of a period that runs past
                  the end of the leave year, so it is not credited: only whole periods count.
                </p>
              )}
            </>
          )}

          {!self && s.storedEntitledDays !== s.annualEntitledDays && (
            <p className="flex items-start gap-2 rounded-md border border-amber-300 bg-amber-50 p-2 text-xs dark:border-amber-900 dark:bg-amber-950/40">
              <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              The balance records {days(s.storedEntitledDays)} entitled; the rules now give{' '}
              {days(s.annualEntitledDays)}. What builds up follows the rules. <em>Repair
              entitlements</em> on the balances page brings the stored figure into line.
            </p>
          )}
        </>
      )}
    </div>
  );
}
