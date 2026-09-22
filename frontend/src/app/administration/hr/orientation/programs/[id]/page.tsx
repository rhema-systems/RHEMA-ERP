'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import type { UseFormReturn } from 'react-hook-form';
import { z } from 'zod';
import { Loader2, AlertTriangle, Trash2, Users, UserPlus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import {
  OrientationProgramForm,
  toOrientationProgramRequest,
  type OrientationProgramFormValues,
} from '@/components/hr/orientation/OrientationProgramForm';
import { ProgramModulesPanel } from '@/components/hr/orientation/ProgramModulesPanel';
import { ProgramQuestionsPanel } from '@/components/hr/orientation/ProgramQuestionsPanel';
import { orientationProgramService } from '@/services/hr/orientation-program.service';
import { orientationCategoryService } from '@/services/hr/orientation-lookup.service';
import { AudienceTargetPicker } from '@/components/hr/orientation/AudienceTargetPicker';
import {
  ORIENTATION_PROGRAM_STATUS_OPTIONS,
  ORIENTATION_ENROLLMENT_TRIGGER_OPTIONS,
  ORIENTATION_AUDIENCE_POPULATION_OPTIONS,
  ORIENTATION_TRIGGER_HINTS,
  DATED_TRIGGERS,
} from '@/types/hr/orientation';
import type {
  OrientationProgramStatus,
  OrientationPrerequisite,
  OrientationAudienceRule,
  OrientationAudiencePopulation,
  OrientationTriggerRunResult,
  HrAudienceTargetType,
} from '@/types/hr/orientation';

const prerequisiteSchema = z.object({
  prerequisiteProgramId: z.string().min(1, 'Choose a programme'),
  isMandatory: z.boolean(),
  notes: z.string().max(1000).optional().or(z.literal('')),
});
type PrerequisiteForm = z.infer<typeof prerequisiteSchema>;
const emptyPrerequisite: PrerequisiteForm = {
  prerequisiteProgramId: '',
  isMandatory: true,
  notes: '',
};

const audienceRuleSchema = z
  .object({
    ruleName: z.string().min(1, 'A name is required').max(200),
    description: z.string().max(1000).optional().or(z.literal('')),
    // Round 4, lane I1: the shared HR audience axis, narrowed by a population.
    targetType: z.enum([
      'AllEmployees',
      'OrganizationUnit',
      'OrganizationLevel',
      'Position',
      'Location',
      'Employee',
    ]),
    targetEntityId: z.string().optional().or(z.literal('')),
    /** Display only — the saved target's name, so the employee picker can show who is chosen. */
    targetLabel: z.string().optional().or(z.literal('')),
    population: z.enum(['Anyone', 'NewHires', 'Management', 'Contractors']),
    trigger: z.enum([
      'OnHire',
      'OnTransfer',
      'OnPromotion',
      'OnProgramPublish',
      'Scheduled',
      'Manual',
    ]),
    enrollmentDelayDays: z.coerce.number().min(0).max(3650),
    isInclusive: z.boolean(),
    isActive: z.boolean(),
  })
  .superRefine((v, ctx) => {
    if (v.targetType !== 'AllEmployees' && !v.targetEntityId)
      ctx.addIssue({ code: 'custom', path: ['targetEntityId'], message: 'Choose what this rule targets.' });
    if (v.enrollmentDelayDays > 0 && !DATED_TRIGGERS.includes(v.trigger))
      ctx.addIssue({
        code: 'custom',
        path: ['enrollmentDelayDays'],
        message: 'Only hire, transfer and promotion rules have a date to wait from. Set it to 0.',
      });
  });
type AudienceRuleForm = z.infer<typeof audienceRuleSchema>;
const emptyAudienceRule: AudienceRuleForm = {
  ruleName: '',
  description: '',
  targetType: 'AllEmployees',
  targetEntityId: '',
  targetLabel: '',
  population: 'NewHires',
  trigger: 'OnHire',
  enrollmentDelayDays: 0,
  isInclusive: true,
  isActive: true,
};

const toRuleRequest = (values: AudienceRuleForm) => ({
  ruleName: values.ruleName,
  description: blank(values.description),
  targetType: values.targetType,
  targetEntityId: values.targetType === 'AllEmployees' ? null : blank(values.targetEntityId),
  population: values.population,
  trigger: values.trigger,
  enrollmentDelayDays: DATED_TRIGGERS.includes(values.trigger) ? values.enrollmentDelayDays : 0,
  isInclusive: values.isInclusive,
  isActive: values.isActive,
});

const triggerLabel = (v: string) =>
  ORIENTATION_ENROLLMENT_TRIGGER_OPTIONS.find((o) => o.value === v)?.label ?? v;
const populationLabel = (v: string) =>
  ORIENTATION_AUDIENCE_POPULATION_OPTIONS.find((o) => o.value === v)?.label ?? v;
const blank = (v?: string) => (v && v.length > 0 ? v : null);

/**
 * The rule's live reach while it is being written — "this rule reaches 412 people" — so a rule that
 * reaches nobody, or everybody, is seen before it is saved rather than after it fires.
 */
function RuleReachPreview({
  targetType,
  targetEntityId,
  population,
}: {
  targetType: HrAudienceTargetType;
  targetEntityId?: string | null;
  population: OrientationAudiencePopulation;
}) {
  const ready = targetType === 'AllEmployees' || !!targetEntityId;
  const { data, isFetching, error } = useQuery({
    queryKey: ['hr', 'orientation', 'rule-reach', targetType, targetEntityId ?? null, population],
    queryFn: () =>
      orientationProgramService.countReach({
        targetType,
        targetEntityId: targetType === 'AllEmployees' ? null : targetEntityId || null,
        population,
      }),
    enabled: ready,
    staleTime: 30_000,
    retry: false,
  });

  if (!ready) return null;
  return (
    <div className="bg-muted/50 flex items-center gap-2 rounded-md border px-3 py-2 text-sm">
      <Users className="text-muted-foreground h-4 w-4 shrink-0" />
      {isFetching ? (
        <span className="text-muted-foreground">Counting…</span>
      ) : error ? (
        <span className="text-destructive">{(error as Error).message}</span>
      ) : data ? (
        <span>
          Reaches <strong className={data.count === 0 ? 'text-destructive' : undefined}>{data.count}</strong>{' '}
          {data.count === 1 ? 'person' : 'people'} today — {data.description}
        </span>
      ) : null}
    </div>
  );
}

/** The rule form's body: a typed target, a population, the trigger explained, the reach. */
function AudienceRuleFields({ form }: { form: UseFormReturn<AudienceRuleForm> }) {
  const targetType = form.watch('targetType');
  const targetEntityId = form.watch('targetEntityId');
  const population = form.watch('population');
  const trigger = form.watch('trigger');
  const dated = DATED_TRIGGERS.includes(trigger);
  const targetError = form.formState.errors.targetEntityId?.message;

  return (
    <>
      <TextField form={form} name="ruleName" label="Rule name" required />
      <TextareaField form={form} name="description" label="Description" rows={2} />

      <AudienceTargetPicker
        targetType={targetType}
        targetId={targetEntityId || null}
        initialLabel={form.getValues('targetLabel') || null}
        onTypeChange={(t) => form.setValue('targetType', t, { shouldValidate: false })}
        onTargetChange={(id) => form.setValue('targetEntityId', id ?? '', { shouldValidate: true })}
        idPrefix="rule-target"
      />
      {targetError && <p className="text-destructive text-sm">{String(targetError)}</p>}

      <div className="space-y-1.5">
        <SelectField
          form={form}
          name="population"
          label="Who, of the people there"
          required
          options={ORIENTATION_AUDIENCE_POPULATION_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
        />
        <p className="text-muted-foreground text-xs">
          {ORIENTATION_AUDIENCE_POPULATION_OPTIONS.find((o) => o.value === population)?.hint}
        </p>
      </div>

      <RuleReachPreview targetType={targetType} targetEntityId={targetEntityId} population={population} />

      <div className="space-y-1.5">
        <SelectField
          form={form}
          name="trigger"
          label="Trigger"
          required
          options={ORIENTATION_ENROLLMENT_TRIGGER_OPTIONS}
        />
        <p className="text-muted-foreground text-xs">{ORIENTATION_TRIGGER_HINTS[trigger]}</p>
      </div>

      {dated && (
        <NumberField
          form={form}
          name="enrollmentDelayDays"
          label="Enrollment delay (days)"
          description="Counted from the hire date or the movement's effective date. A rule fires for 30 days after its day comes, and never reaches further back."
          required
        />
      )}
      <FieldRow>
        <SwitchField
          form={form}
          name="isInclusive"
          label="Enrols"
          description="Turn off to make this an exclusion — it keeps people out whatever the trigger."
        />
        <SwitchField form={form} name="isActive" label="Active" />
      </FieldRow>
    </>
  );
}

/**
 * HR's "Enrol audience now": previews first (nothing written), then enrols on confirmation. Runs the
 * programme's rules that have no date to count from — manual, publish and scheduled.
 */
function EnrolAudiencePanel({ programId, isActive }: { programId: string; isActive: boolean }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [preview, setPreview] = useState<OrientationTriggerRunResult | null>(null);
  const [busy, setBusy] = useState(false);

  const runPreview = async () => {
    setBusy(true);
    try {
      setPreview(await orientationProgramService.enrolAudience(programId, true));
    } catch (e) {
      toast({ title: 'Could not preview', description: (e as Error).message, variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  const enrol = async () => {
    setBusy(true);
    try {
      const result = await orientationProgramService.enrolAudience(programId, false);
      toast({
        title: `${result.enrolled} enrolled`,
        description: `${result.alreadyEnrolled} already on it, ${result.excluded} excluded, ${result.waitingOnPrerequisite} waiting on a prerequisite.`,
      });
      setPreview(null);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-programs', programId] });
    } catch (e) {
      toast({ title: 'Could not enrol', description: (e as Error).message, variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card className="mb-4">
      <CardContent className="flex flex-wrap items-center justify-between gap-3 py-4">
        <div className="text-sm">
          <div className="font-medium">Rules fire by themselves</div>
          <div className="text-muted-foreground">
            Hire, transfer and promotion rules fire on the event; scheduled rules every night. “Enrol
            audience now” runs the manual, publish and scheduled rules immediately.{' '}
            <Link href="/hr/orientation/triggers" className="underline underline-offset-2">
              Why did — or didn’t — a rule reach someone?
            </Link>
          </div>
        </div>
        <Button variant="outline" onClick={runPreview} disabled={busy || !isActive}>
          {busy && !preview ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <UserPlus className="mr-2 h-4 w-4" />}
          Enrol audience now
        </Button>
        {!isActive && (
          <p className="text-muted-foreground w-full text-xs">Only an active programme enrols anyone.</p>
        )}
      </CardContent>

      <ConfirmationDialog
        open={preview !== null}
        onOpenChange={(o) => !o && setPreview(null)}
        title={preview ? `Enrol ${preview.enrolled} ${preview.enrolled === 1 ? 'person' : 'people'}?` : ''}
        description={
          preview
            ? preview.rulesEvaluated === 0
              ? 'This programme has no active manual, publish or scheduled rule to run.'
              : `${preview.rulesEvaluated} rule(s). ${preview.alreadyEnrolled} already on the programme, ${preview.excluded} excluded, ${preview.waitingOnPrerequisite} waiting on a prerequisite.` +
                (preview.enrolments.length > 0
                  ? ` First: ${preview.enrolments
                      .slice(0, 5)
                      .map((e) => e.employeeName ?? e.employeeNumber ?? e.employeeId)
                      .join(', ')}${preview.enrolled > 5 ? '…' : ''}`
                  : '')
            : ''
        }
        confirmText={preview && preview.enrolled > 0 ? 'Enrol them' : 'Close'}
        isLoading={busy}
        onConfirm={preview && preview.enrolled > 0 ? enrol : () => setPreview(null)}
      />
    </Card>
  );
}

/**
 * One programme: its overview, the modules and content people work through, what has to be done
 * first, who is enrolled automatically, and the question paper.
 *
 * Status changes are made here rather than on the form. Draft → Active is the meaningful one, and it
 * is deliberately a separate act from editing: a programme with no modules or an assessment with no
 * questions is legitimate while drafting and a defect once live, so the warnings below sit next to
 * the control that would publish it.
 */
export default function OrientationProgramDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [savingOverview, setSavingOverview] = useState(false);
  const [pendingStatus, setPendingStatus] = useState<OrientationProgramStatus | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [busy, setBusy] = useState(false);

  const queryKey = ['hr', 'orientation-programs', id];
  const { data: program, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => orientationProgramService.getById(id),
    enabled: !!id,
  });

  const { data: categories = [] } = useQuery({
    queryKey: ['hr', 'orientation-categories', 'lookup'],
    queryFn: () => orientationCategoryService.getLookup(),
  });

  // Prerequisite picker options. A programme cannot require itself, so it is filtered out here
  // rather than offered and refused.
  const { data: allPrograms = [] } = useQuery({
    queryKey: ['hr', 'orientation-programs'],
    queryFn: () => orientationProgramService.getAll(),
  });
  const prerequisiteOptions = allPrograms
    .filter((p) => p.id !== id)
    .map((p) => ({ value: p.id, label: `${p.programCode} — ${p.title}` }));

  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-programs'] }),
    ]);

  const handleOverviewSubmit = async (values: OrientationProgramFormValues) => {
    setSavingOverview(true);
    try {
      await orientationProgramService.update(id, {
        id,
        ...toOrientationProgramRequest(values),
      } as any);
      await invalidate();
      toast({ title: 'Saved', description: 'Programme updated.' });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update the programme.',
        variant: 'destructive',
      });
    } finally {
      setSavingOverview(false);
    }
  };

  const changeStatus = async () => {
    if (!pendingStatus) return false;
    setBusy(true);
    try {
      await orientationProgramService.changeStatus(id, {
        programId: id,
        newStatus: pendingStatus,
      });
      await invalidate();
      toast({ title: 'Status changed', description: `Now ${pendingStatus}.` });
      setPendingStatus(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to change the status.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    setBusy(true);
    try {
      await orientationProgramService.remove(id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-programs'] });
      toast({ title: 'Programme deleted' });
      router.push('/administration/hr/orientation/programs');
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete the programme.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (isError || !program) {
    return (
      <div className="p-6">
        <EmptyState title="Programme not found" description="It may have been removed." />
      </div>
    );
  }

  // Warnings that only matter once a programme is live. A draft is allowed to be incomplete.
  const noModules = program.moduleCount === 0;
  const assessmentWithoutQuestions =
    program.requiresAssessment && program.assessmentQuestions.length === 0;
  const isActive = program.status === 'Active';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={program.title}
        description={`${program.programCode}${
          program.categoryName ? ` · ${program.categoryName}` : ''
        }${program.version ? ` · v${program.version}` : ''}`}
        backHref="/administration/hr/orientation/programs"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={program.status} />
            <Select
              value=""
              onValueChange={(v) => setPendingStatus(v as OrientationProgramStatus)}
            >
              <SelectTrigger className="w-[170px]">
                <SelectValue placeholder="Change status…" />
              </SelectTrigger>
              <SelectContent>
                {ORIENTATION_PROGRAM_STATUS_OPTIONS.filter(
                  (o) => o.value !== program.status,
                ).map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button
              variant="outline"
              size="icon"
              onClick={() => setConfirmDelete(true)}
              aria-label="Delete programme"
            >
              <Trash2 className="h-4 w-4" />
            </Button>
          </div>
        }
      />

      <MetricTiles
        tiles={[
          { label: 'Modules', value: program.moduleCount },
          { label: 'Sessions', value: program.sessionCount },
          { label: 'Enrolled', value: program.enrollmentCount },
          {
            label: 'Completed',
            value: program.completedCount,
            hint:
              program.enrollmentCount > 0
                ? `${Math.round((program.completedCount / program.enrollmentCount) * 100)}% of those enrolled`
                : 'Nobody enrolled yet',
          },
        ]}
      />

      {isActive && (noModules || assessmentWithoutQuestions) && (
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>This programme is live but incomplete</AlertTitle>
          <AlertDescription>
            <ul className="ml-4 list-disc">
              {noModules && (
                <li>
                  It has no modules, so there is nothing for a participant to work through.
                </li>
              )}
              {assessmentWithoutQuestions && (
                <li>
                  It requires an assessment but has no questions, so nobody enrolled on it can
                  complete it.
                </li>
              )}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      <Tabs defaultValue="overview">
        <TabsList className="flex-wrap">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="modules">Modules &amp; content ({program.moduleCount})</TabsTrigger>
          <TabsTrigger value="prerequisites">
            Prerequisites ({program.prerequisites.length})
          </TabsTrigger>
          <TabsTrigger value="audience">
            Audience rules ({program.audienceRules.length})
          </TabsTrigger>
          <TabsTrigger value="questions">
            Questions ({program.assessmentQuestions.length})
          </TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="pt-4">
          <OrientationProgramForm
            programCode={program.programCode}
            ownerInitialLabel={null}
            defaultValues={{
              title: program.title,
              description: program.description ?? '',
              objectives: program.objectives ?? '',
              categoryId: program.categoryId ?? '',
              programType: program.programType,
              defaultDeliveryMode: program.defaultDeliveryMode,
              priority: program.priority,
              audienceScope: program.audienceScope,
              estimatedDurationMinutes: program.estimatedDurationMinutes ?? undefined,
              requiresAssessment: program.requiresAssessment,
              passingScorePercent: program.passingScorePercent ?? undefined,
              requiresAcknowledgement: program.requiresAcknowledgement,
              completionDeadlineDays: program.completionDeadlineDays ?? undefined,
              isCertificateIssued: program.isCertificateIssued,
              certificateValidityMonths: program.certificateValidityMonths ?? undefined,
              isRecurring: program.isRecurring,
              recurrenceFrequency: program.recurrenceFrequency ?? '',
              enableReminders: program.enableReminders,
              version: program.version ?? '',
              effectiveFrom: program.effectiveFrom ? program.effectiveFrom.slice(0, 10) : '',
              effectiveTo: program.effectiveTo ? program.effectiveTo.slice(0, 10) : '',
              tags: program.tags ?? '',
              ownerEmployeeId: program.ownerEmployeeId ?? '',
            }}
            categories={categories}
            onSubmit={handleOverviewSubmit}
            submitting={savingOverview}
            submitLabel="Save changes"
            onCancel={() => router.push('/administration/hr/orientation/programs')}
          />
        </TabsContent>

        <TabsContent value="modules" className="pt-4">
          <ProgramModulesPanel programId={id} />
        </TabsContent>

        <TabsContent value="prerequisites" className="pt-4">
          <ResourceCollectionTab<OrientationPrerequisite, PrerequisiteForm>
            parentId={id}
            title="prerequisites"
            singular="prerequisite"
            queryKey={['hr', 'orientation-programs', id, 'prerequisites']}
            invalidateKeys={[queryKey]}
            // There is no update endpoint — a prerequisite is added or removed, not amended.
            allowUpdate={false}
            dialogHint="Another programme that has to be completed before this one."
            emptyDescription="No prerequisites — anyone can be enrolled on this programme directly."
            list={() => orientationProgramService.getPrerequisites(id)}
            create={(programId, values) =>
              orientationProgramService.addPrerequisite(programId, {
                programId,
                prerequisiteProgramId: values.prerequisiteProgramId,
                isMandatory: values.isMandatory,
                notes: blank(values.notes),
              })
            }
            update={() => Promise.resolve()}
            remove={(_p, prerequisiteId) =>
              orientationProgramService.removePrerequisite(prerequisiteId)
            }
            getId={(p) => p.id}
            columns={[
              {
                header: 'Programme',
                cell: (p) => (
                  <div>
                    <span className="font-medium">{p.prerequisiteProgramTitle ?? '—'}</span>
                    {p.prerequisiteProgramCode && (
                      <div className="text-muted-foreground font-mono text-xs">
                        {p.prerequisiteProgramCode}
                      </div>
                    )}
                  </div>
                ),
              },
              {
                header: 'Mandatory',
                cell: (p) =>
                  p.isMandatory ? (
                    <Badge variant="secondary">Mandatory</Badge>
                  ) : (
                    <Badge variant="outline">Advisory</Badge>
                  ),
              },
              { header: 'Notes', cell: (p) => p.notes || '—' },
            ]}
            schema={prerequisiteSchema as any}
            emptyForm={emptyPrerequisite}
            toForm={(p) => ({
              prerequisiteProgramId: p.prerequisiteProgramId,
              isMandatory: p.isMandatory,
              notes: p.notes ?? '',
            })}
            renderFields={(form) => (
              <>
                <SelectField
                  form={form}
                  name="prerequisiteProgramId"
                  label="Prerequisite programme"
                  required
                  options={prerequisiteOptions}
                />
                <SwitchField
                  form={form}
                  name="isMandatory"
                  label="Mandatory"
                  description="An advisory prerequisite is recorded but does not block enrollment."
                />
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />
        </TabsContent>

        <TabsContent value="audience" className="pt-4">
          <EnrolAudiencePanel programId={id} isActive={isActive} />
          <ResourceCollectionTab<OrientationAudienceRule, AudienceRuleForm>
            parentId={id}
            title="audience rules"
            singular="audience rule"
            queryKey={['hr', 'orientation-programs', id, 'audience-rules']}
            invalidateKeys={[queryKey]}
            dialogHint="Who gets enrolled automatically, and what triggers it."
            emptyDescription="No rules — everyone on this programme is enrolled by hand."
            list={() => orientationProgramService.getAudienceRules(id)}
            create={(programId, values) =>
              orientationProgramService.addAudienceRule(programId, { programId, ...toRuleRequest(values) })
            }
            update={(_p, ruleId, values) =>
              orientationProgramService.updateAudienceRule(ruleId, { id: ruleId, ...toRuleRequest(values) })
            }
            remove={(_p, ruleId) => orientationProgramService.removeAudienceRule(ruleId)}
            getId={(r) => r.id}
            columns={[
              {
                header: 'Rule',
                cell: (r) => (
                  <div>
                    <span className="font-medium">{r.ruleName}</span>
                    {r.description && (
                      <div className="text-muted-foreground mt-0.5 line-clamp-1 text-xs">
                        {r.description}
                      </div>
                    )}
                  </div>
                ),
              },
              {
                header: 'Targets',
                cell: (r) => (
                  <div>
                    <div>{r.targetEntityName ?? r.targetType}</div>
                    {r.population !== 'Anyone' && (
                      <div className="text-muted-foreground text-xs">
                        {populationLabel(r.population)} only
                      </div>
                    )}
                  </div>
                ),
              },
              {
                header: 'Reach today',
                cell: (r) =>
                  r.reachCount === null || r.reachCount === undefined ? (
                    '—'
                  ) : (
                    <span className={r.reachCount === 0 ? 'text-destructive' : undefined}>
                      {r.reachCount}
                    </span>
                  ),
              },
              { header: 'Trigger', cell: (r) => triggerLabel(r.trigger) },
              {
                header: 'Delay',
                cell: (r) =>
                  !DATED_TRIGGERS.includes(r.trigger) || r.enrollmentDelayDays === 0
                    ? 'Immediately'
                    : `${r.enrollmentDelayDays} day${r.enrollmentDelayDays === 1 ? '' : 's'} after`,
              },
              {
                header: 'Effect',
                cell: (r) =>
                  r.isInclusive ? (
                    <Badge variant="secondary">Enrols</Badge>
                  ) : (
                    <Badge variant="outline">Excludes</Badge>
                  ),
              },
              { header: 'Status', cell: (r) => <StatusBadge active={r.isActive} /> },
            ]}
            schema={audienceRuleSchema as any}
            emptyForm={emptyAudienceRule}
            toForm={(r) => ({
              ruleName: r.ruleName,
              description: r.description ?? '',
              targetType: r.targetType,
              targetEntityId: r.targetEntityId ?? '',
              targetLabel: r.targetEntityName ?? '',
              population: r.population ?? 'Anyone',
              trigger: r.trigger,
              enrollmentDelayDays: r.enrollmentDelayDays,
              isInclusive: r.isInclusive,
              isActive: r.isActive,
            })}
            renderFields={(form) => <AudienceRuleFields form={form} />}
          />
        </TabsContent>

        <TabsContent value="questions" className="pt-4">
          {!program.requiresAssessment && (
            <Alert className="mb-4">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>This programme does not require an assessment</AlertTitle>
              <AlertDescription>
                Questions can be authored here, but nobody will be asked them until “Requires an
                assessment” is switched on in the overview.
              </AlertDescription>
            </Alert>
          )}
          <ProgramQuestionsPanel programId={id} />
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={pendingStatus !== null}
        onOpenChange={(o) => !o && setPendingStatus(null)}
        title={`Change status to ${pendingStatus}?`}
        description={
          pendingStatus === 'Active'
            ? 'The programme becomes available for enrollment and its audience rules start firing.'
            : pendingStatus === 'Retired' || pendingStatus === 'Archived'
              ? 'Existing enrollments are kept, but nobody new can be enrolled.'
              : 'Enrollment on this programme is paused until it is made active again.'
        }
        confirmText="Change status"
        isLoading={busy}
        onConfirm={changeStatus}
      />

      <ConfirmationDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        title="Delete this programme?"
        description="An active programme cannot be deleted — suspend or retire it first. This cannot be undone."
        confirmText="Delete"
        variant="destructive"
        isLoading={busy}
        onConfirm={remove}
      />
    </div>
  );
}
