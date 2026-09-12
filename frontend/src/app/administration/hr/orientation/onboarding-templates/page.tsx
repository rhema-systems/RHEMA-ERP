'use client';

import Link from 'next/link';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  TextareaField,
  SwitchField,
} from '@/components/hr/employee/tabs/fields';
import { onboardingPlanTemplateService } from '@/services/hr/onboarding.service';
import type { OnboardingPlanTemplateSummary } from '@/types/hr/onboarding';

/**
 * Reusable onboarding checklists.
 *
 * ⚠ `getAll` returns active templates only — a deactivated one is deliberately not offered for new
 * plans and so does not appear here either. Deactivating is therefore how a template is taken out of
 * circulation without disturbing plans already created from it, since the tasks were copied.
 */
const templateSchema = z.object({
  name: z.string().min(1, 'A name is required').max(200),
  description: z.string().max(1000).optional().or(z.literal('')),
  isDefault: z.boolean(),
  isActive: z.boolean(),
});

type TemplateForm = z.infer<typeof templateSchema>;

const emptyTemplate: TemplateForm = {
  name: '',
  description: '',
  isDefault: false,
  isActive: true,
};

export default function OnboardingTemplatesPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Onboarding Templates"
        description="Standard task checklists. Creating a plan from a template copies its tasks, so editing a template later does not rewrite plans already in flight."
        backHref="/administration/hr/orientation"
      />

      <ResourceListPanel<OnboardingPlanTemplateSummary, TemplateForm>
        title="templates"
        singular="template"
        queryKey={['hr', 'onboarding-templates']}
        dialogHint="Add the tasks themselves from the template's own page."
        emptyDescription="No templates yet. Add one, then build up its task list."
        list={() => onboardingPlanTemplateService.getAll()}
        create={(values) =>
          onboardingPlanTemplateService.create({
            ...values,
            description: values.description || null,
          })
        }
        update={(id, values) =>
          onboardingPlanTemplateService.update(id, {
            id,
            ...values,
            description: values.description || null,
          })
        }
        remove={(id) => onboardingPlanTemplateService.remove(id)}
        getId={(t) => t.id}
        columns={[
          {
            header: 'Template',
            cell: (t) => (
              <div>
                <Link
                  href={`/administration/hr/orientation/onboarding-templates/${t.id}`}
                  className="font-medium hover:underline"
                >
                  {t.name}
                </Link>
                {t.description && (
                  <div className="text-muted-foreground mt-0.5 line-clamp-1 text-xs">
                    {t.description}
                  </div>
                )}
              </div>
            ),
          },
          {
            header: 'Tasks',
            cell: (t) => (
              <Badge variant={t.taskTemplateCount > 0 ? 'secondary' : 'outline'}>
                {t.taskTemplateCount}
              </Badge>
            ),
          },
          {
            header: 'Default',
            cell: (t) =>
              t.isDefault ? <Badge variant="default">Default</Badge> : <span>—</span>,
          },
          { header: 'Status', cell: (t) => <StatusBadge active={t.isActive} /> },
        ]}
        schema={templateSchema}
        emptyForm={emptyTemplate}
        toForm={(t) => ({
          name: t.name,
          description: t.description ?? '',
          isDefault: t.isDefault,
          isActive: t.isActive,
        })}
        renderFields={(form) => (
          <div className="space-y-4">
            <TextField form={form} name="name" label="Name" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <SwitchField
              form={form}
              name="isDefault"
              label="Default template"
              description="Used when a plan is created without one being chosen. Marking this clears the flag on whichever template holds it."
            />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="An inactive template is not offered for new plans, but plans already created from it are untouched."
            />
          </div>
        )}
      />
    </div>
  );
}
