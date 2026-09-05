'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plane, Clock, Globe, ShieldAlert, CalendarClock } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelRequestSummary } from '@/types/hr/travel';

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtMoney = (amount?: number | null, currency?: string) =>
  amount === null || amount === undefined
    ? '—'
    : new Intl.NumberFormat(undefined, {
        style: 'currency', currency: currency || 'GHS', currencyDisplay: 'code',
      }).format(amount);

function Tile({
  label, value, hint, icon: Icon,
}: {
  label: string;
  value: React.ReactNode;
  hint?: string;
  icon: typeof Plane;
}) {
  return (
    <Card>
      <CardContent className="p-4">
        <div className="flex items-start justify-between gap-2">
          <div>
            <p className="text-xs text-muted-foreground">{label}</p>
            <p className="mt-1 text-2xl font-semibold">{value}</p>
            {hint && <p className="mt-1 text-xs text-muted-foreground">{hint}</p>}
          </div>
          <Icon className="h-4 w-4 shrink-0 text-muted-foreground" />
        </div>
      </CardContent>
    </Card>
  );
}

function RequestTable({ rows, empty }: { rows: StaffTravelRequestSummary[]; empty: string }) {
  if (rows.length === 0) return <EmptyState title="Nothing here" description={empty} />;
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Number</TableHead>
          <TableHead>Traveller</TableHead>
          <TableHead>Route</TableHead>
          <TableHead>Departs</TableHead>
          <TableHead>Status</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((r) => (
          <TableRow key={r.id}>
            <TableCell className="font-medium">
              <Link href={`/hr/travel/${r.id}`} className="hover:underline">
                {r.requestNumber}
              </Link>
            </TableCell>
            <TableCell>{r.employeeName || '—'}</TableCell>
            <TableCell>
              {r.originCity} → {r.destinationCity}
            </TableCell>
            <TableCell className="whitespace-nowrap">{fmtDate(r.travelStartDate)}</TableCell>
            <TableCell><StatusBadge status={humanize(r.status)} /></TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * Staff travel at a glance.
 *
 * ⚠ **The cost figures are shown per currency, not as one total.** The API also returns scalar
 * totals, but those add every request's estimate together regardless of the currency it was costed
 * in — 5,000 GHS plus 5,000 USD is not 10,000 of anything. They are deliberately **not** converted
 * to a base currency here: travel never invents a rate, and Finance's conversion is currently
 * inverted, so a converted headline would be confidently wrong rather than visibly incomplete.
 * A single currency renders as one figure; more than one renders as a breakdown.
 */
export default function TravelDashboardPage() {
  const { data, isLoading } = useQuery({
    queryKey: ['travel-dashboard'],
    queryFn: () => travelService.getDashboard(30),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }
  if (!data) {
    return (
      <div className="p-6">
        <EmptyState title="No data" description="The travel dashboard could not be loaded." />
      </div>
    );
  }

  const byCurrency = data.costByCurrency ?? [];
  const single = byCurrency.length === 1 ? byCurrency[0] : null;
  const total = data.totalRequests || 1;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Staff travel dashboard"
        description="Where the organisation's travel stands."
        backHref="/hr/travel"
      />

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <Tile label="Requests" value={data.totalRequests} icon={Plane} />
        <Tile
          label="Awaiting approval"
          value={data.pendingApprovalCount}
          hint={data.draftCount > 0 ? `${data.draftCount} still in draft` : undefined}
          icon={Clock}
        />
        <Tile
          label="Departing in 30 days"
          value={data.upcomingTripCount}
          icon={CalendarClock}
        />
        <Tile
          label="High risk"
          value={data.highRiskCount}
          hint={`${data.internationalCount} international`}
          icon={ShieldAlert}
        />
      </div>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Estimated cost</CardTitle>
        </CardHeader>
        <CardContent>
          {byCurrency.length === 0 ? (
            <p className="text-sm text-muted-foreground">No active requests carry a cost.</p>
          ) : single ? (
            <div className="grid grid-cols-2 gap-4 md:grid-cols-3">
              <div>
                <p className="text-xs text-muted-foreground">Estimated</p>
                <p className="text-2xl font-semibold">
                  {fmtMoney(single.estimatedTotal, single.currencyCode)}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Approved budget</p>
                <p className="text-2xl font-semibold">
                  {fmtMoney(single.approvedBudget, single.currencyCode)}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Across</p>
                <p className="text-2xl font-semibold">{single.requestCount} requests</p>
              </div>
            </div>
          ) : (
            <div className="space-y-3">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Currency</TableHead>
                    <TableHead className="text-right">Estimated</TableHead>
                    <TableHead className="text-right">Approved budget</TableHead>
                    <TableHead className="text-right">Requests</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {byCurrency.map((c) => (
                    <TableRow key={c.currencyCode}>
                      <TableCell className="font-medium">{c.currencyCode}</TableCell>
                      <TableCell className="text-right whitespace-nowrap">
                        {fmtMoney(c.estimatedTotal, c.currencyCode)}
                      </TableCell>
                      <TableCell className="text-right whitespace-nowrap">
                        {fmtMoney(c.approvedBudget, c.currencyCode)}
                      </TableCell>
                      <TableCell className="text-right">{c.requestCount}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              {/*
                Said plainly rather than quietly adding them up: the organisation costs travel in
                more than one currency, and there is no honest single number to show without a rate
                travel is not entitled to invent.
              */}
              <p className="text-xs text-muted-foreground">
                Travel is costed in {byCurrency.length} currencies. These are not added together —
                doing so would need an exchange rate, and travel takes rates from Finance rather
                than inventing one. Cancelled and rejected requests are excluded throughout.
              </p>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">By status</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {(data.byStatus ?? []).length === 0 ? (
              <p className="text-sm text-muted-foreground">No requests yet.</p>
            ) : (
              (data.byStatus ?? []).map((s) => (
                <div key={s.statusName} className="space-y-1">
                  <div className="flex items-center justify-between text-sm">
                    <span>{humanize(s.statusName)}</span>
                    <span className="text-muted-foreground">{s.count}</span>
                  </div>
                  <Progress value={(s.count / total) * 100} />
                </div>
              ))
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <Globe className="h-4 w-4" />
              By type
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {(data.byTravelType ?? []).length === 0 ? (
              <p className="text-sm text-muted-foreground">No requests yet.</p>
            ) : (
              (data.byTravelType ?? []).map((t) => (
                <div key={t.travelTypeName} className="space-y-1">
                  <div className="flex items-center justify-between text-sm">
                    <span>{humanize(t.travelTypeName)}</span>
                    <span className="text-muted-foreground">{t.count}</span>
                  </div>
                  <Progress value={(t.count / total) * 100} />
                </div>
              ))
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Requests raised, last six months</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-end gap-3">
            {(data.monthlyTrend ?? []).map((m) => {
              const peak = Math.max(1, ...(data.monthlyTrend ?? []).map((x) => x.count));
              return (
                <div key={m.label} className="flex flex-1 flex-col items-center gap-2">
                  <span className="text-xs text-muted-foreground">{m.count}</span>
                  <div
                    className="w-full rounded-t bg-primary"
                    style={{ height: `${Math.max(4, (m.count / peak) * 96)}px` }}
                  />
                  <span className="text-xs text-muted-foreground">{m.label}</span>
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Awaiting approval</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          <RequestTable
            rows={data.pendingApprovals ?? []}
            empty="Nothing is waiting on an approver."
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Departing soon</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          <RequestTable rows={data.upcomingTrips ?? []} empty="No trips in the next 30 days." />
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Recently raised</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          <RequestTable rows={data.recentRequests ?? []} empty="Nothing raised yet." />
        </CardContent>
      </Card>
    </div>
  );
}
