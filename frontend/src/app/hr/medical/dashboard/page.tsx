'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  FileText,
  Clock,
  Flag,
  BadgeCheck,
  Banknote,
  ShieldPlus,
  CalendarClock,
  Stethoscope,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { medicalDashboardService } from '@/services/hr/medical-claims.service';
import type { MedicalClaimSpotlight } from '@/types/hr/medical';

/**
 * Medical dashboard.
 *
 * The tiles are ordered by what someone would act on rather than by magnitude: claims waiting on a
 * decision and anything flagged come first, cost totals after. Overdue premiums and expiring
 * policies are tinted because they are deadlines, not statistics.
 */
const money = (v: number) =>
  v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

function ClaimTable({ items, empty }: { items: MedicalClaimSpotlight[]; empty: string }) {
  if (items.length === 0) {
    return <EmptyState title="Nothing here" description={empty} icon={FileText} />;
  }
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Claim</TableHead>
          <TableHead>Employee</TableHead>
          <TableHead>Type</TableHead>
          <TableHead className="text-right">Requested</TableHead>
          <TableHead>Status</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {items.map((c) => (
          <TableRow key={c.id}>
            <TableCell>
              <Link
                href={`/hr/medical/claims/${c.id}`}
                className="font-mono text-sm text-primary hover:underline"
              >
                {c.claimNumber}
              </Link>
            </TableCell>
            <TableCell className="font-medium">{c.employeeName}</TableCell>
            <TableCell>{c.expenseType}</TableCell>
            <TableCell className="text-right tabular-nums">{money(c.amountRequested)}</TableCell>
            <TableCell>
              <Badge variant="outline">{c.status}</Badge>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

export default function MedicalDashboardPage() {
  const { data } = useQuery({
    queryKey: ['hr', 'medical-dashboard', 30],
    queryFn: () => medicalDashboardService.get(30),
  });

  const appointments = data?.upcomingAppointmentList ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Medical Dashboard"
        description="Claims awaiting a decision, insurance exposure and upcoming clinical activity."
        backHref="/hr/medical"
      />

      <MetricTiles
        tiles={[
          {
            label: 'Awaiting decision',
            value: data?.pendingClaims ?? 0,
            hint: data ? `${money(data.pendingClaimsAmount)} requested` : undefined,
            icon: Clock,
            tone: (data?.pendingClaims ?? 0) > 0 ? 'warning' : 'default',
          },
          {
            label: 'Flagged for review',
            value: data?.flaggedClaims ?? 0,
            hint: 'Marked for a closer look',
            icon: Flag,
            tone: (data?.flaggedClaims ?? 0) > 0 ? 'danger' : 'default',
          },
          {
            label: 'Approved',
            value: data?.approvedClaims ?? 0,
            hint: 'Awaiting payment',
            icon: BadgeCheck,
          },
          {
            label: 'Reimbursed',
            value: data ? money(data.totalReimbursedAmount) : '0.00',
            hint: `${data?.paidClaims ?? 0} claims paid`,
            icon: Banknote,
            tone: 'success',
          },
        ]}
      />

      <MetricTiles
        tiles={[
          {
            label: 'Active policies',
            value: data?.activePolicies ?? 0,
            icon: ShieldPlus,
          },
          {
            label: 'Policies expiring',
            value: data?.expiringPolicies ?? 0,
            hint: 'Within 30 days',
            icon: CalendarClock,
            tone: (data?.expiringPolicies ?? 0) > 0 ? 'warning' : 'default',
          },
          {
            label: 'Overdue premiums',
            value: data?.overduePremiums ?? 0,
            hint: data ? money(data.overduePremiumAmount) : undefined,
            icon: Banknote,
            tone: (data?.overduePremiums ?? 0) > 0 ? 'danger' : 'default',
          },
          {
            label: 'Examinations due',
            value: data?.examsDue ?? 0,
            hint: 'Recall list',
            icon: Stethoscope,
            tone: (data?.examsDue ?? 0) > 0 ? 'warning' : 'default',
          },
        ]}
      />

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Waiting on a decision</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <ClaimTable
              items={data?.pendingApprovalClaims ?? []}
              empty="No claims are waiting on a decision."
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Recently filed</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <ClaimTable items={data?.recentClaims ?? []} empty="No claims have been filed yet." />
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Claims by month</CardTitle>
          </CardHeader>
          <CardContent>
            {/* A six-point series does not need a chart library to be read accurately. */}
            <div className="flex items-end gap-3">
              {(data?.monthlyClaimTrend ?? []).map((m) => {
                const max = Math.max(1, ...(data?.monthlyClaimTrend ?? []).map((x) => x.count));
                return (
                  <div key={m.label} className="flex flex-1 flex-col items-center gap-1">
                    <span className="text-xs tabular-nums text-muted-foreground">{m.count}</span>
                    <div
                      className="w-full rounded-t bg-primary/70"
                      style={{ height: `${Math.max(4, (m.count / max) * 96)}px` }}
                    />
                    <span className="text-xs text-muted-foreground">{m.label.split(' ')[0]}</span>
                  </div>
                );
              })}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Upcoming appointments</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {appointments.length === 0 ? (
              <EmptyState
                title="Nothing scheduled"
                description="No appointments in the next 30 days."
                icon={CalendarClock}
              />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>When</TableHead>
                    <TableHead>Employee</TableHead>
                    <TableHead>Facility</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {appointments.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell>{fmtDate(a.appointmentDateTime)}</TableCell>
                      <TableCell className="font-medium">{a.employeeName}</TableCell>
                      <TableCell>{a.facilityName}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
