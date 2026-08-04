'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQueryClient } from '@tanstack/react-query';
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
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { overtimeRequestService } from '@/services/hr/attendance.service';
import { OVERTIME_TYPE_OPTIONS } from '@/types/hr/attendance';

const schema = z.object({
  employeeId: z.string().min(1, 'Select an employee'),
  overtimeDate: z.string().min(1, 'Required'),
  plannedStartTime: z.string().min(1, 'Required'),
  plannedEndTime: z.string().min(1, 'Required'),
  plannedOvertimeHours: z.coerce.number().min(0.5).max(24),
  purpose: z.string().min(1, 'A purpose is required').max(1000),
  taskDetails: z.string().max(2000).optional(),
  type: z.enum(['Weekday', 'Weekend', 'Holiday', 'Emergency']),
});

type FormValues = z.input<typeof schema>;

/** Minutes between two `HH:mm:ss` times, wrapping past midnight for night work. */
function hoursBetween(start: string, end: string): number | null {
  if (!start || !end) return null;
  const toMinutes = (t: string) => {
    const [h, m] = t.split(':').map(Number);
    if (Number.isNaN(h) || Number.isNaN(m)) return null;
    return h * 60 + m;
  };
  const s = toMinutes(start);
  const e = toMinutes(end);
  if (s === null || e === null) return null;
  const span = e >= s ? e - s : e + 24 * 60 - s;
  return Math.round((span / 60) * 100) / 100;
}

export default function NewOvertimeRequestPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: {
      employeeId: '',
      overtimeDate: '',
      plannedStartTime: '17:00:00',
      plannedEndTime: '20:00:00',
      plannedOvertimeHours: 3,
      purpose: '',
      taskDetails: '',
      type: 'Weekday',
    },
  });

  const start = form.watch('plannedStartTime');
  const end = form.watch('plannedEndTime');

  // Keep the hours in step with the window, so the two cannot silently disagree.
  useEffect(() => {
    const computed = hoursBetween(start as string, end as string);
    if (computed !== null && computed > 0) {
      form.setValue('plannedOvertimeHours', computed, { shouldValidate: false });
    }
  }, [start, end, form]);

  const onSubmit = async (values: FormValues) => {
    setSaving(true);
    try {
      const v = schema.parse(values);
      const created = await overtimeRequestService.create({
        employeeId: v.employeeId,
        // The API takes a DateTime here rather than a DateOnly.
        overtimeDate: `${v.overtimeDate}T00:00:00`,
        plannedStartTime: v.plannedStartTime,
        plannedEndTime: v.plannedEndTime,
        plannedOvertimeHours: v.plannedOvertimeHours,
        purpose: v.purpose,
        taskDetails: v.taskDetails || null,
        type: v.type,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'overtime-requests'] });
      toast({
        title: 'Submitted',
        description: `${created.requestNumber} was raised and sent for approval.`,
      });
      router.push(`/hr/attendance/overtime/${created.id}`);
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to raise the overtime request.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Overtime Request"
        description="Ask for overtime to be pre-approved before it is worked."
        backHref="/hr/attendance/overtime"
      />

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">When</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <EmployeePickerField form={form} name="employeeId" label="Employee" required />
            <FieldRow>
              <DateField form={form} name="overtimeDate" label="Overtime date" required />
              <SelectField
                form={form}
                name="type"
                label="Type"
                required
                options={OVERTIME_TYPE_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <TimeField form={form} name="plannedStartTime" label="Planned start" required />
              <TimeField form={form} name="plannedEndTime" label="Planned end" required />
            </FieldRow>
            <NumberField
              form={form}
              name="plannedOvertimeHours"
              label="Planned hours"
              step="0.25"
              required
            />
            <p className="text-xs text-muted-foreground">
              Calculated from the window above; adjust it if breaks make the paid time shorter.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Why</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <TextareaField form={form} name="purpose" label="Purpose" rows={3} />
            <TextareaField form={form} name="taskDetails" label="Task details" rows={3} />
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push('/hr/attendance/overtime')}
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
