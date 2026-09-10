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
 * Round 2, lane F1 (plan § 6.6).
 */

import { useQuery } from '@tanstack/react-query';
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
            label: 'Activate',
            visible: (o) => o.status === 'Draft' || o.status === 'OnHold',
            run: (o) => teamActivityService.changeObjectiveStatus(o.id, 'Active'),
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
            visible: (o) => o.status !== 'Completed' && o.status !== 'Cancelled',
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
    </div>
  );
}
