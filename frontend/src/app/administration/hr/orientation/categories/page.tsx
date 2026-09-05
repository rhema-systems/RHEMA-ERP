'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
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
import { orientationCategoryService } from '@/services/hr/orientation-lookup.service';
import type { OrientationCategory } from '@/types/hr/orientation';

/**
 * Orientation catalogue taxonomy. Categories nest one level in practice (Compliance → Fire Safety),
 * though the model allows deeper.
 *
 * A category cannot be deleted while it still holds programmes or sub-categories — the server
 * refuses with 422 and the message is surfaced as-is.
 */
const categorySchema = z.object({
  name: z.string().min(1, 'A name is required').max(200),
  description: z.string().max(500).optional(),
  parentCategoryId: z.string().optional(),
  displayOrder: z.coerce.number().min(0).max(9999),
  isActive: z.boolean(),
});

type CategoryForm = z.input<typeof categorySchema>;

const emptyCategory: CategoryForm = {
  name: '',
  description: '',
  parentCategoryId: '',
  displayOrder: 0,
  isActive: true,
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function OrientationCategoriesPage() {
  // Parent picker options. Uses the flat lookup rather than the full read — this only needs
  // id and name, and the full read carries every category's programme collection.
  const { data: lookup = [] } = useQuery({
    queryKey: ['hr', 'orientation-categories', 'lookup'],
    queryFn: () => orientationCategoryService.getLookup(),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Orientation Categories"
        description="Groups the induction catalogue — onboarding, compliance, health & safety, and so on."
        backHref="/administration/hr/orientation"
      />

      <ResourceListPanel<OrientationCategory, CategoryForm>
        title="categories"
        singular="category"
        queryKey={['hr', 'orientation-categories']}
        invalidateKeys={[['hr', 'orientation-categories', 'lookup']]}
        dialogHint="Leave the parent empty for a top-level category."
        list={() => orientationCategoryService.getAll()}
        create={(values) => {
          const v = categorySchema.parse(values);
          return orientationCategoryService.create({
            ...v,
            description: blank(v.description),
            parentCategoryId: blank(v.parentCategoryId),
          });
        }}
        update={(id, values) => {
          const v = categorySchema.parse(values);
          return orientationCategoryService.update(id, {
            id,
            ...v,
            description: blank(v.description),
            parentCategoryId: blank(v.parentCategoryId),
          });
        }}
        remove={(id) => orientationCategoryService.remove(id)}
        getId={(c) => c.id}
        emptyDescription="No categories yet. Add one to start grouping induction programmes."
        columns={[
          { header: 'Name', cell: (c) => <span className="font-medium">{c.name}</span> },
          { header: 'Parent', cell: (c) => c.parentCategoryName ?? '—' },
          {
            header: 'Programmes',
            cell: (c) => (
              <Badge variant={c.programCount > 0 ? 'secondary' : 'outline'}>{c.programCount}</Badge>
            ),
          },
          {
            header: 'Sub-categories',
            cell: (c) => (
              <Badge variant={c.subCategories.length > 0 ? 'secondary' : 'outline'}>
                {c.subCategories.length}
              </Badge>
            ),
          },
          { header: 'Order', cell: (c) => c.displayOrder },
          {
            header: 'Status',
            cell: (c) => <StatusBadge status={c.isActive ? 'Active' : 'Inactive'} />,
          },
        ]}
        schema={categorySchema}
        emptyForm={emptyCategory}
        toForm={(c) => ({
          name: c.name,
          description: c.description ?? '',
          parentCategoryId: c.parentCategoryId ?? '',
          displayOrder: c.displayOrder,
          isActive: c.isActive,
        })}
        renderFields={(form, editing) => {
          const currentId = form.getValues('parentCategoryId');
          return (
            <div className="space-y-4">
              <TextField form={form} name="name" label="Name" />
              <TextareaField form={form} name="description" label="Description" rows={2} />
              <FieldRow>
                <SelectField
                  form={form}
                  name="parentCategoryId"
                  label="Parent category"
                  allowEmpty
                  emptyLabel="None (top level)"
                  options={lookup
                    // A category cannot be its own parent; the server refuses it anyway, but
                    // offering the option only to reject it is a worse experience.
                    .filter((o) => !editing || o.id !== currentId)
                    .map((o) => ({ value: o.id, label: o.name }))}
                />
                <NumberField form={form} name="displayOrder" label="Display order" />
              </FieldRow>
              <SwitchField
                form={form}
                name="isActive"
                label="Active"
                description="Inactive categories stay on existing programmes but are not offered for new ones."
              />
            </div>
          );
        }}
      />
    </div>
  );
}
