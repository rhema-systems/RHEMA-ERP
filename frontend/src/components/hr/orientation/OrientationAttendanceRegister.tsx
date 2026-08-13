'use client';

import { useEffect, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Save, ClipboardCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { orientationSessionService } from '@/services/hr/orientation-session.service';
import { employeeOrientationService } from '@/services/hr/employee-orientation.service';
import {
  ORIENTATION_ATTENDANCE_STATUS_OPTIONS,
  OCCUPYING_ENROLLMENT_STATUSES,
} from '@/types/hr/orientation';
import type {
  OrientationAttendanceStatus,
  OrientationAttendanceEntry,
} from '@/types/hr/orientation';

interface Props {
  sessionId: string;
  readOnly?: boolean;
}

interface RowState {
  enrollmentId: string;
  employeeName: string;
  employeeNumber: string;
  attendanceStatus: OrientationAttendanceStatus;
  checkInAt: string;
  checkOutAt: string;
  absenceReason: string;
}

/** `HH:mm` for a time input, from the ISO instant the API stores. */
const toTimeInput = (iso?: string | null) => {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
};

/**
 * Back to an ISO instant, hung off the date the register rows already carry.
 *
 * The register is keyed by session *day* rather than by date, so there is no calendar date on the
 * form to attach a time to — today's is used, which is what marking a register live means. A time
 * left blank stays null rather than becoming midnight.
 */
const fromTimeInput = (value: string): string | null => {
  if (!value) return null;
  const [h, m] = value.split(':').map(Number);
  if (Number.isNaN(h) || Number.isNaN(m)) return null;
  const d = new Date();
  d.setHours(h, m, 0, 0);
  return d.toISOString();
};

/**
 * One day of a session's register.
 *
 * The roll is the session's seat-occupying enrollments — a withdrawn or cancelled participant is not
 * on it — pre-filled with whatever was already recorded for the day being viewed, so re-opening it
 * shows what was marked and saving again is a correction rather than a second set of rows.
 *
 * A multi-day session is marked one day at a time: `sessionDay` is what the API upserts against, and
 * it is a counter (day 1, day 2), not a date.
 */
export function OrientationAttendanceRegister({ sessionId, readOnly }: Props) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [sessionDay, setSessionDay] = useState(1);
  const [rows, setRows] = useState<RowState[]>([]);
  const [saving, setSaving] = useState(false);

  const { data: enrollments, isLoading: loadingRoster } = useQuery({
    queryKey: ['hr', 'orientation-sessions', sessionId, 'enrollments'],
    queryFn: () => employeeOrientationService.getBySession(sessionId),
  });

  const { data: marked, isLoading: loadingMarked } = useQuery({
    queryKey: ['hr', 'orientation-sessions', sessionId, 'attendance'],
    queryFn: () => orientationSessionService.getAttendanceForSession(sessionId),
  });

  // Only people holding a seat are on the roll. The occupying set is shared with the server so a
  // withdrawn participant cannot be marked present on one screen and absent from capacity on another.
  const roll = useMemo(
    () =>
      (enrollments ?? []).filter((e) =>
        OCCUPYING_ENROLLMENT_STATUSES.includes(e.enrollmentStatus),
      ),
    [enrollments],
  );

  // Days already marked, so someone re-opening a finished session lands on a day that has rows
  // rather than a blank day 1.
  const markedDays = useMemo(
    () => Array.from(new Set((marked ?? []).map((m) => m.sessionDay))).sort((a, b) => a - b),
    [marked],
  );

  useEffect(() => {
    const forDay = new Map(
      (marked ?? []).filter((m) => m.sessionDay === sessionDay).map((m) => [m.enrollmentId, m]),
    );
    setRows(
      roll.map((e) => {
        const existing = forDay.get(e.id);
        return {
          enrollmentId: e.id,
          employeeName: e.employeeName ?? '—',
          employeeNumber: e.employeeNumber ?? '',
          // Default to Present: a register is quicker to correct than to fill from nothing.
          attendanceStatus: existing?.attendanceStatus ?? 'Present',
          checkInAt: toTimeInput(existing?.checkInAt),
          checkOutAt: toTimeInput(existing?.checkOutAt),
          absenceReason: existing?.absenceReason ?? '',
        };
      }),
    );
  }, [roll, marked, sessionDay]);

  const setRow = (enrollmentId: string, patch: Partial<RowState>) =>
    setRows((prev) =>
      prev.map((r) => (r.enrollmentId === enrollmentId ? { ...r, ...patch } : r)),
    );

  const save = async () => {
    setSaving(true);
    try {
      const entries: OrientationAttendanceEntry[] = rows.map((r) => {
        const away = r.attendanceStatus === 'Absent' || r.attendanceStatus === 'Excused';
        return {
          enrollmentId: r.enrollmentId,
          attendanceStatus: r.attendanceStatus,
          // Times are meaningless for someone who never arrived, and a reason is meaningless for
          // someone who did — neither is sent where it would only be noise.
          checkInAt: away ? null : fromTimeInput(r.checkInAt),
          checkOutAt: away ? null : fromTimeInput(r.checkOutAt),
          markedLate: r.attendanceStatus === 'Late',
          absenceReason: away ? r.absenceReason || null : null,
        };
      });

      await orientationSessionService.markAttendance(sessionId, {
        sessionId,
        sessionDay,
        entries,
      });
      await queryClient.invalidateQueries({
        queryKey: ['hr', 'orientation-sessions', sessionId, 'attendance'],
      });
      toast({
        title: 'Register saved',
        description: `${entries.length} record${entries.length === 1 ? '' : 's'} for day ${sessionDay}.`,
      });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to save the register.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const loading = loadingRoster || loadingMarked;
  const presentCount = rows.filter(
    (r) => r.attendanceStatus === 'Present' || r.attendanceStatus === 'Late' || r.attendanceStatus === 'Partial',
  ).length;

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <CardTitle>Attendance register</CardTitle>
            <CardDescription>
              Seat-holding participants for one day of this session. Saving the same day again
              corrects it rather than adding to it.
            </CardDescription>
          </div>
          <div className="flex items-end gap-2">
            <div className="space-y-2">
              <Label htmlFor="session-day">Session day</Label>
              <Input
                id="session-day"
                type="number"
                min={1}
                value={sessionDay}
                onChange={(e) => setSessionDay(Math.max(1, Number(e.target.value) || 1))}
                className="w-28"
              />
            </div>
            {!readOnly && rows.length > 0 && (
              <Button onClick={save} disabled={saving}>
                {saving ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Save className="mr-2 h-4 w-4" />
                )}
                Save day {sessionDay}
              </Button>
            )}
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <div className="mb-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
          {rows.length > 0 && (
            <p className="text-muted-foreground">
              {presentCount} attending, {rows.length - presentCount} not, of {rows.length} on the
              roll.
            </p>
          )}
          {markedDays.length > 0 && (
            <p className="text-muted-foreground">
              Already marked: {markedDays.map((d) => `day ${d}`).join(', ')}.
            </p>
          )}
        </div>

        <div className="overflow-x-auto rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Participant</TableHead>
                <TableHead className="w-[160px]">Status</TableHead>
                <TableHead className="w-[120px]">In</TableHead>
                <TableHead className="w-[120px]">Out</TableHead>
                <TableHead>Absence reason</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loading ? (
                [...Array(3)].map((_, i) => (
                  <TableRow key={i}>
                    {[...Array(5)].map((__, j) => (
                      <TableCell key={j}>
                        <Skeleton className="h-4 w-[100px]" />
                      </TableCell>
                    ))}
                  </TableRow>
                ))
              ) : rows.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5}>
                    <EmptyState
                      icon={ClipboardCheck}
                      title="Nobody to mark"
                      description="The register lists participants holding a seat on this session. Enrol someone first."
                    />
                  </TableCell>
                </TableRow>
              ) : (
                rows.map((r) => {
                  const away =
                    r.attendanceStatus === 'Absent' || r.attendanceStatus === 'Excused';
                  return (
                    <TableRow key={r.enrollmentId}>
                      <TableCell className="font-medium">
                        {r.employeeName}
                        {r.employeeNumber && (
                          <div className="text-muted-foreground text-xs">{r.employeeNumber}</div>
                        )}
                      </TableCell>
                      <TableCell>
                        <Select
                          value={r.attendanceStatus}
                          disabled={readOnly}
                          onValueChange={(v) =>
                            setRow(r.enrollmentId, {
                              attendanceStatus: v as OrientationAttendanceStatus,
                            })
                          }
                        >
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {ORIENTATION_ATTENDANCE_STATUS_OPTIONS.map((o) => (
                              <SelectItem key={o.value} value={o.value}>
                                {o.label}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </TableCell>
                      <TableCell>
                        <Input
                          type="time"
                          value={r.checkInAt}
                          disabled={readOnly || away}
                          onChange={(e) =>
                            setRow(r.enrollmentId, { checkInAt: e.target.value })
                          }
                        />
                      </TableCell>
                      <TableCell>
                        <Input
                          type="time"
                          value={r.checkOutAt}
                          disabled={readOnly || away}
                          onChange={(e) =>
                            setRow(r.enrollmentId, { checkOutAt: e.target.value })
                          }
                        />
                      </TableCell>
                      <TableCell>
                        <Input
                          value={r.absenceReason}
                          disabled={readOnly || !away}
                          placeholder={away ? 'Why were they away?' : '—'}
                          onChange={(e) =>
                            setRow(r.enrollmentId, { absenceReason: e.target.value })
                          }
                        />
                      </TableCell>
                    </TableRow>
                  );
                })
              )}
            </TableBody>
          </Table>
        </div>
      </CardContent>
    </Card>
  );
}
