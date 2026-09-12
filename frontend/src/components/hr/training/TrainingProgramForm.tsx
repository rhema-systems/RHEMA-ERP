'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import {
  TRAINING_TYPE_OPTIONS,
  TRAINING_SOURCE_OPTIONS,
  TRAINING_LEVEL_OPTIONS,
} from '@/types/hr/training';
import type { TrainingCategoryOption, TrainingProgramGroup } from '@/types/hr/training';

export const trainingProgramSchema = z.object({
  programCode: z.string().min(1, 'Program code is required').max(50),
  programName: z.string().min(1, 'Name is required').max(200),
  description: z.string().max(4000).optional().or(z.literal('')),
  categoryOptionId: z.string().optional().or(z.literal('')),
  programGroupId: z.string().optional().or(z.literal('')),
  type: z.enum(['Workshop', 'Seminar', 'Conference', 'OnTheJob', 'Mentoring', 'Certification']),
  source: z.enum(['Internal', 'External', 'OnlineELearning']),
  level: z.enum(['Beginner', 'Intermediate', 'Advanced', 'Expert', 'AnyLevel']),
  durationDays: z.coerce.number().min(0).max(365),
  durationHours: z.coerce.number().min(0).max(2920),
  prerequisites: z.string().max(2000).optional().or(z.literal('')),
  learningObjectives: z.string().max(4000).optional().or(z.literal('')),
  costPerParticipant: z.coerce.number().min(0),
  currency: z.string().min(1).max(3),
  includesAccommodation: z.boolean(),
  includesMeals: z.boolean(),
  includesTransport: z.boolean(),
  providesCertificate: z.boolean(),
  certificateName: z.string().max(200).optional().or(z.literal('')),
  certificateValidityMonths: z.coerce.number().min(1).max(120).optional(),
  minParticipants: z.coerce.number().min(1).max(1000).optional(),
  maxParticipants: z.coerce.number().min(1).max(1000).optional(),
  isActive: z.boolean(),
  requiresApproval: z.boolean(),
  requiresServiceBond: z.boolean(),
  serviceBondMonths: z.coerce.number().min(1).max(120).optional(),
  serviceBondTerms: z.string().max(4000).optional().or(z.literal('')),
});

export type TrainingProgramFormValues = z.infer<typeof trainingProgramSchema>;

export const emptyTrainingProgram: TrainingProgramFormValues = {
  programCode: '',
  programName: '',
  description: '',
  categoryOptionId: '',
  programGroupId: '',
  type: 'Workshop',
  source: 'Internal',
  level: 'Beginner',
  durationDays: 1,
  durationHours: 8,
  prerequisites: '',
  learningObjectives: '',
  costPerParticipant: 0,
  currency: 'GHS',
  includesAccommodation: false,
  includesMeals: false,
  includesTransport: false,
  providesCertificate: false,
  certificateName: '',
  certificateValidityMonths: undefined,
  minParticipants: undefined,
  maxParticipants: undefined,
  isActive: true,
  requiresApproval: false,
  requiresServiceBond: false,
  serviceBondMonths: undefined,
  serviceBondTerms: '',
};

interface TrainingProgramFormProps {
  defaultValues: TrainingProgramFormValues;
  categories: TrainingCategoryOption[];
  groups: TrainingProgramGroup[];
  onSubmit: (values: TrainingProgramFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  programCodeEditable?: boolean;
}

export function TrainingProgramForm({
  defaultValues,
  categories,
  groups,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  programCodeEditable = true,
}: TrainingProgramFormProps) {
  const form = useForm<TrainingProgramFormValues>({
    resolver: zodResolver(trainingProgramSchema) as any,
    defaultValues,
  });

  const providesCertificate = !!form.watch('providesCertificate');
  const requiresServiceBond = !!form.watch('requiresServiceBond');

  const categoryOptions = categories.map((c) => ({ value: c.id, label: c.name }));
  const groupOptions = groups.map((g) => ({ value: g.id, label: g.name }));

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Program</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            {programCodeEditable ? (
              <TextField form={form} name="programCode" label="Program code" required placeholder="e.g. PRG-001" />
            ) : (
              <div className="space-y-2">
                <Label htmlFor="programCode">Program code</Label>
                <Input id="programCode" value={form.watch('programCode')} disabled />
                <p className="text-xs text-muted-foreground">Cannot be changed after creation.</p>
              </div>
            )}
            <TextField form={form} name="programName" label="Program name" required />
          </FieldRow>
          <TextareaField form={form} name="description" label="Description" rows={3} />
          <FieldRow>
            <SelectField
              form={form}
              name="categoryOptionId"
              label="Category"
              options={categoryOptions}
              allowEmpty
              emptyLabel="Uncategorised"
            />
            <SelectField
              form={form}
              name="programGroupId"
              label="Program group"
              options={groupOptions}
              allowEmpty
              emptyLabel="No group"
            />
          </FieldRow>
          <FieldRow>
            <SelectField form={form} name="type" label="Type" required options={TRAINING_TYPE_OPTIONS} />
            <SelectField form={form} name="source" label="Source" required options={TRAINING_SOURCE_OPTIONS} />
          </FieldRow>
          <FieldRow>
            <SelectField form={form} name="level" label="Level" required options={TRAINING_LEVEL_OPTIONS} />
            <SwitchField form={form} name="isActive" label="Active" />
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Duration &amp; cost</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <NumberField form={form} name="durationDays" label="Duration (days)" required />
            <NumberField form={form} name="durationHours" label="Duration (hours)" required />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="costPerParticipant" label="Cost per participant" step="0.01" required />
            <TextField form={form} name="currency" label="Currency" required placeholder="GHS" />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="minParticipants" label="Min participants" />
            <NumberField form={form} name="maxParticipants" label="Max participants" />
          </FieldRow>
          <FieldRow>
            <SwitchField form={form} name="includesAccommodation" label="Includes accommodation" />
            <SwitchField form={form} name="includesMeals" label="Includes meals" />
          </FieldRow>
          <SwitchField form={form} name="includesTransport" label="Includes transport" />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Prerequisites &amp; objectives</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <TextareaField form={form} name="prerequisites" label="Prerequisites" rows={2} />
          <TextareaField form={form} name="learningObjectives" label="Learning objectives" rows={3} />
          <SwitchField form={form} name="requiresApproval" label="Requires approval to attend" />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Certificate</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <SwitchField form={form} name="providesCertificate" label="Provides a certificate" />
          {providesCertificate && (
            <FieldRow>
              <TextField form={form} name="certificateName" label="Certificate name" />
              <NumberField form={form} name="certificateValidityMonths" label="Validity (months)" />
            </FieldRow>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Service bond</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <SwitchField
            form={form}
            name="requiresServiceBond"
            label="Requires a service bond"
            description="Nominees sponsored on this program commit to a minimum service period."
          />
          {requiresServiceBond && (
            <>
              <NumberField form={form} name="serviceBondMonths" label="Bond duration (months)" />
              <TextareaField form={form} name="serviceBondTerms" label="Bond terms" rows={3} />
            </>
          )}
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
