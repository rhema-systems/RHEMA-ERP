'use client';

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
import { geofenceZoneService } from '@/services/hr/attendance-setup.service';
import { GEOFENCE_SHAPE_OPTIONS } from '@/types/hr/attendance';
import type { GeofenceZoneSummary } from '@/types/hr/attendance';

/**
 * Geofence zones bound where a punch may be made. The backend validates the shape-specific
 * fields (a circle needs a centre and radius, a polygon needs coordinates), so the same
 * rules are mirrored here to fail before the round-trip.
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
    } else if (!v.polygonCoordinatesJson?.trim()) {
      ctx.addIssue({
        code: 'custom',
        message: 'Polygon coordinates are required',
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
    polygonCoordinatesJson: isCircle ? null : parsed.polygonCoordinatesJson,
  };
}

export default function GeofenceZonesPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Geofence Zones"
        description="GPS boundaries that clock-in and clock-out punches are checked against."
        backHref="/administration/hr/attendance"
      />

      <ResourceListPanel<GeofenceZoneSummary, ZoneForm>
        title="geofence zones"
        singular="zone"
        queryKey={['hr', 'geofence-zones']}
        dialogHint="Soft enforcement records a violation; hard enforcement blocks the punch outright."
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
          {
            header: 'Radius',
            cell: (z) => (z.radiusMetres ? `${z.radiusMetres} m` : '—'),
            className: 'text-right',
          },
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
          shape: z.shape,
          centreLatitude: z.centreLatitude ?? undefined,
          centreLongitude: z.centreLongitude ?? undefined,
          radiusMetres: z.radiusMetres ?? undefined,
          softEnforcement: z.softEnforcement,
          hardEnforcement: z.hardEnforcement,
          isActive: z.isActive,
        })}
        renderFields={(form) => {
          const isCircle = form.watch('shape') === 'Circle';
          return (
            <>
              <FieldRow>
                <TextField form={form} name="zoneName" label="Zone name" required />
                <SelectField
                  form={form}
                  name="shape"
                  label="Shape"
                  required
                  options={GEOFENCE_SHAPE_OPTIONS}
                />
              </FieldRow>
              <TextareaField form={form} name="description" label="Description" rows={2} />

              {isCircle ? (
                <>
                  <FieldRow>
                    <NumberField
                      form={form}
                      name="centreLatitude"
                      label="Centre latitude"
                      step="0.000001"
                      required
                    />
                    <NumberField
                      form={form}
                      name="centreLongitude"
                      label="Centre longitude"
                      step="0.000001"
                      required
                    />
                  </FieldRow>
                  <NumberField form={form} name="radiusMetres" label="Radius (metres)" required />
                </>
              ) : (
                <TextareaField
                  form={form}
                  name="polygonCoordinatesJson"
                  label="Polygon coordinates (JSON)"
                  rows={4}
                  placeholder='[{"lat":5.6037,"lng":-0.1870}, …]'
                />
              )}

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
          );
        }}
      />
    </div>
  );
}
