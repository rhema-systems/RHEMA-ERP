'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { NumberField, TextareaField } from '@/components/hr/employee/tabs/fields';
import { OrganizationScopeFields } from '@/components/hr/performance/OrganizationScopeFields';

export const trainingPlanSchema = z.object({
  year: z.coerce.number().min(2000).max(2100),
  organizationLevelId: z.string().optional().or(z.literal('')),
  organizationUnitId: z.string().optional().or(z.literal('')),
  notes: z.string().max(2000).optional().or(z.literal('')),
});

export type TrainingPlanFormValues = z.infer<typeof trainingPlanSchema>;

export const emptyTrainingPlan: TrainingPlanFormValues = {
  year: new Date().getFullYear(),
  organizationLevelId: '',
  organizationUnitId: '',
  notes: '',
};

interface TrainingPlanFormProps {
  defaultValues: TrainingPlanFormValues;
  onSubmit: (values: TrainingPlanFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
}

export function TrainingPlanForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
}: TrainingPlanFormProps) {
  const form = useForm<TrainingPlanFormValues>({
    resolver: zodResolver(trainingPlanSchema) as any,
    defaultValues,
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Plan</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <NumberField form={form} name="year" label="Year" required />
          <OrganizationScopeFields
            levelId={form.watch('organizationLevelId') || ''}
            unitId={form.watch('organizationUnitId') || ''}
            onLevelChange={(v) => form.setValue('organizationLevelId', v, { shouldValidate: true })}
            onUnitChange={(v) => form.setValue('organizationUnitId', v, { shouldValidate: true })}
            hint="Narrow the plan to part of the organization, or leave both blank for a company-wide plan."
          />
          <TextareaField form={form} name="notes" label="Notes" rows={3} />
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel} disabled={submitting}>
          Cancel
        </Button>
        <Button type="submit" disabled={submitting}>
          {submitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          {!submitting && <Save className="mr-2 h-4 w-4" />}
          {submitLabel}
        </Button>
      </div>
    </form>
  );
}
