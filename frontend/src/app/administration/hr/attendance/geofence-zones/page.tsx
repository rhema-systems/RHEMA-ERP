'use client';

import { useMemo } from 'react';
import type { UseFormReturn } from 'react-hook-form';
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
import { GeoPicker } from '@/components/hr/common/geo/GeoPicker';
import {
  parsePolygonJson,
  polygonSpanMetres,
  polygonToJson,
  toNumberOrNull,
} from '@/components/hr/common/geo/geo';
import { geofenceZoneService } from '@/services/hr/attendance-setup.service';
import { GEOFENCE_SHAPE_OPTIONS } from '@/types/hr/attendance';
import type { GeofenceZoneSummary } from '@/types/hr/attendance';

/**
 * Geofence zones bound where a punch may be made. The backend validates the shape-specific
 * fields (a circle needs a centre and radius, a polygon needs at least three corners), so the
 * same rules are mirrored here to fail before the round-trip.
 *
 * Since 2026-09-03 the shape is drawn on a map rather than typed: click to place a centre or the
 * corners, drag to adjust, or stand at the gate and press "Use my position". The numeric fields
 * stay for typing known coordinates, and the polygon JSON stays visible for pasting.
 */
const zoneSchema = z
  .object({
    zoneName: z.string().min(1, 'A name is required').max(150),
    description: z.string().max(500).optional(),
    shape: z.enum(['Circle', 'Polygon']),
    centreLatitude: z.coerce.number().min(-90).max(90).optional(),
    centreLongitude: z.coerce.number().min(-180).max(180).optional(),
    radiusMetres: z.coerce.number().min(1).max(100000).optional(),
    polygonCoordinatesJson: z.string().max(8000).optional(),
    softEnforcement: z.boolean(),
    hardEnforcement: z.boolean(),
    isActive: z.boolean(),
    notes: z.string().max(500).optional(),
  })
  .superRefine((v, ctx) => {
    if (v.shape === 'Circle') {
      if (v.centreLatitude === undefined || Number.isNaN(v.centreLatitude)) {
        ctx.addIssue({ code: 'custom', message: 'Required for a circle', path: ['centreLatitude'] });
      }
      if (v.centreLongitude === undefined || Number.isNaN(v.centreLongitude)) {
        ctx.addIssue({ code: 'custom', message: 'Required for a circle', path: ['centreLongitude'] });
      }
      if (!v.radiusMetres) {
        ctx.addIssue({ code: 'custom', message: 'Required for a circle', path: ['radiusMetres'] });
      }
    } else if (parsePolygonJson(v.polygonCoordinatesJson).length < 3) {
      ctx.addIssue({
        code: 'custom',
        message: 'A polygon needs at least three corners. Click them on the map.',
        path: ['polygonCoordinatesJson'],
      });
    }
  });

type ZoneForm = z.input<typeof zoneSchema>;

const emptyZone: ZoneForm = {
  zoneName: '',
  description: '',
  shape: 'Circle',
  centreLatitude: undefined,
  centreLongitude: undefined,
  radiusMetres: 100,
  polygonCoordinatesJson: '',
  softEnforcement: true,
  hardEnforcement: false,
  isActive: true,
  notes: '',
};

/** Blank optionals must reach the API as null, and the unused shape's fields as null too. */
function toPayload(values: ZoneForm) {
  const parsed = zoneSchema.parse(values);
  const isCircle = parsed.shape === 'Circle';
  return {
    ...parsed,
    description: parsed.description || null,
    notes: parsed.notes || null,
    centreLatitude: isCircle ? parsed.centreLatitude : null,
    centreLongitude: isCircle ? parsed.centreLongitude : null,
    radiusMetres: isCircle ? parsed.radiusMetres : null,
    // Re-serialised so hand-pasted variants reach the server in the documented shape.
    polygonCoordinatesJson: isCircle ? null : polygonToJson(parsePolygonJson(parsed.polygonCoordinatesJson)),
  };
}

/** The shape-specific fields with the map. A component of its own so it may use hooks. */
function ZoneShapeFields({ form }: { form: UseFormReturn<ZoneForm> }) {
  const isCircle = form.watch('shape') === 'Circle';
  const lat = toNumberOrNull(form.watch('centreLatitude') as string | number | null | undefined);
  const lng = toNumberOrNull(form.watch('centreLongitude') as string | number | null | undefined);
  const radius = toNumberOrNull(form.watch('radiusMetres') as string | number | null | undefined);
  const polygonJson = (form.watch('polygonCoordinatesJson') as string | undefined) ?? '';
  const polygon = useMemo(() => parsePolygonJson(polygonJson), [polygonJson]);

  const set = (name: keyof ZoneForm, value: unknown) =>
    form.setValue(name, value as never, { shouldValidate: true, shouldDirty: true });

  if (isCircle) {
    return (
      <>
        <FieldRow>
          <NumberField form={form} name="centreLatitude" label="Centre latitude" step="0.000001" required />
          <NumberField form={form} name="centreLongitude" label="Centre longitude" step="0.000001" required />
        </FieldRow>
        <NumberField form={form} name="radiusMetres" label="Radius (metres)" required />
        <GeoPicker
          mode="circle"
          value={{
            centre: lat !== null && lng !== null ? { lat, lng } : null,
            radiusMetres: radius,
          }}
          onChange={(v) => {
            set('centreLatitude', v.centre ? v.centre.lat : '');
            set('centreLongitude', v.centre ? v.centre.lng : '');
            if (v.radiusMetres != null) set('radiusMetres', v.radiusMetres);
          }}
          height={300}
        />
      </>
    );
  }

  return (
    <>
      <GeoPicker
        mode="polygon"
        value={{ polygon }}
        onChange={(v) => set('polygonCoordinatesJson', v.polygon && v.polygon.length ? polygonToJson(v.polygon) : '')}
        height={340}
      />
      <TextareaField
        form={form}
        name="polygonCoordinatesJson"
        label="Polygon coordinates (JSON)"
        rows={3}
        placeholder='[{"lat":5.6037,"lng":-0.1870}, …]'
      />
    </>
  );
}

function describeExtent(z: GeofenceZoneSummary) {
  if (z.shape === 'Circle') return z.radiusMetres ? `${z.radiusMetres} m radius` : '—';
  const points = parsePolygonJson(z.polygonCoordinatesJson);
  if (!points.length) return 'no corners';
  const span = polygonSpanMetres(points);
  return `${points.length} corners${span ? ` · ~${span} m across` : ''}`;
}

export default function GeofenceZonesPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Geofence Zones"
        description="GPS boundaries that clock-in and clock-out punches from staff phones are checked against. Link a zone to a location on the location's edit screen."
        backHref="/administration/hr/attendance"
      />

      <ResourceListPanel<GeofenceZoneSummary, ZoneForm>
        title="geofence zones"
        singular="zone"
        queryKey={['hr', 'geofence-zones']}
        dialogHint="Soft enforcement records a violation; hard enforcement blocks the punch outright."
        dialogClassName="sm:max-w-[880px]"
        list={() => geofenceZoneService.getAll()}
        create={(values) => geofenceZoneService.create(toPayload(values) as any)}
        update={(id, values) => geofenceZoneService.update(id, { id, ...toPayload(values) } as any)}
        remove={(id) => geofenceZoneService.remove(id)}
        getId={(z) => z.id}
        columns={[
          { header: 'Zone', cell: (z) => <span className="font-medium">{z.zoneName}</span> },
          { header: 'Shape', cell: (z) => z.shape },
          {
            header: 'Centre',
            cell: (z) =>
              z.centreLatitude != null && z.centreLongitude != null
                ? `${z.centreLatitude.toFixed(5)}, ${z.centreLongitude.toFixed(5)}`
                : '—',
          },
          { header: 'Extent', cell: describeExtent },
          {
            header: 'Enforcement',
            cell: (z) => (z.hardEnforcement ? 'Hard' : z.softEnforcement ? 'Soft' : 'None'),
          },
          { header: 'Status', cell: (z) => <StatusBadge active={z.isActive} /> },
        ]}
        schema={zoneSchema as any}
        emptyForm={emptyZone}
        toForm={(z) => ({
          ...emptyZone,
          zoneName: z.zoneName,
          description: z.description ?? '',
          shape: z.shape,
          centreLatitude: z.centreLatitude ?? undefined,
          centreLongitude: z.centreLongitude ?? undefined,
          radiusMetres: z.radiusMetres ?? undefined,
          polygonCoordinatesJson: z.polygonCoordinatesJson ?? '',
          softEnforcement: z.softEnforcement,
          hardEnforcement: z.hardEnforcement,
          isActive: z.isActive,
          notes: z.notes ?? '',
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="zoneName" label="Zone name" required />
              <SelectField form={form} name="shape" label="Shape" required options={GEOFENCE_SHAPE_OPTIONS} />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" rows={2} />

            <ZoneShapeFields form={form} />

            <SwitchField
              form={form}
              name="softEnforcement"
              label="Soft enforcement"
              description="Allow the punch but flag it as outside the zone."
            />
            <SwitchField
              form={form}
              name="hardEnforcement"
              label="Hard enforcement"
              description="Reject punches made outside the zone."
            />
            <SwitchField form={form} name="isActive" label="Active" />
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </>
        )}
      />
    </div>
  );
}
