'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  ORIENTATION_PROGRAM_TYPE_OPTIONS,
  ORIENTATION_DELIVERY_MODE_OPTIONS,
  ORIENTATION_PRIORITY_OPTIONS,
  ORIENTATION_AUDIENCE_SCOPE_OPTIONS,
  ORIENTATION_RECURRENCE_FREQUENCY_OPTIONS,
} from '@/types/hr/orientation';
import type { OrientationCategoryLookup } from '@/types/hr/orientation';

/**
 * Create/edit form for an orientation programme, shared by the new page and the detail page's
 * overview tab — the same arrangement the training catalogue uses.
 *
 * The completion rules are the part worth getting right here. `requiresAssessment`,
 * `isCertificateIssued` and `isRecurring` each gate a group of fields that are meaningless without
 * them, so those groups only appear once the switch is on. A passing score left behind on a
 * programme that no longer assesses anything is not harmless: it is what the completion gate reads.
 */
export const orientationProgramSchema = z
  .object({
    title: z.string().min(1, 'A title is required').max(200),
    description: z.string().max(4000).optional().or(z.literal('')),
    objectives: z.string().max(4000).optional().or(z.literal('')),
    categoryId: z.string().optional().or(z.literal('')),
    programType: z.enum([
      'Onboarding',
      'PolicyAwareness',
      'ProductLaunch',
      'Compliance',
      'HealthAndSafety',
      'SystemsAndTools',
      'CultureAndValues',
      'General',
    ]),
    defaultDeliveryMode: z.enum([
      'InPerson',
      'VirtualInstructor',
      'SelfPacedOnline',
      'Blended',
      'VideoOnDemand',
      'PrintedMaterial',
    ]),
    priority: z.enum(['Low', 'Medium', 'High', 'Critical', 'Mandatory']),
    audienceScope: z.enum([
      'AllEmployees',
      'NewHires',
      'OrganizationUnit',
      'JobGrade',
      'Location',
      'Role',
      'Management',
      'Contractors',
      'Custom',
    ]),
    estimatedDurationMinutes: z.coerce.number().min(0).max(100000).optional(),
    requiresAssessment: z.boolean(),
    passingScorePercent: z.coerce.number().min(0).max(100).optional(),
    requiresAcknowledgement: z.boolean(),
    completionDeadlineDays: z.coerce.number().min(0).max(3650).optional(),
    isCertificateIssued: z.boolean(),
    certificateValidityMonths: z.coerce.number().min(1).max(600).optional(),
    isRecurring: z.boolean(),
    recurrenceFrequency: z
      .enum(['Monthly', 'Quarterly', 'SemiAnnually', 'Annually', 'Biennially'])
      .optional()
      .or(z.literal('')),
    enableReminders: z.boolean(),
    version: z.string().max(50).optional().or(z.literal('')),
    effectiveFrom: z.string().optional().or(z.literal('')),
    effectiveTo: z.string().optional().or(z.literal('')),
    tags: z.string().max(500).optional().or(z.literal('')),
    ownerEmployeeId: z.string().optional().or(z.literal('')),
  })
  // A recurring programme with no frequency never schedules its next round, and the server has no
  // default to fall back on — so it is caught here rather than saved and quietly never recurring.
  .refine((v) => !v.isRecurring || !!v.recurrenceFrequency, {
    message: 'Choose how often this programme recurs.',
    path: ['recurrenceFrequency'],
  })
  .refine(
    (v) => !v.effectiveFrom || !v.effectiveTo || v.effectiveFrom <= v.effectiveTo,
    { message: 'The end of the effective window cannot precede its start.', path: ['effectiveTo'] },
  );

export type OrientationProgramFormValues = z.infer<typeof orientationProgramSchema>;

export const emptyOrientationProgram: OrientationProgramFormValues = {
  title: '',
  description: '',
  objectives: '',
  categoryId: '',
  programType: 'Onboarding',
  defaultDeliveryMode: 'InPerson',
  priority: 'Medium',
  audienceScope: 'NewHires',
  estimatedDurationMinutes: undefined,
  requiresAssessment: false,
  passingScorePercent: undefined,
  requiresAcknowledgement: false,
  completionDeadlineDays: undefined,
  isCertificateIssued: false,
  certificateValidityMonths: undefined,
  isRecurring: false,
  recurrenceFrequency: '',
  enableReminders: true,
  version: '',
  effectiveFrom: '',
  effectiveTo: '',
  tags: '',
  ownerEmployeeId: '',
};

const blank = (v?: string | number | null) =>
  v === '' || v === undefined || v === null ? null : v;

/**
 * Maps form values onto the create/update payload shape both endpoints share.
 *
 * Empty strings become nulls, and the fields behind a switch that is off are nulled rather than
 * passed through — a passing score on a programme that does not assess, or a validity period on one
 * that issues no certificate, would otherwise persist and be read by the completion gate later.
 */
export function toOrientationProgramRequest(values: OrientationProgramFormValues) {
  return {
    title: values.title,
    description: blank(values.description) as string | null,
    objectives: blank(values.objectives) as string | null,
    categoryId: blank(values.categoryId) as string | null,
    programType: values.programType,
    defaultDeliveryMode: values.defaultDeliveryMode,
    priority: values.priority,
    audienceScope: values.audienceScope,
    estimatedDurationMinutes: (blank(values.estimatedDurationMinutes) as number | null) ?? null,
    requiresAssessment: values.requiresAssessment,
    passingScorePercent: values.requiresAssessment
      ? ((blank(values.passingScorePercent) as number | null) ?? null)
      : null,
    requiresAcknowledgement: values.requiresAcknowledgement,
    completionDeadlineDays: (blank(values.completionDeadlineDays) as number | null) ?? null,
    isCertificateIssued: values.isCertificateIssued,
    certificateValidityMonths: values.isCertificateIssued
      ? ((blank(values.certificateValidityMonths) as number | null) ?? null)
      : null,
    isRecurring: values.isRecurring,
    recurrenceFrequency: values.isRecurring
      ? ((blank(values.recurrenceFrequency) as string | null) ?? null)
      : null,
    enableReminders: values.enableReminders,
    version: blank(values.version) as string | null,
    effectiveFrom: blank(values.effectiveFrom) as string | null,
    effectiveTo: blank(values.effectiveTo) as string | null,
    tags: blank(values.tags) as string | null,
    ownerEmployeeId: blank(values.ownerEmployeeId) as string | null,
    ownerOrganizationUnitId: null,
  };
}

interface Props {
  defaultValues: OrientationProgramFormValues;
  categories: OrientationCategoryLookup[];
  onSubmit: (values: OrientationProgramFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  /** Shown read-only on the detail page; the server generates it and it never changes. */
  programCode?: string;
  ownerInitialLabel?: string | null;
}

export function OrientationProgramForm({
  defaultValues,
  categories,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  programCode,
  ownerInitialLabel,
}: Props) {
  const form = useForm<OrientationProgramFormValues>({
    resolver: zodResolver(orientationProgramSchema) as any,
    defaultValues,
  });

  const requiresAssessment = !!form.watch('requiresAssessment');
  const isCertificateIssued = !!form.watch('isCertificateIssued');
  const isRecurring = !!form.watch('isRecurring');

  const categoryOptions = categories.map((c) => ({ value: c.id, label: c.name }));

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Programme</CardTitle>
          {programCode && (
            <CardDescription>
              Code <span className="font-mono">{programCode}</span> — issued on creation and fixed.
            </CardDescription>
          )}
        </CardHeader>
        <CardContent className="space-y-4">
          <TextField form={form} name="title" label="Title" required />
          <TextareaField form={form} name="description" label="Description" rows={3} />
          <TextareaField
            form={form}
            name="objectives"
            label="Objectives"
            rows={3}
            placeholder="What someone should know or be able to do by the end."
          />
          <FieldRow>
            <SelectField
              form={form}
              name="categoryId"
              label="Category"
              options={categoryOptions}
              allowEmpty
              emptyLabel="Uncategorised"
            />
            <SelectField
              form={form}
              name="programType"
              label="Type"
              required
              options={ORIENTATION_PROGRAM_TYPE_OPTIONS}
            />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="defaultDeliveryMode"
              label="Default delivery mode"
              required
              options={ORIENTATION_DELIVERY_MODE_OPTIONS}
            />
            <SelectField
              form={form}
              name="priority"
              label="Priority"
              required
              options={ORIENTATION_PRIORITY_OPTIONS}
            />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="audienceScope"
              label="Audience scope"
              required
              options={ORIENTATION_AUDIENCE_SCOPE_OPTIONS}
            />
            <NumberField
              form={form}
              name="estimatedDurationMinutes"
              label="Estimated duration (minutes)"
            />
          </FieldRow>
          <EmployeePickerField
            form={form}
            name="ownerEmployeeId"
            label="Owner"
            initialLabel={ownerInitialLabel}
            placeholder="Who is accountable for this programme?"
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Completion requirements</CardTitle>
          <CardDescription>
            What a participant has to do before the programme counts as finished.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <SwitchField
            form={form}
            name="requiresAssessment"
            label="Requires an assessment"
            description="Participants sit the programme's question paper and must reach the passing score."
          />
          {requiresAssessment && (
            <NumberField form={form} name="passingScorePercent" label="Passing score (%)" />
          )}
          <SwitchField
            form={form}
            name="requiresAcknowledgement"
            label="Requires an acknowledgement"
            description="A declaration the participant signs — recorded with their IP and a tamper hash."
          />
          <NumberField
            form={form}
            name="completionDeadlineDays"
            label="Completion deadline (days from enrollment)"
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Certificate</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <SwitchField
            form={form}
            name="isCertificateIssued"
            label="Issues a certificate"
            description="A serial is generated when HR certifies a passed enrollment."
          />
          {isCertificateIssued && (
            <NumberField
              form={form}
              name="certificateValidityMonths"
              label="Validity (months)"
              placeholder="Leave empty for a certificate that does not expire"
            />
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Recurrence &amp; lifecycle</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <SwitchField
            form={form}
            name="isRecurring"
            label="Recurs"
            description="Compliance refreshers people must retake on a cycle."
          />
          {isRecurring && (
            <SelectField
              form={form}
              name="recurrenceFrequency"
              label="Frequency"
              required
              options={ORIENTATION_RECURRENCE_FREQUENCY_OPTIONS}
            />
          )}
          <SwitchField
            form={form}
            name="enableReminders"
            label="Send reminders"
            description="Enrollment, deadline and overdue notices for this programme."
          />
          <FieldRow>
            <DateField form={form} name="effectiveFrom" label="Effective from" />
            <DateField form={form} name="effectiveTo" label="Effective to" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="version" label="Version" placeholder="e.g. 2.1" />
            <TextField
              form={form}
              name="tags"
              label="Tags"
              placeholder="Comma-separated, for searching"
            />
          </FieldRow>
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel} disabled={submitting}>
          Cancel
        </Button>
        <Button type="submit" disabled={submitting}>
          {submitting ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <Save className="mr-2 h-4 w-4" />
          )}
          {submitLabel}
        </Button>
      </div>
    </form>
  );
}
