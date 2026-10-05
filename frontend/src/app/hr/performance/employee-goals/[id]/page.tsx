'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import {
  CheckCircle2,
  Loader2,
  Lock,
  MessageSquareWarning,
  Send,
  Target,
  Undo2,
  Unlock,
  XCircle,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { GoalRequiredSkillsPanel } from '@/components/hr/performance/GoalRequiredSkillsPanel';
import {
  NumberField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { employeeGoalService } from '@/services/hr/goals.service';
import { useAuth } from '@/hooks/use-auth';
import { hasAnyPermissionAccess } from '@/lib/permissions';
import { formatDate, formatDateTime, formatPercent, humanizeEnum } from '@/lib/hr/attendance-format';
import { GOAL_PROGRESS_STATUS_OPTIONS } from '@/types/hr/goals';
import type { GoalProgressEntry, GoalProgressStatus } from '@/types/hr/goals';

/**
 * One employee goal: its content, where it sits in the approval workflow, and the progress
 * entries recorded against it.
 *
 * Every transition is a separate command and the API decides who may run each one — the
 * owner submits, their direct manager approves, rejects and locks. The buttons below are
 * offered on status alone, so a manager-only action attempted by someone else comes back
 * 403 with the reason rather than being hidden as if it did not exist.
 *
 * Progress entries drive both of the goal's execution fields: the newest entry's percent
 * becomes the goal's, and its status becomes the goal's — which is how a goal reaches
 * On track or At risk, and therefore how it reaches the at-risk reports. 100% completes it
 * regardless of what the entry says. The API only accepts entries while the goal is approved
 * and still running.
 */
const SUBMITTABLE = new Set(['Draft', 'Rejected']);
const APPROVABLE = new Set(['PendingApproval']);
// What an approved goal measures cannot be edited; the manager sends it back instead (decision
// D-30). Not once it is locked or completed — the server refuses both.
const SENDABLE_BACK = new Set(['Approved', 'InProgress', 'OnTrack', 'AtRisk']);
const LOCKABLE = new Set(['Approved', 'InProgress', 'AtRisk', 'OnTrack', 'Completed']);
// A locked goal's year runs on — a lock freezes what the goal is, not its progress — and a goal
// the old lock left in the Locked status reads as approved.
const PROGRESS_OPEN = new Set(['Approved', 'InProgress', 'AtRisk', 'OnTrack', 'Locked']);
// Agreed with the manager (GoalSetRules.IsAgreed): the only goals whose entries can be corrected
// or removed (closure D-72).
const AGREED = new Set(['Approved', 'InProgress', 'OnTrack', 'AtRisk', 'Completed', 'Locked']);

// No `recordedById`: the recorder is the signed-in user, stamped server-side. It used to be a
// picker defaulting to the goal's owner "so HR could record on someone's behalf", which is the
// same thing as letting anyone attribute a progress claim to a colleague who never made it.
const progressSchema = z.object({
  progressPercent: z.coerce.number().min(0).max(100).optional(),
  actualValue: z.coerce.number().min(0).optional(),
  status: z.string().min(1, 'Required'),
  challenges: z.string().max(500).optional(),
  notes: z.string().max(2000).optional(),
});

type ProgressForm = z.input<typeof progressSchema>;

export default function EmployeeGoalDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { user } = useAuth();

  // 'sendBack' is the same reject call on an approved goal, worded for what it does there.
  const [decision, setDecision] = useState<'approve' | 'reject' | 'sendBack' | null>(null);
  const [feedback, setFeedback] = useState('');
  const [confirmLock, setConfirmLock] = useState(false);
  const [confirmSubmit, setConfirmSubmit] = useState(false);

  const goalKey = ['hr', 'employee-goals', 'detail', id] as const;

  const { data: goal, isLoading } = useQuery({
    queryKey: goalKey,
    queryFn: () => employeeGoalService.getById(id),
    enabled: !!id,
  });

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'employee-goals'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'team-goals'] });
  };

  const failed = (e: any) =>
    toast({
      title: 'Could not complete that',
      // The API's message is the useful part here — it names the rule that stopped the
      // transition, or says the caller is not the manager.
      description: e?.message || 'The request was refused.',
      variant: 'destructive',
    });

  const workflowMutation = useMutation({
    mutationFn: async (action: 'submit' | 'approve' | 'reject' | 'lock' | 'unlock') => {
      if (action === 'submit') return employeeGoalService.submit(id);
      if (action === 'approve') return employeeGoalService.approve(id, feedback.trim() || null);
      if (action === 'reject') return employeeGoalService.reject(id, feedback.trim());
      if (action === 'lock') return employeeGoalService.lock(id);
      return employeeGoalService.unlock(id);
    },
    onSuccess: async (_d, action) => {
      const done =
        action === 'submit' ? 'submitted'
        : action === 'reject' && decision === 'sendBack' ? 'sent back to the employee'
        : `${action}ed`;
      await invalidate();
      setDecision(null);
      setFeedback('');
      setConfirmLock(false);
      setConfirmSubmit(false);
      toast({ title: 'Done', description: `Goal ${done}.` });
    },
    onError: (e) => {
      setDecision(null);
      setConfirmLock(false);
      setConfirmSubmit(false);
      failed(e);
    },
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!goal) {
    return (
      <div className="p-6">
        <EmptyState icon={Target} title="Goal not found" description="It may have been deleted." />
      </div>
    );
  }

  const emptyProgress: ProgressForm = {
    progressPercent: undefined,
    actualValue: undefined,
    status: 'InProgress',
    challenges: '',
    notes: '',
  };

  const progressAllowed = PROGRESS_OPEN.has(goal.status);

  // P8: an entry is corrected or withdrawn by whoever recorded it, or HR when HR is not the
  // goal's owner — the server refuses everyone else, so they are not offered Edit or Remove.
  const me = user?.employeeId ?? null;
  const isDeskWriter = hasAnyPermissionAccess(user, ['HR.Performance.Write', 'HR.Performance.Admin']);
  // D-72: entries are corrected on an agreed goal only — a goal sent back keeps its entries, and
  // correcting the latest one carried its status onto the goal.
  const goalAgreed = AGREED.has(goal.status);
  const mayAmendEntry = (entry: GoalProgressEntry) =>
    goalAgreed && ((!!me && entry.recordedById === me) || (isDeskWriter && me !== goal.employeeId));
  // D-72: nobody unlocks their own goal — an HR officer included.
  const mayUnlock = goal.isLocked && me !== goal.employeeId;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={goal.title}
        description={`${goal.employeeName}${goal.cycleCode ? ` · ${goal.cycleCode}` : ''}`}
        backHref="/hr/performance/employee-goals"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={humanizeEnum(goal.status)} />
            {SUBMITTABLE.has(goal.status) && !goal.isLocked && (
              <Button size="sm" onClick={() => setConfirmSubmit(true)}>
                <Send className="mr-2 h-4 w-4" />
                Submit
              </Button>
            )}
            {APPROVABLE.has(goal.status) && (
              <>
                <Button size="sm" onClick={() => setDecision('approve')}>
                  <CheckCircle2 className="mr-2 h-4 w-4" />
                  Approve
                </Button>
                <Button size="sm" variant="outline" onClick={() => setDecision('reject')}>
                  <XCircle className="mr-2 h-4 w-4" />
                  Reject
                </Button>
              </>
            )}
            {SENDABLE_BACK.has(goal.status) && !goal.isLocked && (
              <Button size="sm" variant="outline" onClick={() => setDecision('sendBack')}>
                <Undo2 className="mr-2 h-4 w-4" />
                Send back
              </Button>
            )}
            {LOCKABLE.has(goal.status) && !goal.isLocked && (
              <Button size="sm" variant="outline" onClick={() => setConfirmLock(true)}>
                <Lock className="mr-2 h-4 w-4" />
                Lock
              </Button>
            )}
            {mayUnlock && (
              <Button
                size="sm"
                variant="outline"
                onClick={() => workflowMutation.mutate('unlock')}
                disabled={workflowMutation.isPending}
              >
                <Unlock className="mr-2 h-4 w-4" />
                Unlock
              </Button>
            )}
          </div>
        }
      />

      {goal.status === 'Rejected' && goal.managerFeedback && (
        <Alert variant="destructive">
          <MessageSquareWarning className="h-4 w-4" />
          <AlertTitle>Returned by the manager</AlertTitle>
          <AlertDescription className="whitespace-pre-wrap">
            {goal.managerFeedback}
          </AlertDescription>
        </Alert>
      )}

      {goal.isLocked && (
        <Alert>
          <Lock className="h-4 w-4" />
          <AlertTitle>Locked</AlertTitle>
          <AlertDescription>
            Locked {formatDateTime(goal.lockedDate)}. What the goal measures — its title, target,
            weight and period — cannot change until it is unlocked. Progress is still recorded
            against it through the year.
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Goal</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4 text-sm">
            <div>
              <p className="text-muted-foreground">Description</p>
              <p className="mt-1 whitespace-pre-wrap">{goal.description || 'Not provided.'}</p>
            </div>
            <div>
              <p className="text-muted-foreground">Success criteria</p>
              <p className="mt-1 whitespace-pre-wrap">{goal.successCriteria || 'Not provided.'}</p>
            </div>
            {goal.managerFeedback && goal.status !== 'Rejected' && (
              <div>
                <p className="text-muted-foreground">Manager feedback</p>
                <p className="mt-1 whitespace-pre-wrap">{goal.managerFeedback}</p>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">At a glance</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div className="space-y-1">
              <div className="flex justify-between gap-2">
                <span className="text-muted-foreground">Progress</span>
                <span className="tabular-nums">{formatPercent(goal.progressPercent)}</span>
              </div>
              <Progress value={Number(goal.progressPercent)} />
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-muted-foreground">Weight</span>
              <span className="tabular-nums">{goal.weight}%</span>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-muted-foreground">Priority</span>
              <Badge variant="outline">{goal.priority}</Badge>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-muted-foreground">Aligned to</span>
              <span className="text-right">
                {goal.parentGoalTitle ? (
                  goal.companyGoalId ? (
                    <Link
                      href={`/hr/performance/company-goals/${goal.companyGoalId}`}
                      className="hover:underline"
                    >
                      {goal.parentGoalTitle}
                    </Link>
                  ) : goal.unitGoalId ? (
                    <Link
                      href={`/hr/performance/unit-goals/${goal.unitGoalId}`}
                      className="hover:underline"
                    >
                      {goal.parentGoalTitle}
                    </Link>
                  ) : (
                    goal.parentGoalTitle
                  )
                ) : (
                  'Standalone'
                )}
              </span>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-muted-foreground">Measured as</span>
              <span>{humanizeEnum(goal.measurementType)}</span>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-muted-foreground">Target</span>
              <span>
                {goal.targetValue == null
                  ? '—'
                  : `${goal.targetValue}${goal.unit ? ` ${goal.unit}` : ''}`}
              </span>
            </div>
            {(goal.minValue != null || goal.maxValue != null) && (
              <div className="flex justify-between gap-2">
                <span className="text-muted-foreground">Range</span>
                <span>
                  {goal.minValue ?? '—'} – {goal.maxValue ?? '—'}
                </span>
              </div>
            )}
            {goal.kpiName && (
              <div className="flex justify-between gap-2">
                <span className="text-muted-foreground">KPI</span>
                <span>{goal.kpiName}</span>
              </div>
            )}
            {goal.libraryItemTitle && (
              <div className="flex justify-between gap-2">
                <span className="text-muted-foreground">From template</span>
                <span className="text-right">{goal.libraryItemTitle}</span>
              </div>
            )}
            <div className="flex justify-between gap-2">
              <span className="text-muted-foreground">Period</span>
              <span>{humanizeEnum(goal.period)}</span>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-muted-foreground">Runs</span>
              <span>
                {formatDate(goal.startDate)} → {formatDate(goal.dueDate)}
              </span>
            </div>
          </CardContent>
        </Card>
      </div>

      <GoalRequiredSkillsPanel goalId={id} readOnly={goal.isLocked} />

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Approval trail</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 text-sm sm:grid-cols-3">
          <div>
            <p className="text-muted-foreground">Submitted to</p>
            <p className="mt-1">{goal.managerName || 'Not submitted'}</p>
          </div>
          <div>
            <p className="text-muted-foreground">Submitted</p>
            <p className="mt-1">{formatDateTime(goal.submittedDate)}</p>
          </div>
          <div>
            <p className="text-muted-foreground">Approved</p>
            <p className="mt-1">{formatDateTime(goal.approvalDate)}</p>
          </div>
        </CardContent>
      </Card>

      <div className="space-y-2">
        <h2 className="text-lg font-semibold">Progress entries</h2>
        {!progressAllowed && (
          <p className="text-sm text-muted-foreground">
            {`Entries are only accepted once the goal is approved — it is currently ${humanizeEnum(goal.status).toLowerCase()}.`}
          </p>
        )}
        <ResourceCollectionTab<GoalProgressEntry, ProgressForm>
          parentId={id}
          title="progress entries"
          singular="progress entry"
          queryKey={['hr', 'employee-goals', id, 'progress']}
          invalidateKeys={[[...goalKey], ['hr', 'employee-goals']]}
          readOnly={!progressAllowed}
          canEditItem={mayAmendEntry}
          canRemoveItem={mayAmendEntry}
          dialogHint="The percentage and status recorded here become the goal's — marking an entry At risk is what puts the goal on the at-risk reports. 100% completes it."
          emptyDescription="Nothing recorded yet. Entries are what move the goal's progress."
          list={(goalId) => employeeGoalService.getProgressEntries(goalId)}
          create={(goalId, values) => {
            const v = progressSchema.parse(values);
            return employeeGoalService.addProgressEntry(goalId, {
              employeeGoalId: goalId,
              progressPercent: v.progressPercent ?? null,
              actualValue: v.actualValue ?? null,
              status: v.status as GoalProgressStatus,
              challenges: v.challenges || null,
              notes: v.notes || null,
              reviewEventId: null,
            });
          }}
          update={(goalId, entryId, values) => {
            const v = progressSchema.parse(values);
            // The server keeps the entry's own goal and review event on an edit (P8); the two
            // fields below are required by the type, not honoured.
            return employeeGoalService.updateProgressEntry(goalId, entryId, {
              id: entryId,
              employeeGoalId: goalId,
              progressPercent: v.progressPercent ?? null,
              actualValue: v.actualValue ?? null,
              status: v.status as GoalProgressStatus,
              challenges: v.challenges || null,
              notes: v.notes || null,
              reviewEventId: null,
            });
          }}
          remove={(goalId, entryId) => employeeGoalService.removeProgressEntry(goalId, entryId)}
          getId={(r) => r.id}
          columns={[
            { header: 'Recorded', cell: (r) => formatDateTime(r.entryDate) },
            { header: 'By', cell: (r) => r.recordedByName || '—' },
            {
              header: 'Progress',
              cell: (r) => (r.progressPercent == null ? '—' : formatPercent(r.progressPercent)),
              className: 'text-right',
            },
            {
              header: 'Actual',
              cell: (r) => (r.actualValue == null ? '—' : String(r.actualValue)),
              className: 'text-right',
            },
            { header: 'Status', cell: (r) => <StatusBadge status={humanizeEnum(r.status)} /> },
            {
              header: 'Notes',
              cell: (r) => <span className="line-clamp-1">{r.notes || r.challenges || '—'}</span>,
            },
          ]}
          schema={progressSchema as any}
          emptyForm={emptyProgress}
          toForm={(r) => ({
            progressPercent: r.progressPercent ?? undefined,
            actualValue: r.actualValue ?? undefined,
            status: r.status,
            challenges: r.challenges ?? '',
            notes: r.notes ?? '',
          })}
          renderFields={(form) => (
            <>
              <FieldRow>
                <NumberField
                  form={form}
                  name="progressPercent"
                  label="Progress (%)"
                  step="0.1"
                  placeholder="Leave blank to note without moving progress"
                />
                <NumberField
                  form={form}
                  name="actualValue"
                  label="Actual value"
                  step="0.01"
                  placeholder={goal.unit ? `In ${goal.unit}` : undefined}
                />
              </FieldRow>
              <SelectField
                form={form}
                name="status"
                label="Status"
                required
                options={GOAL_PROGRESS_STATUS_OPTIONS}
              />
              <TextareaField
                form={form}
                name="challenges"
                label="Challenges"
                rows={2}
                placeholder="What is getting in the way."
              />
              <TextareaField form={form} name="notes" label="Notes" rows={3} />
            </>
          )}
        />
      </div>

      <ConfirmationDialog
        open={confirmSubmit}
        onOpenChange={setConfirmSubmit}
        title="Submit for approval?"
        description="This goes to the employee's direct manager. Only the goal's owner may submit it."
        confirmText="Submit"
        isLoading={workflowMutation.isPending}
        onConfirm={async () => {
          await workflowMutation.mutateAsync('submit');
        }}
      />

      <ConfirmationDialog
        open={confirmLock}
        onOpenChange={setConfirmLock}
        title="Lock this goal?"
        description="Locking fixes what the goal measures — title, measure, target and weight. Progress is still recorded while it is locked. The manager or HR can unlock it until it is scored."
        confirmText="Lock"
        isLoading={workflowMutation.isPending}
        onConfirm={async () => {
          await workflowMutation.mutateAsync('lock');
        }}
      />

      <ConfirmationDialog
        open={decision !== null}
        onOpenChange={(open) => {
          if (!open) {
            setDecision(null);
            setFeedback('');
          }
        }}
        title={
          decision === 'sendBack'
            ? 'Send this goal back for changes?'
            : decision === 'reject'
              ? 'Reject this goal?'
              : 'Approve this goal?'
        }
        description={
          decision === 'sendBack'
            ? 'What an approved goal measures cannot be edited. It returns to the employee to change, and comes back to you for approval. Its progress entries are kept.'
            : decision === 'reject'
              ? 'The goal returns to the employee, who can revise it and submit again.'
              : 'The goal becomes live and progress can be recorded against it.'
        }
        confirmText={decision === 'sendBack' ? 'Send back' : decision === 'reject' ? 'Reject' : 'Approve'}
        variant={decision === 'approve' ? 'default' : 'destructive'}
        // A rejection with no explanation is refused by the API, so the button waits for one.
        confirmDisabled={decision !== 'approve' && feedback.trim().length === 0}
        isLoading={workflowMutation.isPending}
        onConfirm={async () => {
          await workflowMutation.mutateAsync(decision === 'approve' ? 'approve' : 'reject');
        }}
      >
        <div className="space-y-2">
          <Label htmlFor="feedback">
            Feedback
            {decision !== 'approve' && <span className="ml-0.5 text-red-500">*</span>}
          </Label>
          <Textarea
            id="feedback"
            rows={4}
            value={feedback}
            onChange={(e) => setFeedback(e.target.value)}
            placeholder={
              decision !== 'approve'
                ? 'Required — say what needs to change so the employee can act on it.'
                : 'Optional comment, stored against the goal.'
            }
          />
        </div>
      </ConfirmationDialog>
    </div>
  );
}
