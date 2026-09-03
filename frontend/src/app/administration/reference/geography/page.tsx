'use client';

/**
 * Division schemes — one per country, each describing how that country divides itself up.
 *
 * ⚠ **This is not the Location tree.** A Location is one of *our* sites — an office, a depot, a
 * clock-in station — and roughly thirty foreign keys across HR, SHE, Attendance and Finance point
 * at it. A GeoArea is a place on the map of a country, true whether or not the company operates
 * there. Seeding regions as Locations would put "Greater Accra Region" in the incident-site picker.
 *
 * ⚠ **Schemes are per country on purpose.** Ghana is Region → District → Town, Nigeria is State →
 * LGA → Ward, the UK is County → District → Parish. Because the tiers are data, a second country
 * needs a new scheme, not a schema change.
 *
 * See docs/GEOGRAPHY-REFERENCE-DESIGN.md.
 */

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
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
import { geographyService } from '@/services/reference/geography.service';
import { countryService } from '@/services/hr/country.service';
import type { GeoScheme } from '@/types/reference/geography';

const schemeSchema = z.object({
  countryId: z.string().min(1, 'Pick the country this scheme divides'),
  name: z.string().min(1, 'A name is required').max(150),
  code: z.string().min(1, 'A code is required').max(50),
  description: z.string().max(1000).optional(),
  isDefault: z.boolean(),
  isActive: z.boolean(),
});

type SchemeForm = z.input<typeof schemeSchema>;

const emptyScheme: SchemeForm = {
  countryId: '',
  name: '',
  code: '',
  description: '',
  isDefault: true,
  isActive: true,
};

/** Blank optionals must reach the API as null, not as empty strings. */
function toPayload(values: SchemeForm) {
  const parsed = schemeSchema.parse(values);
  return { ...parsed, description: parsed.description || null };
}

export default function GeographySchemesPage() {
  // Countries are an HR-owned lookup today; geography only reads it. If Country ever moves to this
  // reference module, this import is the single place that changes.
  const { data: countries = [] } = useQuery({
    queryKey: ['reference', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const countryOptions = countries.map((c) => ({ value: c.id, label: `${c.name} (${c.code})` }));

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Geography"
        description="How each country divides itself up — the Region, District and Town tiers every module's addresses resolve through."
        backHref="/administration"
      />

      <ResourceListPanel<GeoScheme, SchemeForm>
        title="division schemes"
        singular="scheme"
        queryKey={['reference', 'geo', 'schemes']}
        dialogHint="One scheme per country. Mark it as the default and every address form for that country will use it."
        emptyDescription="No division scheme yet. Add one for a country, then give it tiers — Region, District, Town or whatever that country calls them."
        list={() => geographyService.getSchemes()}
        create={(values) => geographyService.createScheme(toPayload(values) as never)}
        update={(id, values) => geographyService.updateScheme(id, toPayload(values) as never)}
        remove={(id) => geographyService.removeScheme(id)}
        getId={(s) => s.id}
        columns={[
          {
            header: 'Scheme',
            cell: (s) => (
              <Link
                href={`/administration/reference/geography/${s.id}`}
                className="font-medium hover:underline"
              >
                {s.name}
              </Link>
            ),
          },
          { header: 'Country', cell: (s) => s.countryName || '—' },
          { header: 'Code', cell: (s) => s.code },
          {
            header: 'Default',
            // A country with no default scheme is why an address form silently falls back to free
            // text, so it is a column rather than something to discover in a log.
            cell: (s) => (s.isDefault ? <Badge variant="secondary">Default</Badge> : '—'),
          },
          { header: 'Tiers', cell: (s) => s.levelCount, className: 'text-right' },
          {
            header: 'Areas',
            // Zero means the seed never ran — the commonest reason a cascade shows nothing.
            cell: (s) =>
              s.areaCount > 0 ? (
                s.areaCount.toLocaleString()
              ) : (
                <span className="text-muted-foreground">none</span>
              ),
            className: 'text-right',
          },
          { header: 'Status', cell: (s) => <StatusBadge active={s.isActive} /> },
        ]}
        schema={schemeSchema as never}
        emptyForm={emptyScheme}
        toForm={(s) => ({
          countryId: s.countryId,
          name: s.name,
          code: s.code,
          description: s.description ?? '',
          isDefault: s.isDefault,
          isActive: s.isActive,
        })}
        renderFields={(form) => (
          <>
            <SelectField
              form={form}
              name="countryId"
              label="Country"
              required
              options={countryOptions}
              placeholder="Select a country…"
            />
            <FieldRow>
              <TextField
                form={form}
                name="name"
                label="Scheme name"
                required
                placeholder="Ghana Administrative Divisions"
              />
              <TextField form={form} name="code" label="Code" required placeholder="GH-ADMIN" />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <SwitchField
              form={form}
              name="isDefault"
              label="Default for this country"
              description="Address forms use the default. Turning this on turns it off for whichever scheme held it — a country has exactly one."
            />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="An inactive scheme stops being offered on new addresses; records that already resolve through it keep working."
            />
          </>
        )}
      />
    </div>
  );
}
