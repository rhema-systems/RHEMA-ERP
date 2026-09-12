'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, AlertTriangle, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
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
import {
  ORIENTATION_PROGRAM_STATUS_OPTIONS,
  ORIENTATION_AUDIENCE_SCOPE_OPTIONS,
  ORIENTATION_ENROLLMENT_TRIGGER_OPTIONS,
} from '@/types/hr/orientation';
import type {
  OrientationProgramStatus,
  OrientationPrerequisite,
  OrientationAudienceRule,
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

const audienceRuleSchema = z.object({
  ruleName: z.string().min(1, 'A name is required').max(200),
  description: z.string().max(1000).optional().or(z.literal('')),
  targetType: z.enum([
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
  targetEntityId: z.string().optional().or(z.literal('')),
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
});
type AudienceRuleForm = z.infer<typeof audienceRuleSchema>;
const emptyAudienceRule: AudienceRuleForm = {
  ruleName: '',
  description: '',
  targetType: 'NewHires',
  targetEntityId: '',
  trigger: 'OnHire',
  enrollmentDelayDays: 0,
  isInclusive: true,
  isActive: true,
};

const scopeLabel = (v: string) =>
  ORIENTATION_AUDIENCE_SCOPE_OPTIONS.find((o) => o.value === v)?.label ?? v;
const triggerLabel = (v: string) =>
  ORIENTATION_ENROLLMENT_TRIGGER_OPTIONS.find((o) => o.value === v)?.label ?? v;
const blank = (v?: string) => (v && v.length > 0 ? v : null);

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
              orientationProgramService.addAudienceRule(programId, {
                programId,
                ...values,
                description: blank(values.description),
                targetEntityId: blank(values.targetEntityId),
              })
            }
            update={(_p, ruleId, values) =>
              orientationProgramService.updateAudienceRule(ruleId, {
                id: ruleId,
                ...values,
                description: blank(values.description),
                targetEntityId: blank(values.targetEntityId),
              })
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
              { header: 'Targets', cell: (r) => scopeLabel(r.targetType) },
              { header: 'Trigger', cell: (r) => triggerLabel(r.trigger) },
              {
                header: 'Delay',
                cell: (r) =>
                  r.enrollmentDelayDays === 0
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
              trigger: r.trigger,
              enrollmentDelayDays: r.enrollmentDelayDays,
              isInclusive: r.isInclusive,
              isActive: r.isActive,
            })}
            renderFields={(form) => (
              <>
                <TextField form={form} name="ruleName" label="Rule name" required />
                <TextareaField form={form} name="description" label="Description" rows={2} />
                <FieldRow>
                  <SelectField
                    form={form}
                    name="targetType"
                    label="Targets"
                    required
                    options={ORIENTATION_AUDIENCE_SCOPE_OPTIONS}
                  />
                  <SelectField
                    form={form}
                    name="trigger"
                    label="Trigger"
                    required
                    options={ORIENTATION_ENROLLMENT_TRIGGER_OPTIONS}
                  />
                </FieldRow>
                <TextField
                  form={form}
                  name="targetEntityId"
                  label="Target id"
                  placeholder="The unit, grade, location or role id this rule narrows to"
                />
                <NumberField
                  form={form}
                  name="enrollmentDelayDays"
                  label="Enrollment delay (days)"
                  required
                />
                <FieldRow>
                  <SwitchField
                    form={form}
                    name="isInclusive"
                    label="Enrols"
                    description="Turn off to make this an exclusion instead."
                  />
                  <SwitchField form={form} name="isActive" label="Active" />
                </FieldRow>
              </>
            )}
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
