'use client';

/**
 * Area 25 slice 4 — My Leave: the portal's leave hub.
 *
 * Spec destination #4 (my leave requests) + the balances the request form draws on. The
 * desk registers under /hr/leave are HR-shaped (employee pickers, org-wide reads) and stay
 * where they are (D3) — this screen reads only the self-armed routes with the caller's own
 * employee id.
 */

import Link from 'next/link';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  CalendarDays,
  CalendarPlus,
  CalendarRange,
  Coins,
  TreePalm,
} from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import { leaveService } from '@/services/hr/leave.service';
import { LEAVE_STATUS_BADGE } from '@/components/me/leave/leave-status';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { AccrualStatementPanel, fmtDay } from '@/components/hr/leave/AccrualStatementPanel';
import { useLeaveYear } from '@/components/hr/leave/use-leave-year';
import type { LeaveBalance } from '@/types/hr/leave-request';

const fmtDate = (d: string) =>
  new Date(d).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });

export default function MyLeavePage() {
  const { user } = useAuth();
  const employeeId = user?.employeeId ?? '';
  // Round 5, C4: opens on the leave year we are in (not the calendar year, for a leave year that
  // starts later than January); a year the employee picks wins.
  const { currentYear } = useLeaveYear();
  const [chosenYear, setChosenYear] = useState<number | null>(null);
  const year = chosenYear ?? currentYear;
  // Round 5, C2: the balance whose accrual statement is open.
  const [statementFor, setStatementFor] = useState<LeaveBalance | null>(null);

  const { data: balances, isLoading: balancesLoading } = useQuery({
    // 'live' keeps this apart in the cache from reads without the live annual row.
    queryKey: ['me', 'leave-balances', employeeId, year, 'live'],
    // Round 5, lane J: annual leave is shown from the first day, worked out live until a request
    // opens its record — and it comes first.
    queryFn: () => leaveService.getEmployeeBalances(employeeId, year, true),
    enabled: !!employeeId,
  });

  const { data: history, isLoading: historyLoading } = useQuery({
    queryKey: ['me', 'leave-history', employeeId, year],
    queryFn: () => leaveService.getEmployeeHistory(employeeId, year, 1, 50),
    enabled: !!employeeId,
  });

  const years = [currentYear + 1, currentYear, currentYear - 1, currentYear - 2];

  return (
    <div className="space-y-8">
      <Dialog open={!!statementFor} onOpenChange={(open) => !open && setStatementFor(null)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[680px]">
          <DialogHeader>
            <DialogTitle>
              {statementFor?.leaveTypeName} {statementFor?.year}
            </DialogTitle>
            <DialogDescription>
              Worked out from your entitlement and service — the same figures a request is checked
              against.
            </DialogDescription>
          </DialogHeader>
          {statementFor && <AccrualStatementPanel balanceId={statementFor.id} audience="self" />}
        </DialogContent>
      </Dialog>

      <PageHeader
        title="My Leave"
        description="Your balances and requests. Approvals travel through the configured workflow."
        backHref="/me"
        actions={
          <>
            <Button variant="outline" asChild>
              <Link href="/me/leave/calendar">
                <CalendarDays className="mr-2 h-4 w-4" /> Calendar
              </Link>
            </Button>
            <Button variant="outline" asChild>
              <Link href="/me/leave/planner">
                <CalendarRange className="mr-2 h-4 w-4" /> Planner
              </Link>
            </Button>
            <Button variant="outline" asChild>
              <Link href="/me/leave/encashments">
                <Coins className="mr-2 h-4 w-4" /> Encashments
              </Link>
            </Button>
            <Button asChild>
              <Link href="/me/leave/new">
                <CalendarPlus className="mr-2 h-4 w-4" /> New request
              </Link>
            </Button>
          </>
        }
      />

      {/* ── Balances ───────────────────────────────────────────────────── */}
      <section>
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
            Balances
          </h2>
          <Select value={String(year)} onValueChange={(v) => setChosenYear(Number(v))}>
            <SelectTrigger className="w-28">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {years.map((y) => (
                <SelectItem key={y} value={String(y)}>
                  {y}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {balancesLoading ? (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {[0, 1, 2].map((i) => (
              <Skeleton key={i} className="h-28" />
            ))}
          </div>
        ) : balances?.length ? (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {balances.map((b) => (
              <Card key={b.hasRecord === false ? `live-${b.leaveTypeId}` : b.id}>
                <CardContent className="p-4">
                  <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                    <TreePalm className="h-3.5 w-3.5" /> {b.leaveTypeName}
                    {b.leaveSubTypeName ? ` · ${b.leaveSubTypeName}` : ''}
                  </div>
                  {/*
                    Round 5, lane J: what you can book today leads, because it is what a request is
                    checked against. On leave that builds up it is below the year's figure until the
                    year has built up, and the card says both.
                  */}
                  <div className="mt-2 text-2xl font-bold leading-none">
                    {b.accruedAvailableDays}
                    <span className="ml-1 text-sm font-normal text-muted-foreground">
                      days you can take now
                    </span>
                  </div>
                  {b.accruedAvailableDays !== b.availableDays && (
                    <div className="mt-1 text-sm text-muted-foreground">
                      {b.availableDays} available for the whole year
                    </div>
                  )}
                  <div className="mt-2 text-xs text-muted-foreground">
                    entitled {b.entitledDays} · used {b.usedDays} · pending {b.pendingDays}
                    {b.carriedOverDays ? ` · carried over ${b.carriedOverDays}` : ''}
                    {b.encashedDays ? ` · encashed ${b.encashedDays}` : ''}
                  </div>
                  {b.accessibleFrom && (
                    <div className="mt-2 text-xs text-amber-700 dark:text-amber-400">
                      You can take it from {fmtDay(b.accessibleFrom)}, once you have served the
                      qualifying period.
                    </div>
                  )}
                  {/*
                    Round 5, C2: leave that builds up says how much, and as at when — and shows its
                    working. Answering "why only 12.25?" used to take a call to HR.
                  */}
                  {b.accruedAsOf && (
                    <div className="mt-2 flex flex-wrap items-center justify-between gap-2 text-xs">
                      <span className="text-muted-foreground">
                        built up {b.accruedToDateDays} as at {fmtDay(b.accruedAsOf)}
                      </span>
                      {/* The statement reads a record; a figure worked out live has none yet. */}
                      {b.hasRecord !== false && (
                        <Button
                          variant="link"
                          size="sm"
                          className="h-auto p-0 text-xs"
                          onClick={() => setStatementFor(b)}
                        >
                          How it builds up
                        </Button>
                      )}
                    </div>
                  )}
                </CardContent>
              </Card>
            ))}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">
            No leave balances recorded for {year}. Balances appear once HR sets up your
            entitlements.
          </p>
        )}
      </section>

      {/* ── Requests ───────────────────────────────────────────────────── */}
      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          Requests in {year}
        </h2>
        {historyLoading ? (
          <Skeleton className="h-40" />
        ) : history?.items?.length ? (
          <div className="space-y-2">
            {history.items.map((r) => (
              <Link
                key={r.id}
                href={`/me/leave/${r.id}`}
                className="flex flex-wrap items-center justify-between gap-2 rounded-lg border bg-card px-4 py-3 text-sm transition-colors hover:bg-accent"
              >
                <div className="min-w-0">
                  <div className="font-medium">
                    {r.leaveTypeName}
                    {r.leaveSubTypeName ? ` · ${r.leaveSubTypeName}` : ''}
                    <span className="ml-2 text-xs font-normal text-muted-foreground">
                      {r.requestNumber}
                    </span>
                  </div>
                  <div className="mt-0.5 text-xs text-muted-foreground">
                    {fmtDate(r.startDate)} – {fmtDate(r.endDate)} · {r.totalDays} day
                    {r.totalDays === 1 ? '' : 's'}
                  </div>
                </div>
                <Badge className={LEAVE_STATUS_BADGE[r.status] ?? ''} variant="outline">
                  {r.status}
                </Badge>
              </Link>
            ))}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">
            No requests in {year}. When you file one it will appear here with its approval
            state.
          </p>
        )}
      </section>
    </div>
  );
}
