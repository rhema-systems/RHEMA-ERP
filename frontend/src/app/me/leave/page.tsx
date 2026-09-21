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

const fmtDate = (d: string) =>
  new Date(d).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });

export default function MyLeavePage() {
  const { user } = useAuth();
  const employeeId = user?.employeeId ?? '';
  const currentYear = new Date().getFullYear();
  const [year, setYear] = useState(currentYear);

  const { data: balances, isLoading: balancesLoading } = useQuery({
    queryKey: ['me', 'leave-balances', employeeId, year],
    queryFn: () => leaveService.getEmployeeBalances(employeeId, year),
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
          <Select value={String(year)} onValueChange={(v) => setYear(Number(v))}>
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
              <Card key={b.id}>
                <CardContent className="p-4">
                  <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                    <TreePalm className="h-3.5 w-3.5" /> {b.leaveTypeName}
                    {b.leaveSubTypeName ? ` · ${b.leaveSubTypeName}` : ''}
                  </div>
                  <div className="mt-2 text-2xl font-bold leading-none">
                    {b.availableDays}
                    <span className="ml-1 text-sm font-normal text-muted-foreground">
                      days available
                    </span>
                  </div>
                  <div className="mt-2 text-xs text-muted-foreground">
                    entitled {b.entitledDays} · used {b.usedDays} · pending {b.pendingDays}
                    {b.carriedOverDays ? ` · carried over ${b.carriedOverDays}` : ''}
                    {b.encashedDays ? ` · encashed ${b.encashedDays}` : ''}
                  </div>
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
