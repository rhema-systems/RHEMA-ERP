'use client';

import { useState } from 'react';
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
  TextField,
  DateField,
  TextareaField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { remoteWorkRequestService } from '@/services/hr/attendance.service';

const schema = z
  .object({
    employeeId: z.string().min(1, 'Select an employee'),
    startDate: z.string().min(1, 'Required'),
    endDate: z.string().min(1, 'Required'),
    reason: z.string().min(1, 'A reason is required').max(1000),
    remoteLocation: z.string().max(500).optional(),
    equipmentConfirmed: z.boolean(),
  })
  .refine((v) => v.endDate >= v.startDate, {
    message: 'The end date cannot be before the start date',
    path: ['endDate'],
  });

type FormValues = z.input<typeof schema>;

export default function NewRemoteWorkRequestPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: {
      employeeId: '',
      startDate: '',
      endDate: '',
      reason: '',
      remoteLocation: '',
      equipmentConfirmed: false,
    },
  });

  const onSubmit = async (values: FormValues) => {
    setSaving(true);
    try {
      const v = schema.parse(values);
      const created = await remoteWorkRequestService.create({
        employeeId: v.employeeId,
        startDate: v.startDate,
        endDate: v.endDate,
        reason: v.reason,
        remoteLocation: v.remoteLocation || null,
        equipmentConfirmed: v.equipmentConfirmed,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'remote-work-requests'] });
      toast({
        title: 'Submitted',
        description: `${created.requestNumber} was raised and sent for approval.`,
      });
      router.push(`/hr/attendance/remote-work/${created.id}`);
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to raise the request.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Remote Work Request"
        description="Ask to work from home or another location for a period."
        backHref="/hr/attendance/remote-work"
      />

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Request</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <EmployeePickerField form={form} name="employeeId" label="Employee" required />
            <FieldRow>
              <DateField form={form} name="startDate" label="From" required />
              <DateField form={form} name="endDate" label="To" required />
            </FieldRow>
            <TextField
              form={form}
              name="remoteLocation"
              label="Remote location"
              placeholder="e.g. Home — Accra"
            />
            <TextareaField form={form} name="reason" label="Reason" rows={3} />
            <SwitchField
              form={form}
              name="equipmentConfirmed"
              label="Equipment confirmed"
              description="The employee has confirmed a suitable setup: connectivity, workspace and any kit they need."
            />
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push('/hr/attendance/remote-work')}
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
