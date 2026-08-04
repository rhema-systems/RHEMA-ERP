'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  NumberField,
  DateField,
  TimeField,
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { dailyAttendanceService } from '@/services/hr/attendance.service';
import { workScheduleService, payPeriodService } from '@/services/hr/attendance-setup.service';
import { today } from '@/lib/hr/attendance-format';
import { ATTENDANCE_STATUS_OPTIONS } from '@/types/hr/attendance';

/**
 * Recording a day by hand.
 *
 * Normally a daily record is built from punches; this is the fallback for sites without
 * devices and for filling gaps. Derived flags (late minutes, overtime) are entered here
 * rather than computed, because the server only derives them when it processes real punches.
 */
const schema = z
  .object({
    employeeId: z.string().min(1, 'Select an employee'),
    attendanceDate: z.string().min(1, 'Required'),
    workScheduleId: z.string().optional(),
    payPeriodId: z.string().optional(),
    actualCheckInTime: z.string().optional(),
    actualCheckOutTime: z.string().optional(),
    actualWorkHours: z.coerce.number().min(0).max(24).optional(),
    status: z.enum([
      'Present',
      'Absent',
      'Late',
      'HalfDay',
      'OnLeave',
      'PublicHoliday',
      'Weekend',
      'OffDay',
      'RemoteWork',
      'OnDuty',
    ]),
    statusReason: z.string().max(500).optional(),
    isLate: z.boolean(),
    lateMinutes: z.coerce.number().min(0).max(1440).optional(),
    isEarlyDeparture: z.boolean(),
    earlyDepartureMinutes: z.coerce.number().min(0).max(1440).optional(),
    totalBreakMinutes: z.coerce.number().min(0).max(1440).optional(),
    isOvertime: z.boolean(),
    overtimeHours: z.coerce.number().min(0).max(24).optional(),
    isRemoteWork: z.boolean(),
    remoteWorkLocation: z.string().max(500).optional(),
    notes: z.string().max(1000).optional(),
  })
  .refine((v) => !v.isLate || (v.lateMinutes ?? 0) > 0, {
    message: 'Give the minutes late',
    path: ['lateMinutes'],
  })
  .refine((v) => !v.isOvertime || (v.overtimeHours ?? 0) > 0, {
    message: 'Give the overtime hours',
    path: ['overtimeHours'],
  });

type FormValues = z.input<typeof schema>;

export default function NewDailyAttendancePage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);

  const { data: schedules } = useQuery({
    queryKey: ['hr', 'work-schedules', 'active'],
    queryFn: () => workScheduleService.getActive(),
  });

  const { data: currentPeriod } = useQuery({
    queryKey: ['hr', 'pay-periods', 'current'],
    queryFn: () => payPeriodService.getCurrentOpen(),
  });

  const form = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: {
      employeeId: '',
      attendanceDate: today(),
      workScheduleId: '',
      payPeriodId: '',
      actualCheckInTime: '',
      actualCheckOutTime: '',
      actualWorkHours: undefined,
      status: 'Present',
      statusReason: '',
      isLate: false,
      lateMinutes: undefined,
      isEarlyDeparture: false,
      earlyDepartureMinutes: undefined,
      totalBreakMinutes: undefined,
      isOvertime: false,
      overtimeHours: undefined,
      isRemoteWork: false,
      remoteWorkLocation: '',
      notes: '',
    },
  });

  const scheduleOptions = (schedules ?? []).map((s) => ({ value: s.id, label: s.scheduleName }));

  const onSubmit = async (values: FormValues) => {
    setSaving(true);
    try {
      const v = schema.parse(values);
      const created = await dailyAttendanceService.create({
        employeeId: v.employeeId,
        attendanceDate: v.attendanceDate,
        workScheduleId: v.workScheduleId || null,
        // Default to the open period so the day rolls into the right monthly summary.
        payPeriodId: v.payPeriodId || currentPeriod?.id || null,
        actualCheckInTime: v.actualCheckInTime || null,
        actualCheckOutTime: v.actualCheckOutTime || null,
        actualWorkHours: v.actualWorkHours ?? null,
        status: v.status,
        statusReason: v.statusReason || null,
        isLate: v.isLate,
        lateMinutes: v.isLate ? (v.lateMinutes ?? null) : null,
        isEarlyDeparture: v.isEarlyDeparture,
        earlyDepartureMinutes: v.isEarlyDeparture ? (v.earlyDepartureMinutes ?? null) : null,
        totalBreakMinutes: v.totalBreakMinutes ?? null,
        isOvertime: v.isOvertime,
        overtimeHours: v.isOvertime ? (v.overtimeHours ?? null) : null,
        isRemoteWork: v.isRemoteWork,
        remoteWorkLocation: v.isRemoteWork ? v.remoteWorkLocation || null : null,
        notes: v.notes || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'daily-attendance'] });
      toast({ title: 'Recorded', description: 'The attendance record was created.' });
      router.push(`/hr/attendance/daily/${created.id}`);
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to record attendance.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const isLate = !!form.watch('isLate');
  const isEarly = !!form.watch('isEarlyDeparture');
  const isOvertime = !!form.watch('isOvertime');
  const isRemote = !!form.watch('isRemoteWork');

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Record Attendance"
        description="Enter a day by hand — for sites without devices, or to fill a gap."
        backHref="/hr/attendance/daily"
      />

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Who and when</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <EmployeePickerField form={form} name="employeeId" label="Employee" required />
            <FieldRow>
              <DateField form={form} name="attendanceDate" label="Date" required />
              <SelectField
                form={form}
                name="status"
                label="Status"
                required
                options={ATTENDANCE_STATUS_OPTIONS}
              />
            </FieldRow>
            <SelectField
              form={form}
              name="workScheduleId"
              label="Work schedule"
              options={scheduleOptions}
              allowEmpty
            />
            {currentPeriod && (
              <p className="text-xs text-muted-foreground">
                Will be assigned to the open pay period: {currentPeriod.periodName}.
              </p>
            )}
            <TextareaField form={form} name="statusReason" label="Status reason" rows={2} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Hours</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TimeField form={form} name="actualCheckInTime" label="Check in" />
              <TimeField form={form} name="actualCheckOutTime" label="Check out" />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="actualWorkHours" label="Hours worked" step="0.25" />
              <NumberField form={form} name="totalBreakMinutes" label="Break (minutes)" />
            </FieldRow>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Flags</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <SwitchField form={form} name="isLate" label="Late arrival" />
            {isLate && <NumberField form={form} name="lateMinutes" label="Minutes late" />}

            <SwitchField form={form} name="isEarlyDeparture" label="Early departure" />
            {isEarly && (
              <NumberField form={form} name="earlyDepartureMinutes" label="Minutes early" />
            )}

            <SwitchField form={form} name="isOvertime" label="Overtime worked" />
            {isOvertime && (
              <NumberField form={form} name="overtimeHours" label="Overtime hours" step="0.25" />
            )}

            <SwitchField form={form} name="isRemoteWork" label="Worked remotely" />
            {isRemote && (
              <TextField form={form} name="remoteWorkLocation" label="Remote location" />
            )}

            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push('/hr/attendance/daily')}
            disabled={saving}
          >
            Cancel
          </Button>
          <Button type="submit" disabled={saving}>
            {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Record attendance
          </Button>
        </div>
      </form>
    </div>
  );
}
