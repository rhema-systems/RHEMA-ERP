'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, Save, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  TextField,
  NumberField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { appraisalSettingsService } from '@/services/hr/appraisal.service';
import {
  evaluationWeightTotal,
  HR_REVIEW_TIMING_OPTIONS,
  INTERIM_REVIEW_DEPTH_OPTIONS,
  PEER_EVALUATION_OPEN_MODE_OPTIONS,
  PEER_NOMINATION_MODE_OPTIONS,
  READINESS_LEVEL_OPTIONS,
  REVIEW_FREQUENCY_OPTIONS,
} from '@/types/hr/appraisal';
import type { AppraisalSettings, CreateAppraisalSettings } from '@/types/hr/appraisal';

/**
 * One appraisal settings profile, in full.
 *
 * The single rule worth knowing before saving: **self + peer + manager weights must total
 * exactly 1.0**, and the API zeroes the weight of any evaluator whose switch is off *before*
 * checking. So turning peer reviews off does not free you from the total — it means self and
 * manager have to carry the whole 1.0 between them. The live total below applies the same
 * rule, so what it shows is what the server will validate.
 *
 * Everything else here is policy with no cross-field constraint; the tabs group it by the
 * stage of the appraisal it governs.
 */
const settingsSchema = z
  .object({
    settingsName: z.string().min(1, 'Required').max(100),

    requireSelfEvaluation: z.boolean(),
    allowSelfSoftSkillRating: z.boolean(),
    selfEvaluationWeight: z.coerce.number().min(0).max(1),

    requirePeerReviews: z.boolean(),
    peerNominationMode: z.string().min(1),
    minPeerEvaluators: z.coerce.number().int().min(0).max(10),
    maxPeerEvaluators: z.coerce.number().int().min(0).max(10),
    peerReviewsAnonymous: z.boolean(),
    allowPeerKpiEvaluation: z.boolean(),
    peerEvaluationWeight: z.coerce.number().min(0).max(1),
    peerEvaluationOpenMode: z.string().min(1),

    requireManagerEvaluation: z.boolean(),
    managerEvaluationWeight: z.coerce.number().min(0).max(1),

    showSelfScoreToManager: z.boolean(),
    showPeerScoresToManager: z.boolean(),
    showScoreBreakdownToEmployee: z.boolean(),

    requireCalibration: z.boolean(),
    requireHRReview: z.boolean(),
    hrCanModifyScores: z.boolean(),
    hrReviewTiming: z.string().min(1),

    requireEmployeeAcknowledgment: z.boolean(),
    allowEmployeeResponse: z.boolean(),
    allowAcknowledgmentWithoutConversation: z.boolean(),

    enableAppeals: z.boolean(),
    appealWindowDays: z.coerce.number().int().min(1).max(30),
    appealReevaluationWindowDays: z.coerce.number().int().min(1).max(30),

    requireGoalSetting: z.boolean(),
    requireManagerGoalApproval: z.boolean(),
    maxGoalsPerEmployee: z.coerce.number().int().min(1).optional(),
    minGoalsPerEmployee: z.coerce.number().int().min(1).optional(),

    enableCheckIns: z.boolean(),
    enablePrivateJournal: z.boolean(),

    requireKickOffConversation: z.boolean(),
    requireMidYearConversation: z.boolean(),
    requireFinalConversation: z.boolean(),

    reviewFrequency: z.string().min(1),
    interimReviewDepth: z.string().min(1),
    requireMidYearSelfAssessment: z.boolean(),
    requireGoalProgressUpdateAtReview: z.boolean(),

    autoLockOnDeadline: z.boolean(),

    defaultHRReviewerId: z.string().optional(),
    probationExtensionMonths: z.coerce.number().int().min(1).max(24),
    managerWorkloadThreshold: z.coerce.number().int().min(1),
    deadlineRiskHighDays: z.coerce.number().int().min(0),
    deadlineRiskMediumDays: z.coerce.number().int().min(0),
    deadlineRiskLowDays: z.coerce.number().int().min(0),
    successionPoolName: z.string().min(1).max(200),
    successionDefaultReadiness: z.string().min(1),
  })
  .refine(
    (v) =>
      v.requireSelfEvaluation || v.requirePeerReviews || v.requireManagerEvaluation,
    {
      message: 'At least one of self, peer or manager evaluation must be enabled',
      path: ['requireManagerEvaluation'],
    },
  )
  .refine((v) => Math.abs(evaluationWeightTotal(v) - 1) <= 0.005, {
    message: 'Enabled evaluation weights must total exactly 1.0',
    path: ['managerEvaluationWeight'],
  })
  .refine((v) => v.minPeerEvaluators <= v.maxPeerEvaluators, {
    message: 'The minimum cannot be above the maximum',
    path: ['maxPeerEvaluators'],
  })
  .refine(
    (v) =>
      v.minGoalsPerEmployee === undefined ||
      v.maxGoalsPerEmployee === undefined ||
      v.minGoalsPerEmployee <= v.maxGoalsPerEmployee,
    { message: 'The minimum cannot be above the maximum', path: ['maxGoalsPerEmployee'] },
  );

type SettingsForm = z.input<typeof settingsSchema>;

/** The API's own defaults, so a new profile starts somewhere sane rather than at zero. */
const emptySettings: SettingsForm = {
  settingsName: '',
  requireSelfEvaluation: true,
  allowSelfSoftSkillRating: false,
  selfEvaluationWeight: 0.1,
  requirePeerReviews: false,
  peerNominationMode: 'Employee',
  minPeerEvaluators: 0,
  maxPeerEvaluators: 5,
  peerReviewsAnonymous: true,
  allowPeerKpiEvaluation: false,
  peerEvaluationWeight: 0,
  peerEvaluationOpenMode: 'WithSelfEval',
  requireManagerEvaluation: true,
  managerEvaluationWeight: 0.9,
  showSelfScoreToManager: true,
  showPeerScoresToManager: true,
  showScoreBreakdownToEmployee: true,
  requireCalibration: true,
  requireHRReview: true,
  hrCanModifyScores: false,
  hrReviewTiming: 'AfterCalibration',
  requireEmployeeAcknowledgment: true,
  allowEmployeeResponse: true,
  allowAcknowledgmentWithoutConversation: false,
  enableAppeals: true,
  appealWindowDays: 7,
  appealReevaluationWindowDays: 5,
  requireGoalSetting: true,
  requireManagerGoalApproval: true,
  maxGoalsPerEmployee: undefined,
  minGoalsPerEmployee: undefined,
  enableCheckIns: true,
  enablePrivateJournal: true,
  requireKickOffConversation: false,
  requireMidYearConversation: false,
  requireFinalConversation: true,
  reviewFrequency: 'MidYearOnly',
  interimReviewDepth: 'LightTouch',
  requireMidYearSelfAssessment: false,
  requireGoalProgressUpdateAtReview: true,
  autoLockOnDeadline: false,
  defaultHRReviewerId: '',
  probationExtensionMonths: 3,
  managerWorkloadThreshold: 10,
  deadlineRiskHighDays: 2,
  deadlineRiskMediumDays: 5,
  deadlineRiskLowDays: 7,
  successionPoolName: 'Appraisal Nominations',
  successionDefaultReadiness: 'ReadyIn12Months',
};

const toForm = (s: AppraisalSettings): SettingsForm => ({
  ...emptySettings,
  ...s,
  maxGoalsPerEmployee: s.maxGoalsPerEmployee ?? undefined,
  minGoalsPerEmployee: s.minGoalsPerEmployee ?? undefined,
  defaultHRReviewerId: s.defaultHRReviewerId ?? '',
});

const toPayload = (values: SettingsForm): CreateAppraisalSettings => {
  const v = settingsSchema.parse(values);
  return {
    ...v,
    peerNominationMode: v.peerNominationMode as CreateAppraisalSettings['peerNominationMode'],
    peerEvaluationOpenMode:
      v.peerEvaluationOpenMode as CreateAppraisalSettings['peerEvaluationOpenMode'],
    hrReviewTiming: v.hrReviewTiming as CreateAppraisalSettings['hrReviewTiming'],
    reviewFrequency: v.reviewFrequency as CreateAppraisalSettings['reviewFrequency'],
    interimReviewDepth: v.interimReviewDepth as CreateAppraisalSettings['interimReviewDepth'],
    successionDefaultReadiness:
      v.successionDefaultReadiness as CreateAppraisalSettings['successionDefaultReadiness'],
    maxGoalsPerEmployee: v.maxGoalsPerEmployee ?? null,
    minGoalsPerEmployee: v.minGoalsPerEmployee ?? null,
    defaultHRReviewerId: v.defaultHRReviewerId || null,
  };
};

function Section({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: React.ReactNode;
}) {
  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base">{title}</CardTitle>
        {description && <CardDescription>{description}</CardDescription>}
      </CardHeader>
      <CardContent className="space-y-4">{children}</CardContent>
    </Card>
  );
}

export default function AppraisalSettingsEditorPage() {
  const params = useParams();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const id = (params?.id as string) ?? '';
  const isNew = id === 'new';
  const [confirmDelete, setConfirmDelete] = useState(false);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'appraisal-settings', id],
    queryFn: () => appraisalSettingsService.getById(id),
    enabled: !isNew && !!id,
  });

  const form = useForm<SettingsForm>({
    resolver: zodResolver(settingsSchema as any) as any,
    defaultValues: emptySettings,
  });

  useEffect(() => {
    if (data) form.reset(toForm(data));
  }, [data, form]);

  // Recomputed on every keystroke so the total is visible while the weights are being set,
  // not only once the form is submitted and rejected.
  const watched = form.watch();
  const total = evaluationWeightTotal({
    requireSelfEvaluation: !!watched.requireSelfEvaluation,
    selfEvaluationWeight: Number(watched.selfEvaluationWeight) || 0,
    requirePeerReviews: !!watched.requirePeerReviews,
    peerEvaluationWeight: Number(watched.peerEvaluationWeight) || 0,
    requireManagerEvaluation: !!watched.requireManagerEvaluation,
    managerEvaluationWeight: Number(watched.managerEvaluationWeight) || 0,
  });
  const weightsBalanced = Math.abs(total - 1) <= 0.005;

  const save = useMutation({
    mutationFn: async (values: SettingsForm) => {
      const payload = toPayload(values);
      return isNew
        ? appraisalSettingsService.create(payload)
        : appraisalSettingsService.update(id, { id, ...payload });
    },
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-settings'] });
      toast({ title: 'Saved', description: `Profile "${saved.settingsName}" saved.` });
      if (isNew) router.replace(`/administration/hr/performance/settings/${saved.id}`);
    },
    onError: (e: any) =>
      toast({
        title: 'Error',
        description: e?.message || 'Failed to save the settings profile.',
        variant: 'destructive',
      }),
  });

  const remove = useMutation({
    mutationFn: () => appraisalSettingsService.remove(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-settings'] });
      toast({ title: 'Deleted', description: 'Settings profile removed.' });
      router.push('/administration/hr/performance/settings');
    },
    onError: (e: any) =>
      toast({
        title: 'Could not delete',
        description: e?.message || 'A profile still used by a cycle cannot be deleted.',
        variant: 'destructive',
      }),
  });

  if (!isNew && isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!isNew && isError) {
    return (
      <div className="p-6">
        <EmptyState
          title="Settings profile not found"
          description="It may have been removed."
        />
      </div>
    );
  }

  return (
    <form
      className="space-y-6 p-6"
      onSubmit={form.handleSubmit((values) => save.mutate(values))}
    >
      <PageHeader
        title={isNew ? 'New Settings Profile' : (data?.settingsName ?? 'Settings Profile')}
        description="What a cycle running under this profile requires, and how its scores are combined."
        backHref="/administration/hr/performance/settings"
        actions={
          <div className="flex items-center gap-2">
            {!isNew && (
              <Button
                type="button"
                variant="outline"
                onClick={() => setConfirmDelete(true)}
                disabled={remove.isPending}
              >
                <Trash2 className="mr-2 h-4 w-4" />
                Delete
              </Button>
            )}
            <Button type="submit" disabled={save.isPending}>
              {save.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Save className="mr-2 h-4 w-4" />
              )}
              Save
            </Button>
          </div>
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-center justify-between gap-4 p-4">
          <div className="min-w-[280px] flex-1">
            <TextField
              form={form}
              name="settingsName"
              label="Profile name"
              required
              placeholder="e.g. Standard annual appraisal"
            />
          </div>
          <div className="space-y-1 text-right">
            <p className="text-sm text-muted-foreground">Evaluation weights total</p>
            <Badge variant={weightsBalanced ? 'default' : 'destructive'} className="text-base">
              {Math.round(total * 100)}%
            </Badge>
            <p className="max-w-[22rem] text-xs text-muted-foreground">
              Disabled evaluators count as zero, so the enabled ones must still reach 100%.
            </p>
          </div>
        </CardContent>
      </Card>

      <Tabs defaultValue="evaluation">
        <TabsList>
          <TabsTrigger value="evaluation">Evaluation</TabsTrigger>
          <TabsTrigger value="signoff">Sign-off &amp; appeals</TabsTrigger>
          <TabsTrigger value="goals">Goals &amp; conversations</TabsTrigger>
          <TabsTrigger value="operations">Operations</TabsTrigger>
        </TabsList>

        <TabsContent value="evaluation" className="mt-4 space-y-4">
          <Section
            title="Self-evaluation"
            description="The employee's own assessment, and what it contributes to the final score."
          >
            <SwitchField
              form={form}
              name="requireSelfEvaluation"
              label="Require a self-evaluation"
            />
            <FieldRow>
              <NumberField
                form={form}
                name="selfEvaluationWeight"
                label="Weight (0–1)"
                step="0.05"
              />
              <div />
            </FieldRow>
            {/* The field is named "allow", but what it does is require: employees can always score
                the behavioural criteria; with this on, the self-evaluation cannot be submitted until
                every one is scored (closure B2 relabelled it — it read "may rate"). */}
            <SwitchField
              form={form}
              name="allowSelfSoftSkillRating"
              label="Employees must score every behavioural criterion before submitting"
              description="Employees can always score the behavioural criteria. On, the self-evaluation is refused until every one has a score."
            />
          </Section>

          <Section
            title="Peer review"
            description="Off in most policies. When on, the nomination and anonymity rules below apply."
          >
            <SwitchField form={form} name="requirePeerReviews" label="Require peer reviews" />
            <FieldRow>
              <NumberField
                form={form}
                name="peerEvaluationWeight"
                label="Weight (0–1)"
                step="0.05"
              />
              <SelectField
                form={form}
                name="peerNominationMode"
                label="Who nominates peers"
                options={PEER_NOMINATION_MODE_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="minPeerEvaluators" label="Minimum peers" />
              <NumberField form={form} name="maxPeerEvaluators" label="Maximum peers" />
            </FieldRow>
            <SelectField
              form={form}
              name="peerEvaluationOpenMode"
              label="Peer window opens"
              options={PEER_EVALUATION_OPEN_MODE_OPTIONS}
            />
            <SwitchField
              form={form}
              name="peerReviewsAnonymous"
              label="Peer reviews are anonymous"
            />
            <SwitchField
              form={form}
              name="allowPeerKpiEvaluation"
              label="Peers may score KPIs too"
              description="Normally off — peers speak to behaviour, not to someone else's numbers."
            />
          </Section>

          <Section title="Manager evaluation">
            <SwitchField
              form={form}
              name="requireManagerEvaluation"
              label="Require a manager evaluation"
            />
            <FieldRow>
              <NumberField
                form={form}
                name="managerEvaluationWeight"
                label="Weight (0–1)"
                step="0.05"
              />
              <div />
            </FieldRow>
          </Section>

          <Section
            title="Score visibility"
            description="Who sees which numbers, and when."
          >
            <SwitchField
              form={form}
              name="showSelfScoreToManager"
              label="Managers see the self-score"
            />
            <SwitchField
              form={form}
              name="showPeerScoresToManager"
              label="Managers see peer scores"
            />
            <SwitchField
              form={form}
              name="showScoreBreakdownToEmployee"
              label="Employees see the score breakdown"
              description="Off shows the employee only their final result, not how it was composed."
            />
          </Section>
        </TabsContent>

        <TabsContent value="signoff" className="mt-4 space-y-4">
          <Section
            title="Calibration and HR review"
            description="The steps between a manager's score and the employee seeing it."
          >
            <SwitchField
              form={form}
              name="requireCalibration"
              label="Require a calibration session"
            />
            <SwitchField form={form} name="requireHRReview" label="Require an HR review" />
            <SelectField
              form={form}
              name="hrReviewTiming"
              label="HR reviews"
              options={HR_REVIEW_TIMING_OPTIONS}
            />
            <SwitchField
              form={form}
              name="hrCanModifyScores"
              label="HR may change scores"
              description="Off keeps HR's role to sign-off; a disputed score goes back to the manager."
            />
          </Section>

          <Section title="Employee acknowledgment">
            <SwitchField
              form={form}
              name="requireEmployeeAcknowledgment"
              label="Require the employee to acknowledge the result"
            />
            <SwitchField
              form={form}
              name="allowEmployeeResponse"
              label="Employees may record a written response"
            />
            <SwitchField
              form={form}
              name="allowAcknowledgmentWithoutConversation"
              label="Allow acknowledgment before the final conversation"
              description="Off is the stricter reading: the conversation has to happen first."
            />
          </Section>

          <Section
            title="Appeals"
            description="Both windows are in days and are counted from the acknowledgment."
          >
            <SwitchField form={form} name="enableAppeals" label="Allow appeals" />
            <FieldRow>
              <NumberField form={form} name="appealWindowDays" label="Appeal window (days)" />
              <NumberField
                form={form}
                name="appealReevaluationWindowDays"
                label="Re-evaluation window (days)"
              />
            </FieldRow>
          </Section>
        </TabsContent>

        <TabsContent value="goals" className="mt-4 space-y-4">
          <Section
            title="Goal setting"
            description="Governs the goal cascade for cycles on this profile."
          >
            <SwitchField form={form} name="requireGoalSetting" label="Require goal setting" />
            <SwitchField
              form={form}
              name="requireManagerGoalApproval"
              label="Goals need manager approval"
            />
            <FieldRow>
              <NumberField
                form={form}
                name="minGoalsPerEmployee"
                label="Minimum goals per employee"
                placeholder="Blank for no minimum"
              />
              <NumberField
                form={form}
                name="maxGoalsPerEmployee"
                label="Maximum goals per employee"
                placeholder="Blank for no cap"
              />
            </FieldRow>
          </Section>

          <Section title="Check-ins and journals">
            <SwitchField form={form} name="enableCheckIns" label="Enable check-ins" />
            <SwitchField
              form={form}
              name="enablePrivateJournal"
              label="Enable the private performance journal"
            />
          </Section>

          <Section
            title="Conversations"
            description="Which conversations must be recorded for an appraisal to complete."
          >
            <SwitchField
              form={form}
              name="requireKickOffConversation"
              label="Kick-off conversation"
              description="Held before the employee can submit the self-evaluation."
            />
            <SwitchField
              form={form}
              name="requireMidYearConversation"
              label="Mid-year conversation"
              description="Held before the manager can submit their evaluation."
            />
            <SwitchField
              form={form}
              name="requireFinalConversation"
              label="Final conversation"
              description="Held before the appraisal completes — and before the acknowledgment, unless that may go first."
            />
          </Section>

          <Section
            title="Interim reviews"
            description="How many review events a cycle generates, and how heavy each one is."
          >
            <FieldRow>
              <SelectField
                form={form}
                name="reviewFrequency"
                label="Frequency"
                options={REVIEW_FREQUENCY_OPTIONS}
              />
              <SelectField
                form={form}
                name="interimReviewDepth"
                label="Depth"
                options={INTERIM_REVIEW_DEPTH_OPTIONS}
              />
            </FieldRow>
            <SwitchField
              form={form}
              name="requireMidYearSelfAssessment"
              label="Require a mid-year self-assessment"
            />
            <SwitchField
              form={form}
              name="requireGoalProgressUpdateAtReview"
              label="Require a goal progress update at each review"
            />
          </Section>
        </TabsContent>

        <TabsContent value="operations" className="mt-4 space-y-4">
          <Section
            title="Deadlines"
            description="How many days out a deadline starts counting as a risk. These drive the cycle progress dashboard and the reminders HR sends from it."
          >
            <SwitchField
              form={form}
              name="autoLockOnDeadline"
              label="Advance overdue appraisals on deadline"
              description="When on, HR's advance-overdue action moves stalled steps along. When off it changes nothing."
            />
            <FieldRow>
              <NumberField form={form} name="deadlineRiskHighDays" label="High risk within (days)" />
              <NumberField
                form={form}
                name="deadlineRiskMediumDays"
                label="Medium risk within (days)"
              />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="deadlineRiskLowDays" label="Low risk within (days)" />
              <NumberField
                form={form}
                name="managerWorkloadThreshold"
                label="Manager workload threshold"
              />
            </FieldRow>
          </Section>

          <Section
            title="Outcomes"
            description="Defaults applied when an appraisal produces a downstream action."
          >
            {/* The DTO carries only the id, so a saved reviewer shows as a search box until
                one is picked again. Naming them would need a second read per profile. */}
            <EmployeePickerField
              form={form}
              name="defaultHRReviewerId"
              label="Default HR reviewer"
              placeholder="Search for an employee…"
            />
            <FieldRow>
              <NumberField
                form={form}
                name="probationExtensionMonths"
                label="Probation extension (months)"
              />
              <TextField form={form} name="successionPoolName" label="Succession pool name" />
            </FieldRow>
            <SelectField
              form={form}
              name="successionDefaultReadiness"
              label="Default succession readiness"
              options={READINESS_LEVEL_OPTIONS}
            />
          </Section>
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        title="Delete this settings profile?"
        description="Cycles already using it will block the delete."
        confirmText="Delete"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={async () => {
          await remove.mutateAsync();
        }}
      />
    </form>
  );
}
