'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import {
  CheckCircle2,
  Gauge,
  Library,
  Loader2,
  Lock,
  MoreHorizontal,
  Pencil,
  Plus,
  Scale,
  Send,
  Target,
  Trash2,
  Unlock,
  UserRound,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { GoalFormDialog } from '@/components/hr/performance/GoalFormDialog';
import { GoalLibraryPicker } from '@/components/hr/performance/GoalLibraryPicker';
import {
  TextField,
  NumberField,
  DateField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import {
  companyGoalService,
  employeeGoalService,
  kpiDefinitionService,
  unitGoalService,
} from '@/services/hr/goals.service';
import { formatDate, formatPercent, humanizeEnum, today } from '@/lib/hr/attendance-format';
import {
  GOAL_PERIOD_OPTIONS,
  GOAL_PRIORITY_OPTIONS,
  MEASUREMENT_TYPE_OPTIONS,
} from '@/types/hr/goals';
import type {
  EmployeeGoal,
  GoalPeriod,
  GoalPriority,
  MeasurementType,
} from '@/types/hr/goals';

/**
 * One employee's goals for a cycle — the bottom of the cascade and the only level with an
 * approval workflow.
 *
 * Two rules govern the set rather than any single goal, and both are shown above the table:
 *   • The weights should total 100. Nothing blocks a submit that leaves them unbalanced, but
 *     it is what the manager's governance view flags, so it is surfaced here first.
 *   • A goal must be aligned to something for the cascade to mean anything. Unaligned goals
 *     are allowed and called out.
 *
 * Status only ever moves through submit / approve / reject / lock. Editing a goal never
 * changes its status — the API ignores a status sent on an edit — so the row actions here are
 * the only route between states.
 */
const ALIGN_COMPANY = 'company:';
const ALIGN_UNIT = 'unit:';

const goalSchema = z
  .object({
    alignment: z.string().optional(),
    kpiDefinitionId: z.string().optional(),
    title: z.string().min(1, 'Required').max(300),
    description: z.string().max(2000).optional(),
    successCriteria: z.string().max(1000).optional(),
    weight: z.coerce.number().int().min(0).max(100),
    priority: z.string().min(1, 'Required'),
    measurementType: z.string().min(1, 'Required'),
    period: z.string().min(1, 'Required'),
    targetValue: z.coerce.number().min(0).optional(),
    minValue: z.coerce.number().min(0).optional(),
    maxValue: z.coerce.number().min(0).optional(),
    unit: z.string().max(50).optional(),
    startDate: z.string().min(1, 'Required'),
    dueDate: z.string().min(1, 'Required'),
  })
  .refine((v) => v.dueDate >= v.startDate, {
    message: 'The due date cannot be before the start date',
    path: ['dueDate'],
  })
  .refine((v) => v.minValue == null || v.maxValue == null || v.maxValue >= v.minValue, {
    message: 'The maximum cannot be below the minimum',
    path: ['maxValue'],
  });

type GoalForm = z.input<typeof goalSchema>;

const emptyForm: GoalForm = {
  alignment: '',
  kpiDefinitionId: '',
  title: '',
  description: '',
  successCriteria: '',
  weight: 0,
  priority: 'Medium',
  measurementType: 'NumericAbsolute',
  period: 'FullCycle',
  targetValue: undefined,
  minValue: undefined,
  maxValue: undefined,
  unit: '',
  startDate: today(),
  dueDate: '',
};

const PRIORITY_VARIANT: Record<GoalPriority, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Critical: 'destructive',
  High: 'default',
  Medium: 'secondary',
  Low: 'outline',
};

/** Draft and Rejected are the two states a submit is allowed from. */
const SUBMITTABLE = new Set(['Draft', 'Rejected']);
/** Everything past approval, which is where a lock becomes possible. */
const LOCKABLE = new Set(['Approved', 'InProgress', 'AtRisk', 'OnTrack', 'Completed']);
/**
 * Not yet agreed with the manager — the goals that can be deleted (closure D-72). An agreed goal is
 * sent back first.
 */
const NOT_AGREED = new Set(['Draft', 'PendingApproval', 'Rejected']);

export default function EmployeeGoalsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { user } = useAuth();
  const me = user?.employeeId ?? null;

  // Empty string rather than null for the ids: both are only ever read inside a query that
  // `enabled` already gates, and a plain string keeps them assignable without assertions.
  const [employeeId, setEmployeeId] = useState('');
  const [employeeLabel, setEmployeeLabel] = useState<string | null>(null);
  const [cycleId, setCycleId] = useState('');

  const [dialogOpen, setDialogOpen] = useState(false);
  const [libraryOpen, setLibraryOpen] = useState(false);
  const [editing, setEditing] = useState<EmployeeGoal | null>(null);
  const [libraryId, setLibraryId] = useState<string | null>(null);
  const [seedValues, setSeedValues] = useState<GoalForm | null>(null);
  const [pendingDelete, setPendingDelete] = useState<EmployeeGoal | null>(null);
  const [pendingSubmit, setPendingSubmit] = useState<EmployeeGoal | null>(null);

  const enabled = !!employeeId && !!cycleId;

  const goalsKey = ['hr', 'employee-goals', employeeId, cycleId] as const;

  const { data: goals, isLoading } = useQuery({
    queryKey: goalsKey,
    queryFn: () => employeeGoalService.getByEmployee(employeeId, cycleId),
    enabled,
  });

  const { data: summary } = useQuery({
    queryKey: ['hr', 'employee-goals', employeeId, cycleId, 'summary'],
    queryFn: () => employeeGoalService.getSummary(employeeId, cycleId),
    enabled,
  });

  const { data: companyGoals } = useQuery({
    queryKey: ['hr', 'company-goals', cycleId, 'visible'],
    queryFn: () => companyGoalService.getVisible(cycleId),
    enabled: !!cycleId,
  });

  const { data: unitGoals } = useQuery({
    queryKey: ['hr', 'unit-goals', cycleId, 'by-cycle'],
    queryFn: () => unitGoalService.getByCycle(cycleId),
    enabled: !!cycleId,
  });

  const { data: kpis } = useQuery({
    queryKey: ['hr', 'kpi-definitions'],
    queryFn: () => kpiDefinitionService.getAll(),
  });

  /**
   * Company and unit goals share one select because a goal aligns to at most one of them —
   * the server derives the parent type from whichever FK it receives, so offering two
   * independent pickers would let someone set both and have one silently win.
   */
  const alignmentOptions = useMemo(
    () => [
      ...(companyGoals ?? []).map((g) => ({
        value: `${ALIGN_COMPANY}${g.id}`,
        label: `Company · ${g.title}`,
      })),
      ...(unitGoals ?? []).map((g) => ({
        value: `${ALIGN_UNIT}${g.id}`,
        label: `Unit · ${g.organizationUnitName} — ${g.title}`,
      })),
    ],
    [companyGoals, unitGoals],
  );

  const kpiOptions = useMemo(
    () => (kpis ?? []).filter((k) => k.isActive).map((k) => ({ value: k.id, label: k.kpiName })),
    [kpis],
  );

  const rows = goals ?? [];
  const totalWeight = rows.reduce((sum, g) => sum + (g.weight ?? 0), 0);
  const unaligned = rows.filter((g) => !g.companyGoalId && !g.unitGoalId).length;

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'employee-goals'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'team-goals'] });
  };

  const failed = (verb: string) => (e: any) =>
    toast({
      title: 'Error',
      description: e?.message || `Failed to ${verb} the goal.`,
      variant: 'destructive',
    });

  const saveMutation = useMutation({
    mutationFn: async (values: GoalForm) => {
      const v = goalSchema.parse(values);
      const alignment = v.alignment ?? '';
      const companyGoalId = alignment.startsWith(ALIGN_COMPANY)
        ? alignment.slice(ALIGN_COMPANY.length)
        : null;
      const unitGoalId = alignment.startsWith(ALIGN_UNIT)
        ? alignment.slice(ALIGN_UNIT.length)
        : null;

      const content = {
        employeeId,
        appraisalCycleId: cycleId,
        companyGoalId,
        unitGoalId,
        parentGoalId: null,
        kpiDefinitionId: v.kpiDefinitionId || null,
        title: v.title,
        description: v.description || null,
        successCriteria: v.successCriteria || null,
        weight: v.weight,
        priority: v.priority as GoalPriority,
        measurementType: v.measurementType as MeasurementType,
        period: v.period as GoalPeriod,
        targetValue: v.targetValue ?? null,
        minValue: v.minValue ?? null,
        maxValue: v.maxValue ?? null,
        unit: v.unit || null,
        startDate: v.startDate,
        dueDate: v.dueDate,
      };

      if (editing) {
        // Progress is not this form's: it belongs to the progress entries (closure D-72), and
        // sending back the loaded value overwrote any entry made while the dialog was open.
        return employeeGoalService.update(editing.id, { ...content, id: editing.id });
      }
      // The library link is only ever set at creation — it records where the wording came
      // from and there is nothing to re-point it at afterwards.
      return employeeGoalService.create({ ...content, goalLibraryId: libraryId });
    },
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Saved', description: `Goal ${editing ? 'updated' : 'created'}.` });
      setDialogOpen(false);
      setEditing(null);
      setLibraryId(null);
      setSeedValues(null);
    },
    onError: failed('save'),
  });

  const workflowMutation = useMutation({
    mutationFn: ({ goal, action }: { goal: EmployeeGoal; action: 'submit' | 'lock' | 'unlock' }) =>
      action === 'submit'
        ? employeeGoalService.submit(goal.id)
        : action === 'lock'
          ? employeeGoalService.lock(goal.id)
          : employeeGoalService.unlock(goal.id),
    onSuccess: async (_data, variables) => {
      await invalidate();
      setPendingSubmit(null);
      toast({
        title: 'Done',
        description:
          variables.action === 'submit'
            ? 'Sent to the employee’s manager for approval.'
            : variables.action === 'lock'
              ? 'Goal locked — what it measures is fixed; progress is still recorded.'
              : 'Goal unlocked.',
      });
    },
    onError: (e: any) => {
      setPendingSubmit(null);
      failed('update')(e);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (goal: EmployeeGoal) => employeeGoalService.remove(goal.id),
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Deleted', description: 'Goal removed.' });
      setPendingDelete(null);
    },
    onError: failed('delete'),
  });

  const openCreate = () => {
    setEditing(null);
    setLibraryId(null);
    setSeedValues(null);
    setDialogOpen(true);
  };

  const openEdit = (goal: EmployeeGoal) => {
    setEditing(goal);
    setLibraryId(goal.goalLibraryId ?? null);
    setSeedValues(null);
    setDialogOpen(true);
  };

  const formValues: GoalForm = editing
    ? {
        alignment: editing.companyGoalId
          ? `${ALIGN_COMPANY}${editing.companyGoalId}`
          : editing.unitGoalId
            ? `${ALIGN_UNIT}${editing.unitGoalId}`
            : '',
        kpiDefinitionId: editing.kpiDefinitionId ?? '',
        title: editing.title,
        description: editing.description ?? '',
        successCriteria: editing.successCriteria ?? '',
        weight: editing.weight,
        priority: editing.priority,
        measurementType: editing.measurementType,
        period: editing.period,
        targetValue: editing.targetValue ?? undefined,
        minValue: editing.minValue ?? undefined,
        maxValue: editing.maxValue ?? undefined,
        unit: editing.unit ?? '',
        startDate: editing.startDate?.slice(0, 10) ?? today(),
        dueDate: editing.dueDate?.slice(0, 10) ?? '',
      }
    : (seedValues ?? emptyForm);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Employee Goals"
        description="An employee's goals for one cycle, and the approval workflow they move through."
        backHref="/hr/performance"
        actions={
          <Button onClick={openCreate} disabled={!enabled}>
            <Plus className="mr-2 h-4 w-4" />
            New goal
          </Button>
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-end gap-4 p-4">
          <div className="min-w-[300px] flex-1 space-y-2">
            <p className="text-sm font-medium">Employee</p>
            <EmployeePicker
              value={employeeId || null}
              initialLabel={employeeLabel}
              onChange={(id, label) => {
                setEmployeeId(id ?? '');
                setEmployeeLabel(label);
              }}
            />
          </div>
          <div className="min-w-[280px] flex-1">
            <CycleSelect value={cycleId} onChange={setCycleId} standalone={false} />
          </div>
        </CardContent>
      </Card>

      {!enabled ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={UserRound}
              title="Choose an employee and a cycle"
              description="Goals are held per employee per appraisal cycle."
            />
          </CardContent>
        </Card>
      ) : (
        <>
          <MetricTiles
            tiles={[
              {
                label: 'Goals',
                value: rows.length,
                hint: summary ? `${summary.approvedGoals} approved` : undefined,
                icon: Target,
              },
              {
                label: 'Total weight',
                value: `${totalWeight}%`,
                hint:
                  totalWeight === 100
                    ? 'Balanced'
                    : `${totalWeight > 100 ? 'Over' : 'Under'} by ${Math.abs(100 - totalWeight)}%`,
                icon: Scale,
                tone: rows.length === 0 ? 'default' : totalWeight === 100 ? 'success' : 'warning',
              },
              {
                label: 'Overall progress',
                value: summary ? formatPercent(summary.overallProgressPercent) : '—',
                hint: 'Weighted across the set',
                icon: Gauge,
              },
              {
                label: 'Awaiting approval',
                value: summary?.pendingApprovalGoals ?? '—',
                hint: summary?.atRiskGoals ? `${summary.atRiskGoals} at risk` : undefined,
                icon: Send,
                tone: summary?.atRiskGoals ? 'warning' : 'default',
              },
            ]}
          />

          {rows.length > 0 && totalWeight !== 100 && (
            <Alert>
              <Scale className="h-4 w-4" />
              <AlertTitle>Weights do not total 100%</AlertTitle>
              <AlertDescription>
                They currently add up to {totalWeight}%. Goals can still be submitted and approved,
                but the manager&apos;s governance view will flag this employee until it balances.
              </AlertDescription>
            </Alert>
          )}

          {unaligned > 0 && (
            <Alert>
              <Target className="h-4 w-4" />
              <AlertTitle>
                {unaligned} goal{unaligned === 1 ? '' : 's'} not aligned to anything
              </AlertTitle>
              <AlertDescription>
                Aligning a goal to a company or unit goal is what makes it show up in the cascade
                counts. Standalone goals are allowed, just invisible from above.
              </AlertDescription>
            </Alert>
          )}

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Goals</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {isLoading ? (
                <div className="flex items-center justify-center py-12">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : rows.length === 0 ? (
                <EmptyState
                  icon={Target}
                  title="No goals for this cycle"
                  description="Write them from scratch, or start from a goal library template."
                  action={
                    <Button size="sm" variant="outline" onClick={openCreate}>
                      <Plus className="mr-2 h-4 w-4" />
                      New goal
                    </Button>
                  }
                />
              ) : (
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Goal</TableHead>
                        <TableHead>Aligned to</TableHead>
                        <TableHead className="text-right">Weight</TableHead>
                        <TableHead>Priority</TableHead>
                        <TableHead>Status</TableHead>
                        <TableHead className="w-[150px]">Progress</TableHead>
                        <TableHead>Due</TableHead>
                        <TableHead className="w-[60px]" />
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {rows.map((goal) => (
                        <TableRow key={goal.id}>
                          <TableCell>
                            <Link
                              href={`/hr/performance/employee-goals/${goal.id}`}
                              className="font-medium hover:underline"
                            >
                              {goal.title}
                            </Link>
                            {goal.kpiName && (
                              <p className="text-xs text-muted-foreground">KPI: {goal.kpiName}</p>
                            )}
                          </TableCell>
                          <TableCell>
                            {goal.parentGoalTitle ? (
                              <span className="text-sm">
                                {goal.parentType && (
                                  <Badge variant="outline" className="mr-1">
                                    {goal.parentType}
                                  </Badge>
                                )}
                                {goal.parentGoalTitle}
                              </span>
                            ) : (
                              <span className="text-sm text-muted-foreground">Standalone</span>
                            )}
                          </TableCell>
                          <TableCell className="text-right tabular-nums">{goal.weight}%</TableCell>
                          <TableCell>
                            <Badge variant={PRIORITY_VARIANT[goal.priority]}>{goal.priority}</Badge>
                          </TableCell>
                          <TableCell>
                            <div className="flex items-center gap-1">
                              <StatusBadge status={humanizeEnum(goal.status)} />
                              {goal.isLocked && (
                                <Lock className="h-3 w-3 text-muted-foreground" aria-label="Locked" />
                              )}
                            </div>
                          </TableCell>
                          <TableCell>
                            <div className="flex items-center gap-2">
                              <Progress value={Number(goal.progressPercent)} className="h-2" />
                              <span className="w-12 shrink-0 text-right text-xs tabular-nums">
                                {formatPercent(goal.progressPercent)}
                              </span>
                            </div>
                          </TableCell>
                          <TableCell>{formatDate(goal.dueDate)}</TableCell>
                          <TableCell>
                            <DropdownMenu>
                              <DropdownMenuTrigger asChild>
                                <Button variant="ghost" size="icon" className="h-8 w-8">
                                  <MoreHorizontal className="h-4 w-4" />
                                  <span className="sr-only">Actions</span>
                                </Button>
                              </DropdownMenuTrigger>
                              <DropdownMenuContent align="end">
                                {/* An agreed goal's description, priority and dates stay editable;
                                    what it measures is refused with the reason (D-30). */}
                                {!goal.isLocked && (
                                  <DropdownMenuItem onClick={() => openEdit(goal)}>
                                    <Pencil className="mr-2 h-4 w-4" />
                                    Edit
                                  </DropdownMenuItem>
                                )}
                                {SUBMITTABLE.has(goal.status) && (
                                  <DropdownMenuItem onClick={() => setPendingSubmit(goal)}>
                                    <Send className="mr-2 h-4 w-4" />
                                    Submit for approval
                                  </DropdownMenuItem>
                                )}
                                {LOCKABLE.has(goal.status) && !goal.isLocked && (
                                  <DropdownMenuItem
                                    onClick={() =>
                                      workflowMutation.mutate({ goal, action: 'lock' })
                                    }
                                  >
                                    <Lock className="mr-2 h-4 w-4" />
                                    Lock
                                  </DropdownMenuItem>
                                )}
                                {goal.isLocked && goal.employeeId !== me && (
                                  <DropdownMenuItem
                                    onClick={() =>
                                      workflowMutation.mutate({ goal, action: 'unlock' })
                                    }
                                  >
                                    <Unlock className="mr-2 h-4 w-4" />
                                    Unlock
                                  </DropdownMenuItem>
                                )}
                                <DropdownMenuItem asChild>
                                  <Link href={`/hr/performance/employee-goals/${goal.id}`}>
                                    <CheckCircle2 className="mr-2 h-4 w-4" />
                                    Open detail
                                  </Link>
                                </DropdownMenuItem>
                                {!goal.isLocked && NOT_AGREED.has(goal.status) && (
                                  <>
                                    <DropdownMenuSeparator />
                                    <DropdownMenuItem
                                      className="text-red-600"
                                      onClick={() => setPendingDelete(goal)}
                                    >
                                      <Trash2 className="mr-2 h-4 w-4" />
                                      Delete
                                    </DropdownMenuItem>
                                  </>
                                )}
                              </DropdownMenuContent>
                            </DropdownMenu>
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

      <GoalFormDialog<GoalForm>
        open={dialogOpen}
        onOpenChange={(open) => {
          setDialogOpen(open);
          if (!open) {
            setEditing(null);
            setLibraryId(null);
            setSeedValues(null);
          }
        }}
        editingId={editing?.id ?? null}
        title="goal"
        hint="New goals start as a draft. Submitting is what sends them to the manager."
        schema={goalSchema as any}
        values={formValues}
        submitting={saveMutation.isPending}
        onSubmit={async (values) => saveMutation.mutateAsync(values)}
        header={
          !editing ? (
            <div className="flex items-center justify-between rounded-md border border-dashed p-3">
              <div className="text-sm">
                <p className="font-medium">Start from a template</p>
                <p className="text-muted-foreground">
                  {libraryId
                    ? 'Wording copied from the goal library.'
                    : 'Copies the wording; everything stays editable.'}
                </p>
              </div>
              <Button type="button" variant="outline" size="sm" onClick={() => setLibraryOpen(true)}>
                <Library className="mr-2 h-4 w-4" />
                Goal library
              </Button>
            </div>
          ) : undefined
        }
        renderFields={(form) => (
          <>
            <TextField form={form} name="title" label="Title" required />
            <SelectField
              form={form}
              name="alignment"
              label="Aligned to"
              options={alignmentOptions}
              allowEmpty
              emptyLabel="Standalone — not aligned"
              placeholder={
                alignmentOptions.length ? 'Company or unit goal…' : 'Nothing to align to yet'
              }
            />
            <TextareaField form={form} name="description" label="Description" rows={3} />
            <TextareaField form={form} name="successCriteria" label="Success criteria" rows={2} />
            <FieldRow>
              <NumberField form={form} name="weight" label="Weight (%)" required />
              <SelectField
                form={form}
                name="priority"
                label="Priority"
                required
                options={GOAL_PRIORITY_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="measurementType"
                label="Measurement type"
                required
                options={MEASUREMENT_TYPE_OPTIONS}
              />
              <SelectField
                form={form}
                name="period"
                label="Period"
                required
                options={GOAL_PERIOD_OPTIONS}
              />
            </FieldRow>
            <SelectField
              form={form}
              name="kpiDefinitionId"
              label="KPI definition"
              options={kpiOptions}
              allowEmpty
              emptyLabel="No KPI"
              placeholder={kpiOptions.length ? 'Select a KPI…' : 'No active KPI definitions'}
            />
            <FieldRow>
              <NumberField form={form} name="targetValue" label="Target value" step="0.01" />
              <TextField form={form} name="unit" label="Unit" placeholder="%, GHS, calls…" />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="minValue" label="Minimum" step="0.01" />
              <NumberField form={form} name="maxValue" label="Maximum" step="0.01" />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="startDate" label="Start date" required />
              <DateField form={form} name="dueDate" label="Due date" required />
            </FieldRow>
          </>
        )}
      />

      <GoalLibraryPicker
        open={libraryOpen}
        onOpenChange={setLibraryOpen}
        onSelect={(item) => {
          setLibraryId(item.id);
          // Seeding through `values` rather than the live form: the dialog resets from
          // `values` whenever it opens, so writing to the form directly would be undone.
          setSeedValues({
            ...emptyForm,
            title: item.title,
            description: item.description ?? '',
            successCriteria: item.successCriteria ?? '',
          });
        }}
      />

      <ConfirmationDialog
        open={pendingSubmit !== null}
        onOpenChange={(open) => !open && setPendingSubmit(null)}
        title="Submit for approval?"
        description={
          pendingSubmit
            ? `“${pendingSubmit.title}” goes to the employee's direct manager to approve or reject. It cannot be re-submitted from any later status, so a change of mind means asking them to reject it first.`
            : ''
        }
        confirmText="Submit"
        isLoading={workflowMutation.isPending}
        onConfirm={async () => {
          if (pendingSubmit) {
            await workflowMutation.mutateAsync({ goal: pendingSubmit, action: 'submit' });
          }
        }}
      />

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title="Delete goal?"
        description={pendingDelete ? `“${pendingDelete.title}” and its progress entries.` : ''}
        confirmText="Delete"
        variant="destructive"
        isLoading={deleteMutation.isPending}
        onConfirm={async () => {
          if (pendingDelete) await deleteMutation.mutateAsync(pendingDelete);
        }}
      />
    </div>
  );
}
