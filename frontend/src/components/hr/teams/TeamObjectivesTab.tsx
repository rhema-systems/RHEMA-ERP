'use client';

/**
 * What a team has undertaken to achieve.
 *
 * ⚠ **The progress bar is derived when the mode is "counted from tasks".** The field is hidden in
 * that mode rather than shown disabled, because the server REFUSES a supplied figure — showing a
 * greyed box the save would reject is worse than showing nothing.
 *
 * ⚠ **The weight total is advisory.** It is shown because a team planning its year wants to know,
 * and nothing blocks on it: a half-built set is the ordinary state of a team mid-planning.
 *
 * ⚠ **An objective becomes active by being APPROVED, not by being activated (lane F3).** The
 * direct Draft → Active move is refused by the server, so this screen offers "Send for approval"
 * instead: the head of the unit the team serves — or HR — signs it off through the workflow
 * engine. Leaving an Activate button on a draft would have been a button whose only outcome is a
 * refusal, and worse, a door round the approval if the server had allowed it.
 *
 * Round 2, lanes F1 and F3 (plan § 6.6).
 */

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { WorkflowReasonDialog } from '@/components/workflow/WorkflowReasonDialog';
import { teamActivityService } from '@/services/hr/team-activity.service';
import { teamService } from '@/services/hr/team.service';
import {
  TEAM_OBJECTIVE_PROGRESS_MODE_OPTIONS,
  TEAM_OBJECTIVE_STATUS_LABELS,
  type TeamObjective,
} from '@/types/hr/team-activity';

const schema = z
  .object({
    code: z.string().max(50).optional().or(z.literal('')),
    title: z.string().min(1, 'A title is required').max(300),
    description: z.string().max(4000).optional().or(z.literal('')),
    measure: z.string().max(1000).optional().or(z.literal('')),
    targetValue: z.string().optional().or(z.literal('')),
    unit: z.string().max(50).optional().or(z.literal('')),
    weight: z.string().optional().or(z.literal('')),
    startDate: z.string().min(1, 'A start date is required'),
    dueDate: z.string().optional().or(z.literal('')),
    ownerMemberId: z.string().optional().or(z.literal('')),
    progressMode: z.enum(['FromTasks', 'Manual']),
    progressPercent: z.string().optional().or(z.literal('')),
  })
  .refine((v) => !v.dueDate || v.dueDate >= v.startDate, {
    message: 'An objective cannot be due before it starts',
    path: ['dueDate'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  code: '',
  title: '',
  description: '',
  measure: '',
  targetValue: '',
  unit: '',
  weight: '',
  startDate: new Date().toISOString().slice(0, 10),
  dueDate: '',
  ownerMemberId: '',
  progressMode: 'FromTasks',
  progressPercent: '',
};

/**
 * ⚠ `progressPercent` is sent ONLY under Manual. Under FromTasks the server refuses a supplied
 * figure with 422 rather than ignoring it, so sending one would turn every save of a
 * counted-from-tasks objective into an error.
 */
const toPayload = (v: FormValues) => ({
  code: v.code || null,
  title: v.title,
  description: v.description || null,
  measure: v.measure || null,
  targetValue: v.targetValue ? Number(v.targetValue) : null,
  unit: v.unit || null,
  weight: v.weight ? Number(v.weight) : null,
  startDate: v.startDate,
  dueDate: v.dueDate || null,
  ownerMemberId: v.ownerMemberId || null,
  progressMode: v.progressMode,
  progressPercent:
    v.progressMode === 'Manual' && v.progressPercent ? Number(v.progressPercent) : null,
});

const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'outline' | 'destructive'> = {
  Draft: 'outline',
  PendingApproval: 'secondary',
  Active: 'default',
  OnHold: 'secondary',
  Completed: 'default',
  Cancelled: 'destructive',
};

export function TeamObjectivesTab({ teamId }: { teamId: string }) {
  const queryClient = useQueryClient();
  const [reasonFor, setReasonFor] = useState<{ id: string; mode: 'reject' | 'recall' } | null>(null);
  const [busy, setBusy] = useState(false);

  const refreshObjectives = () => {
    void queryClient.invalidateQueries({ queryKey: ['hr', 'teams', teamId, 'objectives'] });
    void queryClient.invalidateQueries({ queryKey: ['hr', 'teams', teamId, 'dashboard'] });
  };

  const { data: members = [] } = useQuery({
    queryKey: ['hr', 'teams', teamId, 'members'],
    queryFn: () => teamService.getMembers(teamId),
  });

  // Only members who are on the team RIGHT NOW can own an objective — the server refuses anyone
  // else, so offering a former member would be offering a way to fail.
  //
  // ⚠ `isCurrent`, not `isActive`. The former is the server's single definition of "on the team
  // right now" (active, not deleted, not past its leaving date) and its own doc comment says to
  // read it rather than re-derive it, so the roster, the member count and this picker cannot
  // disagree about who is on the team.
  const memberOptions = members
    .filter((m) => m.isCurrent)
    .map((m) => ({ value: m.id, label: m.employeeName || m.employeeNumber || m.id }));

  const { data: weights } = useQuery({
    queryKey: ['hr', 'teams', teamId, 'objectives', 'weight-total'],
    queryFn: () => teamActivityService.getObjectiveWeightTotal(teamId),
  });

  return (
    <div className="space-y-4">
      {weights && weights.total > 0 && !weights.isBalanced && (
        <p className="text-muted-foreground text-xs">
          The active objectives carry {weights.total}% of weight between them, not 100%. That is fine
          while the year is still being planned — nothing depends on it.
        </p>
      )}

      <ResourceCollectionTab<TeamObjective, FormValues>
        parentId={teamId}
        title="objectives"
        singular="objective"
        queryKey={['hr', 'teams', teamId, 'objectives']}
        invalidateKeys={[['hr', 'teams', teamId, 'objectives', 'weight-total']]}
        dialogHint="What the team has undertaken to achieve, and how anyone will know it was achieved."
        dialogClassName="sm:max-w-[720px]"
        getId={(o) => o.id}
        list={() => teamActivityService.getObjectives(teamId)}
        create={(_id, values) => teamActivityService.createObjective(teamId, toPayload(values))}
        update={(_id, id, values) => teamActivityService.updateObjective(id, toPayload(values))}
        remove={(_id, id) => teamActivityService.deleteObjective(id)}
        loadForEdit={async (o) => {
          const full = await teamActivityService.getObjectiveById(o.id);
          return {
            code: full.code ?? '',
            title: full.title,
            description: full.description ?? '',
            measure: full.measure ?? '',
            targetValue: full.targetValue != null ? String(full.targetValue) : '',
            unit: full.unit ?? '',
            weight: full.weight != null ? String(full.weight) : '',
            startDate: full.startDate?.slice(0, 10) ?? '',
            dueDate: full.dueDate?.slice(0, 10) ?? '',
            ownerMemberId: full.ownerMemberId ?? '',
            progressMode: full.progressMode,
            progressPercent: String(full.progressPercent),
          };
        }}
        actions={[
          {
            // ⚠ Draft is NOT here. A draft objective reaches Active only through the approval
            // below; resuming one that was put on hold is the team's own call and stays direct.
            label: 'Resume',
            visible: (o) => o.status === 'OnHold',
            run: (o) => teamActivityService.changeObjectiveStatus(o.id, 'Active'),
          },
          {
            label: 'Send for approval',
            visible: (o) => o.status === 'Draft',
            run: (o) => teamActivityService.submitObjective(o.id),
            confirm: {
              title: 'Send this objective for approval?',
              description:
                'It needs an owner and a due date. The head of the unit this team serves, or HR, decides '
                + 'whether it becomes active.',
            },
          },
          {
            label: 'Approve',
            visible: (o) => o.status === 'PendingApproval',
            run: (o) => teamActivityService.approveObjective(o.id),
            confirm: {
              title: 'Approve this objective?',
              description: 'Approving it is what makes it active and puts the team to work on it.',
            },
          },
          {
            label: 'Send back…',
            visible: (o) => o.status === 'PendingApproval',
            run: async (o) => {
              setReasonFor({ id: o.id, mode: 'reject' });
            },
          },
          {
            label: 'Withdraw…',
            visible: (o) => o.status === 'PendingApproval',
            run: async (o) => {
              setReasonFor({ id: o.id, mode: 'recall' });
            },
          },
          {
            label: 'Put on hold',
            visible: (o) => o.status === 'Active',
            run: (o) => teamActivityService.changeObjectiveStatus(o.id, 'OnHold'),
          },
          {
            // ⚠ Below 100% the server requires an outcome summary. Rather than fail the click, the
            // screen sends the objective's own summary if it has one and lets the refusal explain
            // itself when it does not — the message names the percentage.
            label: 'Complete',
            // Only an objective that was approved: the server refuses Draft and PendingApproval,
            // which this used to offer — completing an undertaking nobody signed off.
            visible: (o) => o.status === 'Active' || o.status === 'OnHold',
            run: async (o) => {
              const full = await teamActivityService.getObjectiveById(o.id);
              return teamActivityService.changeObjectiveStatus(o.id, 'Completed', {
                outcomeSummary: full.outcomeSummary ?? undefined,
              });
            },
            confirm: {
              title: 'Complete this objective?',
              description:
                'Below 100% you will be asked how the team achieved what it did — record that in the objective first.',
            },
          },
        ]}
        columns={[
          { header: 'Code', cell: (o) => o.code || '—' },
          { header: 'Objective', cell: (o) => <span className="font-medium">{o.title}</span> },
          { header: 'Owner', cell: (o) => o.ownerName || '—' },
          {
            header: 'Progress',
            cell: (o) => (
              <div className="w-32 space-y-1">
                <Progress value={o.progressPercent} />
                <span className="text-muted-foreground text-xs">
                  {o.progressPercent}%
                  {o.progressMode === 'FromTasks' && ` · ${o.completedTaskCount}/${o.taskCount} tasks`}
                </span>
              </div>
            ),
          },
          {
            header: 'Weight',
            cell: (o) => (o.weight != null ? `${o.weight}%` : '—'),
            className: 'text-right',
          },
          {
            header: 'Due',
            cell: (o) =>
              o.dueDate ? (
                <span className={o.isOverdue ? 'text-red-600' : undefined}>
                  {o.dueDate.slice(0, 10)}
                  {o.isOverdue && ' · overdue'}
                </span>
              ) : (
                '—'
              ),
          },
          {
            header: 'Status',
            cell: (o) => (
              <Badge variant={STATUS_VARIANT[o.status] ?? 'outline'}>
                {TEAM_OBJECTIVE_STATUS_LABELS[o.status]}
              </Badge>
            ),
          },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={() => empty}
        renderFields={(form) => {
          const mode = form.watch('progressMode');
          return (
            <>
              <FieldRow>
                <TextField form={form} name="code" label="Code" placeholder="OBJ-01" />
                <SelectField
                  form={form}
                  name="ownerMemberId"
                  label="Owner"
                  options={memberOptions}
                  allowEmpty
                  emptyLabel="Nobody yet"
                />
              </FieldRow>
              <TextField form={form} name="title" label="Objective" required />
              <TextareaField form={form} name="description" label="Description" rows={2} />
              <TextField
                form={form}
                name="measure"
                label="How will anyone know it was achieved?"
                placeholder="Lost-time injuries per quarter"
              />
              <FieldRow>
                <NumberField form={form} name="targetValue" label="Target" step="0.01" />
                <TextField form={form} name="unit" label="Unit" placeholder="incidents, %, days" />
              </FieldRow>
              <FieldRow>
                <DateField form={form} name="startDate" label="Starts" required />
                <DateField form={form} name="dueDate" label="Due" />
              </FieldRow>
              <FieldRow>
                <NumberField form={form} name="weight" label="Weight (%)" />
                <SelectField
                  form={form}
                  name="progressMode"
                  label="Progress"
                  required
                  options={TEAM_OBJECTIVE_PROGRESS_MODE_OPTIONS.map((o) => ({
                    value: o.value,
                    label: o.label,
                  }))}
                />
              </FieldRow>
              <p className="text-muted-foreground -mt-2 text-xs">
                {TEAM_OBJECTIVE_PROGRESS_MODE_OPTIONS.find((o) => o.value === mode)?.hint}
              </p>
              {/*
                ⚠ Shown only under Manual. Under FromTasks the server refuses a supplied figure, so
                a disabled box would be a field whose value the save rejects rather than ignores.
              */}
              {mode === 'Manual' && (
                <NumberField form={form} name="progressPercent" label="Progress (%)" />
              )}
            </>
          );
        }}
      />

      {/*
        Same two doors as the charter, and the same distinction: a refusal needs a reason because
        the team has to know what to change; a withdrawal does not, because nobody refused anything.
      */}
      <WorkflowReasonDialog
        open={reasonFor !== null}
        onOpenChange={(open) => !open && setReasonFor(null)}
        title={reasonFor?.mode === 'reject' ? 'Send this objective back?' : 'Withdraw this submission?'}
        description={
          reasonFor?.mode === 'reject'
            ? 'It returns to draft for the team to rework. Say what needs to change.'
            : 'It returns to draft and nobody is asked to approve it. Nothing is recorded against it.'
        }
        reasonLabel={reasonFor?.mode === 'reject' ? 'What needs to change' : 'Note (optional)'}
        reasonPlaceholder={
          reasonFor?.mode === 'reject'
            ? 'The target is not measurable as written.'
            : 'Withdrawn to name an owner.'
        }
        confirmText={reasonFor?.mode === 'reject' ? 'Send back' : 'Withdraw'}
        requireReason={reasonFor?.mode === 'reject'}
        variant={reasonFor?.mode === 'reject' ? 'destructive' : 'default'}
        isLoading={busy}
        onConfirm={async (reason) => {
          if (!reasonFor) return;
          setBusy(true);
          try {
            if (reasonFor.mode === 'reject') {
              await teamActivityService.rejectObjective(reasonFor.id, reason);
            } else {
              await teamActivityService.recallObjective(reasonFor.id, reason || undefined);
            }
            setReasonFor(null);
            refreshObjectives();
          } finally {
            setBusy(false);
          }
        }}
      />
    </div>
  );
}
