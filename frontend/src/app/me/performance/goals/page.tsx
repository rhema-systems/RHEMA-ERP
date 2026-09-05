'use client';

/**
 * Area 25 slice 5 — my goals (spec destination #8, the census's verdict-E hole).
 *
 * The desk register (`/hr/performance/employee-goals`) is HR-shaped — an employee picker,
 * the whole cascade, the KPI library — and stays where it is (D3). This page is the
 * employee's own leg of the bespoke goal lifecycle (deliberately OFF the workflow engine):
 * draft my goal → submit to my manager → they approve or reject with feedback → I record
 * progress against it until the cycle scores it.
 *
 * Every read and write here is self-armed: the list is `by-employee/{my id}`, and
 * submit/progress take the actor from the token — creating or acting on anyone else's
 * goal is refused server-side (proven in the slice harness).
 */

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { CheckCircle2, Gauge, Plus, Send, Target, TriangleAlert } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { GoalFormDialog } from '@/components/hr/performance/GoalFormDialog';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  TextareaField,
  TextField,
} from '@/components/hr/employee/tabs/fields';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum, today } from '@/lib/hr/attendance-format';
import { employeeGoalService } from '@/services/hr/goals.service';
import {
  GOAL_PERIOD_OPTIONS,
  GOAL_PRIORITY_OPTIONS,
  GOAL_PROGRESS_STATUS_OPTIONS,
  MEASUREMENT_TYPE_OPTIONS,
  type EmployeeGoal,
  type GoalProgressStatus,
} from '@/types/hr/goals';

const goalSchema = z
  .object({
    title: z.string().min(1, 'A title is required').max(300),
    description: z.string().max(2000).optional().or(z.literal('')),
    weight: z.coerce.number().min(1, 'Weight must be at least 1').max(100),
    priority: z.string().min(1),
    measurementType: z.string().min(1),
    period: z.string().min(1),
    targetValue: z.coerce.number().optional(),
    unit: z.string().max(50).optional().or(z.literal('')),
    startDate: z.string().min(1, 'Start date is required'),
    dueDate: z.string().min(1, 'Due date is required'),
  })
  .refine((v) => v.dueDate >= v.startDate, {
    message: 'Due date cannot be before the start date',
    path: ['dueDate'],
  });

type GoalFormValues = z.infer<typeof goalSchema>;

const emptyGoal: GoalFormValues = {
  title: '',
  description: '',
  weight: 20,
  priority: 'Medium',
  measurementType: 'PercentageTarget',
  period: 'FullCycle',
  targetValue: 100,
  unit: '',
  startDate: today(),
  dueDate: '',
};

/** Statuses whose goal the owner may still edit and re-submit. */
const EDITABLE = ['Draft', 'Rejected'];
/** Statuses accepting progress entries (mirrors the service's LiveExecutionStatuses). */
const LIVE = ['Approved', 'InProgress', 'OnTrack', 'AtRisk'];

export default function MyGoalsPage() {
  const { user } = useAuth();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const employeeId = user?.employeeId ?? '';

  const [cycleId, setCycleId] = useState('');
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<EmployeeGoal | null>(null);
  const [progressGoal, setProgressGoal] = useState<EmployeeGoal | null>(null);
  const [progress, setProgress] = useState({ percent: '', actual: '', status: 'InProgress', notes: '' });

  const { data: goals, isLoading, isError, error } = useQuery({
    queryKey: ['me', 'goals', employeeId, cycleId],
    queryFn: () => employeeGoalService.getByEmployee(employeeId, cycleId || undefined),
    enabled: !!employeeId,
  });

  const { data: summary } = useQuery({
    queryKey: ['me', 'goal-summary', employeeId, cycleId],
    queryFn: () => employeeGoalService.getSummary(employeeId, cycleId),
    enabled: !!employeeId && !!cycleId,
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['me', 'goals'] });
    queryClient.invalidateQueries({ queryKey: ['me', 'goal-summary'] });
  };

  const save = useMutation({
    mutationFn: (v: GoalFormValues) => {
      const payload = {
        employeeId,
        appraisalCycleId: cycleId,
        title: v.title.trim(),
        description: v.description?.trim() || null,
        weight: v.weight,
        priority: v.priority as EmployeeGoal['priority'],
        measurementType: v.measurementType as EmployeeGoal['measurementType'],
        period: v.period as EmployeeGoal['period'],
        targetValue: v.targetValue ?? null,
        unit: v.unit?.trim() || null,
        startDate: v.startDate,
        dueDate: v.dueDate,
      };
      return editing
        ? employeeGoalService.update(editing.id, {
            id: editing.id,
            progressPercent: editing.progressPercent,
            ...payload,
          })
        : employeeGoalService.create(payload);
    },
    onSuccess: () => {
      toast({
        title: editing ? 'Goal updated' : 'Goal drafted',
        description: 'Submit it to send it to your manager for approval.',
      });
      setFormOpen(false);
      setEditing(null);
      refresh();
    },
    onError: (e: Error) =>
      toast({ title: 'Could not save the goal', description: e.message, variant: 'destructive' }),
  });

  const submit = useMutation({
    mutationFn: (goalId: string) => employeeGoalService.submit(goalId),
    onSuccess: () => {
      toast({ title: 'Submitted', description: 'Your manager has it now.' });
      refresh();
    },
    onError: (e: Error) =>
      toast({ title: 'Could not submit', description: e.message, variant: 'destructive' }),
  });

  const addProgress = useMutation({
    mutationFn: () => {
      if (!progressGoal) return Promise.reject(new Error('Choose a goal first.'));
      return employeeGoalService.addProgressEntry(progressGoal.id, {
        employeeGoalId: progressGoal.id,
        progressPercent: progress.percent === '' ? null : Number(progress.percent),
        actualValue: progress.actual === '' ? null : Number(progress.actual),
        status: progress.status as GoalProgressStatus,
        notes: progress.notes.trim() || null,
      });
    },
    onSuccess: () => {
      toast({ title: 'Progress recorded', description: '100% completes the goal.' });
      setProgressGoal(null);
      setProgress({ percent: '', actual: '', status: 'InProgress', notes: '' });
      refresh();
    },
    onError: (e: Error) =>
      toast({ title: 'Could not record progress', description: e.message, variant: 'destructive' }),
  });

  const rows = goals ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="My goals"
        description="Draft your goals for the cycle, send them to your manager, and record progress as you go."
        backHref="/me"
        actions={
          <Button
            onClick={() => {
              setEditing(null);
              setFormOpen(true);
            }}
            disabled={!cycleId}
          >
            <Plus className="mr-2 h-4 w-4" />
            New goal
          </Button>
        }
      />

      <CycleSelect value={cycleId} onChange={setCycleId} />
      {!cycleId && (
        <p className="text-sm text-muted-foreground">
          Choose an appraisal cycle — goals belong to one.
        </p>
      )}

      {summary && (
        <MetricTiles
          tiles={[
            { label: 'Goals', value: summary.totalGoals, icon: Target },
            {
              label: 'Awaiting approval',
              value: summary.pendingApprovalGoals,
              tone: summary.pendingApprovalGoals > 0 ? ('warning' as const) : ('default' as const),
            },
            {
              label: 'Overall progress',
              value: `${Math.round(summary.overallProgressPercent)}%`,
              icon: Gauge,
            },
            {
              label: 'Goal setting',
              value: summary.goalSettingComplete ? 'Complete' : 'Open',
              icon: CheckCircle2,
              tone: summary.goalSettingComplete ? ('success' as const) : ('default' as const),
            },
          ]}
        />
      )}

      {isError ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Could not load your goals"
              description={(error as Error)?.message ?? 'Try again shortly.'}
            />
          </CardContent>
        </Card>
      ) : isLoading ? (
        <div className="space-y-2">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-24 w-full" />
          ))}
        </div>
      ) : rows.length === 0 ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={Target}
              title="No goals in this cycle"
              description="Draft your first goal — it goes to your manager for approval before it counts."
            />
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {rows.map((goal) => (
            <Card key={goal.id}>
              <CardContent className="space-y-3 p-4">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="min-w-0">
                    <div className="font-medium">{goal.title}</div>
                    <div className="mt-0.5 text-xs text-muted-foreground">
                      {humanizeEnum(goal.priority)} · weight {goal.weight}
                      {goal.kpiName ? ` · KPI: ${goal.kpiName}` : ''}
                      {' · '}
                      {formatDate(goal.startDate)} – {formatDate(goal.dueDate)}
                    </div>
                    {goal.description && (
                      <p className="mt-1 text-sm text-muted-foreground">{goal.description}</p>
                    )}
                    {goal.status === 'Rejected' && goal.managerFeedback && (
                      <p className="mt-1 rounded-md border border-red-200 bg-red-50 px-2 py-1 text-xs text-red-900 dark:border-red-500/40 dark:bg-red-950/40 dark:text-red-200">
                        Your manager: “{goal.managerFeedback}”
                      </p>
                    )}
                  </div>
                  <div className="flex shrink-0 items-center gap-2">
                    <StatusBadge status={humanizeEnum(goal.status)} />
                    {goal.isLocked && <Badge variant="secondary">Locked</Badge>}
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  <Progress value={Number(goal.progressPercent) || 0} className="h-2" />
                  <span className="w-10 text-right text-xs tabular-nums text-muted-foreground">
                    {Math.round(Number(goal.progressPercent) || 0)}%
                  </span>
                </div>

                <div className="flex flex-wrap gap-2">
                  {EDITABLE.includes(goal.status) && !goal.isLocked && (
                    <>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => {
                          setEditing(goal);
                          setFormOpen(true);
                        }}
                      >
                        Edit
                      </Button>
                      <Button
                        size="sm"
                        disabled={submit.isPending}
                        onClick={() => submit.mutate(goal.id)}
                      >
                        <Send className="mr-1 h-4 w-4" />
                        Submit for approval
                      </Button>
                    </>
                  )}
                  {LIVE.includes(goal.status) && !goal.isLocked && (
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setProgressGoal(goal)}
                    >
                      <Gauge className="mr-1 h-4 w-4" />
                      Record progress
                    </Button>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <GoalFormDialog
        open={formOpen}
        onOpenChange={(open) => {
          setFormOpen(open);
          if (!open) setEditing(null);
        }}
        editingId={editing?.id ?? null}
        title={editing ? 'Edit goal' : 'New goal'}
        hint="Your manager approves goals before they count towards the cycle."
        schema={goalSchema}
        values={
          editing
            ? {
                title: editing.title,
                description: editing.description ?? '',
                weight: editing.weight,
                priority: editing.priority,
                measurementType: editing.measurementType,
                period: editing.period,
                targetValue: editing.targetValue ?? undefined,
                unit: editing.unit ?? '',
                startDate: editing.startDate,
                dueDate: editing.dueDate,
              }
            : emptyGoal
        }
        onSubmit={(v) => save.mutateAsync(v)}
        submitting={save.isPending}
        renderFields={(form) => (
          <>
            <TextField form={form} name="title" label="Title" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField form={form} name="priority" label="Priority" options={GOAL_PRIORITY_OPTIONS} />
              <NumberField form={form} name="weight" label="Weight (%)" required />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="measurementType"
                label="Measured as"
                options={MEASUREMENT_TYPE_OPTIONS}
              />
              <SelectField form={form} name="period" label="Period" options={GOAL_PERIOD_OPTIONS} />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="targetValue" label="Target value" />
              <TextField form={form} name="unit" label="Unit" />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="startDate" label="Starts" required />
              <DateField form={form} name="dueDate" label="Due" required />
            </FieldRow>
          </>
        )}
      />

      <Dialog open={!!progressGoal} onOpenChange={(open) => !open && setProgressGoal(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record progress</DialogTitle>
            <DialogDescription>
              {progressGoal?.title} — the latest entry becomes the goal&apos;s progress, and
              100% completes it.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Progress %</Label>
                <Input
                  type="number"
                  min={0}
                  max={100}
                  value={progress.percent}
                  onChange={(e) => setProgress((p) => ({ ...p, percent: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label>Actual value</Label>
                <Input
                  type="number"
                  value={progress.actual}
                  onChange={(e) => setProgress((p) => ({ ...p, actual: e.target.value }))}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Status</Label>
              <Select
                value={progress.status}
                onValueChange={(v) => setProgress((p) => ({ ...p, status: v }))}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {GOAL_PROGRESS_STATUS_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea
                rows={2}
                value={progress.notes}
                onChange={(e) => setProgress((p) => ({ ...p, notes: e.target.value }))}
                placeholder="What moved, and what is in the way"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setProgressGoal(null)}>
              Close
            </Button>
            <Button
              disabled={progress.percent === '' || addProgress.isPending}
              onClick={() => addProgress.mutate()}
            >
              Record
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
