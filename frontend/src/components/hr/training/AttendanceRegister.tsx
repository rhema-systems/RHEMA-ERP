'use client';

import { useEffect, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Save, ClipboardCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { trainingNominationService } from '@/services/hr/training-nomination.service';
import type { AttendanceEntry } from '@/types/hr/training-delivery';

interface Props {
  scheduleId: string;
  /** Defaults the register to the schedule's start date rather than today. */
  defaultDate: string;
  readOnly?: boolean;
}

interface RowState {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  isPresent: boolean;
  absenceReason: string;
}

/**
 * A day's register. The roll is built from the schedule's confirmed nominees, pre-filled with any
 * attendance already marked for that date — so re-opening the register shows what was recorded
 * rather than a blank sheet, and re-saving is a correction rather than a duplicate.
 */
export function AttendanceRegister({ scheduleId, defaultDate, readOnly }: Props) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [date, setDate] = useState(defaultDate.slice(0, 10));
  const [rows, setRows] = useState<RowState[]>([]);
  const [saving, setSaving] = useState(false);

  const { data: nominees, isLoading: loadingNominees } = useQuery({
    queryKey: ['hr', 'training', 'schedules', scheduleId, 'nominees'],
    queryFn: () => trainingNominationService.getBySchedule(scheduleId),
  });

  const { data: marked, isLoading: loadingMarked } = useQuery({
    queryKey: ['hr', 'training', 'schedules', scheduleId, 'attendance', date],
    queryFn: () => trainingNominationService.getAttendanceByDate(scheduleId, new Date(date).toISOString()),
    enabled: !!date,
  });

  // Only people actually going are on the roll — drafts, rejections and withdrawals are not.
  const attending = useMemo(
    () => (nominees ?? []).filter((n) => n.status === 'Approved' || n.status === 'Confirmed'),
    [nominees],
  );

  useEffect(() => {
    if (!attending.length) {
      setRows([]);
      return;
    }
    const byEmployee = new Map((marked ?? []).map((m) => [m.employeeId, m]));
    setRows(
      attending.map((n) => {
        const existing = byEmployee.get(n.employeeId);
        return {
          employeeId: n.employeeId,
          employeeName: n.employeeName,
          employeeNumber: n.employeeNumber,
          // Default to present: a register is quicker to correct than to fill in from nothing.
          isPresent: existing ? existing.isPresent : true,
          absenceReason: existing?.absenceReason ?? '',
        };
      }),
    );
  }, [attending, marked]);

  const setRow = (employeeId: string, patch: Partial<RowState>) =>
    setRows((prev) => prev.map((r) => (r.employeeId === employeeId ? { ...r, ...patch } : r)));

  const save = async () => {
    setSaving(true);
    try {
      const entries: AttendanceEntry[] = rows.map((r) => ({
        employeeId: r.employeeId,
        isPresent: r.isPresent,
        absenceReason: r.isPresent ? null : r.absenceReason || null,
      }));
      await trainingNominationService.bulkMarkAttendance(scheduleId, new Date(date).toISOString(), entries);
      await queryClient.invalidateQueries({
        queryKey: ['hr', 'training', 'schedules', scheduleId, 'attendance'],
      });
      toast({ title: 'Register saved', description: `${entries.length} record(s) for ${date}.` });
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

  const loading = loadingNominees || loadingMarked;
  const presentCount = rows.filter((r) => r.isPresent).length;

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <CardTitle>Attendance register</CardTitle>
            <CardDescription>
              Confirmed nominees for one day. Saving again corrects the same day rather than adding to it.
            </CardDescription>
          </div>
          <div className="flex items-end gap-2">
            <div className="space-y-2">
              <Label htmlFor="register-date">Date</Label>
              <Input
                id="register-date"
                type="date"
                value={date}
                onChange={(e) => setDate(e.target.value)}
                className="w-44"
              />
            </div>
            {!readOnly && rows.length > 0 && (
              <Button onClick={save} disabled={saving}>
                {saving ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Save className="mr-2 h-4 w-4" />
                )}
                Save register
              </Button>
            )}
          </div>
        </div>
      </CardHeader>
      <CardContent>
        {rows.length > 0 && (
          <p className="mb-3 text-sm text-muted-foreground">
            {presentCount} present, {rows.length - presentCount} absent, of {rows.length} on the roll.
          </p>
        )}
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Employee</TableHead>
                <TableHead className="w-[120px]">Present</TableHead>
                <TableHead>Absence reason</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loading ? (
                [...Array(3)].map((_, i) => (
                  <TableRow key={i}>
                    {[...Array(3)].map((__, j) => (
                      <TableCell key={j}>
                        <Skeleton className="h-4 w-[120px]" />
                      </TableCell>
                    ))}
                  </TableRow>
                ))
              ) : rows.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={3}>
                    <EmptyState
                      icon={ClipboardCheck}
                      title="Nobody to mark"
                      description="The register lists approved and confirmed nominees only. Approve nominations first."
                    />
                  </TableCell>
                </TableRow>
              ) : (
                rows.map((r) => (
                  <TableRow key={r.employeeId}>
                    <TableCell className="font-medium">
                      {r.employeeName}
                      <div className="text-xs text-muted-foreground">{r.employeeNumber}</div>
                    </TableCell>
                    <TableCell>
                      <Switch
                        checked={r.isPresent}
                        disabled={readOnly}
                        onCheckedChange={(v) => setRow(r.employeeId, { isPresent: v })}
                      />
                    </TableCell>
                    <TableCell>
                      <Input
                        value={r.absenceReason}
                        disabled={readOnly || r.isPresent}
                        placeholder={r.isPresent ? '—' : 'Why were they away?'}
                        onChange={(e) => setRow(r.employeeId, { absenceReason: e.target.value })}
                      />
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>
      </CardContent>
    </Card>
  );
}
