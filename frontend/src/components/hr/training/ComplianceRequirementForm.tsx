'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import {
  TextField,
  NumberField,
  DateField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { OrganizationScopeFields } from '@/components/hr/performance/OrganizationScopeFields';
import { trainingProgramService } from '@/services/hr/training-program.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { COMPLIANCE_FREQUENCY_OPTIONS } from '@/types/hr/training-compliance';
import type { ComplianceRequirementCreate } from '@/types/hr/training-compliance';

export const complianceRequirementSchema = z
  .object({
    requirementCode: z.string().min(1, 'Required').max(50),
    requirementName: z.string().min(1, 'Required').max(200),
    description: z.string().max(1000).optional().or(z.literal('')),
    regulatoryReference: z.string().max(200).optional().or(z.literal('')),
    programId: z.string().min(1, 'The training that satisfies this is required'),
    organizationLevelId: z.string().optional().or(z.literal('')),
    organizationUnitId: z.string().optional().or(z.literal('')),
    positionId: z.string().optional().or(z.literal('')),
    frequency: z.enum(['OneTime', 'Annual', 'BiAnnual', 'Quarterly', 'Monthly', 'Custom']),
    customFrequencyDays: z.string().optional().or(z.literal('')),
    gracePeriodDays: z.string().optional().or(z.literal('')),
    isActive: z.boolean(),
    effectiveDate: z.string().min(1, 'Required'),
    expiryDate: z.string().optional().or(z.literal('')),
    nonComplianceConsequences: z.string().max(1000).optional().or(z.literal('')),
  })
  // A custom interval that does not say how long is not a schedule.
  .refine((v) => v.frequency !== 'Custom' || !!v.customFrequencyDays, {
    message: 'Say how many days between recurrences.',
    path: ['customFrequencyDays'],
  })
  .refine((v) => !v.expiryDate || new Date(v.expiryDate) >= new Date(v.effectiveDate), {
    message: 'A requirement cannot expire before it takes effect.',
    path: ['expiryDate'],
  });

export type ComplianceRequirementFormValues = z.infer<typeof complianceRequirementSchema>;

export const emptyComplianceRequirement: ComplianceRequirementFormValues = {
  requirementCode: '',
  requirementName: '',
  description: '',
  regulatoryReference: '',
  programId: '',
  organizationLevelId: '',
  organizationUnitId: '',
  positionId: '',
  frequency: 'Annual',
  customFrequencyDays: '',
  gracePeriodDays: '',
  isActive: true,
  effectiveDate: new Date().toISOString().slice(0, 10),
  expiryDate: '',
  nonComplianceConsequences: '',
};

export function toRequirementRequest(v: ComplianceRequirementFormValues): ComplianceRequirementCreate {
  return {
    requirementCode: v.requirementCode,
    requirementName: v.requirementName,
    description: v.description || null,
    regulatoryReference: v.regulatoryReference || null,
    programId: v.programId,
    organizationLevelId: v.organizationLevelId || null,
    organizationUnitId: v.organizationUnitId || null,
    positionId: v.positionId || null,
    frequency: v.frequency,
    customFrequencyDays: v.customFrequencyDays ? Number(v.customFrequencyDays) : null,
    gracePeriodDays: v.gracePeriodDays ? Number(v.gracePeriodDays) : null,
    isActive: v.isActive,
    effectiveDate: new Date(v.effectiveDate).toISOString(),
    expiryDate: v.expiryDate ? new Date(v.expiryDate).toISOString() : null,
    nonComplianceConsequences: v.nonComplianceConsequences || null,
  };
}

interface Props {
  defaultValues: ComplianceRequirementFormValues;
  onSubmit: (values: ComplianceRequirementFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel?: string;
  onCancel: () => void;
}

export function ComplianceRequirementForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel = 'Save requirement',
  onCancel,
}: Props) {
  const form = useForm<ComplianceRequirementFormValues>({
    resolver: zodResolver(complianceRequirementSchema) as any,
    defaultValues,
  });

  const { data: programs } = useQuery({
    queryKey: ['hr', 'training', 'programs', 'active'],
    queryFn: () => trainingProgramService.getActive(),
  });
  const programOptions = (programs ?? []).map((p) => ({
    value: p.id,
    label: `${p.programCode} — ${p.programName}`,
  }));

  const { data: positions } = useQuery({
    queryKey: ['hr', 'employee-positions'],
    queryFn: () => employeePositionService.getAll(),
    staleTime: 5 * 60 * 1000,
  });
  const positionOptions = (positions ?? []).map((p: any) => ({ value: p.id, label: p.title }));

  const frequency = form.watch('frequency');
  // OrganizationScopeFields is controlled rather than form-bound, so the values are bridged here.
  const levelId = form.watch('organizationLevelId') ?? '';
  const unitId = form.watch('organizationUnitId') ?? '';

  return (
    <form onSubmit={form.handleSubmit(onSubmit)}>
      <Card>
        <CardHeader>
          <CardTitle>Requirement</CardTitle>
          <CardDescription>
            What must be held, who must hold it, and how often it has to be renewed.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <section className="space-y-4">
            <FieldRow>
              <TextField form={form} name="requirementCode" label="Code" placeholder="FIRE-ANN" required />
              <TextField
                form={form}
                name="requirementName"
                label="Requirement"
                placeholder="Annual fire safety training"
                required
              />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <TextField
              form={form}
              name="regulatoryReference"
              label="Regulatory reference"
              placeholder="Factories Act s.24"
            />
            <p className="-mt-2 text-xs text-muted-foreground">
              The rule this exists to satisfy — it is what makes the requirement defensible when
              somebody asks to be excused.
            </p>
            <SelectField
              form={form}
              name="programId"
              label="Training that satisfies it"
              required
              options={programOptions}
            />
          </section>

          <section className="space-y-4">
            <h3 className="text-sm font-medium text-muted-foreground">Who it applies to</h3>
            <OrganizationScopeFields
              levelId={levelId}
              unitId={unitId}
              onLevelChange={(v) =>
                form.setValue('organizationLevelId', v, { shouldDirty: true })
              }
              onUnitChange={(v) => form.setValue('organizationUnitId', v, { shouldDirty: true })}
              hint="Leave both blank to apply it organisation-wide."
            />
            <SelectField
              form={form}
              name="positionId"
              label="Position"
              options={positionOptions}
              allowEmpty
              emptyLabel="Any position"
            />
            <p className="text-xs text-muted-foreground">
              The three narrow independently — a requirement can be "all Drivers", "everyone in
              Operations", or both.
            </p>
          </section>

          <section className="space-y-4">
            <h3 className="text-sm font-medium text-muted-foreground">Recurrence</h3>
            <FieldRow>
              <SelectField
                form={form}
                name="frequency"
                label="Frequency"
                required
                options={COMPLIANCE_FREQUENCY_OPTIONS}
              />
              {frequency === 'Custom' ? (
                <NumberField form={form} name="customFrequencyDays" label="Every (days)" required />
              ) : (
                <NumberField form={form} name="gracePeriodDays" label="Grace period (days)" />
              )}
            </FieldRow>
            {frequency === 'Custom' && (
              <NumberField form={form} name="gracePeriodDays" label="Grace period (days)" />
            )}
            <p className="-mt-1 text-xs text-muted-foreground">
              The grace period is how long past the due date before it counts as a breach rather than
              a reminder.
            </p>
            <FieldRow>
              <DateField form={form} name="effectiveDate" label="Effective from" required />
              <DateField form={form} name="expiryDate" label="Expires" />
            </FieldRow>
          </section>

          <section className="space-y-4">
            <TextareaField
              form={form}
              name="nonComplianceConsequences"
              label="Consequences of non-compliance"
              rows={2}
              placeholder="Site access withdrawn until completed"
            />
            <SwitchField form={form} name="isActive" label="Active" />
          </section>
        </CardContent>
        <CardFooter className="flex justify-end gap-2">
          <Button variant="outline" type="button" onClick={onCancel} disabled={submitting}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            {submitLabel}
          </Button>
        </CardFooter>
      </Card>
    </form>
  );
}
