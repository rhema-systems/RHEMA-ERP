'use client';

import { useState } from 'react';
import { z } from 'zod';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  NumberField,
  DateField,
  TimeField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { attendanceRecordService } from '@/services/hr/attendance.service';
import { formatDate, formatTime, formatHours, today } from '@/lib/hr/attendance-format';
import { ATTENDANCE_STATUS_OPTIONS } from '@/types/hr/attendance';
import type { StaffAttendanceRecordSummary } from '@/types/hr/attendance';

/**
 * The lightweight clock-in / clock-out register.
 *
 * This is a separate, simpler table from daily attendance — no schedules, geofencing or
 * exceptions, just a time in, a time out and a status. It suits sites that record hours by
 * hand; the fuller picture lives under Daily Attendance.
 */
const recordSchema = z
  .object({
    employeeId: z.string().min(1, 'Select an employee'),
    date: z.string().min(1, 'Required'),
    checkInTime: z.string().optional(),
    checkOutTime: z.string().optional(),
    workedHours: z.coerce.number().min(0).max(24).optional(),
    overtimeHours: z.coerce.number().min(0).max(24).optional(),
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
    notes: z.string().max(1000).optional(),
  })
  .refine(
    (v) => !v.checkInTime || !v.checkOutTime || v.checkOutTime !== v.checkInTime,
    { message: 'Check-out cannot equal check-in', path: ['checkOutTime'] },
  );

type RecordForm = z.input<typeof recordSchema>;

const emptyRecord: RecordForm = {
  employeeId: '',
  date: today(),
  checkInTime: '',
  checkOutTime: '',
  workedHours: undefined,
  overtimeHours: undefined,
  status: 'Present',
  notes: '',
};

export default function AttendanceRecordsPage() {
  const [date, setDate] = useState(today());

  const toPayload = (values: RecordForm) => {
    const v = recordSchema.parse(values);
    return {
      ...v,
      checkInTime: v.checkInTime || null,
      checkOutTime: v.checkOutTime || null,
      workedHours: v.workedHours ?? null,
      overtimeHours: v.overtimeHours ?? null,
      notes: v.notes || null,
    };
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Attendance Records"
        description="The simple clock-in / clock-out register, one row per employee per day."
        backHref="/hr/attendance"
      />

      <Card>
        <CardHeader>
          <CardTitle>Date</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="max-w-xs space-y-2">
            <label className="text-sm font-medium">Showing</label>
            <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          </div>
        </CardContent>
      </Card>

      <ResourceListPanel<StaffAttendanceRecordSummary, RecordForm>
        title="records"
        singular="record"
        // The date is in the key so changing it refetches instead of showing the old day.
        queryKey={['hr', 'attendance-records', date]}
        dialogHint="For the fuller picture — schedules, geofencing, exceptions — use Daily Attendance instead."
        emptyDescription={`Nothing recorded for ${formatDate(date)}.`}
        list={() => attendanceRecordService.getByDate(date)}
        create={(values) => attendanceRecordService.create(toPayload(values) as any)}
        update={(id, values) => {
          const v = toPayload(values);
          // The update DTO does not take the employee or the date — those identify the row.
          return attendanceRecordService.update(id, {
            id,
            checkInTime: v.checkInTime,
            checkOutTime: v.checkOutTime,
            workedHours: v.workedHours,
            overtimeHours: v.overtimeHours,
            status: v.status,
            notes: v.notes,
          });
        }}
        remove={(id) => attendanceRecordService.remove(id)}
        getId={(r) => r.id}
        columns={[
          { header: 'Employee', cell: (r) => <span className="font-medium">{r.employeeName}</span> },
          { header: 'Date', cell: (r) => formatDate(r.date) },
          { header: 'In', cell: (r) => formatTime(r.checkInTime) },
          { header: 'Out', cell: (r) => formatTime(r.checkOutTime) },
          {
            header: 'Worked',
            cell: (r) => formatHours(r.workedHours),
            className: 'text-right',
          },
          { header: 'Status', cell: (r) => <StatusBadge status={r.status} /> },
        ]}
        schema={recordSchema as any}
        emptyForm={{ ...emptyRecord, date }}
        toForm={(r) => ({
          ...emptyRecord,
          employeeId: r.employeeId,
          date: r.date,
          checkInTime: r.checkInTime ?? '',
          checkOutTime: r.checkOutTime ?? '',
          workedHours: r.workedHours ?? undefined,
          status: r.status,
        })}
        renderFields={(form) => (
          <>
            <EmployeePickerField form={form} name="employeeId" label="Employee" required />
            <FieldRow>
              <DateField form={form} name="date" label="Date" required />
              <SelectField
                form={form}
                name="status"
                label="Status"
                required
                options={ATTENDANCE_STATUS_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <TimeField form={form} name="checkInTime" label="Check in" />
              <TimeField form={form} name="checkOutTime" label="Check out" />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="workedHours" label="Worked hours" step="0.25" />
              <NumberField form={form} name="overtimeHours" label="Overtime hours" step="0.25" />
            </FieldRow>
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </>
        )}
      />
    </div>
  );
}
