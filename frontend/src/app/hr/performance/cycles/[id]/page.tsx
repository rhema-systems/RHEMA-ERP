'use client';

import { useMemo, useState } from 'react';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import {
  BellRing,
  CalendarClock,
  CheckCircle2,
  CircleSlash,
  Loader2,
  Lock,
  PlayCircle,
  TriangleAlert,
  Users,
  Wand2,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Progress } from '@/components/ui/progress';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  NumberField,
  SelectField,
  SwitchField,
  TextareaField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { CycleInterimReviewsPanel } from '@/components/hr/performance/CycleInterimReviewsPanel';
import { CyclePhaseDatesDialog } from '@/components/hr/performance/CyclePhaseDatesDialog';
import {
  appraisalCycleService,
  appraisalCycleTargetService,
  appraisalCycleTemplateService,
  appraisalTemplateService,
} from '@/services/hr/appraisal.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { APPRAISAL_TARGET_TYPE_OPTIONS } from '@/types/hr/appraisal';
import type {
  AppraisalCycleTarget,
  AppraisalCycleTargetExclusion,
  AppraisalCycleTemplate,
  AppraisalTargetType,
  DeadlineRisk,
  ProgressMetric,
  RiskLevel,
} from '@/types/hr/appraisal';
import { scopeLabel } from '@/lib/hr/appraisal-scope';
import { humanizeEnum } from '@/lib/hr/attendance-format';

/**
 * One appraisal cycle, end to end.
 *
 * The order of the tabs is the order the work happens in, and it is not arbitrary:
 *
 *   Targets   — who this cycle covers, as rules rather than a list of names.
 *   Templates — which forms it may draw on, and how ties between them break.
 *   Coverage  — a dry run of the two above. Nothing is written; this is where a missing
 *               template or a tie between two equally-specific ones shows up.
 *   Generate  — only once coverage is clean, because generation refuses on a gap or a conflict.
 *
 * Opening the cycle sits before all of that and is checked separately: an employee may be
 * covered by only one non-closed cycle of the same type and year, and the refusal names the
 * cycles that overlap.
 */
const day = (value?: string | null) => value?.slice(0, 10) ?? '—';

const RISK_TONE: Record<RiskLevel, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  None: 'outline',
  Low: 'outline',
  Medium: 'secondary',
  High: 'destructive',
  Critical: 'destructive',
};

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

/** A completion bar that says so when the settings profile switched the step off. */
function PhaseProgress({ label, metric }: { label: string; metric: ProgressMetric }) {
  if (!metric.isRequired) {
    return (
      <div className="space-y-1">
        <div className="flex items-center justify-between text-sm">
          <span>{label}</span>
          <span className="text-muted-foreground">Not required</span>
        </div>
        <Progress value={0} className="opacity-40" />
      </div>
    );
  }
  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-sm">
        <span>{label}</span>
        <span className="tabular-nums text-muted-foreground">
          {metric.completed} / {metric.total} ({Math.round(metric.percentageCompleted)}%)
        </span>
      </div>
      <Progress value={metric.percentageCompleted} />
    </div>
  );
}

function DeadlineRow({ risk }: { risk: DeadlineRisk }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 border-b py-2 last:border-0">
      <div>
        <p className="text-sm font-medium">{risk.phase}</p>
        <p className="text-xs text-muted-foreground">
          {day(risk.deadline)} · {risk.relativeTime}
        </p>
      </div>
      <Badge variant={risk.isOverdue ? 'destructive' : RISK_TONE[risk.riskLevel]}>
        {risk.isOverdue ? 'Overdue' : humanizeEnum(risk.riskLevel)}
      </Badge>
    </div>
  );
}

// ── Target group form ────────────────────────────────────────────────────────────

const targetSchema = z.object({
  targetType: z.string().min(1, 'Required'),
  organizationLevelId: z.string().optional(),
  organizationUnitId: z.string().optional(),
  positionId: z.string().optional(),
  estimatedEmployeeCount: z.coerce.number().int().min(0),
  notes: z.string().max(1000).optional(),
  isActive: z.boolean(),
});

type TargetForm = z.input<typeof targetSchema>;

const emptyTarget: TargetForm = {
  targetType: 'OrganizationUnit',
  organizationLevelId: '',
  organizationUnitId: '',
  positionId: '',
  estimatedEmployeeCount: 0,
  notes: '',
  isActive: true,
};

const exclusionSchema = z.object({
  organizationLevelId: z.string().optional(),
  organizationUnitId: z.string().optional(),
  positionId: z.string().optional(),
  employeeId: z.string().optional(),
  reason: z.string().min(1, 'Required').max(500),
  isActive: z.boolean(),
});

type ExclusionForm = z.input<typeof exclusionSchema>;

const emptyExclusion: ExclusionForm = {
  organizationLevelId: '',
  organizationUnitId: '',
  positionId: '',
  employeeId: '',
  reason: '',
  isActive: true,
};

export default function AppraisalCycleDetailPage() {
  const params = useParams();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const id = (params?.id as string) ?? '';
  const [action, setAction] = useState<null | 'open' | 'close' | 'generate' | 'remind'>(null);
  const [exclusionsFor, setExclusionsFor] = useState<AppraisalCycleTarget | null>(null);
  const [phaseDatesOpen, setPhaseDatesOpen] = useState(false);

  const {
    data: cycle,
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['hr', 'appraisal-cycles', id],
    queryFn: () => appraisalCycleService.getById(id),
    enabled: !!id,
  });

  const { data: coverage, isFetching: coverageLoading } = useQuery({
    queryKey: ['hr', 'appraisal-cycle-coverage', id],
    queryFn: () => appraisalCycleService.getCoveragePreview(id),
    enabled: !!id,
  });

  // A Draft cycle has no appraisals yet, so the progress dashboard has nothing to report on.
  const { data: progress } = useQuery({
    queryKey: ['hr', 'appraisal-cycle-progress', id],
    queryFn: () => appraisalCycleService.getProgress(id),
    enabled: !!id && !!cycle && cycle.status !== 'Draft',
  });

  const { data: calendar } = useQuery({
    queryKey: ['hr', 'appraisal-cycle-calendar', id],
    queryFn: () => appraisalCycleService.getCalendar(id),
    enabled: !!id,
  });

  const { data: templates } = useQuery({
    queryKey: ['hr', 'appraisal-templates'],
    queryFn: () => appraisalTemplateService.getSummaries(),
  });
  const { data: levels } = useQuery({
    queryKey: ['hr', 'organization-levels'],
    queryFn: () => organizationLevelService.getAll(),
  });
  const { data: units } = useQuery({
    queryKey: ['hr', 'organization-units'],
    queryFn: () => organizationUnitService.getAll(),
  });
  const { data: positions } = useQuery({
    queryKey: ['hr', 'positions', 'active'],
    queryFn: () => employeePositionService.getActive(),
  });

  const levelOptions = useMemo(
    () => (levels ?? []).map((l) => ({ value: l.id, label: l.name })),
    [levels],
  );
  const unitOptions = useMemo(
    () => (units ?? []).map((u) => ({ value: u.id, label: u.name })),
    [units],
  );
  const positionOptions = useMemo(
    () => (positions ?? []).map((p) => ({ value: p.id, label: p.title })),
    [positions],
  );

  /**
   * Only an approved, active template is worth offering — the cycle would resolve to it and
   * then generation would score people on a form nobody signed off.
   */
  const assignableTemplates = useMemo(
    () =>
      (templates ?? [])
        .filter((t) => t.isActive && t.approvalStatus === 'Approved')
        .map((t) => ({ value: t.id, label: `${t.templateName} — ${scopeLabel(t)}` })),
    [templates],
  );

  const refreshAll = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-cycles'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-cycle-coverage', id] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-cycle-progress', id] });
  };

  const runAction = useMutation({
    mutationFn: async () => {
      if (action === 'open') return appraisalCycleService.open(id);
      if (action === 'close') return appraisalCycleService.close(id);
      if (action === 'generate') return appraisalCycleService.generateAppraisals(id);
      if (action === 'remind') return appraisalCycleService.sendDeadlineReminders(id);
      return null;
    },
    onSuccess: async (result: any) => {
      await refreshAll();
      if (action === 'generate') {
        toast({
          title: 'Appraisals generated',
          description: `${result?.appraisalsCreated ?? 0} appraisal(s), ${result?.evaluationsCreated ?? 0} evaluation(s) and ${result?.reviewEventsCreated ?? 0} review event(s) created.`,
        });
      } else if (action === 'remind') {
        const raised = result?.notificationsRaised ?? 0;
        toast({
          title: raised > 0 ? 'Reminders sent' : 'Nothing to remind about',
          description:
            raised > 0
              ? `${raised} notification(s) raised.`
              : 'No phase is overdue or close enough to its deadline, or everyone already has an unread reminder.',
        });
      } else {
        toast({ title: 'Done', description: `Cycle ${action === 'open' ? 'opened' : 'closed'}.` });
      }
      setAction(null);
    },
    onError: (e: any) => {
      toast({
        title: 'Action failed',
        description: e?.message || 'The cycle could not be changed.',
        variant: 'destructive',
      });
      setAction(null);
    },
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !cycle) {
    return (
      <div className="p-6">
        <EmptyState title="Cycle not found" description="It may have been removed." />
      </div>
    );
  }

  const isDraft = cycle.status === 'Draft';
  const isClosed = cycle.status === 'Closed';
  const canGenerate = !isClosed;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${cycle.cycleCode} · ${cycle.cycleName}`}
        description={`${humanizeEnum(cycle.appraisalType)} · ${day(cycle.startDate)} → ${day(cycle.endDate)} · ${cycle.appraisalSettingsName ?? 'No settings profile'}`}
        backHref="/hr/performance/cycles"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={humanizeEnum(cycle.status)} />
            {isDraft && (
              <Button onClick={() => setAction('open')}>
                <PlayCircle className="mr-2 h-4 w-4" />
                Open cycle
              </Button>
            )}
            {canGenerate && (
              <Button variant="outline" onClick={() => setAction('generate')}>
                <Wand2 className="mr-2 h-4 w-4" />
                Generate appraisals
              </Button>
            )}
            {!isDraft && !isClosed && (
              <>
                <Button variant="outline" onClick={() => setAction('remind')}>
                  <BellRing className="mr-2 h-4 w-4" />
                  Send reminders
                </Button>
                <Button variant="outline" onClick={() => setAction('close')}>
                  <Lock className="mr-2 h-4 w-4" />
                  Close cycle
                </Button>
              </>
            )}
          </div>
        }
      />

      <MetricTiles
        tiles={[
          {
            label: 'Employees in scope',
            value: coverage?.totalTargetedEmployees ?? '—',
            hint: coverage?.hasActiveTargets === false ? 'No target groups yet' : undefined,
            icon: Users,
            tone: coverage?.hasActiveTargets === false ? 'warning' : 'default',
          },
          {
            label: 'Template coverage',
            value: coverage ? `${coverage.coveragePercentage}%` : '—',
            hint: coverage ? `${coverage.employeesWithTemplate} covered` : undefined,
            icon: CheckCircle2,
            tone:
              coverage && coverage.coveragePercentage < 100 ? 'warning' : 'success',
          },
          {
            label: 'Without a template',
            value: coverage?.employeesWithoutTemplate ?? '—',
            hint: 'Blocks generation until zero',
            icon: CircleSlash,
            tone: (coverage?.employeesWithoutTemplate ?? 0) > 0 ? 'danger' : 'default',
          },
          {
            label: 'Template conflicts',
            value: coverage?.conflictCount ?? '—',
            hint: 'Two templates tied at the same priority',
            icon: TriangleAlert,
            tone: (coverage?.conflictCount ?? 0) > 0 ? 'danger' : 'default',
          },
        ]}
      />

      {coverage && !coverage.isGenerationSafe && (
        <Card className="border-amber-500/50">
          <CardContent className="flex items-start gap-3 p-4">
            <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-600 dark:text-amber-500" />
            <div className="space-y-1 text-sm">
              <p className="font-medium">Generation would be refused</p>
              <p className="text-muted-foreground">
                {!coverage.hasActiveTargets
                  ? 'No active target groups — nobody is in scope yet.'
                  : !coverage.hasActiveTemplates
                    ? 'No active template assignments — there is no form to score anyone on.'
                    : `${coverage.employeesWithoutTemplate} employee(s) resolve to no template and ${coverage.conflictCount} have a tie between two equally-specific ones. The Coverage tab lists who.`}
              </p>
            </div>
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="targets">Targets</TabsTrigger>
          <TabsTrigger value="templates">Templates</TabsTrigger>
          <TabsTrigger value="coverage">Coverage</TabsTrigger>
          <TabsTrigger value="progress">Progress &amp; alerts</TabsTrigger>
          <TabsTrigger value="interim">Interim reviews</TabsTrigger>
          <TabsTrigger value="calendar">Calendar</TabsTrigger>
        </TabsList>

        {/* ── Overview ────────────────────────────────────────────────────── */}
        <TabsContent value="overview" className="space-y-4 pt-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Cycle</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Code" value={cycle.cycleCode} />
              <InfoRow label="Type" value={humanizeEnum(cycle.appraisalType)} />
              <InfoRow label="Year" value={cycle.year} />
              <InfoRow label="Period" value={`${day(cycle.startDate)} → ${day(cycle.endDate)}`} />
              <InfoRow label="Settings profile" value={cycle.appraisalSettingsName} />
              <InfoRow label="Status" value={humanizeEnum(cycle.status)} />
              <InfoRow
                label="Opened"
                value={
                  cycle.openedDate
                    ? `${day(cycle.openedDate)}${cycle.openedByName ? ` by ${cycle.openedByName}` : ''}`
                    : undefined
                }
              />
              <InfoRow
                label="Closed"
                value={
                  cycle.closedDate
                    ? `${day(cycle.closedDate)}${cycle.closedByName ? ` by ${cycle.closedByName}` : ''}`
                    : undefined
                }
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row items-start justify-between gap-4 pb-2">
              <div className="space-y-1.5">
                <CardTitle className="text-base">Phase dates</CardTitle>
                <CardDescription>
                  A phase with no deadline never appears on the calendar or in reminders.
                </CardDescription>
              </div>
              <Button variant="outline" size="sm" onClick={() => setPhaseDatesOpen(true)}>
                Edit dates
              </Button>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Goal setting opens" value={day(cycle.goalSettingOpenDate)} />
              <InfoRow label="Goal setting due" value={day(cycle.goalSettingDeadline)} />
              <InfoRow label="Peer nomination due" value={day(cycle.peerNominationDeadline)} />
              <InfoRow label="Self-evaluation opens" value={day(cycle.selfEvaluationOpenDate)} />
              <InfoRow label="Self-evaluation due" value={day(cycle.selfEvaluationDeadline)} />
              <InfoRow label="Peer evaluation due" value={day(cycle.peerEvaluationDeadline)} />
              <InfoRow
                label="Manager evaluation due"
                value={day(cycle.managerEvaluationDeadline)}
              />
              <InfoRow label="Calibration due" value={day(cycle.calibrationDeadline)} />
              <InfoRow label="HR review due" value={day(cycle.hrReviewDeadline)} />
              <InfoRow
                label="Acknowledgment due"
                value={day(cycle.employeeAcknowledgeDeadline)}
              />
              <InfoRow
                label="Final conversation due"
                value={day(cycle.finalConversationDeadline)}
              />
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Targets ─────────────────────────────────────────────────────── */}
        <TabsContent value="targets" className="space-y-4 pt-4">
          <p className="text-sm text-muted-foreground">
            Who this cycle covers, stated as rules rather than a list of names — so someone who
            joins the unit tomorrow is in scope without anyone editing anything. Exclusions carve
            people back out of a rule.
          </p>

          <ResourceCollectionTab<AppraisalCycleTarget, TargetForm>
            parentId={id}
            title="target groups"
            singular="target group"
            queryKey={['hr', 'appraisal-cycle-targets', id]}
            invalidateKeys={[['hr', 'appraisal-cycle-coverage', id]]}
            dialogHint="Pick the scope that matches the target type — the other two are ignored."
            emptyDescription="Add a target group so the cycle covers somebody."
            list={(cycleId) => appraisalCycleService.getTargets(cycleId)}
            create={(cycleId, values) =>
              appraisalCycleService.addTarget(cycleId, {
                appraisalCycleId: cycleId,
                targetType: values.targetType as AppraisalTargetType,
                organizationLevelId: values.organizationLevelId || null,
                organizationUnitId: values.organizationUnitId || null,
                positionId: values.positionId || null,
                estimatedEmployeeCount: Number(values.estimatedEmployeeCount) || 0,
                notes: values.notes || null,
                isActive: values.isActive,
              })
            }
            update={(cycleId, targetId, values) =>
              appraisalCycleService.updateTarget(cycleId, targetId, {
                id: targetId,
                appraisalCycleId: cycleId,
                targetType: values.targetType as AppraisalTargetType,
                organizationLevelId: values.organizationLevelId || null,
                organizationUnitId: values.organizationUnitId || null,
                positionId: values.positionId || null,
                estimatedEmployeeCount: Number(values.estimatedEmployeeCount) || 0,
                notes: values.notes || null,
                isActive: values.isActive,
              })
            }
            remove={(cycleId, targetId) => appraisalCycleService.removeTarget(cycleId, targetId)}
            readOnly={isClosed}
            getId={(r) => r.id}
            actions={[
              {
                label: 'Exclusions…',
                run: async (r) => setExclusionsFor(r),
              },
            ]}
            columns={[
              { header: 'Type', cell: (r) => <Badge variant="outline">{humanizeEnum(r.targetType)}</Badge> },
              { header: 'Scope', cell: (r) => scopeLabel(r) },
              {
                header: 'Estimated',
                cell: (r) => r.estimatedEmployeeCount,
                className: 'text-right',
              },
              {
                header: 'Resolves to',
                cell: (r) => <span className="tabular-nums">{r.activeEmployeeCount}</span>,
                className: 'text-right',
              },
              { header: 'Status', cell: (r) => <StatusBadge active={r.isActive} /> },
            ]}
            schema={targetSchema as any}
            emptyForm={emptyTarget}
            toForm={(r) => ({
              targetType: r.targetType,
              organizationLevelId: r.organizationLevelId ?? '',
              organizationUnitId: r.organizationUnitId ?? '',
              positionId: r.positionId ?? '',
              estimatedEmployeeCount: r.estimatedEmployeeCount,
              notes: r.notes ?? '',
              isActive: r.isActive,
            })}
            renderFields={(form) => (
              <>
                <SelectField
                  form={form}
                  name="targetType"
                  label="Target type"
                  required
                  options={APPRAISAL_TARGET_TYPE_OPTIONS}
                />
                <SelectField
                  form={form}
                  name="organizationLevelId"
                  label="Organisation level"
                  options={levelOptions}
                  allowEmpty
                  emptyLabel="Not set"
                />
                <SelectField
                  form={form}
                  name="organizationUnitId"
                  label="Organisation unit"
                  options={unitOptions}
                  allowEmpty
                  emptyLabel="Not set"
                />
                <SelectField
                  form={form}
                  name="positionId"
                  label="Position"
                  options={positionOptions}
                  allowEmpty
                  emptyLabel="Not set"
                />
                <NumberField
                  form={form}
                  name="estimatedEmployeeCount"
                  label="Estimated headcount"
                  placeholder="Your planning figure — the real count is resolved live"
                />
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
                <SwitchField
                  form={form}
                  name="isActive"
                  label="Active"
                  description="Inactive target groups are ignored entirely, including by the coverage preview."
                />
              </>
            )}
          />

          {exclusionsFor && (
            <Card>
              <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-3">
                <div>
                  <CardTitle className="text-base">
                    Exclusions — {humanizeEnum(exclusionsFor.targetType)}: {scopeLabel(exclusionsFor)}
                  </CardTitle>
                  <CardDescription>
                    Anyone matching an active exclusion drops out of this target group.
                  </CardDescription>
                </div>
                <Button variant="ghost" size="sm" onClick={() => setExclusionsFor(null)}>
                  Close
                </Button>
              </CardHeader>
              <CardContent>
                <ResourceCollectionTab<AppraisalCycleTargetExclusion, ExclusionForm>
                  parentId={exclusionsFor.id}
                  title="exclusions"
                  singular="exclusion"
                  queryKey={['hr', 'appraisal-target-exclusions', exclusionsFor.id]}
                  invalidateKeys={[
                    ['hr', 'appraisal-cycle-targets', id],
                    ['hr', 'appraisal-cycle-coverage', id],
                  ]}
                  dialogHint="Name whichever scope identifies the people to leave out — an employee, a position, a unit or a level."
                  emptyDescription="Nobody is excluded from this target group."
                  list={(targetId) => appraisalCycleTargetService.getExclusions(targetId)}
                  create={(targetId, values) =>
                    appraisalCycleTargetService.addExclusion(targetId, {
                      appraisalCycleTargetId: targetId,
                      organizationLevelId: values.organizationLevelId || null,
                      organizationUnitId: values.organizationUnitId || null,
                      positionId: values.positionId || null,
                      employeeId: values.employeeId || null,
                      reason: values.reason,
                      isActive: values.isActive,
                    })
                  }
                  update={(targetId, exclusionId, values) =>
                    appraisalCycleTargetService.updateExclusion(targetId, exclusionId, {
                      id: exclusionId,
                      appraisalCycleTargetId: targetId,
                      organizationLevelId: values.organizationLevelId || null,
                      organizationUnitId: values.organizationUnitId || null,
                      positionId: values.positionId || null,
                      employeeId: values.employeeId || null,
                      reason: values.reason,
                      isActive: values.isActive,
                    })
                  }
                  remove={(targetId, exclusionId) =>
                    appraisalCycleTargetService.removeExclusion(targetId, exclusionId)
                  }
                  readOnly={isClosed}
                  getId={(r) => r.id}
                  columns={[
                    {
                      header: 'Excludes',
                      cell: (r) => r.employeeName || scopeLabel(r),
                    },
                    { header: 'Reason', cell: (r) => r.reason },
                    { header: 'Status', cell: (r) => <StatusBadge active={r.isActive} /> },
                  ]}
                  schema={exclusionSchema as any}
                  emptyForm={emptyExclusion}
                  toForm={(r) => ({
                    organizationLevelId: r.organizationLevelId ?? '',
                    organizationUnitId: r.organizationUnitId ?? '',
                    positionId: r.positionId ?? '',
                    employeeId: r.employeeId ?? '',
                    reason: r.reason,
                    isActive: r.isActive,
                  })}
                  renderFields={(form) => (
                    <>
                      <EmployeePickerField
                        form={form}
                        name="employeeId"
                        label="Employee"
                        placeholder="Search for an employee…"
                      />
                      <SelectField
                        form={form}
                        name="positionId"
                        label="Position"
                        options={positionOptions}
                        allowEmpty
                        emptyLabel="Not set"
                      />
                      <SelectField
                        form={form}
                        name="organizationUnitId"
                        label="Organisation unit"
                        options={unitOptions}
                        allowEmpty
                        emptyLabel="Not set"
                      />
                      <SelectField
                        form={form}
                        name="organizationLevelId"
                        label="Organisation level"
                        options={levelOptions}
                        allowEmpty
                        emptyLabel="Not set"
                      />
                      <TextareaField
                        form={form}
                        name="reason"
                        label="Reason"
                        rows={2}
                        placeholder="e.g. On secondment for the whole period"
                      />
                      <SwitchField form={form} name="isActive" label="Active" />
                    </>
                  )}
                />
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* ── Templates ───────────────────────────────────────────────────── */}
        <TabsContent value="templates" className="space-y-4 pt-4">
          <p className="text-sm text-muted-foreground">
            The forms this cycle may draw on. Each employee gets the most specific one that
            matches them — position beats unit beats level beats global — and{' '}
            <strong>priority</strong> is what breaks a tie between two that are equally specific.
            Only approved, active templates are offered.
          </p>

          <ResourceCollectionTab<
            AppraisalCycleTemplate,
            { appraisalTemplateId: string; priority: number; isActive: boolean }
          >
            parentId={id}
            title="template assignments"
            singular="template assignment"
            queryKey={['hr', 'appraisal-cycle-templates', id]}
            invalidateKeys={[['hr', 'appraisal-cycle-coverage', id]]}
            emptyDescription="Assign at least one template, or nobody can be generated an appraisal."
            list={(cycleId) => appraisalCycleTemplateService.getByCycle(cycleId)}
            create={(cycleId, values) =>
              appraisalCycleTemplateService.create({
                appraisalCycleId: cycleId,
                appraisalTemplateId: values.appraisalTemplateId,
                priority: Number(values.priority) || 0,
                isActive: values.isActive,
              })
            }
            update={(cycleId, assignmentId, values) =>
              appraisalCycleTemplateService.update(assignmentId, {
                id: assignmentId,
                appraisalCycleId: cycleId,
                appraisalTemplateId: values.appraisalTemplateId,
                priority: Number(values.priority) || 0,
                isActive: values.isActive,
              })
            }
            remove={(_cycleId, assignmentId) => appraisalCycleTemplateService.remove(assignmentId)}
            readOnly={isClosed}
            getId={(r) => r.id}
            columns={[
              { header: 'Template', cell: (r) => <span className="font-medium">{r.templateName}</span> },
              { header: 'Scope', cell: (r) => <Badge variant="outline">{scopeLabel(r)}</Badge> },
              { header: 'Priority', cell: (r) => r.priority, className: 'text-right' },
              { header: 'Status', cell: (r) => <StatusBadge active={r.isActive} /> },
            ]}
            schema={
              z.object({
                appraisalTemplateId: z.string().min(1, 'Required'),
                priority: z.coerce.number().int().min(0),
                isActive: z.boolean(),
              }) as any
            }
            emptyForm={{ appraisalTemplateId: '', priority: 0, isActive: true }}
            toForm={(r) => ({
              appraisalTemplateId: r.appraisalTemplateId,
              priority: r.priority,
              isActive: r.isActive,
            })}
            renderFields={(form) => (
              <>
                <SelectField
                  form={form}
                  name="appraisalTemplateId"
                  label="Template"
                  required
                  options={assignableTemplates}
                  placeholder={
                    assignableTemplates.length === 0
                      ? 'No approved, active templates yet'
                      : 'Select a template…'
                  }
                />
                <FieldRow>
                  <NumberField
                    form={form}
                    name="priority"
                    label="Priority"
                    placeholder="Higher wins a tie"
                  />
                  <div />
                </FieldRow>
                <SwitchField form={form} name="isActive" label="Active" />
              </>
            )}
          />
        </TabsContent>

        {/* ── Coverage ────────────────────────────────────────────────────── */}
        <TabsContent value="coverage" className="space-y-4 pt-4">
          <p className="text-sm text-muted-foreground">
            A dry run of generation. Nothing here is written — it simulates resolving every
            in-scope employee to a template, and reports whom it could not place.
          </p>

          {coverageLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : !coverage ? (
            <EmptyState
              title="No coverage preview"
              description="Add target groups and template assignments first."
            />
          ) : (
            <>
              {(coverage.scopeOverlaps ?? []).length > 0 && (
                <Card
                  className={
                    coverage.scopeOverlaps.some((o) => o.blocksOpening)
                      ? 'border-red-500/50'
                      : 'border-amber-500/50'
                  }
                >
                  <CardHeader className="pb-2">
                    <CardTitle className="text-base">Competing cycles</CardTitle>
                    <CardDescription>
                      Other cycles of the same type and year that cover some of the same people.
                      A cycle that is already running blocks this one from opening; a draft does
                      not, but is worth re-scoping before it becomes one.
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    {coverage.scopeOverlaps.map((o) => (
                      <div
                        key={o.cycleId}
                        className="flex flex-wrap items-center justify-between gap-2 border-b py-2 last:border-0"
                      >
                        <div>
                          <p className="text-sm font-medium">
                            {o.cycleCode} · {o.cycleName}
                          </p>
                          <p className="text-xs text-muted-foreground">
                            {o.sharedEmployeeCount} shared employee
                            {o.sharedEmployeeCount === 1 ? '' : 's'}
                          </p>
                        </div>
                        <div className="flex items-center gap-2">
                          <StatusBadge status={humanizeEnum(o.status)} />
                          <Badge variant={o.blocksOpening ? 'destructive' : 'outline'}>
                            {o.blocksOpening ? 'Blocks opening' : 'Advisory'}
                          </Badge>
                        </div>
                      </div>
                    ))}
                  </CardContent>
                </Card>
              )}

              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-base">By template</CardTitle>
                  <CardDescription>
                    How many people each assigned template would service.
                  </CardDescription>
                </CardHeader>
                <CardContent className="p-0">
                  {coverage.templateBreakdown.length === 0 ? (
                    <EmptyState
                      title="Nothing resolved"
                      description="No employee matched any assigned template."
                    />
                  ) : (
                    <div className="overflow-x-auto">
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead>Template</TableHead>
                            <TableHead>Scope</TableHead>
                            <TableHead className="text-right">Priority</TableHead>
                            <TableHead className="text-right">Employees</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {coverage.templateBreakdown.map((t) => (
                            <TableRow key={t.templateId}>
                              <TableCell className="font-medium">{t.templateName}</TableCell>
                              <TableCell>
                                {t.scopeType ? `${t.scopeType}${t.scopeName ? `: ${t.scopeName}` : ''}` : 'Global'}
                              </TableCell>
                              <TableCell className="text-right tabular-nums">{t.priority}</TableCell>
                              <TableCell className="text-right tabular-nums">
                                {t.assignedEmployeeCount}
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </div>
                  )}
                </CardContent>
              </Card>

              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-base">
                    By employee ({coverage.items.length} of {coverage.totalTargetedEmployees})
                  </CardTitle>
                  <CardDescription>
                    Anyone not marked Covered is a reason generation would be refused, except an
                    explicit exclusion.
                  </CardDescription>
                </CardHeader>
                <CardContent className="p-0">
                  {coverage.items.length === 0 ? (
                    <EmptyState
                      title="Nobody in scope"
                      description="The cycle's target groups resolve to no employees."
                    />
                  ) : (
                    <div className="max-h-[32rem] overflow-auto">
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead>Employee</TableHead>
                            <TableHead>Position</TableHead>
                            <TableHead>Unit</TableHead>
                            <TableHead>Template</TableHead>
                            <TableHead>Matched on</TableHead>
                            <TableHead>Status</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {coverage.items.map((row) => (
                            <TableRow key={row.employeeId}>
                              <TableCell>
                                <span className="font-medium">{row.employeeName}</span>
                                <span className="ml-2 text-xs text-muted-foreground">
                                  {row.employeeNumber}
                                </span>
                              </TableCell>
                              <TableCell>{row.positionTitle || '—'}</TableCell>
                              <TableCell>{row.unitName || '—'}</TableCell>
                              <TableCell>
                                {row.resolvedTemplateName ||
                                  (row.conflictingTemplateNames.length > 0
                                    ? row.conflictingTemplateNames.join(' vs ')
                                    : '—')}
                              </TableCell>
                              <TableCell>{row.sourceScope || '—'}</TableCell>
                              <TableCell>
                                <Badge
                                  variant={
                                    row.status === 'Covered'
                                      ? 'default'
                                      : row.status === 'Excluded'
                                        ? 'outline'
                                        : 'destructive'
                                  }
                                  title={row.exclusionReason ?? undefined}
                                >
                                  {humanizeEnum(row.status)}
                                </Badge>
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </div>
                  )}
                </CardContent>
              </Card>
            </>
          )}
        </TabsContent>

        {/* ── Progress & alerts ───────────────────────────────────────────── */}
        <TabsContent value="progress" className="space-y-4 pt-4">
          {isDraft ? (
            <EmptyState
              title="Nothing to report yet"
              description="Progress is measured against generated appraisals, which only exist once the cycle is open and generated."
            />
          ) : !progress ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <>
              <Card>
                <CardHeader className="pb-3">
                  <CardTitle className="text-base">
                    Completion — currently in {progress.currentPhase}
                  </CardTitle>
                  <CardDescription>
                    A step the settings profile does not require is shown greyed out rather than at
                    zero, so it does not read as a gap.
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <PhaseProgress label="Self-evaluation" metric={progress.selfEvaluationProgress} />
                  <PhaseProgress label="Peer evaluation" metric={progress.peerEvaluationProgress} />
                  <PhaseProgress
                    label="Manager evaluation"
                    metric={progress.managerEvaluationProgress}
                  />
                  <PhaseProgress label="HR review" metric={progress.hrReviewProgress} />
                </CardContent>
              </Card>

              <div className="grid gap-4 lg:grid-cols-2">
                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="flex items-center gap-2 text-base">
                      <CalendarClock className="h-4 w-4" />
                      Deadline risks
                    </CardTitle>
                    <CardDescription>
                      The five year-end phases only, banded by the settings profile. “Send
                      reminders” covers more than this — goal setting, peer nomination and the
                      final conversation as well — so it can raise notices for phases that do
                      not appear here.
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    {progress.deadlineRisks.length === 0 ? (
                      <p className="py-4 text-sm text-muted-foreground">
                        No deadline is close enough to be a risk.
                      </p>
                    ) : (
                      progress.deadlineRisks.map((risk) => (
                        <DeadlineRow key={`${risk.phase}-${risk.deadline}`} risk={risk} />
                      ))
                    )}
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="flex items-center gap-2 text-base">
                      <TriangleAlert className="h-4 w-4" />
                      Bottlenecks
                    </CardTitle>
                    <CardDescription>Where the cycle is actually stuck.</CardDescription>
                  </CardHeader>
                  <CardContent>
                    {progress.topBottlenecks.length === 0 ? (
                      <p className="py-4 text-sm text-muted-foreground">Nothing is blocked.</p>
                    ) : (
                      progress.topBottlenecks.map((b) => (
                        <div
                          key={b.category}
                          className="flex items-center justify-between gap-2 border-b py-2 last:border-0"
                        >
                          <div>
                            <p className="text-sm font-medium">{b.category}</p>
                            <p className="text-xs text-muted-foreground">{b.description}</p>
                          </div>
                          <Badge variant={RISK_TONE[b.severity]}>{b.count}</Badge>
                        </div>
                      ))
                    )}
                  </CardContent>
                </Card>
              </div>

              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-base">Participation</CardTitle>
                </CardHeader>
                <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-4">
                  <InfoRow label="Targeted" value={progress.totalEmployeesTargeted} />
                  <InfoRow label="Excluded" value={progress.totalEmployeesExcluded} />
                  <InfoRow
                    label="Not started self-evaluation"
                    value={progress.employeesNotStartedSelfEvaluation}
                  />
                  <InfoRow
                    label="Managers over workload"
                    value={progress.managersWithHighWorkload}
                  />
                  <InfoRow label="Level targets" value={progress.targetBreakdown.organizationLevelTargets} />
                  <InfoRow label="Unit targets" value={progress.targetBreakdown.organizationUnitTargets} />
                  <InfoRow label="Position targets" value={progress.targetBreakdown.positionTargets} />
                  <InfoRow
                    label="Individual targets"
                    value={progress.targetBreakdown.individualEmployeeTargets}
                  />
                </CardContent>
              </Card>
            </>
          )}
        </TabsContent>

        {/* ── Interim reviews ─────────────────────────────────────────────── */}
        <TabsContent value="interim" className="pt-4">
          <CycleInterimReviewsPanel cycleId={id} />
        </TabsContent>

        {/* ── Calendar ────────────────────────────────────────────────────── */}
        <TabsContent value="calendar" className="pt-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Cycle calendar</CardTitle>
              <CardDescription>
                Derived, not stored: every phase date on the cycle plus its review events and
                check-ins, in date order.
              </CardDescription>
            </CardHeader>
            <CardContent className="p-0">
              {(calendar ?? []).length === 0 ? (
                <EmptyState
                  title="Nothing dated yet"
                  description="Set some phase dates on the cycle and they will appear here."
                />
              ) : (
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-[140px]">Date</TableHead>
                        <TableHead>Event</TableHead>
                        <TableHead>Phase</TableHead>
                        <TableHead>Kind</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {(calendar ?? []).map((e, index) => (
                        <TableRow key={`${e.date}-${e.title}-${index}`}>
                          <TableCell className="tabular-nums">{day(e.date)}</TableCell>
                          <TableCell className="font-medium">{e.title}</TableCell>
                          <TableCell className="text-muted-foreground">{e.phase || '—'}</TableCell>
                          <TableCell>
                            <Badge variant={e.category === 'Deadline' ? 'secondary' : 'outline'}>
                              {e.category}
                            </Badge>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={action !== null}
        onOpenChange={(open) => !open && setAction(null)}
        title={
          action === 'open'
            ? 'Open this cycle?'
            : action === 'close'
              ? 'Close this cycle?'
              : action === 'generate'
                ? 'Generate appraisals?'
                : 'Send deadline reminders?'
        }
        description={
          action === 'open'
            ? 'Everyone in scope is notified that the cycle is open. Opening is refused if another non-closed cycle of the same type and year already covers any of them.'
            : action === 'close'
              ? 'A closed cycle refuses every further edit. This cannot be undone.'
              : action === 'generate'
                ? 'Creates the appraisal records for everyone in scope. Refused if anyone has no template or a template conflict — check the Coverage tab first.'
                : 'Raises an in-app notification for every phase that is overdue or closing soon, to everyone in scope. Repeat-safe: identical unread reminders are skipped.'
        }
        confirmText={
          action === 'open'
            ? 'Open cycle'
            : action === 'close'
              ? 'Close cycle'
              : action === 'generate'
                ? 'Generate'
                : 'Send'
        }
        variant={action === 'close' ? 'destructive' : 'default'}
        isLoading={runAction.isPending}
        onConfirm={async () => {
          await runAction.mutateAsync();
        }}
      />

      <CyclePhaseDatesDialog
        cycle={cycle}
        open={phaseDatesOpen}
        onOpenChange={setPhaseDatesOpen}
      />
    </div>
  );
}
