'use client';

/**
 * The tiers of one division scheme — Region, District, Town, or whatever the country calls them.
 *
 * ⚠ **A tier's name is the label the address form prints.** That is the whole mechanism by which
 * one address component serves every country: rename "District" to "Municipal Assembly" here and
 * every employee form using this scheme re-labels itself. There is no per-country frontend code.
 *
 * ⚠ **Depth cannot be changed once areas exist.** The tier number is what orders the cascade and
 * what every parent check compares against; renumbering a populated tier would reorder the form
 * under the data rather than with it. The API refuses it, and the count column is the warning.
 */

import { use } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { MapPin } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  NumberField,
  TextareaField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { geographyService } from '@/services/reference/geography.service';
import type { GeoLevel } from '@/types/reference/geography';

const levelSchema = z.object({
  name: z.string().min(1, 'A name is required').max(100),
  code: z.string().min(1, 'A code is required').max(50),
  description: z.string().max(1000).optional(),
  levelNumber: z.coerce.number().int('Whole numbers only').min(1).max(20),
  isRequiredInAddress: z.boolean(),
  allowsAddressAssignment: z.boolean(),
  isActive: z.boolean(),
});

type LevelForm = z.input<typeof levelSchema>;

const emptyLevel: LevelForm = {
  name: '',
  code: '',
  description: '',
  levelNumber: 1,
  isRequiredInAddress: false,
  allowsAddressAssignment: true,
  isActive: true,
};

export default function GeoSchemeLevelsPage({
  params,
}: {
  params: Promise<{ schemeId: string }>;
}) {
  const { schemeId } = use(params);

  const { data: scheme } = useQuery({
    queryKey: ['reference', 'geo', 'scheme', schemeId],
    queryFn: () => geographyService.getScheme(schemeId),
  });

  function toPayload(values: LevelForm) {
    const parsed = levelSchema.parse(values);
    return { ...parsed, schemeId, description: parsed.description || null };
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={scheme?.name ?? 'Division scheme'}
        description={
          scheme
            ? `Tiers of ${scheme.countryName}'s administrative geography. The tier name is the label every address form prints.`
            : 'Loading…'
        }
        backHref="/administration/reference/geography"
        actions={
          <Button asChild variant="outline">
            <Link href={`/administration/reference/geography/${schemeId}/areas`}>
              <MapPin className="mr-2 h-4 w-4" />
              Browse areas
            </Link>
          </Button>
        }
      />

      <ResourceListPanel<GeoLevel, LevelForm>
        title="tiers"
        singular="tier"
        queryKey={['reference', 'geo', 'levels', schemeId]}
        // The scheme header shows the tier count, and the areas browser builds its drill-down from
        // the tier list, so both go stale when a tier changes.
        invalidateKeys={[
          ['reference', 'geo', 'scheme', schemeId],
          ['reference', 'geo', 'schemes'],
        ]}
        dialogHint="Tier 1 is the broadest. Ghana: 1 Region, 2 District, 3 Town. Nigeria: 1 State, 2 LGA, 3 Ward."
        emptyDescription="No tiers yet. Add the broadest one first — an address form has nothing to render until at least one exists."
        list={() => geographyService.getLevels(schemeId)}
        create={(values) => geographyService.createLevel(toPayload(values) as never)}
        update={(id, values) => geographyService.updateLevel(id, toPayload(values) as never)}
        remove={(id) => geographyService.removeLevel(id)}
        getId={(l) => l.id}
        columns={[
          { header: 'Depth', cell: (l) => l.levelNumber, className: 'text-right' },
          { header: 'Tier', cell: (l) => <span className="font-medium">{l.name}</span> },
          { header: 'Code', cell: (l) => l.code },
          {
            header: 'On the form',
            cell: (l) => (
              <div className="flex flex-wrap gap-1">
                {l.isRequiredInAddress && <Badge variant="secondary">Required</Badge>}
                {!l.allowsAddressAssignment && <Badge variant="outline">Grouping only</Badge>}
                {!l.isRequiredInAddress && l.allowsAddressAssignment && (
                  <span className="text-muted-foreground">Optional</span>
                )}
              </div>
            ),
          },
          {
            header: 'Areas',
            // ⚠ Not decoration. A tier with areas cannot be renumbered or removed, and this
            // number is the only warning before either attempt.
            cell: (l) =>
              l.areaCount > 0 ? (
                l.areaCount.toLocaleString()
              ) : (
                <span className="text-muted-foreground">none</span>
              ),
            className: 'text-right',
          },
          { header: 'Status', cell: (l) => <StatusBadge active={l.isActive} /> },
        ]}
        schema={levelSchema as never}
        emptyForm={emptyLevel}
        toForm={(l) => ({
          name: l.name,
          code: l.code,
          description: l.description ?? '',
          levelNumber: l.levelNumber,
          isRequiredInAddress: l.isRequiredInAddress,
          allowsAddressAssignment: l.allowsAddressAssignment,
          isActive: l.isActive,
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField
                form={form}
                name="name"
                label="Tier name"
                required
                placeholder="Region"
              />
              <TextField form={form} name="code" label="Code" required placeholder="REGION" />
            </FieldRow>
            <NumberField form={form} name="levelNumber" label="Depth (1 = broadest)" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <SwitchField
              form={form}
              name="isRequiredInAddress"
              label="Required on an address"
              description="An address using this scheme must name an area at this tier. Usually true for the region and false for the town."
            />
            <SwitchField
              form={form}
              name="allowsAddressAssignment"
              label="An address can stop here"
              description="Turn off for a tier that exists only to group — one that is never itself an answer to “where do you live?”."
            />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="An inactive tier disappears from new address forms; areas already recorded at it keep resolving."
            />
          </>
        )}
      />
    </div>
  );
}
