'use client';

/**
 * The area browser — one tier at a time, drilling down through the scheme.
 *
 * ⚠ **A boundary change is never a rename.** Ghana went from 10 regions to 16 in 2019 and districts
 * split most election cycles. Renaming an area in place silently rewrites history: a record created
 * in 2018 starts claiming a region that did not exist then. Instead, set the old area's *ends*
 * date and point it at its successor — the record still resolves to what it actually said, and
 * "headcount by region as at 2018" stays answerable.
 *
 * ⚠ **The code is the seeder's idempotency key.** Use the official statutory code (a Ghana
 * Statistical Service district code, an ISO 3166-2 subdivision code) — never an invented one.
 * Invent one and the next seed of the same data creates a second Tema Metropolitan.
 */

import { use, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { ChevronRight, Home } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  NumberField,
  TextareaField,
  DateField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { geographyService } from '@/services/reference/geography.service';
import type { GeoArea, GeoAreaAlias, GeoAreaAliasKind } from '@/types/reference/geography';

/**
 * ⚠ An empty number input arrives as `''`, and `z.coerce.number()` turns that into **0** — which
 * for a coordinate means the Gulf of Guinea, not "not recorded". The blank is mapped to
 * `undefined` before coercion so a left-empty latitude stays empty.
 */
const optionalCoordinate = (min: number, max: number) =>
  z.preprocess(
    (value) => (value === '' || value === null ? undefined : value),
    z.coerce.number().min(min).max(max).optional(),
  );

const areaSchema = z.object({
  name: z.string().min(1, 'A name is required').max(200),
  code: z.string().min(1, 'A code is required').max(50),
  parentAreaId: z.string().optional(),
  latitude: optionalCoordinate(-90, 90),
  longitude: optionalCoordinate(-180, 180),
  effectiveFrom: z.string().optional(),
  effectiveTo: z.string().optional(),
  supersededByGeoAreaId: z.string().optional(),
  isActive: z.boolean(),
  notes: z.string().max(1000).optional(),
});

type AreaForm = z.input<typeof areaSchema>;

const emptyArea: AreaForm = {
  name: '',
  code: '',
  parentAreaId: '',
  latitude: undefined,
  longitude: undefined,
  effectiveFrom: '',
  effectiveTo: '',
  supersededByGeoAreaId: '',
  isActive: true,
  notes: '',
};

const aliasSchema = z.object({
  alias: z.string().min(1, 'A name is required').max(200),
  kind: z.string().min(1),
  notes: z.string().max(500).optional(),
});

type AliasForm = z.input<typeof aliasSchema>;

const emptyAlias: AliasForm = { alias: '', kind: 'FormerName', notes: '' };

const ALIAS_KINDS: { value: GeoAreaAliasKind; label: string }[] = [
  { value: 'FormerName', label: 'Former name' },
  { value: 'Spelling', label: 'Alternate spelling' },
  { value: 'Abbreviation', label: 'Abbreviation' },
  { value: 'Vernacular', label: 'Local name' },
  { value: 'Other', label: 'Other' },
];

function toAliasPayload(geoAreaId: string, values: AliasForm) {
  const parsed = aliasSchema.parse(values);
  return {
    geoAreaId,
    alias: parsed.alias,
    kind: parsed.kind as GeoAreaAliasKind,
    notes: parsed.notes || null,
  };
}

/** One step of the drill-down. */
interface Crumb {
  id: string;
  name: string;
}

export default function GeoAreasPage({ params }: { params: Promise<{ schemeId: string }> }) {
  const { schemeId } = use(params);

  const [trail, setTrail] = useState<Crumb[]>([]);
  const [includeHistorical, setIncludeHistorical] = useState(false);
  const [aliasTarget, setAliasTarget] = useState<GeoArea | null>(null);

  const { data: scheme } = useQuery({
    queryKey: ['reference', 'geo', 'scheme', schemeId],
    queryFn: () => geographyService.getScheme(schemeId),
  });

  // Active tiers only, broadest first — the backend orders by depth. The drill-down position is
  // simply how many crumbs deep we are.
  const { data: levels = [] } = useQuery({
    queryKey: ['reference', 'geo', 'levels', schemeId, 'active'],
    queryFn: () => geographyService.getLevels(schemeId, true),
  });

  const depth = trail.length;
  const currentLevel = levels[depth];
  const parentLevel = depth > 0 ? levels[depth - 1] : undefined;
  const parentId = depth > 0 ? trail[depth - 1].id : undefined;
  const hasDeeperTier = depth + 1 < levels.length;

  // Only loaded when reparenting is actually possible — at the broadest tier there is no parent to
  // choose, and asking for one would be a dropdown that can never be right.
  const { data: parentOptions = [] } = useQuery({
    queryKey: ['reference', 'geo', 'areas', schemeId, 'parent-options', parentLevel?.id],
    queryFn: () => geographyService.getAreas(schemeId, { levelId: parentLevel!.id }),
    enabled: !!parentLevel,
  });

  const parentSelectOptions = useMemo(
    () => parentOptions.map((a) => ({ value: a.id, label: a.name })),
    [parentOptions],
  );

  // ⚠ A successor sits at the SAME tier, not the tier above — a district is replaced by a
  // district. Historical rows are included: a merge can name an area that has since been
  // end-dated itself, and the trail has to stay followable.
  const { data: siblingAreas = [] } = useQuery({
    queryKey: ['reference', 'geo', 'areas', schemeId, 'successor-options', currentLevel?.id],
    queryFn: () =>
      geographyService.getAreas(schemeId, {
        levelId: currentLevel!.id,
        includeHistorical: true,
      }),
    enabled: !!currentLevel,
  });

  const successorOptions = useMemo(
    () => siblingAreas.map((a) => ({ value: a.id, label: a.name })),
    [siblingAreas],
  );

  function toAreaPayload(values: AreaForm) {
    const parsed = areaSchema.parse(values);
    return {
      schemeId,
      geoLevelId: currentLevel?.id ?? '',
      // The browse position supplies the parent; the field only overrides it when someone
      // deliberately moves the area.
      parentAreaId: (parsed.parentAreaId || parentId) ?? null,
      name: parsed.name,
      code: parsed.code,
      latitude: parsed.latitude ?? null,
      longitude: parsed.longitude ?? null,
      polygonCoordinatesJson: null,
      effectiveFrom: parsed.effectiveFrom || null,
      effectiveTo: parsed.effectiveTo || null,
      supersededByGeoAreaId: parsed.supersededByGeoAreaId || null,
      isActive: parsed.isActive,
      notes: parsed.notes || null,
    };
  }

  if (levels.length === 0) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader
          title="Areas"
          description="This scheme has no tiers yet, so there is nowhere to put an area."
          backHref={`/administration/reference/geography/${schemeId}`}
        />
        <p className="text-muted-foreground text-sm">
          Add at least one tier — Region, State, Province, whatever the country calls its broadest
          division — and this page will start at it.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`Areas — ${currentLevel?.name ?? 'all tiers'}`}
        description={
          scheme
            ? `${scheme.name}. Drill down a tier at a time; the list below is one tier of the tree.`
            : 'Loading…'
        }
        backHref={`/administration/reference/geography/${schemeId}`}
        actions={
          <div className="flex items-center gap-2">
            <Switch
              id="include-historical"
              checked={includeHistorical}
              onCheckedChange={setIncludeHistorical}
            />
            <Label htmlFor="include-historical" className="text-sm font-normal">
              Show areas that no longer exist
            </Label>
          </div>
        }
      />

      {/* Breadcrumb — click any step to go back up. */}
      <div className="flex flex-wrap items-center gap-1 text-sm">
        <Button
          variant="ghost"
          size="sm"
          className="h-7 gap-1 px-2"
          onClick={() => {
            setTrail([]);
            setAliasTarget(null);
          }}
        >
          <Home className="h-3.5 w-3.5" />
          {levels[0]?.name ?? 'Top'}
        </Button>
        {trail.map((crumb, index) => (
          <span key={crumb.id} className="flex items-center gap-1">
            <ChevronRight className="text-muted-foreground h-3.5 w-3.5" />
            <Button
              variant="ghost"
              size="sm"
              className="h-7 px-2"
              onClick={() => {
                setTrail(trail.slice(0, index + 1));
                setAliasTarget(null);
              }}
            >
              {crumb.name}
            </Button>
          </span>
        ))}
      </div>

      {currentLevel ? (
        <ResourceListPanel<GeoArea, AreaForm>
          title={`${currentLevel.name.toLowerCase()} areas`}
          singular={currentLevel.name.toLowerCase()}
          queryKey={[
            'reference',
            'geo',
            'areas',
            schemeId,
            currentLevel.id,
            parentId ?? 'root',
            includeHistorical,
          ]}
          invalidateKeys={[
            ['reference', 'geo', 'levels', schemeId],
            ['reference', 'geo', 'scheme', schemeId],
            ['reference', 'geo', 'schemes'],
          ]}
          dialogHint="Use the official statutory code — it is what makes re-running a geography seed a no-op instead of a duplicate."
          emptyDescription={`No ${currentLevel.name.toLowerCase()} here yet.`}
          list={() =>
            geographyService.getAreas(schemeId, {
              levelId: currentLevel.id,
              parentId,
              includeHistorical,
            })
          }
          create={(values) => geographyService.createArea(toAreaPayload(values) as never)}
          update={(id, values) => geographyService.updateArea(id, toAreaPayload(values) as never)}
          remove={(id) => geographyService.removeArea(id)}
          getId={(a) => a.id}
          actions={[
            {
              label: 'Open',
              visible: () => hasDeeperTier,
              run: async (area) => {
                setTrail([...trail, { id: area.id, name: area.name }]);
                setAliasTarget(null);
              },
            },
            {
              label: 'Alternate names',
              run: async (area) => setAliasTarget(area),
            },
          ]}
          columns={[
            { header: 'Name', cell: (a) => <span className="font-medium">{a.name}</span> },
            { header: 'Code', cell: (a) => a.code },
            {
              header: 'Below',
              cell: (a) =>
                a.childCount > 0 ? (
                  a.childCount.toLocaleString()
                ) : (
                  <span className="text-muted-foreground">—</span>
                ),
              className: 'text-right',
            },
            {
              header: 'Names',
              // Alternate names are what let an old spreadsheet still import, so the count is
              // visible rather than hidden behind the action.
              cell: (a) =>
                a.aliases.length > 0 ? (
                  a.aliases.length
                ) : (
                  <span className="text-muted-foreground">—</span>
                ),
              className: 'text-right',
            },
            {
              header: 'Status',
              cell: (a) =>
                a.isHistorical ? (
                  <div className="flex flex-col gap-1">
                    <Badge variant="destructive">No longer exists</Badge>
                    {a.supersededByAreaName && (
                      <span className="text-muted-foreground text-xs">
                        now {a.supersededByAreaName}
                      </span>
                    )}
                  </div>
                ) : (
                  <StatusBadge active={a.isActive} />
                ),
            },
          ]}
          schema={areaSchema as never}
          emptyForm={emptyArea}
          toForm={(a) => ({
            name: a.name,
            code: a.code,
            parentAreaId: a.parentAreaId ?? '',
            latitude: a.latitude ?? undefined,
            longitude: a.longitude ?? undefined,
            effectiveFrom: a.effectiveFrom ?? '',
            effectiveTo: a.effectiveTo ?? '',
            supersededByGeoAreaId: a.supersededByGeoAreaId ?? '',
            isActive: a.isActive,
            notes: a.notes ?? '',
          })}
          renderFields={(form) => (
            <>
              <FieldRow>
                <TextField form={form} name="name" label="Name" required />
                <TextField
                  form={form}
                  name="code"
                  label="Official code"
                  required
                  placeholder="GH-GA-TMA"
                />
              </FieldRow>

              {parentLevel && (
                <SelectField
                  form={form}
                  name="parentAreaId"
                  label={`Sits under (${parentLevel.name})`}
                  options={parentSelectOptions}
                  placeholder="Keep where it is…"
                  allowEmpty
                  emptyLabel="Keep where it is"
                />
              )}

              <FieldRow>
                <NumberField form={form} name="latitude" label="Latitude" />
                <NumberField form={form} name="longitude" label="Longitude" />
              </FieldRow>

              <FieldRow>
                <DateField form={form} name="effectiveFrom" label="Exists from" />
                <DateField form={form} name="effectiveTo" label="Ceased to exist" />
              </FieldRow>

              <SelectField
                form={form}
                name="supersededByGeoAreaId"
                label="Replaced by"
                options={successorOptions}
                placeholder="Nothing — it still exists"
                allowEmpty
                emptyLabel="Nothing — it still exists"
              />

              <TextareaField form={form} name="notes" label="Notes" rows={2} />

              <SwitchField
                form={form}
                name="isActive"
                label="Active"
                description="Inactive keeps it off new addresses. If the area has genuinely ceased to exist, set the “ceased” date instead — that keeps records pointing at it resolvable."
              />
            </>
          )}
        />
      ) : null}

      {aliasTarget && (
        <div className="space-y-3 border-t pt-6">
          <div className="flex items-start justify-between gap-4">
            <div>
              <h2 className="text-lg font-semibold">Alternate names — {aliasTarget.name}</h2>
              <p className="text-muted-foreground mt-1 text-sm">
                What this area is also called. A spreadsheet that still says an old name resolves
                through these instead of failing the row.
              </p>
            </div>
            <Button variant="ghost" size="sm" onClick={() => setAliasTarget(null)}>
              Close
            </Button>
          </div>

          <ResourceListPanel<GeoAreaAlias, AliasForm>
            title="alternate names"
            singular="name"
            queryKey={['reference', 'geo', 'aliases', aliasTarget.id]}
            invalidateKeys={[['reference', 'geo', 'areas', schemeId]]}
            emptyDescription="No alternate names recorded."
            list={() => geographyService.getAliases(aliasTarget.id)}
            create={(values) => geographyService.createAlias(toAliasPayload(aliasTarget.id, values))}
            update={(id, values) =>
              geographyService.updateAlias(id, toAliasPayload(aliasTarget.id, values))
            }
            remove={(id) => geographyService.removeAlias(id)}
            getId={(a) => a.id}
            columns={[
              { header: 'Name', cell: (a) => <span className="font-medium">{a.alias}</span> },
              { header: 'Kind', cell: (a) => a.kindLabel },
              {
                header: 'Notes',
                cell: (a) => a.notes || <span className="text-muted-foreground">—</span>,
              },
            ]}
            schema={aliasSchema as never}
            emptyForm={emptyAlias}
            toForm={(a) => ({ alias: a.alias, kind: a.kind, notes: a.notes ?? '' })}
            renderFields={(form) => (
              <>
                <TextField form={form} name="alias" label="Name" required />
                <SelectField
                  form={form}
                  name="kind"
                  label="Kind"
                  required
                  options={ALIAS_KINDS.map((k) => ({ value: k.value, label: k.label }))}
                />
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />
        </div>
      )}
    </div>
  );
}
