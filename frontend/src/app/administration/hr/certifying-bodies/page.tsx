'use client';

/**
 * The certifying-body catalogue — the institutes, boards and awarding bodies behind a skill.
 *
 * ⚠ **This does not replace the free-text certifier on a skill, it sits beside it.** Existing rows
 * are free text and dropping that column would discard them, and a genuinely one-off certifier does
 * not deserve a catalogue row. The catalogue is what makes the common case consistent; the free
 * text stays for the tail.
 */

import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { referenceDimensionService } from '@/services/hr/lookup.service';
import { countryService } from '@/services/hr/country.service';
import type { CertifyingBody } from '@/types/hr/lookups';

const bodySchema = z.object({
  name: z.string().min(1, 'A name is required').max(200),
  abbreviation: z.string().max(50).optional(),
  description: z.string().max(500).optional(),
  countryId: z.string().optional(),
  website: z.string().max(255).optional(),
  isActive: z.boolean(),
});

type BodyForm = z.input<typeof bodySchema>;

const emptyBody: BodyForm = {
  name: '',
  abbreviation: '',
  description: '',
  countryId: '',
  website: '',
  isActive: true,
};

/** Blank optionals must reach the API as null — an empty string is a value, and would be stored. */
function toPayload(values: BodyForm) {
  const parsed = bodySchema.parse(values);
  return {
    ...parsed,
    abbreviation: parsed.abbreviation || null,
    description: parsed.description || null,
    countryId: parsed.countryId || null,
    website: parsed.website || null,
  };
}

export default function CertifyingBodiesPage() {
  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const countryOptions = (countries ?? []).map((c) => ({ value: c.id, label: c.name }));

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Certifying Bodies"
        description="Organisations that certify a skill — institutes, boards and awarding bodies."
        backHref="/administration/hr"
      />

      <ResourceListPanel<CertifyingBody, BodyForm>
        title="certifying bodies"
        singular="certifying body"
        queryKey={['hr', 'certifying-bodies']}
        dialogHint="The abbreviation is what people actually write — ICAG, CIPS, IET."
        list={() => referenceDimensionService.getCertifyingBodies()}
        create={(values) => referenceDimensionService.createCertifyingBody(toPayload(values) as any)}
        update={(id, values) =>
          referenceDimensionService.updateCertifyingBody(id, toPayload(values) as any)
        }
        remove={(id) => referenceDimensionService.removeCertifyingBody(id)}
        getId={(b) => b.id}
        columns={[
          { header: 'Body', cell: (b) => <span className="font-medium">{b.name}</span> },
          { header: 'Abbreviation', cell: (b) => b.abbreviation || '—' },
          { header: 'Country', cell: (b) => b.countryName || '—' },
          {
            header: 'Website',
            cell: (b) =>
              b.website ? (
                // Not a link: the value is free text typed by an administrator, and rendering
                // unvalidated input as a navigable href is a different decision from displaying it.
                <span className="text-muted-foreground">{b.website}</span>
              ) : (
                '—'
              ),
          },
          { header: 'Status', cell: (b) => <StatusBadge active={b.isActive} /> },
        ]}
        schema={bodySchema as any}
        emptyForm={emptyBody}
        toForm={(b) => ({
          name: b.name,
          abbreviation: b.abbreviation ?? '',
          description: b.description ?? '',
          countryId: b.countryId ?? '',
          website: b.website ?? '',
          isActive: b.isActive,
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="name" label="Name" required />
              <TextField
                form={form}
                name="abbreviation"
                label="Abbreviation"
                placeholder="ICAG"
              />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="countryId"
                label="Country"
                options={countryOptions}
                allowEmpty
                emptyLabel="Not set"
              />
              <TextField form={form} name="website" label="Website" placeholder="icagh.org" />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="A retired body stops being offered on new skills; those already citing it keep it."
            />
          </>
        )}
      />
    </div>
  );
}
