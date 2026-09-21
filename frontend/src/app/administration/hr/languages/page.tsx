'use client';

/**
 * The language catalogue — what a candidate picks from when they list the languages they speak
 * (round 3, lane C1; decision D-16). Employee-side languages are a follow-on and will read the
 * same list.
 *
 * ⚠ Retire and delete are different acts: retiring withdraws a value from new records and leaves
 * the ones already naming it alone; deleting is refused with a count while anything still points
 * at the row. Records mirror the language's NAME into their own text column when saved, so a
 * rename here does not rewrite what a candidate said last year.
 */

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  FieldRow,
  NumberField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { languageService } from '@/services/hr/language.service';
import type { Language } from '@/types/hr/language';

const schema = z.object({
  name: z.string().min(1, 'A name is required').max(100),
  code: z.string().max(10).optional(),
  description: z.string().max(500).optional(),
  sortOrder: z.coerce.number().int().min(0).max(9999),
  isActive: z.boolean(),
});

type LanguageForm = z.input<typeof schema>;

const empty: LanguageForm = { name: '', code: '', description: '', sortOrder: 0, isActive: true };

function toPayload(values: LanguageForm) {
  const parsed = schema.parse(values);
  return { ...parsed, code: parsed.code?.trim().toUpperCase() || null, description: parsed.description || null };
}

export default function LanguagesPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Languages"
        description="The languages a candidate can say they speak or write — a dropdown instead of free text, so a shortlisting criterion can match on it."
        backHref="/administration/hr"
      />

      <ResourceListPanel<Language, LanguageForm>
        title="languages"
        singular="language"
        queryKey={['hr', 'languages']}
        invalidateKeys={[['hr', 'languages', 'active']]}
        dialogHint="The code is ISO 639 where one exists (EN, FR), otherwise a short local code (AK, DAG)."
        getId={(l) => l.id}
        list={() => languageService.getAll()}
        create={(values) => languageService.create(toPayload(values))}
        update={(id, values) => languageService.update(id, toPayload(values))}
        // Refused by the server with a 422 and a count while any candidate record still names the row.
        remove={(id) => languageService.remove(id)}
        columns={[
          { header: 'Language', cell: (l) => <span className="font-medium">{l.name}</span> },
          { header: 'Code', cell: (l) => l.code || '—' },
          { header: 'Order', cell: (l) => l.sortOrder },
          {
            header: 'In use',
            cell: (l) => (l.usageCount > 0 ? <Badge variant="secondary">{l.usageCount}</Badge> : <span className="text-muted-foreground">—</span>),
          },
          { header: 'Status', cell: (l) => <StatusBadge status={l.isActive ? 'Active' : 'Retired'} /> },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={(l) => ({
          name: l.name,
          code: l.code ?? '',
          description: l.description ?? '',
          sortOrder: l.sortOrder,
          isActive: l.isActive,
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="name" label="Name" required placeholder="e.g. Akan (Twi)" />
              <TextField form={form} name="code" label="Code" placeholder="e.g. AK" />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" placeholder="Optional" />
            <FieldRow>
              <NumberField form={form} name="sortOrder" label="Sort order" />
              <SwitchField form={form} name="isActive" label="Active" description="Retired languages stay on records that already name them." />
            </FieldRow>
          </>
        )}
      />
    </div>
  );
}
