'use client';

import { useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, ShieldAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/hooks/use-toast';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { successionService } from '@/services/hr/succession.service';
import type { SuccessionPlan } from '@/types/hr/succession';

const CRITICALITIES = ['Low', 'Medium', 'High', 'Critical'] as const;
const RISK_LEVELS = ['HighRisk', 'MediumRisk', 'LowRisk', 'NoRisk'] as const;
const VACANCY_REASONS = [
  'Retirement',
  'Resignation',
  'Promotion',
  'Transfer',
  'Termination',
  'Restructure',
  'Death',
  'Other',
] as const;

const label = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: label(v) }));

const schema = z.object({
  planName: z.string().min(1, 'Required').max(100),
  description: z.string().max(2000).optional(),
  positionId: z.string().min(1, 'Select the position this plan covers'),
  currentIncumbentId: z.string().optional(),
  planYear: z.coerce.number().int().min(2000).max(2100),
  criticality: z.enum(CRITICALITIES),
  riskLevel: z.enum(RISK_LEVELS),
  riskAssessmentNotes: z.string().max(2000).optional(),
  businessImpactIfVacant: z.string().max(2000).optional(),
  incumbentRetirementDate: z.string().optional(),
  anticipatedVacancyDate: z.string().optional(),
  anticipatedVacancyReason: z.enum(VACANCY_REASONS).optional().or(z.literal('')),
  incumbentSuccessionNotes: z.string().max(2000).optional(),
  emergencySuccessorId: z.string().optional(),
  emergencyProtocol: z.string().max(2000).optional(),
  targetSuccessionDate: z.string().optional(),
  estimatedTimeToReadyMonths: z.coerce.number().int().min(0).max(600).optional(),
  reviewFrequencyMonths: z.coerce.number().int().min(1, 'At least 1').max(60),
  nextReviewDate: z.string().optional(),
});

type FormValues = z.input<typeof schema>;

const toDateInput = (v?: string | null) => (v ? v.slice(0, 10) : '');

/**
 * ⚠ An empty date input must be sent as `null`, never as `""`. The DateTime binder 400s on an empty
 * string, and TypeScript is happy either way — the area-12 UI-payload lesson. Same for the optional
 * employee pickers and the optional enum.
 */
const orNull = (v?: string) => (v && v.trim() !== '' ? v : null);

/**
 * The one succession-plan form, used by both the create and edit routes.
 *
 * ⚠ **What this form does not have is the point.** There is no "prepared by", "reviewed by" or
 * "approved by" field, because the create DTO carries no actor and the review/approve DTOs had
 * theirs removed in slice 1: those people are the signed-in user, taken from the token. Building
 * this form is how that gets audited — a `*ById` a form would have to invent is either dead or
 * spoofable. The two employee pickers here are different in kind: the incumbent and the emergency
 * successor are *subjects* of the plan, facts about the position, not claims about who is acting.
 */
export function SuccessionPlanForm({ plan }: { plan?: SuccessionPlan }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const isEdit = !!plan;

  const { data: positions, isLoading: positionsLoading } = useQuery({
    queryKey: ['employee-positions', 'active'],
    queryFn: () => employeePositionService.getActive(),
  });

  const positionOptions = useMemo(
    () => (positions ?? []).map((p) => ({ value: p.id, label: p.title })),
    [positions],
  );

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      planName: plan?.planName ?? '',
      description: plan?.description ?? '',
      positionId: plan?.positionId ?? '',
      currentIncumbentId: plan?.currentIncumbentId ?? '',
      planYear: plan?.planYear ?? new Date().getFullYear(),
      criticality: plan?.criticality ?? 'High',
      riskLevel: plan?.riskLevel ?? 'MediumRisk',
      riskAssessmentNotes: plan?.riskAssessmentNotes ?? '',
      businessImpactIfVacant: plan?.businessImpactIfVacant ?? '',
      incumbentRetirementDate: toDateInput(plan?.incumbentRetirementDate),
      anticipatedVacancyDate: toDateInput(plan?.anticipatedVacancyDate),
      anticipatedVacancyReason: plan?.anticipatedVacancyReason ?? '',
      incumbentSuccessionNotes: plan?.incumbentSuccessionNotes ?? '',
      emergencySuccessorId: plan?.emergencySuccessorId ?? '',
      emergencyProtocol: plan?.emergencyProtocol ?? '',
      targetSuccessionDate: toDateInput(plan?.targetSuccessionDate),
      estimatedTimeToReadyMonths: plan?.estimatedTimeToReadyMonths ?? undefined,
      reviewFrequencyMonths: plan?.reviewFrequencyMonths ?? 12,
      nextReviewDate: toDateInput(plan?.nextReviewDate),
    },
  });

  const submitting = form.formState.isSubmitting;

  const onSubmit = form.handleSubmit(async (values) => {
    const payload = {
      planName: values.planName,
      description: orNull(values.description),
      positionId: values.positionId,
      currentIncumbentId: orNull(values.currentIncumbentId),
      planYear: Number(values.planYear),
      criticality: values.criticality,
      riskLevel: values.riskLevel,
      riskAssessmentNotes: orNull(values.riskAssessmentNotes),
      businessImpactIfVacant: orNull(values.businessImpactIfVacant),
      incumbentRetirementDate: orNull(values.incumbentRetirementDate),
      anticipatedVacancyDate: orNull(values.anticipatedVacancyDate),
      anticipatedVacancyReason: orNull(values.anticipatedVacancyReason),
      incumbentSuccessionNotes: orNull(values.incumbentSuccessionNotes),
      emergencySuccessorId: orNull(values.emergencySuccessorId),
      emergencyProtocol: orNull(values.emergencyProtocol),
      targetSuccessionDate: orNull(values.targetSuccessionDate),
      estimatedTimeToReadyMonths:
        values.estimatedTimeToReadyMonths === undefined || values.estimatedTimeToReadyMonths === null
          ? null
          : Number(values.estimatedTimeToReadyMonths),
      reviewFrequencyMonths: Number(values.reviewFrequencyMonths),
      nextReviewDate: orNull(values.nextReviewDate),
    } as any;

    try {
      const saved = plan
        ? await successionService.update(plan.id, { ...payload, id: plan.id })
        : await successionService.create(payload);

      await queryClient.invalidateQueries({ queryKey: ['succession-plans'] });
      toast({
        title: isEdit ? 'Plan updated' : `Plan ${saved.planNumber} created`,
        description: isEdit
          ? undefined
          : 'It is a draft until it is submitted and approved. Approval is what makes it the active version for the position.',
      });
      router.push(`/hr/succession/${saved.id}`);
    } catch (error: any) {
      // The in-progress rule answers 409 and names the plan that blocks it. That message is the
      // only thing that tells the user what to do next, so it is shown verbatim.
      const detail = error?.response?.data?.detail ?? error?.message;
      toast({
        variant: 'destructive',
        title: error?.response?.status === 409 ? 'This position already has a plan' : 'Could not save the plan',
        description: detail,
      });
    }
  });

  return (
    <form onSubmit={onSubmit} className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>The position</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <SelectField
              form={form}
              name="positionId"
              label="Position"
              required
              placeholder={positionsLoading ? 'Loading positions…' : 'Select a position'}
              options={positionOptions}
            />
            <NumberField form={form} name="planYear" label="Plan year" required />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="planName" label="Plan name" required />
            <EmployeePickerField
              form={form}
              name="currentIncumbentId"
              label="Current incumbent"
              initialLabel={plan?.currentIncumbentName ?? undefined}
            />
          </FieldRow>
          <TextareaField form={form} name="description" label="Description" />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Criticality and risk</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <SelectField
              form={form}
              name="criticality"
              label="Position criticality"
              required
              options={options(CRITICALITIES)}
            />
            <SelectField
              form={form}
              name="riskLevel"
              label="Succession risk"
              required
              options={options(RISK_LEVELS)}
            />
          </FieldRow>
          <TextareaField form={form} name="riskAssessmentNotes" label="Risk assessment notes" />
          <TextareaField
            form={form}
            name="businessImpactIfVacant"
            label="Business impact if the post falls vacant"
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>When the post is expected to fall vacant</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <DateField form={form} name="incumbentRetirementDate" label="Incumbent retirement date" />
            <DateField form={form} name="anticipatedVacancyDate" label="Anticipated vacancy date" />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="anticipatedVacancyReason"
              label="Anticipated reason"
              options={options(VACANCY_REASONS)}
              allowEmpty
              emptyLabel="Not known yet"
            />
            <DateField form={form} name="targetSuccessionDate" label="Target succession date" />
          </FieldRow>
          <TextareaField
            form={form}
            name="incumbentSuccessionNotes"
            label="Notes on the incumbent's succession"
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <ShieldAlert className="h-4 w-4" />
            Emergency cover
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm text-muted-foreground">
            Who steps in tomorrow if the post empties without warning. This is a different question
            from who is being groomed to succeed, and a plan can have one without the other.
          </p>
          <FieldRow>
            <EmployeePickerField
              form={form}
              name="emergencySuccessorId"
              label="Emergency successor"
              initialLabel={plan?.emergencySuccessorName ?? undefined}
            />
            <NumberField
              form={form}
              name="estimatedTimeToReadyMonths"
              label="Estimated months until a successor is ready"
            />
          </FieldRow>
          <TextareaField form={form} name="emergencyProtocol" label="Emergency protocol" />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Review cycle</CardTitle>
        </CardHeader>
        <CardContent>
          <FieldRow>
            <NumberField
              form={form}
              name="reviewFrequencyMonths"
              label="Review every (months)"
              required
            />
            <DateField form={form} name="nextReviewDate" label="Next review date" />
          </FieldRow>
        </CardContent>
      </Card>

      <div className="flex justify-end gap-3">
        <Button type="button" variant="outline" onClick={() => router.back()} disabled={submitting}>
          Cancel
        </Button>
        <Button type="submit" disabled={submitting}>
          {submitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          {isEdit ? 'Save changes' : 'Create draft plan'}
        </Button>
      </div>
    </form>
  );
}
