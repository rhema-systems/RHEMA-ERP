'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { z } from 'zod';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  TextareaField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyReturnToWorkService } from '@/services/hr/safety-return-to-work.service';

/**
 * Opens a return-to-work plan. The plan number is a user-assigned code and must be unique —
 * a duplicate is refused. New plans start as Pending Medical Clearance; the clearance,
 * phases and reviews are recorded on the detail page. Linking the originating safety
 * incident is done by pasting its id (an incident picker is a later nicety).
 */
const planSchema = z.object({
  planNumber: z.string().min(1, 'A plan number is required').max(30),
  employeeId: z.string().min(1, 'An employee is required'),
  safetyIncidentId: z.string().optional().or(z.literal('')),
  planDate: z.string().min(1, 'A plan date is required'),
  plannedReturnDate: z.string().optional().or(z.literal('')),
  medicalRestrictions: z.string().max(1000).optional().or(z.literal('')),
  requiresWorkplaceModifications: z.boolean(),
  workplaceModificationsDescription: z.string().max(1000).optional().or(z.literal('')),
  coordinatorId: z.string().optional().or(z.literal('')),
  supervisorId: z.string().optional().or(z.literal('')),
});

type PlanForm = z.input<typeof planSchema>;

const blank = (v?: string) => (v && v.length > 0 ? v : null);
const dateOrNull = (v?: string) => (v && v.length > 0 ? new Date(v).toISOString() : null);

export default function NewReturnToWorkPlanPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);

  const form = useForm<PlanForm>({
    resolver: zodResolver(planSchema),
    defaultValues: {
      planNumber: '',
      employeeId: '',
      safetyIncidentId: '',
      planDate: new Date().toISOString().slice(0, 10),
      plannedReturnDate: '',
      medicalRestrictions: '',
      requiresWorkplaceModifications: false,
      workplaceModificationsDescription: '',
      coordinatorId: '',
      supervisorId: '',
    },
  });

  const submit = form.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = planSchema.parse(values);
      const created = await safetyReturnToWorkService.create({
        planNumber: v.planNumber,
        employeeId: v.employeeId,
        safetyIncidentId: blank(v.safetyIncidentId),
        planDate: new Date(v.planDate).toISOString(),
        plannedReturnDate: dateOrNull(v.plannedReturnDate),
        medicalRestrictions: blank(v.medicalRestrictions),
        requiresWorkplaceModifications: v.requiresWorkplaceModifications,
        workplaceModificationsDescription: blank(v.workplaceModificationsDescription),
        coordinatorId: blank(v.coordinatorId),
        supervisorId: blank(v.supervisorId),
      });
      toast({
        title: 'Plan opened',
        description: `${created.planNumber} starts as Pending Medical Clearance.`,
      });
      router.push(`/hr/safety/return-to-work/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Opening the plan failed.',
        variant: 'destructive',
      });
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Return-to-Work Plan"
        description="Opens as Pending Medical Clearance — record the clearance and phases on the detail page."
        backHref="/hr/safety/return-to-work"
      />

      <form onSubmit={submit}>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Plan</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TextField
                form={form}
                name="planNumber"
                label="Plan number (unique, e.g. RTW-2026-001)"
                required
              />
              <DateField form={form} name="planDate" label="Plan date" required />
            </FieldRow>
            <FieldRow>
              <EmployeePickerField form={form} name="employeeId" label="Returning employee" required />
              <DateField form={form} name="plannedReturnDate" label="Planned return date" />
            </FieldRow>
            <TextField
              form={form}
              name="safetyIncidentId"
              label="Originating safety incident id (optional)"
            />
            <TextareaField
              form={form}
              name="medicalRestrictions"
              label="Medical restrictions"
              rows={2}
            />
            <SwitchField
              form={form}
              name="requiresWorkplaceModifications"
              label="Requires workplace modifications"
            />
            <TextareaField
              form={form}
              name="workplaceModificationsDescription"
              label="Modifications description"
              rows={2}
            />
            <FieldRow>
              <EmployeePickerField form={form} name="coordinatorId" label="Coordinator" />
              <EmployeePickerField form={form} name="supervisorId" label="Supervisor" />
            </FieldRow>
            <div className="flex justify-end gap-2">
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => router.push('/hr/safety/return-to-work')}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Open plan
              </Button>
            </div>
          </CardContent>
        </Card>
      </form>
    </div>
  );
}
