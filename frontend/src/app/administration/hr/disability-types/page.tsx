'use client';

/**
 * The disability catalogue — what the employee form and the dependant form pick from once the
 * disability box is ticked (round 3, lane P2; register row E-5).
 *
 * ⚠ **The description on the record is NOT replaced by this.** Both records keep their free text as
 * NOTES — the person's own words, the accommodation needed, a condition this list does not name.
 * A record may carry a type, notes, or both.
 *
 * ⚠ **Retire and delete are different acts.** Retiring withdraws a value from new records and
 * leaves the ones already naming it alone. Deleting erases it, and is refused with a count while
 * any employee or dependant still points at the row.
 *
 * ⚠ **Renaming shows everywhere.** Unlike the relationship types, nothing mirrors this row's name
 * into the record — a record reads the row's current name — so correcting a term corrects it on
 * every profile at once, which for a medical term is the right behaviour.
 *
 * Seeded from the groupings Ghana's census and Persons with Disability Act (Act 715) practice use.
 */

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { disabilityTypeService } from '@/services/hr/disability-type.service';
import {
  DISABILITY_CATEGORY_LABEL,
  DISABILITY_CATEGORY_OPTIONS,
  type DisabilityType,
} from '@/types/hr/disability-type';

const schema = z.object({
  name: z.string().min(1, 'A name is required').max(100),
  code: z.string().max(50).optional(),
  category: z.enum([
    'Physical',
    'Visual',
    'Hearing',
    'Speech',
    'Intellectual',
    'Psychosocial',
    'Neurological',
    'ChronicHealth',
    'Multiple',
    'Other',
  ]),
  description: z.string().max(500).optional(),
  sortOrder: z.coerce.number().int().min(0).max(9999),
  isActive: z.boolean(),
});

type DisabilityTypeForm = z.input<typeof schema>;

const empty: DisabilityTypeForm = {
  name: '',
  code: '',
  category: 'Physical',
  description: '',
  sortOrder: 0,
  isActive: true,
};

/** Blank optionals must reach the API as null, not as empty strings. */
function toPayload(values: DisabilityTypeForm) {
  const parsed = schema.parse(values);
  return { ...parsed, code: parsed.code || null, description: parsed.description || null };
}

export default function DisabilityTypesPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Disability Types"
        description="What the employee and dependant forms pick from once the disability box is ticked. The record's own notes stay beside it."
        backHref="/administration/hr"
      />

      <ResourceListPanel<DisabilityType, DisabilityTypeForm>
        title="disability types"
        singular="disability type"
        queryKey={['hr', 'disability-types']}
        invalidateKeys={[['hr', 'disability-types', 'active']]}
        dialogHint="The category is how the dropdown is sectioned and how a headcount report counts. Retire a value rather than deleting it once records name it."
        getId={(t) => t.id}
        list={() => disabilityTypeService.getAll()}
        create={(values) => disabilityTypeService.create(toPayload(values) as any)}
        update={(id, values) => disabilityTypeService.update(id, toPayload(values) as any)}
        // Refused by the server with a 422 and a count while any record still names the row.
        remove={(id) => disabilityTypeService.remove(id)}
        columns={[
          { header: 'Disability type', cell: (t) => <span className="font-medium">{t.name}</span> },
          { header: 'Code', cell: (t) => t.code || '—' },
          {
            header: 'Category',
            cell: (t) => <Badge variant="outline">{DISABILITY_CATEGORY_LABEL[t.category] ?? t.category}</Badge>,
          },
          {
            header: 'In use',
            // ⚠ Zero is not decoration: it is the difference between a row that can be deleted and
            // one that can only be retired, and the delete refuses on this exact number.
            cell: (t) =>
              t.usageCount > 0 ? t.usageCount : <span className="text-muted-foreground">none</span>,
            className: 'text-right',
          },
          { header: 'Status', cell: (t) => <StatusBadge active={t.isActive} /> },
        ]}
        schema={schema as any}
        emptyForm={empty}
        toForm={(t) => ({
          name: t.name,
          code: t.code ?? '',
          category: t.category,
          description: t.description ?? '',
          sortOrder: t.sortOrder,
          isActive: t.isActive,
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="name" label="Name" required />
              <TextField form={form} name="code" label="Code" placeholder="VISUAL, EPILEPSY" />
            </FieldRow>
            <SelectField
              form={form}
              name="category"
              label="Category"
              required
              options={DISABILITY_CATEGORY_OPTIONS}
            />
            <TextareaField
              form={form}
              name="description"
              label="Description"
              rows={2}
              placeholder="When this value is the right one to pick."
            />
            <FieldRow>
              <NumberField form={form} name="sortOrder" label="Sort order" />
              <div />
            </FieldRow>
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="A retired value stops being offered on new records; the ones already using it keep it."
            />
          </>
        )}
      />
    </div>
  );
}
