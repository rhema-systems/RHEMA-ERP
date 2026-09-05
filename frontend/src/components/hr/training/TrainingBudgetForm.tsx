'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { TextField, NumberField, TextareaField, SelectField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { OrganizationScopeFields } from '@/components/hr/performance/OrganizationScopeFields';

const QUARTER_OPTIONS = [
  { value: '1', label: 'Q1' },
  { value: '2', label: 'Q2' },
  { value: '3', label: 'Q3' },
  { value: '4', label: 'Q4' },
];

export const trainingBudgetSchema = z.object({
  budgetCode: z.string().min(1, 'Budget code is required').max(50),
  year: z.coerce.number().min(2000).max(2100),
  quarter: z.string().optional().or(z.literal('')),
  organizationLevelId: z.string().optional().or(z.literal('')),
  organizationUnitId: z.string().optional().or(z.literal('')),
  currency: z.string().min(1).max(3),
  allocatedAmount: z.coerce.number().min(0),
  glAccountCode: z.string().max(50).optional().or(z.literal('')),
  costCenterCode: z.string().max(50).optional().or(z.literal('')),
  notes: z.string().max(2000).optional().or(z.literal('')),
});

export type TrainingBudgetFormValues = z.infer<typeof trainingBudgetSchema>;

export const emptyTrainingBudget: TrainingBudgetFormValues = {
  budgetCode: '',
  year: new Date().getFullYear(),
  quarter: '',
  organizationLevelId: '',
  organizationUnitId: '',
  currency: 'GHS',
  allocatedAmount: 0,
  glAccountCode: '',
  costCenterCode: '',
  notes: '',
};

interface TrainingBudgetFormProps {
  defaultValues: TrainingBudgetFormValues;
  onSubmit: (values: TrainingBudgetFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  budgetCodeEditable?: boolean;
}

export function TrainingBudgetForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  budgetCodeEditable = true,
}: TrainingBudgetFormProps) {
  const form = useForm<TrainingBudgetFormValues>({
    resolver: zodResolver(trainingBudgetSchema) as any,
    defaultValues,
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Budget</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            {budgetCodeEditable ? (
              <TextField form={form} name="budgetCode" label="Budget code" required placeholder="e.g. TB-2026-001" />
            ) : (
              <div className="space-y-2">
                <Label htmlFor="budgetCode">Budget code</Label>
                <Input id="budgetCode" value={form.watch('budgetCode')} disabled />
                <p className="text-xs text-muted-foreground">Cannot be changed after creation.</p>
              </div>
            )}
            <NumberField form={form} name="year" label="Year" required />
          </FieldRow>
          <SelectField
            form={form}
            name="quarter"
            label="Quarter"
            options={QUARTER_OPTIONS}
            allowEmpty
            emptyLabel="Full year"
          />
          <OrganizationScopeFields
            levelId={form.watch('organizationLevelId') || ''}
            unitId={form.watch('organizationUnitId') || ''}
            onLevelChange={(v) => form.setValue('organizationLevelId', v, { shouldValidate: true })}
            onUnitChange={(v) => form.setValue('organizationUnitId', v, { shouldValidate: true })}
            hint="Narrow the budget to part of the organization, or leave both blank for a company-wide budget."
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Allocation</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <NumberField form={form} name="allocatedAmount" label="Allocated amount" step="0.01" required />
            <TextField form={form} name="currency" label="Currency" required placeholder="GHS" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="glAccountCode" label="GL account code" />
            <TextField form={form} name="costCenterCode" label="Cost center code" />
          </FieldRow>
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
