'use client';

/**
 * How one person is tied to another — the catalogue the referee, guarantor, next-of-kin and
 * candidate-referee screens pick from.
 *
 * ⚠ **The category is not a tag; it is what each screen filters on.** A next-of-kin dropdown
 * offering "Former manager" and a referee dropdown offering "Nephew" are the same defect — a list
 * that is technically complete and practically useless — so each screen accepts a set:
 *
 *   · Next of kin — familial and other
 *   · Guarantor — all three; an employer, a brother and a landlord can each stand surety
 *   · Professional or academic referee — professional and other
 *   · Personal referee — familial and other
 *   · Candidate referee — professional and other
 *
 * The server enforces those sets on the write. Changing a value's category therefore changes which
 * screens offer it, which is why the dialog says so.
 *
 * ⚠ **Retire and delete are different acts.** Retiring withdraws a value from new records and
 * leaves the ones already naming it alone — the usual answer. Deleting erases it, and is refused
 * with a count while anything still points at the row: the foreign keys are Restrict, and a soft
 * delete would be worse still, releasing nothing while the row vanished from every read.
 *
 * ⚠ **Renaming does not rewrite history.** Records mirror the row's name into their own free-text
 * column when they are saved. Renaming "Wife" to "Spouse" leaves last year's next of kin saying
 * "Wife", which is what the record actually said at the time.
 *
 * Round 2, lane D2 (register rows E-11a, E-11b).
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
import { relationshipTypeService } from '@/services/hr/relationship-type.service';
import {
  DEPENDENT_RELATIONSHIP_OPTIONS,
  RELATIONSHIP_CATEGORY_OPTIONS,
  type RelationshipType,
} from '@/types/hr/relationship-type';

const schema = z.object({
  name: z.string().min(1, 'A name is required').max(100),
  code: z.string().max(50).optional(),
  category: z.enum(['Familial', 'Professional', 'Other']),
  description: z.string().max(500).optional(),
  mapsToDependentRelationship: z.string().optional(),
  sortOrder: z.coerce.number().int().min(0).max(9999),
  isActive: z.boolean(),
});

type RelationshipTypeForm = z.input<typeof schema>;

const empty: RelationshipTypeForm = {
  name: '',
  code: '',
  category: 'Familial',
  description: '',
  mapsToDependentRelationship: '',
  sortOrder: 0,
  isActive: true,
};

/**
 * Blank optionals must reach the API as null, not as empty strings.
 *
 * ⚠ The dependant mapping is cleared when the category is not familial. The server refuses the
 * pairing outright — the dependant enum has no professional members, so the mapping would be one
 * nothing could ever use — and sending it anyway would turn a category change into a 400 the user
 * did not ask for.
 */
function toPayload(values: RelationshipTypeForm) {
  const parsed = schema.parse(values);
  return {
    ...parsed,
    code: parsed.code || null,
    description: parsed.description || null,
    mapsToDependentRelationship:
      parsed.category === 'Familial' ? parsed.mapsToDependentRelationship || null : null,
  };
}

const CATEGORY_HINT: Record<string, string> = {
  Familial: 'Offered on the next-of-kin, guarantor and personal-referee screens.',
  Professional: 'Offered on the guarantor and professional-referee screens.',
  Other: 'Offered on every screen — a tie that is neither blood nor work.',
};

export default function RelationshipTypesPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Relationship Types"
        description="How one person is tied to another — what the referee, guarantor and next-of-kin screens pick from."
        backHref="/administration/hr"
      />

      <ResourceListPanel<RelationshipType, RelationshipTypeForm>
        title="relationship types"
        singular="relationship type"
        queryKey={['hr', 'relationship-types']}
        // Every screen's picker reads a category-filtered list; invalidating the root of that
        // namespace refreshes all four at once.
        invalidateKeys={[['hr', 'relationship-types', 'screen']]}
        dialogHint="The category decides which screens offer the value. Familial ties may also name the dependant relationship they mean."
        getId={(t) => t.id}
        list={() => relationshipTypeService.getAll()}
        create={(values) => relationshipTypeService.create(toPayload(values) as any)}
        update={(id, values) => relationshipTypeService.update(id, toPayload(values) as any)}
        // Refused by the server with a 422 and a count while any record still names the row.
        remove={(id) => relationshipTypeService.remove(id)}
        columns={[
          { header: 'Relationship', cell: (t) => <span className="font-medium">{t.name}</span> },
          { header: 'Code', cell: (t) => t.code || '—' },
          {
            header: 'Category',
            cell: (t) => <Badge variant="outline">{t.category}</Badge>,
          },
          {
            header: 'Dependant',
            // Only familial rows can carry one, so a dash here is the normal state, not a gap.
            cell: (t) => t.mapsToDependentRelationship || '—',
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
          mapsToDependentRelationship: t.mapsToDependentRelationship ?? '',
          sortOrder: t.sortOrder,
          isActive: t.isActive,
        })}
        renderFields={(form) => {
          const category = form.watch('category');
          return (
            <>
              <FieldRow>
                <TextField form={form} name="name" label="Name" required />
                <TextField form={form} name="code" label="Code" placeholder="SPOUSE, COLLEAGUE" />
              </FieldRow>
              <SelectField
                form={form}
                name="category"
                label="Category"
                required
                options={RELATIONSHIP_CATEGORY_OPTIONS}
              />
              <p className="text-muted-foreground -mt-2 text-xs">
                {CATEGORY_HINT[category as string] ?? ''}
              </p>
              {category === 'Familial' && (
                <SelectField
                  form={form}
                  name="mapsToDependentRelationship"
                  label="Means this dependant relationship"
                  options={DEPENDENT_RELATIONSHIP_OPTIONS}
                  allowEmpty
                  emptyLabel="None"
                />
              )}
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
          );
        }}
      />
    </div>
  );
}
