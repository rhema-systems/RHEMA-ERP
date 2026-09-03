'use client';

import { z } from 'zod';
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
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import { SHE_CORRECTIVE_ACTION_CATEGORY_OPTIONS } from '@/types/hr/safety';
import type { SheCorrectiveActionTemplate } from '@/types/hr/safety';

/**
 * Reusable corrective actions. Incident types attach these as defaults, so an incident of that
 * type starts with its standard remediation already listed.
 */
const templateSchema = z.object({
  code: z.string().min(1, 'A code is required').max(20),
  title: z.string().min(1, 'A title is required').max(200),
  description: z.string().max(1000).optional().or(z.literal('')),
  category: z.enum([
    'Engineering',
    'Administrative',
    'Behavioural',
    'PPE',
    'Maintenance',
    'Training',
    'PolicyProcedure',
    'Environmental',
    'Other',
  ]),
  defaultDeadlineDays: z.coerce.number().min(1).max(3650).optional(),
  isActive: z.boolean(),
});

type TemplateForm = z.input<typeof templateSchema>;

const emptyTemplate: TemplateForm = {
  code: '',
  title: '',
  description: '',
  category: 'Engineering',
  defaultDeadlineDays: undefined,
  isActive: true,
};

export default function SafetyCorrectiveActionTemplatesPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Corrective Action Templates"
        description="Standard remediations, attachable as defaults to incident types."
        backHref="/administration/safety"
      />

      <ResourceListPanel<SheCorrectiveActionTemplate, TemplateForm>
        title="templates"
        singular="template"
        queryKey={['hr', 'safety-reference', 'corrective-action-templates']}
        dialogHint="The code is fixed once created."
        list={() => safetyReferenceService.getCorrectiveActionTemplates()}
        create={(values) => {
          const v = templateSchema.parse(values);
          return safetyReferenceService.createCorrectiveActionTemplate({
            ...v,
            description: v.description ?? '',
            defaultDeadlineDays: v.defaultDeadlineDays ?? null,
          });
        }}
        update={(id, values) => {
          const v = templateSchema.parse(values);
          return safetyReferenceService.updateCorrectiveActionTemplate(id, {
            id,
            title: v.title,
            description: v.description ?? '',
            category: v.category,
            defaultDeadlineDays: v.defaultDeadlineDays ?? null,
            isActive: v.isActive,
          });
        }}
        remove={(id) => safetyReferenceService.removeCorrectiveActionTemplate(id)}
        getId={(t) => t.id}
        emptyDescription="No templates yet. Incident types cannot carry default corrective actions without them."
        columns={[
          { header: 'Code', cell: (t) => <span className="font-mono">{t.code}</span> },
          { header: 'Title', cell: (t) => <span className="font-medium">{t.title}</span> },
          {
            header: 'Category',
            cell: (t) =>
              SHE_CORRECTIVE_ACTION_CATEGORY_OPTIONS.find((o) => o.value === t.category)?.label ??
              t.categoryName,
          },
          {
            header: 'Default deadline',
            cell: (t) => (t.defaultDeadlineDays ? `${t.defaultDeadlineDays} days` : '—'),
          },
          {
            header: 'Status',
            cell: (t) => <StatusBadge status={t.isActive ? 'Active' : 'Inactive'} />,
          },
        ]}
        schema={templateSchema}
        emptyForm={emptyTemplate}
        toForm={(t) => ({
          code: t.code,
          title: t.title,
          description: t.description ?? '',
          category: t.category,
          defaultDeadlineDays: t.defaultDeadlineDays ?? undefined,
          isActive: t.isActive,
        })}
        renderFields={(form, editing) => (
          <div className="space-y-4">
            {editing ? (
              <p className="text-muted-foreground text-sm">
                Code <span className="font-mono">{form.getValues('code')}</span> — fixed at creation.
              </p>
            ) : (
              <TextField form={form} name="code" label="Code" required />
            )}
            <TextField form={form} name="title" label="Title" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={form}
                name="category"
                label="Category"
                required
                options={SHE_CORRECTIVE_ACTION_CATEGORY_OPTIONS}
              />
              <NumberField form={form} name="defaultDeadlineDays" label="Default deadline (days)" />
            </FieldRow>
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive templates stay where already attached but are not offered for new attachments."
            />
          </div>
        )}
      />
    </div>
  );
}
