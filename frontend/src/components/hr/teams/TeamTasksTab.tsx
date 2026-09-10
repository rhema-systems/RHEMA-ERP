'use client';

/**
 * Who is doing what on a team, and where it has stopped.
 *
 * ⚠ **An ordinary member may MOVE a task assigned to them and nothing else.** The status control is
 * therefore a separate affordance from Edit, mirroring the two separate doors on the server —
 * progressing work you were given is not the same privilege as rewriting the task record. This
 * screen does not try to decide who the caller is; it offers both and lets the server refuse the
 * one it should, with a sentence.
 *
 * ⚠ **"Mine" is resolved from the token server-side.** It is not a member id this screen supplies,
 * because a filter the caller defines is not a filter — it is a way to read anyone's list.
 *
 * Round 2, lane F1 (plan § 6.6).
 */

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  DateField,
  FieldRow,
  SelectField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { teamActivityService } from '@/services/hr/team-activity.service';
import { teamService } from '@/services/hr/team.service';
import {
  TEAM_TASK_PRIORITY_OPTIONS,
  TEAM_TASK_STATUS_LABELS,
  type TeamTask,
} from '@/types/hr/team-activity';
import { TeamTaskDetailDialog } from './TeamTaskDetailDialog';

const schema = z
  .object({
    objectiveId: z.string().optional().or(z.literal('')),
    title: z.string().min(1, 'A title is required').max(300),
    description: z.string().max(4000).optional().or(z.literal('')),
    assigneeMemberId: z.string().optional().or(z.literal('')),
    priority: z.enum(['Low', 'Normal', 'High', 'Urgent']),
    startDate: z.string().optional().or(z.literal('')),
    dueDate: z.string().optional().or(z.literal('')),
  })
  .refine((v) => !v.startDate || !v.dueDate || v.dueDate >= v.startDate, {
    message: 'A task cannot be due before it starts',
    path: ['dueDate'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  objectiveId: '',
  title: '',
  description: '',
  assigneeMemberId: '',
  priority: 'Normal',
  startDate: '',
  dueDate: '',
};

const toPayload = (v: FormValues) => ({
  objectiveId: v.objectiveId || null,
  title: v.title,
  description: v.description || null,
  assigneeMemberId: v.assigneeMemberId || null,
  priority: v.priority,
  startDate: v.startDate || null,
  dueDate: v.dueDate || null,
});

const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'outline' | 'destructive'> = {
  NotStarted: 'outline',
  InProgress: 'secondary',
  Blocked: 'destructive',
  Completed: 'default',
  Cancelled: 'outline',
};

const PRIORITY_CLASS: Record<string, string> = {
  Urgent: 'text-red-600 font-medium',
  High: 'text-amber-600',
  Normal: '',
  Low: 'text-muted-foreground',
};

export function TeamTasksTab({ teamId }: { teamId: string }) {
  const queryClient = useQueryClient();
  const [mineOnly, setMineOnly] = useState(false);
  const [openTask, setOpenTask] = useState<TeamTask | null>(null);

  const { data: members = [] } = useQuery({
    queryKey: ['hr', 'teams', teamId, 'members'],
    queryFn: () => teamService.getMembers(teamId),
  });
  const memberOptions = members
    .filter((m) => m.isCurrent)
    .map((m) => ({ value: m.id, label: m.employeeName || m.employeeNumber || m.id }));

  const { data: objectives = [] } = useQuery({
    queryKey: ['hr', 'teams', teamId, 'objectives'],
    queryFn: () => teamActivityService.getObjectives(teamId),
  });
  const objectiveOptions = objectives.map((o) => ({
    value: o.id,
    label: o.code ? `${o.code} — ${o.title}` : o.title,
  }));

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['hr', 'teams', teamId, 'tasks'] });
    // Completing a task moves its objective's progress, so that list is stale too.
    void queryClient.invalidateQueries({ queryKey: ['hr', 'teams', teamId, 'objectives'] });
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2">
        <Switch id="mine-only" checked={mineOnly} onCheckedChange={setMineOnly} />
        <Label htmlFor="mine-only" className="text-sm font-normal">
          Only tasks assigned to me
        </Label>
      </div>

      <ResourceCollectionTab<TeamTask, FormValues>
        parentId={teamId}
        title="tasks"
        singular="task"
        // ⚠ `mineOnly` is in the key: without it, toggling the switch would serve the cached list
        // from the other setting and the filter would look broken.
        queryKey={['hr', 'teams', teamId, 'tasks', mineOnly ? 'mine' : 'all']}
        invalidateKeys={[['hr', 'teams', teamId, 'objectives']]}
        dialogHint="A piece of work on the team. It need not belong to an objective — plenty of real committee work does not."
        dialogClassName="sm:max-w-[680px]"
        getId={(t) => t.id}
        list={() => teamActivityService.getTasks(teamId, { mine: mineOnly })}
        create={(_id, values) => teamActivityService.createTask(teamId, toPayload(values))}
        update={(_id, id, values) => teamActivityService.updateTask(id, toPayload(values))}
        remove={(_id, id) => teamActivityService.deleteTask(id)}
        loadForEdit={async (t) => {
          const full = await teamActivityService.getTaskById(t.id);
          return {
            objectiveId: full.objectiveId ?? '',
            title: full.title,
            description: full.description ?? '',
            assigneeMemberId: full.assigneeMemberId ?? '',
            priority: full.priority,
            startDate: full.startDate?.slice(0, 10) ?? '',
            dueDate: full.dueDate?.slice(0, 10) ?? '',
          };
        }}
        actions={[
          {
            label: 'Open…',
            run: async (t) => {
              setOpenTask(t);
            },
          },
          {
            label: 'Start',
            visible: (t) => t.status === 'NotStarted' || t.status === 'Blocked',
            run: (t) => teamActivityService.changeTaskStatus(t.id, { status: 'InProgress' }),
          },
          {
            label: 'Complete',
            visible: (t) => t.status !== 'Completed' && t.status !== 'Cancelled',
            run: (t) => teamActivityService.changeTaskStatus(t.id, { status: 'Completed' }),
          },
          {
            label: 'Re-open',
            visible: (t) => t.status === 'Completed' || t.status === 'Cancelled',
            run: (t) => teamActivityService.changeTaskStatus(t.id, { status: 'InProgress' }),
          },
          {
            label: 'Cancel task',
            visible: (t) => t.status !== 'Cancelled' && t.status !== 'Completed',
            run: (t) => teamActivityService.changeTaskStatus(t.id, { status: 'Cancelled' }),
            destructive: true,
            confirm: {
              title: 'Cancel this task?',
              description:
                'It stops counting towards its objective’s progress — on both sides of the fraction, so the percentage does not fall.',
            },
          },
        ]}
        columns={[
          { header: 'Task', cell: (t) => <span className="font-medium">{t.title}</span> },
          { header: 'Objective', cell: (t) => t.objectiveTitle || '—' },
          { header: 'Assigned to', cell: (t) => t.assigneeName || <span className="text-muted-foreground">Nobody</span> },
          {
            header: 'Priority',
            cell: (t) => <span className={PRIORITY_CLASS[t.priority] ?? ''}>{t.priority}</span>,
          },
          {
            header: 'Due',
            cell: (t) =>
              t.dueDate ? (
                <span className={t.isOverdue ? 'text-red-600' : undefined}>
                  {t.dueDate.slice(0, 10)}
                  {t.isOverdue && ' · overdue'}
                </span>
              ) : (
                '—'
              ),
          },
          {
            header: 'Checklist',
            cell: (t) =>
              t.checklistTotal > 0 ? (
                <span className="text-xs">
                  {t.checklistDone}/{t.checklistTotal}
                </span>
              ) : (
                <span className="text-muted-foreground text-xs">—</span>
              ),
          },
          {
            header: 'Status',
            cell: (t) => (
              <div className="space-y-1">
                <Badge variant={STATUS_VARIANT[t.status] ?? 'outline'}>
                  {TEAM_TASK_STATUS_LABELS[t.status]}
                </Badge>
                {/* A blocked card that cannot say why tells the lead nothing — so it says why. */}
                {t.status === 'Blocked' && t.blockedReason && (
                  <p className="text-muted-foreground max-w-[14rem] truncate text-xs" title={t.blockedReason}>
                    {t.blockedReason}
                  </p>
                )}
              </div>
            ),
          },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={() => empty}
        renderFields={(form) => (
          <>
            <TextField form={form} name="title" label="Task" required />
            <TextareaField form={form} name="description" label="Description" rows={3} />
            <FieldRow>
              <SelectField
                form={form}
                name="objectiveId"
                label="Objective"
                options={objectiveOptions}
                allowEmpty
                emptyLabel="Not under an objective"
              />
              <SelectField
                form={form}
                name="assigneeMemberId"
                label="Assigned to"
                options={memberOptions}
                allowEmpty
                emptyLabel="Nobody yet"
              />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="priority"
                label="Priority"
                required
                options={TEAM_TASK_PRIORITY_OPTIONS}
              />
              <div />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="startDate" label="Starts" />
              <DateField form={form} name="dueDate" label="Due" />
            </FieldRow>
            <p className="text-muted-foreground text-xs">
              Blocking a task asks for a reason, and files are attached from the task itself after
              saving.
            </p>
          </>
        )}
      />

      <TeamTaskDetailDialog
        taskId={openTask?.id ?? null}
        onOpenChange={(open) => !open && setOpenTask(null)}
        onChanged={refresh}
      />
    </div>
  );
}
