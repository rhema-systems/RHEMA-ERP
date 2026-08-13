'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  TextField,
  NumberField,
  DateField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { ASSESSMENT_SOURCE_OPTIONS, TRAINING_PRIORITY_OPTIONS } from '@/types/hr/training';

export const needsAssessmentSchema = z.object({
  employeeId: z.string().min(1, 'Employee is required'),
  year: z.coerce.number().min(2000).max(2100),
  source: z.enum(['PerformanceReview', 'SelfAssessment', 'ManagerRequest', 'SkillsGapAnalysis', 'JobRoleChange']),
  identifiedGaps: z.string().min(1, 'Required').max(4000),
  priority: z.enum(['Critical', 'High', 'Medium', 'Low']),
  identifiedById: z.string().min(1, 'Required'),
  identifiedDate: z.string().min(1, 'Required'),
  additionalNotes: z.string().max(2000).optional().or(z.literal('')),
  trainingProvided: z.boolean(),
  trainingProvidedDate: z.string().optional().or(z.literal('')),
});

export type NeedsAssessmentFormValues = z.infer<typeof needsAssessmentSchema>;

const today = () => new Date().toISOString().slice(0, 10);

export const emptyNeedsAssessment: NeedsAssessmentFormValues = {
  employeeId: '',
  year: new Date().getFullYear(),
  source: 'PerformanceReview',
  identifiedGaps: '',
  priority: 'Medium',
  identifiedById: '',
  identifiedDate: today(),
  additionalNotes: '',
  trainingProvided: false,
  trainingProvidedDate: '',
};

interface NeedsAssessmentFormProps {
  mode: 'create' | 'edit';
  defaultValues: NeedsAssessmentFormValues;
  defaultEmployeeLabel?: string | null;
  defaultIdentifiedByLabel?: string | null;
  onSubmit: (values: NeedsAssessmentFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
}

export function NeedsAssessmentForm({
  mode,
  defaultValues,
  defaultEmployeeLabel,
  defaultIdentifiedByLabel,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
}: NeedsAssessmentFormProps) {
  const form = useForm<NeedsAssessmentFormValues>({
    resolver: zodResolver(needsAssessmentSchema) as any,
    defaultValues,
  });

  const trainingProvided = !!form.watch('trainingProvided');

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Assessment</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {mode === 'create' ? (
            <FieldRow>
              <EmployeePickerField
                form={form}
                name="employeeId"
                label="Employee"
                required
                initialLabel={defaultEmployeeLabel}
              />
              <EmployeePickerField
                form={form}
                name="identifiedById"
                label="Identified by"
                required
                initialLabel={defaultIdentifiedByLabel}
              />
            </FieldRow>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2 text-sm">
              <div>
                <p className="text-muted-foreground">Employee</p>
                <p className="font-medium">{defaultEmployeeLabel ?? '—'}</p>
              </div>
              <div>
                <p className="text-muted-foreground">Identified by</p>
                <p className="font-medium">{defaultIdentifiedByLabel ?? '—'}</p>
              </div>
            </div>
          )}
          <FieldRow>
            {mode === 'create' ? (
              <NumberField form={form} name="year" label="Year" required />
            ) : (
              <div className="space-y-2 text-sm">
                <p className="text-muted-foreground">Year</p>
                <p className="font-medium">{defaultValues.year}</p>
              </div>
            )}
            {mode === 'create' && <DateField form={form} name="identifiedDate" label="Identified date" required />}
          </FieldRow>
          <FieldRow>
            <SelectField form={form} name="source" label="Source" required options={ASSESSMENT_SOURCE_OPTIONS} />
            <SelectField form={form} name="priority" label="Priority" required options={TRAINING_PRIORITY_OPTIONS} />
          </FieldRow>
          <TextareaField form={form} name="identifiedGaps" label="Identified gaps" required rows={4} />
          <TextareaField form={form} name="additionalNotes" label="Additional notes" rows={3} />
        </CardContent>
      </Card>

      {mode === 'edit' && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Fulfilment</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <SwitchField
              form={form}
              name="trainingProvided"
              label="Training provided"
              description="Mark once the recommended training has actually been delivered."
            />
            {trainingProvided && (
              <DateField form={form} name="trainingProvidedDate" label="Training provided date" />
            )}
          </CardContent>
        </Card>
      )}

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
