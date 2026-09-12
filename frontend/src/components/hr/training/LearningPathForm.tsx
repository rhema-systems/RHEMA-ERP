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
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { OrganizationScopeFields } from '@/components/hr/performance/OrganizationScopeFields';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { LEARNING_PATH_STATUS_OPTIONS } from '@/types/hr/learning-paths';
import type { LearningPathCreate } from '@/types/hr/learning-paths';

export const learningPathSchema = z
  .object({
    name: z.string().min(1, 'Required').max(200),
    description: z.string().min(1, 'Required').max(2000),
    organizationLevelId: z.string().optional().or(z.literal('')),
    organizationUnitId: z.string().optional().or(z.literal('')),
    positionId: z.string().optional().or(z.literal('')),
    status: z.enum(['Draft', 'Active', 'Inactive']),
    estimatedDurationDays: z.string().optional().or(z.literal('')),
    estimatedDurationHours: z.string().optional().or(z.literal('')),
    providesCertificate: z.boolean(),
    completionCertificateName: z.string().max(200).optional().or(z.literal('')),
  })
  // A certificate nobody can name is not something you can issue.
  .refine((v) => !v.providesCertificate || !!v.completionCertificateName, {
    message: 'Name the certificate this path awards.',
    path: ['completionCertificateName'],
  });

export type LearningPathFormValues = z.infer<typeof learningPathSchema>;

export const emptyLearningPath: LearningPathFormValues = {
  name: '',
  description: '',
  organizationLevelId: '',
  organizationUnitId: '',
  positionId: '',
  status: 'Draft',
  estimatedDurationDays: '',
  estimatedDurationHours: '',
  providesCertificate: false,
  completionCertificateName: '',
};

export function toLearningPathRequest(v: LearningPathFormValues): LearningPathCreate {
  return {
    name: v.name,
    description: v.description,
    organizationLevelId: v.organizationLevelId || null,
    organizationUnitId: v.organizationUnitId || null,
    positionId: v.positionId || null,
    status: v.status,
    estimatedDurationDays: v.estimatedDurationDays ? Number(v.estimatedDurationDays) : null,
    estimatedDurationHours: v.estimatedDurationHours ? Number(v.estimatedDurationHours) : null,
    providesCertificate: v.providesCertificate,
    completionCertificateName: v.completionCertificateName || null,
  };
}

interface Props {
  defaultValues: LearningPathFormValues;
  onSubmit: (values: LearningPathFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel?: string;
  onCancel: () => void;
}

export function LearningPathForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel = 'Save path',
  onCancel,
}: Props) {
  const form = useForm<LearningPathFormValues>({
    resolver: zodResolver(learningPathSchema) as any,
    defaultValues,
  });

  const { data: positions } = useQuery({
    queryKey: ['hr', 'employee-positions'],
    queryFn: () => employeePositionService.getAll(),
    staleTime: 5 * 60 * 1000,
  });
  const positionOptions = (positions ?? []).map((p: any) => ({ value: p.id, label: p.title }));

  const providesCertificate = form.watch('providesCertificate');
  // OrganizationScopeFields is controlled rather than form-bound, so the values are bridged here.
  const levelId = form.watch('organizationLevelId') ?? '';
  const unitId = form.watch('organizationUnitId') ?? '';

  return (
    <form onSubmit={form.handleSubmit(onSubmit)}>
      <Card>
        <CardHeader>
          <CardTitle>Path</CardTitle>
          <CardDescription>
            The curriculum itself. Its programmes and their order are set on the path once it exists.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <section className="space-y-4">
            <TextField
              form={form}
              name="name"
              label="Name"
              placeholder="Operations Induction"
              required
            />
            <TextareaField form={form} name="description" label="Description" rows={3} required />
            <SelectField
              form={form}
              name="status"
              label="Status"
              required
              options={LEARNING_PATH_STATUS_OPTIONS}
            />
            <p className="-mt-2 text-xs text-muted-foreground">
              Draft keeps it out of the way while you assemble the sequence.
            </p>
          </section>

          <section className="space-y-4">
            <h3 className="text-sm font-medium text-muted-foreground">Who it is aimed at</h3>
            <OrganizationScopeFields
              levelId={levelId}
              unitId={unitId}
              onLevelChange={(v) => form.setValue('organizationLevelId', v, { shouldDirty: true })}
              onUnitChange={(v) => form.setValue('organizationUnitId', v, { shouldDirty: true })}
              hint="Leave both blank if the path suits anyone."
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
              This is guidance, not a gate — enrolment is explicit, so narrowing the scope does not
              enrol or exclude anybody on its own.
            </p>
          </section>

          <section className="space-y-4">
            <h3 className="text-sm font-medium text-muted-foreground">Effort and outcome</h3>
            <FieldRow>
              <NumberField form={form} name="estimatedDurationDays" label="Estimated days" />
              <NumberField form={form} name="estimatedDurationHours" label="Estimated hours" />
            </FieldRow>
            <SwitchField
              form={form}
              name="providesCertificate"
              label="Awards a certificate on completion"
            />
            {providesCertificate && (
              <TextField
                form={form}
                name="completionCertificateName"
                label="Certificate name"
                placeholder="Certified Operations Inductee"
                required
              />
            )}
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
