'use client';

import { useMemo, useState } from 'react';
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
import { DateField, TextareaField, SelectField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  consultantClientService,
  clientEngagementService,
  consultantTimesheetService,
} from '@/services/hr/consultant.service';

const schema = z
  .object({
    consultantId: z.string().min(1, 'Select a consultant'),
    clientId: z.string().min(1, 'Select a client'),
    engagementId: z.string().optional(),
    periodStartDate: z.string().min(1, 'Required'),
    periodEndDate: z.string().min(1, 'Required'),
    notes: z.string().max(1000).optional(),
  })
  .refine((v) => v.periodEndDate >= v.periodStartDate, {
    message: 'The end date cannot be before the start date',
    path: ['periodEndDate'],
  });

type FormValues = z.input<typeof schema>;

/**
 * Creating a timesheet.
 *
 * The engagement is optional on the API but effectively required for billing — it is where
 * the hourly rate comes from — so the picker is narrowed to that client's engagements and
 * the consequence of leaving it blank is spelled out.
 */
export default function NewConsultantTimesheetPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema) as any,
    defaultValues: {
      consultantId: '',
      clientId: '',
      engagementId: '',
      periodStartDate: '',
      periodEndDate: '',
      notes: '',
    },
  });

  const clientId = form.watch('clientId');

  const { data: clients } = useQuery({
    queryKey: ['hr', 'consultant-clients', 'active'],
    queryFn: () => consultantClientService.getActive(),
  });

  const { data: engagements } = useQuery({
    queryKey: ['hr', 'client-engagements', 'by-client', clientId],
    queryFn: () => clientEngagementService.getByClient(clientId),
    enabled: !!clientId,
  });

  const clientOptions = useMemo(
    () => (clients ?? []).map((c) => ({ value: c.id, label: `${c.clientName} (${c.clientCode})` })),
    [clients],
  );

  const engagementOptions = useMemo(
    () =>
      (engagements ?? [])
        .filter((e) => e.status === 'Active')
        .map((e) => ({
          value: e.id,
          label: `${e.engagementCode} · ${e.title} · ${e.consultantName}`,
        })),
    [engagements],
  );

  const onSubmit = async (values: FormValues) => {
    setSaving(true);
    try {
      const v = schema.parse(values);
      const created = await consultantTimesheetService.create({
        consultantId: v.consultantId,
        clientId: v.clientId,
        engagementId: v.engagementId || null,
        periodStartDate: v.periodStartDate,
        periodEndDate: v.periodEndDate,
        notes: v.notes || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'consultant-timesheets'] });
      toast({
        title: 'Created',
        description: `${created.timesheetNumber} created as a draft — add the day entries next.`,
      });
      router.push(`/hr/consulting/timesheets/${created.id}`);
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to create the timesheet.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Timesheet"
        description="Open a timesheet for a billing period, then log the days worked."
        backHref="/hr/consulting/timesheets"
      />

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Who and where</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <EmployeePickerField form={form} name="consultantId" label="Consultant" required />
            <SelectField
              form={form}
              name="clientId"
              label="Client"
              required
              options={clientOptions}
              placeholder={clientOptions.length ? 'Select a client…' : 'No active clients'}
            />
            <SelectField
              form={form}
              name="engagementId"
              label="Engagement"
              options={engagementOptions}
              allowEmpty
              emptyLabel="No engagement"
              placeholder={
                !clientId
                  ? 'Choose a client first'
                  : engagementOptions.length
                    ? 'Select an engagement…'
                    : 'No active engagements for this client'
              }
            />
            <p className="text-xs text-muted-foreground">
              The engagement supplies the hourly rate used when invoicing. Without one, the rate
              has to be entered by hand on the invoice.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Billing period</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <DateField form={form} name="periodStartDate" label="Period start" required />
              <DateField form={form} name="periodEndDate" label="Period end" required />
            </FieldRow>
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push('/hr/consulting/timesheets')}
            disabled={saving}
          >
            Cancel
          </Button>
          <Button type="submit" disabled={saving}>
            {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Create draft
          </Button>
        </div>
      </form>
    </div>
  );
}
