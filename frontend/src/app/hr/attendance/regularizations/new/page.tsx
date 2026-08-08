'use client';

import { useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TimeField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { dailyAttendanceService, regularizationService } from '@/services/hr/attendance.service';
import { formatDate, formatTime, today } from '@/lib/hr/attendance-format';
import { REGULARIZATION_TYPE_OPTIONS } from '@/types/hr/attendance';

/**
 * Raising a regularization.
 *
 * A regularization corrects one specific daily-attendance row, so `attendanceId` is
 * required. It can arrive as a query parameter (from the attendance record itself), and
 * otherwise the employee and date are used to look the record up — there is no way to
 * regularize a day that was never recorded.
 */
const schema = z
  .object({
    employeeId: z.string().min(1, 'Select an employee'),
    attendanceId: z.string().min(1, 'Pick the day to correct'),
    type: z.enum([
      'MissingCheckIn',
      'MissingCheckOut',
      'WrongTimeEntry',
      'ForgotToMark',
      'SystemError',
    ]),
    requestedCheckInTime: z.string().optional(),
    requestedCheckOutTime: z.string().optional(),
    reason: z.string().min(1, 'A reason is required').max(1000),
    supportingDocuments: z.string().max(2000).optional(),
  })
  .refine((v) => !!v.requestedCheckInTime || !!v.requestedCheckOutTime, {
    message: 'Give at least one corrected time',
    path: ['requestedCheckInTime'],
  });

type FormValues = z.input<typeof schema>;

export default function NewRegularizationPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);
  const [lookupDate, setLookupDate] = useState(today());

  const presetAttendanceId = searchParams?.get('attendanceId') ?? '';

  const form = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: {
      employeeId: '',
      attendanceId: presetAttendanceId,
      type: 'MissingCheckIn',
      requestedCheckInTime: '',
      requestedCheckOutTime: '',
      reason: '',
      supportingDocuments: '',
    },
  });

  const employeeId = form.watch('employeeId');
  const attendanceId = form.watch('attendanceId');

  // Resolve the day being corrected from employee + date, unless one was passed in.
  const { data: lookedUp, isFetching: looking } = useQuery({
    queryKey: ['hr', 'daily-attendance', 'lookup', employeeId, lookupDate],
    queryFn: () => dailyAttendanceService.getByEmployeeAndDate(employeeId, lookupDate),
    enabled: !!employeeId && !presetAttendanceId,
  });

  const { data: preset } = useQuery({
    queryKey: ['hr', 'daily-attendance', presetAttendanceId],
    queryFn: () => dailyAttendanceService.getById(presetAttendanceId),
    enabled: !!presetAttendanceId,
  });

  const targetDay = preset ?? lookedUp ?? null;

  const onSubmit = async (values: FormValues) => {
    setSaving(true);
    try {
      const v = schema.parse(values);
      const created = await regularizationService.create({
        employeeId: v.employeeId,
        attendanceId: v.attendanceId,
        type: v.type,
        requestedCheckInTime: v.requestedCheckInTime || null,
        requestedCheckOutTime: v.requestedCheckOutTime || null,
        reason: v.reason,
        supportingDocuments: v.supportingDocuments || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'regularizations'] });
      toast({
        title: 'Submitted',
        description: `${created.regularizationNumber} was raised and sent for approval.`,
      });
      router.push(`/hr/attendance/regularizations/${created.id}`);
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to raise the regularization.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Regularization"
        description="Ask for a recorded day's times to be corrected. Approval runs through the workflow."
        backHref="/hr/attendance/regularizations"
      />

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Which day</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {presetAttendanceId ? (
              <p className="text-sm text-muted-foreground">
                Correcting{' '}
                <span className="font-medium text-foreground">
                  {preset ? formatDate(preset.attendanceDate) : 'the selected day'}
                </span>
                {preset ? ` for ${preset.employeeName}` : ''}.
              </p>
            ) : (
              <>
                <EmployeePickerField
                  form={form}
                  name="employeeId"
                  label="Employee"
                  required
                />
                <div className="space-y-2">
                  <Label htmlFor="lookupDate">Attendance date</Label>
                  <Input
                    id="lookupDate"
                    type="date"
                    value={lookupDate}
                    onChange={(e) => {
                      setLookupDate(e.target.value);
                      form.setValue('attendanceId', '', { shouldValidate: false });
                    }}
                  />
                </div>

                {employeeId && (
                  <div className="rounded-md border p-3 text-sm">
                    {looking ? (
                      <span className="flex items-center gap-2 text-muted-foreground">
                        <Loader2 className="h-4 w-4 animate-spin" /> Looking up that day…
                      </span>
                    ) : lookedUp ? (
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <span>
                          {formatDate(lookedUp.attendanceDate)} · in{' '}
                          {formatTime(lookedUp.actualCheckInTime)} · out{' '}
                          {formatTime(lookedUp.actualCheckOutTime)} · {lookedUp.status}
                        </span>
                        <Button
                          type="button"
                          size="sm"
                          variant={attendanceId ? 'outline' : 'default'}
                          onClick={() =>
                            form.setValue('attendanceId', lookedUp.id, { shouldValidate: true })
                          }
                        >
                          {attendanceId ? 'Selected' : 'Use this day'}
                        </Button>
                      </div>
                    ) : (
                      <span className="text-muted-foreground">
                        No attendance was recorded for that date. A regularization can only
                        correct an existing record.
                      </span>
                    )}
                  </div>
                )}

                {(form.formState.errors as any)?.attendanceId?.message && (
                  <p className="text-sm text-red-500">
                    {(form.formState.errors as any).attendanceId.message}
                  </p>
                )}
              </>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">The correction</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <SelectField
              form={form}
              name="type"
              label="What went wrong"
              required
              options={REGULARIZATION_TYPE_OPTIONS}
            />
            <FieldRow>
              <TimeField form={form} name="requestedCheckInTime" label="Corrected check-in" />
              <TimeField form={form} name="requestedCheckOutTime" label="Corrected check-out" />
            </FieldRow>
            {targetDay && (
              <p className="text-xs text-muted-foreground">
                Currently recorded as in {formatTime(targetDay.actualCheckInTime)}, out{' '}
                {formatTime(targetDay.actualCheckOutTime)}.
              </p>
            )}
            <TextareaField form={form} name="reason" label="Reason" rows={3} />
            <TextareaField
              form={form}
              name="supportingDocuments"
              label="Supporting documents"
              rows={2}
              placeholder="References to any evidence provided"
            />
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push('/hr/attendance/regularizations')}
            disabled={saving}
          >
            Cancel
          </Button>
          <Button type="submit" disabled={saving}>
            {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Submit for approval
          </Button>
        </div>
      </form>
    </div>
  );
}
