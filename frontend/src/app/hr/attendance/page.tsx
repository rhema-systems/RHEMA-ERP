'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  CalendarCheck,
  ClipboardList,
  Timer,
  House,
  BellRing,
  FileClock,
  Upload,
  Wallet,
  ScrollText,
  Fingerprint,
  Loader2,
} from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid, type NavCardItem } from '@/components/hr/common/NavCardGrid';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { attendanceDashboardService } from '@/services/hr/attendance.service';
import {
  formatDate,
  formatDateTime,
  formatHours,
  formatPercent,
  humanizeEnum,
  today,
} from '@/lib/hr/attendance-format';
import type { AttendanceDashboard } from '@/types/hr/attendance';

/**
 * Attendance landing page, backed by `GET api/attendance-dashboard` — one aggregated request
 * rather than the six list calls this screen used to count client-side.
 */
const navItems: NavCardItem[] = [
  {
    title: 'Daily Attendance',
    description: 'The full day-by-day record: hours, lateness, exceptions and verification.',
    href: '/hr/attendance/daily',
    icon: CalendarCheck,
  },
  {
    title: 'Attendance Records',
    description: 'The lightweight clock-in / clock-out register.',
    href: '/hr/attendance/records',
    icon: ClipboardList,
  },
  {
    title: 'Punch Logs',
    description: 'Raw device punches, geofence verification and unprocessed entries.',
    href: '/hr/attendance/logs',
    icon: ScrollText,
  },
  {
    title: 'Regularizations',
    description: 'Requests to correct a day’s attendance, approved through the workflow.',
    href: '/hr/attendance/regularizations',
    icon: FileClock,
  },
  {
    title: 'Overtime Requests',
    description: 'Pre-approval and supervisor confirmation of hours actually worked.',
    href: '/hr/attendance/overtime',
    icon: Timer,
  },
  {
    title: 'Remote Work',
    description: 'Work-from-home requests and their approvals.',
    href: '/hr/attendance/remote-work',
    icon: House,
  },
  {
    title: 'Monthly Summaries',
    description: 'Per-employee roll-ups that payroll reads, and their finalisation.',
    href: '/hr/attendance/summaries',
    icon: Wallet,
  },
  {
    title: 'Alerts',
    description: 'Absence, lateness and overtime alerts raised by the alert rules.',
    href: '/hr/attendance/alerts',
    icon: BellRing,
  },
  {
    title: 'Bulk Imports',
    description: 'Batch attendance loads with per-row error reporting.',
    href: '/hr/attendance/imports',
    icon: Upload,
  },
  {
    title: 'Payroll Exports',
    description: 'Hand-offs of finalised attendance to payroll, with their audit trail.',
    href: '/hr/attendance/payroll-exports',
    icon: Wallet,
  },
  {
    title: 'Biometrics',
    description: 'Enrolled fingerprint and face templates backing device recognition.',
    href: '/hr/attendance/biometrics',
    icon: Fingerprint,
  },
];

function StatTile({
  label,
  value,
  href,
  loading,
  tone,
  suffix,
}: {
  label: string;
  value?: number | string;
  href: string;
  loading: boolean;
  tone?: 'danger' | 'warn';
  suffix?: string;
}) {
  const toneClass =
    tone === 'danger' ? 'text-red-600' : tone === 'warn' ? 'text-amber-600' : undefined;
  return (
    <Link href={href}>
      <Card className="h-full transition-colors hover:bg-muted/50">
        <CardContent className="p-4">
          <p className="text-xs text-muted-foreground">{label}</p>
          {loading ? (
            <Skeleton className="mt-2 h-7 w-12" />
          ) : (
            <p className={`mt-1 text-2xl font-semibold ${toneClass ?? ''}`}>
              {value ?? 0}
              {suffix && <span className="ml-1 text-sm font-normal text-muted-foreground">{suffix}</span>}
            </p>
          )}
        </CardContent>
      </Card>
    </Link>
  );
}

/**
 * A bare CSS-grid bar chart. The trend is at most 90 points and needs no interaction, so a
 * charting dependency would be more weight than the feature is worth.
 */
function TrendBars({ trend }: { trend: AttendanceDashboard['dailyTrend'] }) {
  if (trend.length === 0) {
    return <p className="text-sm text-muted-foreground">No attendance recorded in this window.</p>;
  }

  const peak = Math.max(...trend.map((d) => d.present + d.absent + d.onLeave), 1);

  return (
    <div className="flex items-end gap-2 overflow-x-auto pb-2">
      {trend.map((d) => {
        const total = d.present + d.absent + d.onLeave;
        const height = Math.max(Math.round((total / peak) * 96), 2);
        const presentShare = total === 0 ? 0 : (d.present / total) * height;
        const absentShare = total === 0 ? 0 : (d.absent / total) * height;
        return (
          <div key={d.date} className="flex min-w-[44px] flex-1 flex-col items-center gap-1">
            <span className="text-[10px] text-muted-foreground">{formatPercent(d.attendanceRate)}</span>
            <div
              className="flex w-full flex-col-reverse overflow-hidden rounded-sm bg-muted"
              style={{ height: `${height}px` }}
              title={`${formatDate(d.date)} — ${d.present} present, ${d.absent} absent, ${d.onLeave} on leave`}
            >
              <div className="w-full bg-primary" style={{ height: `${presentShare}px` }} />
              <div className="w-full bg-red-500" style={{ height: `${absentShare}px` }} />
            </div>
            <span className="text-[10px] text-muted-foreground">{d.dayOfWeek.slice(0, 3)}</span>
          </div>
        );
      })}
    </div>
  );
}

export default function AttendanceHomePage() {
  const day = today();

  const { data, isLoading, isFetching, isError, error } = useQuery({
    queryKey: ['hr', 'attendance-dashboard', day],
    queryFn: () => attendanceDashboardService.get(day),
  });

  const d = data;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Attendance & Time"
        description={`Today, ${formatDate(day)} — attendance, approvals and the hand-off to payroll.`}
        actions={
          isFetching && !isLoading ? (
            <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
          ) : undefined
        }
      />

      {isError && (
        <Card>
          <CardContent className="p-4">
            <p className="text-sm text-red-600">
              {(error as any)?.message || 'Could not load the attendance dashboard.'}
            </p>
          </CardContent>
        </Card>
      )}

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">
          Today
          {d && (
            <span className="ml-2 font-normal">
              · {formatPercent(d.attendanceRateToday)} attendance of {d.totalEmployees} active
              employees
            </span>
          )}
        </h2>
        <div className="grid gap-3 sm:grid-cols-3 lg:grid-cols-5">
          <StatTile
            label="Present"
            value={d?.presentToday}
            href="/hr/attendance/daily"
            loading={isLoading}
          />
          <StatTile
            label="Absent"
            value={d?.absentToday}
            href="/hr/attendance/daily"
            loading={isLoading}
            tone="danger"
          />
          <StatTile
            label="Late"
            value={d?.lateToday}
            href="/hr/attendance/daily"
            loading={isLoading}
            tone="warn"
          />
          <StatTile
            label="On leave"
            value={d?.onLeaveToday}
            href="/hr/leave/requests"
            loading={isLoading}
          />
          <StatTile
            label="Remote"
            value={d?.remoteToday}
            href="/hr/attendance/remote-work"
            loading={isLoading}
          />
        </div>
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Needs attention</h2>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <StatTile
            label="Regularizations awaiting approval"
            value={d?.pendingRegularizations}
            href="/hr/attendance/regularizations"
            loading={isLoading}
          />
          <StatTile
            label="Overtime awaiting approval"
            value={d?.pendingOvertimeRequests}
            href="/hr/attendance/overtime"
            loading={isLoading}
          />
          <StatTile
            label="Remote work awaiting approval"
            value={d?.pendingRemoteWorkRequests}
            href="/hr/attendance/remote-work"
            loading={isLoading}
          />
          <StatTile
            label="Unacknowledged alerts"
            value={(d?.activeCriticalAlerts ?? 0) + (d?.activeWarningAlerts ?? 0)}
            href="/hr/attendance/alerts"
            loading={isLoading}
            tone={d && d.activeCriticalAlerts > 0 ? 'danger' : 'warn'}
          />
          <StatTile
            label="Unprocessed punches"
            value={d?.unprocessedAttendanceLogs}
            href="/hr/attendance/logs"
            loading={isLoading}
          />
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Attendance, last 7 days</CardTitle>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-32 w-full" />
            ) : (
              <TrendBars trend={d?.dailyTrend ?? []} />
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Current pay period</CardTitle>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-20 w-full" />
            ) : d?.currentPayPeriodName ? (
              <div className="space-y-2 text-sm">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium">{d.currentPayPeriodName}</span>
                  {d.currentPayPeriodStatus && <StatusBadge status={d.currentPayPeriodStatus} />}
                </div>
                <p className="text-muted-foreground">
                  {formatDate(d.currentPayPeriodStart)} – {formatDate(d.currentPayPeriodEnd)}
                </p>
                <p className="text-muted-foreground">
                  {formatHours(d.totalOvertimeHoursThisPeriod)} overtime this period ·{' '}
                  {d.approvedOvertimeRequests} approved requests
                </p>
                <p className="text-muted-foreground">
                  {d.activeDevices} active devices
                  {d.devicesWithPendingSync > 0 && (
                    <span className="text-amber-600">
                      {' '}
                      · {d.devicesWithPendingSync} awaiting sync
                    </span>
                  )}
                </p>
                <Link
                  href="/hr/attendance/summaries"
                  className="inline-block text-primary hover:underline"
                >
                  View summaries
                </Link>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">
                No pay period is open.{' '}
                <Link
                  href="/administration/hr/attendance/pay-periods"
                  className="text-primary hover:underline"
                >
                  Create one
                </Link>{' '}
                before the next cycle.
              </p>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader className="pb-2">
            <div className="flex items-center justify-between">
              <CardTitle className="text-base">Top open alerts</CardTitle>
              <Link href="/hr/attendance/alerts" className="text-sm text-primary hover:underline">
                All alerts
              </Link>
            </div>
          </CardHeader>
          <CardContent className="p-0">
            {isLoading ? (
              <div className="p-4">
                <Skeleton className="h-24 w-full" />
              </div>
            ) : (d?.topAlerts.length ?? 0) === 0 ? (
              <EmptyState title="No open alerts" description="Nothing needs acknowledging." />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Employee</TableHead>
                    <TableHead>Trigger</TableHead>
                    <TableHead>Severity</TableHead>
                    <TableHead>Raised</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {d?.topAlerts.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell className="font-medium">{a.employeeName}</TableCell>
                      <TableCell>{humanizeEnum(a.triggerType)}</TableCell>
                      <TableCell>
                        <Badge
                          variant={
                            a.severity === 'Critical'
                              ? 'destructive'
                              : a.severity === 'Warning'
                                ? 'secondary'
                                : 'outline'
                          }
                        >
                          {a.severity}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {formatDateTime(a.triggeredDate)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Chronic absentees, last 30 days</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {isLoading ? (
              <div className="p-4">
                <Skeleton className="h-24 w-full" />
              </div>
            ) : (d?.chronicAbsentees.length ?? 0) === 0 ? (
              <EmptyState
                title="Nobody flagged"
                description="No employee has an unexplained absence in the last 30 days."
              />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Employee</TableHead>
                    <TableHead>Department</TableHead>
                    <TableHead className="text-right">Absent</TableHead>
                    <TableHead className="text-right">Late</TableHead>
                    <TableHead className="text-right">Attendance</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {d?.chronicAbsentees.map((e) => (
                    <TableRow key={e.employeeId}>
                      <TableCell>
                        <div className="font-medium">{e.employeeName}</div>
                        <div className="text-xs text-muted-foreground">{e.employeeNumber}</div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {e.departmentName || '—'}
                      </TableCell>
                      <TableCell className="text-right font-medium text-red-600">
                        {e.absentDaysLast30}
                      </TableCell>
                      <TableCell className="text-right">{e.lateDaysLast30}</TableCell>
                      <TableCell className="text-right">
                        {formatPercent(e.attendancePercentageLast30)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">All screens</h2>
        <NavCardGrid items={navItems} />
      </div>
    </div>
  );
}
