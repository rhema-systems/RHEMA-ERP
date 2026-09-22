'use client';

import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { OnboardingTemplateAudiencePanel } from '@/components/hr/orientation/OnboardingTemplateAudiencePanel';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { onboardingPlanTemplateService } from '@/services/hr/onboarding.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { ONBOARDING_TASK_CATEGORY_OPTIONS } from '@/types/hr/onboarding';
import type { OnboardingTaskTemplate } from '@/types/hr/onboarding';

const taskTemplateSchema = z.object({
  taskName: z.string().min(1, 'A name is required').max(200),
  description: z.string().max(2000).optional().or(z.literal('')),
  category: z.enum([
    'Documentation',
    'SystemAccess',
    'Orientation',
    'Training',
    'EquipmentSetup',
    'PayrollSetup',
    'PolicyAcknowledgement',
    'MeetAndGreet',
    'HealthAndSafety',
    'Compliance',
    'Other',
  ]),
  dueDaysFromStartDate: z.coerce.number().min(-90).max(365),
  isMandatory: z.boolean(),
  displayOrder: z.coerce.number().min(0).max(9999),
  instructionsUrl: z.string().max(1000).optional().or(z.literal('')),
  ownerPositionId: z.string().optional().or(z.literal('')),
});

type TaskTemplateForm = z.infer<typeof taskTemplateSchema>;

const emptyTaskTemplate: TaskTemplateForm = {
  taskName: '',
  description: '',
  category: 'Documentation',
  dueDaysFromStartDate: 0,
  isMandatory: true,
  displayOrder: 0,
  instructionsUrl: '',
  ownerPositionId: '',
};

const categoryLabel = (v: string) =>
  ONBOARDING_TASK_CATEGORY_OPTIONS.find((o) => o.value === v)?.label ?? v;
const blank = (v?: string) => (v && v.length > 0 ? v : null);

/** "3 days before the start", "on the start date", "5 days in" — the offset read as English. */
const dueLabel = (days: number) => {
  if (days === 0) return 'On the start date';
  if (days < 0) return `${Math.abs(days)} day${Math.abs(days) === 1 ? '' : 's'} before starting`;
  return `${days} day${days === 1 ? '' : 's'} after starting`;
};

/**
 * One template and the tasks it instantiates.
 *
 * Each task carries an offset rather than a date: when a plan is created, its due date becomes the
 * plan's start date plus this many days. A negative offset is legitimate and useful — contracts and
 * system accounts are meant to be done before someone walks in.
 */
export default function OnboardingTemplateDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';

  const { data: template, isLoading, isError } = useQuery({
    queryKey: ['hr', 'onboarding-templates', id],
    queryFn: () => onboardingPlanTemplateService.getWithTasks(id),
    enabled: !!id,
  });

  const { data: positions = [] } = useQuery({
    queryKey: ['hr', 'employee-positions', 'active'],
    queryFn: () => employeePositionService.getActive(),
  });
  const positionOptions = positions.map((p) => ({ value: p.id, label: p.title }));

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (isError || !template) {
    return (
      <div className="p-6">
        <EmptyState title="Template not found" description="It may have been removed." />
      </div>
    );
  }

  const mandatory = template.taskTemplates.filter((t) => t.isMandatory).length;
  const earliest = template.taskTemplates.reduce(
    (min, t) => Math.min(min, t.dueDaysFromStartDate),
    0,
  );
  const latest = template.taskTemplates.reduce(
    (max, t) => Math.max(max, t.dueDaysFromStartDate),
    0,
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={template.name}
        description={template.description ?? 'Onboarding checklist template.'}
        backHref="/administration/hr/orientation/onboarding-templates"
        actions={
          <div className="flex items-center gap-2">
            {template.isDefault && <Badge variant="default">Default</Badge>}
            <StatusBadge active={template.isActive} />
          </div>
        }
      />

      <MetricTiles
        tiles={[
          { label: 'Tasks', value: template.taskTemplates.length },
          { label: 'Mandatory', value: mandatory },
          {
            label: 'Earliest task',
            value: template.taskTemplates.length ? dueLabel(earliest) : '—',
          },
          {
            label: 'Latest task',
            value: template.taskTemplates.length ? dueLabel(latest) : '—',
          },
        ]}
      />

      <OnboardingTemplateAudiencePanel templateId={id} isDefault={template.isDefault} />

      <ResourceCollectionTab<OnboardingTaskTemplate, TaskTemplateForm>
        parentId={id}
        title="tasks"
        singular="task"
        queryKey={['hr', 'onboarding-templates', id, 'task-templates']}
        invalidateKeys={[
          ['hr', 'onboarding-templates', id],
          ['hr', 'onboarding-templates'],
        ]}
        dialogHint="Due dates are offsets from the plan's start date, not fixed dates."
        emptyDescription="No tasks yet. Add the steps a new hire works through."
        list={() => onboardingPlanTemplateService.getTaskTemplates(id)}
        create={(planTemplateId, values) =>
          onboardingPlanTemplateService.addTaskTemplate(planTemplateId, {
            planTemplateId,
            ...values,
            description: blank(values.description),
            instructionsUrl: blank(values.instructionsUrl),
            ownerPositionId: blank(values.ownerPositionId),
          })
        }
        update={(_p, taskTemplateId, values) =>
          onboardingPlanTemplateService.updateTaskTemplate(taskTemplateId, {
            id: taskTemplateId,
            ...values,
            description: blank(values.description),
            instructionsUrl: blank(values.instructionsUrl),
            ownerPositionId: blank(values.ownerPositionId),
          })
        }
        remove={(_p, taskTemplateId) =>
          onboardingPlanTemplateService.removeTaskTemplate(taskTemplateId)
        }
        getId={(t) => t.id}
        columns={[
          { header: '#', cell: (t) => t.displayOrder, className: 'w-[60px]' },
          {
            header: 'Task',
            cell: (t) => (
              <div>
                <span className="font-medium">{t.taskName}</span>
                {t.description && (
                  <div className="text-muted-foreground mt-0.5 line-clamp-1 text-xs">
                    {t.description}
                  </div>
                )}
              </div>
            ),
          },
          { header: 'Category', cell: (t) => categoryLabel(t.category) },
          { header: 'Due', cell: (t) => dueLabel(t.dueDaysFromStartDate) },
          { header: 'Owner', cell: (t) => t.ownerPositionTitle ?? 'Unassigned' },
          {
            header: 'Mandatory',
            cell: (t) =>
              t.isMandatory ? (
                <Badge variant="secondary">Mandatory</Badge>
              ) : (
                <Badge variant="outline">Optional</Badge>
              ),
          },
        ]}
        schema={taskTemplateSchema as any}
        emptyForm={emptyTaskTemplate}
        toForm={(t) => ({
          taskName: t.taskName,
          description: t.description ?? '',
          category: t.category,
          dueDaysFromStartDate: t.dueDaysFromStartDate,
          isMandatory: t.isMandatory,
          displayOrder: t.displayOrder,
          instructionsUrl: t.instructionsUrl ?? '',
          ownerPositionId: t.ownerPositionId ?? '',
        })}
        renderFields={(form) => (
          <>
            <TextField form={form} name="taskName" label="Task" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={form}
                name="category"
                label="Category"
                required
                options={ONBOARDING_TASK_CATEGORY_OPTIONS}
              />
              <NumberField
                form={form}
                name="dueDaysFromStartDate"
                label="Due (days from start)"
                required
                placeholder="Negative for before the start date"
              />
            </FieldRow>
            <SelectField
              form={form}
              name="ownerPositionId"
              label="Owned by position"
              options={positionOptions}
              allowEmpty
              emptyLabel="Unassigned"
            />
            <TextField
              form={form}
              name="instructionsUrl"
              label="Instructions URL"
              placeholder="https://…"
            />
            <FieldRow>
              <NumberField form={form} name="displayOrder" label="Display order" required />
              <SwitchField
                form={form}
                name="isMandatory"
                label="Mandatory"
                description="An optional task does not hold up the plan."
              />
            </FieldRow>
          </>
        )}
      />
    </div>
  );
}
