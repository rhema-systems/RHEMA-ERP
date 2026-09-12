'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { strategicGoalService } from '@/services/hr/goals.service';
import { GOAL_PRIORITY_OPTIONS } from '@/types/hr/goals';
import type { GoalPriority, StrategicGoal } from '@/types/hr/goals';

/**
 * Strategic goals — the top of the cascade and the only level that outlives a cycle.
 *
 * They carry a year span rather than a cycle, and each year's company goals link back to
 * one. That link is what makes a cycle's objectives traceable to a multi-year intent, and
 * it is also what blocks deletion: the API refuses to remove a strategic goal while company
 * goals still point at it, so deactivate instead.
 */
const strategicSchema = z
  .object({
    title: z.string().min(1, 'Required').max(300),
    description: z.string().max(2000).optional(),
    successCriteria: z.string().max(1000).optional(),
    priority: z.string().min(1, 'Required'),
    startYear: z.coerce.number().int().min(2000).max(2100),
    endYear: z.coerce.number().int().min(2000).max(2100),
    isActive: z.boolean(),
  })
  .refine((v) => v.endYear >= v.startYear, {
    message: 'The end year cannot be before the start year',
    path: ['endYear'],
  });

type StrategicForm = z.input<typeof strategicSchema>;

const thisYear = new Date().getFullYear();

const emptyStrategic: StrategicForm = {
  title: '',
  description: '',
  successCriteria: '',
  priority: 'High',
  startYear: thisYear,
  endYear: thisYear + 2,
  isActive: true,
};

const toPayload = (values: StrategicForm) => {
  const v = strategicSchema.parse(values);
  return {
    title: v.title,
    description: v.description || null,
    successCriteria: v.successCriteria || null,
    priority: v.priority as GoalPriority,
    startYear: v.startYear,
    endYear: v.endYear,
    isActive: v.isActive,
  };
};

const PRIORITY_VARIANT: Record<GoalPriority, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Critical: 'destructive',
  High: 'default',
  Medium: 'secondary',
  Low: 'outline',
};

export default function StrategicGoalsPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Strategic Goals"
        description="Multi-year company intent. Each cycle’s company goals link back to one of these."
        backHref="/administration/hr/performance"
      />

      <ResourceListPanel<StrategicGoal, StrategicForm>
        title="strategic goals"
        singular="strategic goal"
        queryKey={['hr', 'strategic-goals']}
        dialogHint="Spans years rather than a cycle — company goals attach a cycle to it."
        emptyDescription="Add the multi-year objectives this organisation is working towards."
        list={() => strategicGoalService.getAll()}
        create={(values) => strategicGoalService.create(toPayload(values))}
        update={(id, values) => strategicGoalService.update(id, { id, ...toPayload(values) })}
        remove={(id) => strategicGoalService.remove(id)}
        getId={(r) => r.id}
        actions={[
          {
            label: (r) => (r.isActive ? 'Deactivate' : 'Activate'),
            run: (r) => strategicGoalService.setActive(r.id, !r.isActive),
          },
        ]}
        columns={[
          { header: 'Title', cell: (r) => <span className="font-medium">{r.title}</span> },
          {
            header: 'Priority',
            cell: (r) => <Badge variant={PRIORITY_VARIANT[r.priority]}>{r.priority}</Badge>,
          },
          {
            header: 'Years',
            cell: (r) => (r.startYear === r.endYear ? r.startYear : `${r.startYear}–${r.endYear}`),
          },
          {
            // The count that decides whether a delete will be refused, so it is worth showing
            // before someone tries.
            header: 'Company goals',
            cell: (r) => r.yearlyObjectiveCount,
            className: 'text-right',
          },
          { header: 'Status', cell: (r) => <StatusBadge active={r.isActive} /> },
        ]}
        schema={strategicSchema as any}
        emptyForm={emptyStrategic}
        toForm={(r) => ({
          title: r.title,
          description: r.description ?? '',
          successCriteria: r.successCriteria ?? '',
          priority: r.priority,
          startYear: r.startYear,
          endYear: r.endYear,
          isActive: r.isActive,
        })}
        renderFields={(form) => (
          <>
            <TextField
              form={form}
              name="title"
              label="Title"
              required
              placeholder="e.g. Double regional service coverage"
            />
            <TextareaField form={form} name="description" label="Description" rows={3} />
            <TextareaField
              form={form}
              name="successCriteria"
              label="Success criteria"
              rows={2}
              placeholder="How the organisation will know this has been achieved."
            />
            <FieldRow>
              <SelectField
                form={form}
                name="priority"
                label="Priority"
                required
                options={GOAL_PRIORITY_OPTIONS}
              />
              <div />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="startYear" label="Start year" required />
              <NumberField form={form} name="endYear" label="End year" required />
            </FieldRow>
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive goals stay linked to existing company goals but cannot be chosen for new ones."
            />
          </>
        )}
      />
    </div>
  );
}
