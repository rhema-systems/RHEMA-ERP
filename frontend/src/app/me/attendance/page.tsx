'use client';

/**
 * Area 25 slice 4 — my attendance & punch (spec destination #2, D9: punch MOVES IN).
 *
 * The punch is token-actor (`POST staff-attendance-logs/punch`): the server decides who
 * punched, immediately folds it into the daily record, and verifies GPS against any
 * geofence zone — a hard-enforced zone rejects the punch, a soft one records the
 * violation. Location is offered, never demanded: punching must work on a desktop with no
 * GPS at all, so a denied/absent position simply sends none.
 *
 * ⚠ The from/to on the logs read are DateTimes and `to` is midnight-exclusive — asking
 * for from=today&to=today returns nothing. Every range here sends `to` as the NEXT day.
 */

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import {
  AlertTriangle,
  CheckCircle2,
  Clock,
  LogIn,
  LogOut,
  MapPin,
  ShieldCheck,
} from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import { attendanceLogService, dailyAttendanceService } from '@/services/hr/attendance.service';
import type { AttendanceLogType, StaffAttendancePunchResult } from '@/types/hr/attendance';

const isoDate = (d: Date) => {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
};
const plusDays = (d: Date, days: number) => {
  const copy = new Date(d);
  copy.setDate(copy.getDate() + days);
  return copy;
};
const fmtTime = (t?: string | null) => (t ? t.split('.')[0].slice(0, 5) : '—');
const fmtDateTime = (d: string) =>
  new Date(d).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });

/** Best-effort geolocation: resolve to null rather than fail when unavailable/denied. */
const getPosition = (): Promise<GeolocationPosition | null> =>
  new Promise((resolve) => {
    if (typeof navigator === 'undefined' || !navigator.geolocation) return resolve(null);
    navigator.geolocation.getCurrentPosition(
      (pos) => resolve(pos),
      () => resolve(null),
      { timeout: 5000, maximumAge: 60000 },
    );
  });

const STATUS_TONE: Record<string, string> = {
  Present: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/60 dark:text-emerald-200',
  Absent: 'bg-red-100 text-red-900 dark:bg-red-950/60 dark:text-red-200',
  OnLeave: 'bg-blue-100 text-blue-900 dark:bg-blue-950/60 dark:text-blue-200',
  Holiday: 'bg-blue-100 text-blue-900 dark:bg-blue-950/60 dark:text-blue-200',
};

export default function MyAttendancePage() {
  const { user } = useAuth();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const employeeId = user?.employeeId ?? '';
  const [lastResult, setLastResult] = useState<StaffAttendancePunchResult | null>(null);

  const now = new Date();
  const today = isoDate(now);
  const tomorrow = isoDate(plusDays(now, 1));
  const monthStart = isoDate(new Date(now.getFullYear(), now.getMonth(), 1));

  const { data: todayRecord, isLoading: todayLoading } = useQuery({
    queryKey: ['me', 'attendance', 'today', employeeId, today],
    queryFn: () => dailyAttendanceService.getByEmployeeAndDate(employeeId, today),
    enabled: !!employeeId,
  });

  const { data: todayLogs } = useQuery({
    queryKey: ['me', 'attendance', 'logs', employeeId, today],
    // `to` is exclusive at midnight — see the header note.
    queryFn: () => attendanceLogService.getByEmployee(employeeId, today, tomorrow),
    enabled: !!employeeId,
  });

  const { data: monthDays, isLoading: monthLoading } = useQuery({
    queryKey: ['me', 'attendance', 'month', employeeId, monthStart],
    queryFn: () => dailyAttendanceService.getByEmployee(employeeId, monthStart, today),
    enabled: !!employeeId,
  });

  const punchMutation = useMutation({
    mutationFn: async (logType: AttendanceLogType) => {
      const pos = await getPosition();
      return attendanceLogService.punch({
        logType,
        latitude: pos?.coords.latitude ?? null,
        longitude: pos?.coords.longitude ?? null,
        location: null,
        processImmediately: true,
      });
    },
    onSuccess: async (result, logType) => {
      setLastResult(result);
      toast({
        title: logType === 'CheckIn' ? 'Checked in' : 'Checked out',
        description: `Recorded at ${fmtDateTime(result.log.logDateTime)}.`,
      });
      await queryClient.invalidateQueries({ queryKey: ['me', 'attendance'] });
    },
    onError: (e: any) =>
      toast({
        title: 'Punch refused',
        description: e?.message || 'The punch could not be recorded.',
        variant: 'destructive',
      }),
  });

  const checkedIn = !!todayRecord?.actualCheckInTime;
  const checkedOut = !!todayRecord?.actualCheckOutTime;
  const verification = lastResult?.verification;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">My attendance</h1>
        <p className="text-sm text-muted-foreground">
          Punch in and out, and see how your month is recorded.
        </p>
      </div>

      {/* ── Today ──────────────────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Clock className="h-4 w-4" />
            Today ·{' '}
            {now.toLocaleDateString(undefined, {
              weekday: 'long',
              day: 'numeric',
              month: 'long',
            })}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {todayLoading ? (
            <Skeleton className="h-20" />
          ) : (
            <div className="flex flex-wrap items-center gap-6">
              <div>
                <div className="text-xs uppercase tracking-wide text-muted-foreground">
                  Checked in
                </div>
                <div className="text-2xl font-bold">{fmtTime(todayRecord?.actualCheckInTime)}</div>
              </div>
              <div>
                <div className="text-xs uppercase tracking-wide text-muted-foreground">
                  Checked out
                </div>
                <div className="text-2xl font-bold">{fmtTime(todayRecord?.actualCheckOutTime)}</div>
              </div>
              {todayRecord?.actualWorkHours != null && (
                <div>
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">
                    Hours
                  </div>
                  <div className="text-2xl font-bold">{todayRecord.actualWorkHours}</div>
                </div>
              )}
              <div className="ml-auto flex gap-2">
                <Button
                  size="lg"
                  disabled={punchMutation.isPending || (checkedIn && !checkedOut)}
                  onClick={() => punchMutation.mutate('CheckIn')}
                >
                  <LogIn className="mr-2 h-5 w-5" /> Check in
                </Button>
                <Button
                  size="lg"
                  variant="outline"
                  disabled={punchMutation.isPending || !checkedIn}
                  onClick={() => punchMutation.mutate('CheckOut')}
                >
                  <LogOut className="mr-2 h-5 w-5" /> Check out
                </Button>
              </div>
            </div>
          )}

          {verification && (
            <div
              className={`flex items-start gap-2 rounded-md border p-3 text-xs ${
                verification.status === 'WithinZone'
                  ? 'border-emerald-300/60 bg-emerald-50 text-emerald-900 dark:border-emerald-500/40 dark:bg-emerald-950/40 dark:text-emerald-200'
                  : 'bg-muted/40 text-muted-foreground'
              }`}
            >
              {verification.status === 'WithinZone' ? (
                <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" />
              ) : (
                <MapPin className="mt-0.5 h-4 w-4 shrink-0" />
              )}
              <span>
                {verification.message ||
                  (verification.status === 'WithinZone'
                    ? `Location verified${verification.geofenceZoneName ? ` — ${verification.geofenceZoneName}` : ''}.`
                    : 'Location could not be verified.')}
              </span>
            </div>
          )}

          {!!todayLogs?.length && (
            <div>
              <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Today&apos;s punches
              </div>
              <div className="flex flex-wrap gap-2">
                {todayLogs.map((l) => (
                  <span
                    key={l.id}
                    className="rounded-full border px-3 py-1 text-xs text-muted-foreground"
                  >
                    {l.logType === 'CheckIn' ? 'In' : l.logType === 'CheckOut' ? 'Out' : l.logType}{' '}
                    · {fmtDateTime(l.logDateTime)}
                  </span>
                ))}
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* ── This month ─────────────────────────────────────────────────── */}
      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          This month
        </h2>
        {monthLoading ? (
          <Skeleton className="h-40" />
        ) : monthDays?.length ? (
          <div className="overflow-x-auto rounded-lg border">
            <table className="w-full text-sm">
              <thead className="bg-muted/50 text-left text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="px-3 py-2">Date</th>
                  <th className="px-3 py-2">In</th>
                  <th className="px-3 py-2">Out</th>
                  <th className="px-3 py-2">Hours</th>
                  <th className="px-3 py-2">Status</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody>
                {[...monthDays]
                  .sort((a, b) => (a.attendanceDate < b.attendanceDate ? 1 : -1))
                  .map((d) => (
                    <tr key={d.id} className="border-t">
                      <td className="px-3 py-2">
                        {new Date(d.attendanceDate).toLocaleDateString(undefined, {
                          weekday: 'short',
                          day: 'numeric',
                          month: 'short',
                        })}
                      </td>
                      <td className="px-3 py-2">{fmtTime(d.actualCheckInTime)}</td>
                      <td className="px-3 py-2">{fmtTime(d.actualCheckOutTime)}</td>
                      <td className="px-3 py-2">{d.actualWorkHours ?? '—'}</td>
                      <td className="px-3 py-2">
                        <Badge variant="outline" className={STATUS_TONE[d.status] ?? ''}>
                          {d.status}
                        </Badge>
                      </td>
                      <td className="px-3 py-2 text-xs text-muted-foreground">
                        {d.isLate && (
                          <span className="mr-2 inline-flex items-center gap-1 text-amber-600">
                            <AlertTriangle className="h-3 w-3" /> late
                            {d.lateMinutes ? ` ${d.lateMinutes}m` : ''}
                          </span>
                        )}
                        {d.isOvertime && (
                          <span className="mr-2 inline-flex items-center gap-1">
                            <CheckCircle2 className="h-3 w-3" /> overtime
                          </span>
                        )}
                        {d.isRemoteWork && <span>remote</span>}
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">
            No attendance recorded this month yet — your first punch creates today&apos;s
            record.
          </p>
        )}
      </section>
    </div>
  );
}
