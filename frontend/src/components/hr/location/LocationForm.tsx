'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import {
  Card,
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import Link from 'next/link';
import type { Location, LocationLevel, LocationStructureSummary } from '@/types/hr/location';
import type { Country } from '@/types/hr/country';
import type { GeofenceZoneSummary } from '@/types/hr/attendance';
import { GeoPicker } from '@/components/hr/common/geo/GeoPicker';
import { isValidLat, isValidLng, parsePolygonJson, toNumberOrNull } from '@/components/hr/common/geo/geo';
import { AddressFields } from '@/components/reference/AddressFields';

const NONE = 'none';
const opt = z.string().max(500).optional().or(z.literal(''));

/**
 * Coordinates stay strings in the form so an empty field is empty rather than 0 (`z.coerce.number`
 * turns '' into 0, which is a real place in the Gulf of Guinea). They become numbers in the payload.
 */
export const locationSchema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  code: z.string().max(50).optional().or(z.literal('')),
  description: z.string().max(1000).optional().or(z.literal('')),
  structureId: z.string().min(1, 'Structure is required'),
  locationLevelId: z.string().min(1, 'Level is required'),
  parentLocationId: z.string().optional().or(z.literal('')),
  addressLine1: opt,
  addressLine2: opt,
  city: z.string().max(100).optional().or(z.literal('')),
  /** The administrative area this site stands in; City is a snapshot of it. */
  geoAreaId: z.string().optional().or(z.literal('')),
  postalCode: z.string().max(20).optional().or(z.literal('')),
  countryId: z.string().optional().or(z.literal('')),
  digitalAddress: z.string().max(50).optional().or(z.literal('')),
  latitude: z.string().max(30).optional().or(z.literal('')),
  longitude: z.string().max(30).optional().or(z.literal('')),
  geofenceZoneId: z.string().optional().or(z.literal('')),
  phone: z.string().max(50).optional().or(z.literal('')),
  email: z.string().email('Invalid email').max(100).optional().or(z.literal('')),
  website: z.string().max(200).optional().or(z.literal('')),
  faxNumber: z.string().max(50).optional().or(z.literal('')),
  sequence: z.coerce.number().int('Must be a whole number').min(1, 'Must be at least 1'),
  isActive: z.boolean(),
}).superRefine((v, ctx) => {
  const lat = toNumberOrNull(v.latitude);
  const lng = toNumberOrNull(v.longitude);
  if (lat !== null && !isValidLat(lat)) {
    ctx.addIssue({ code: 'custom', path: ['latitude'], message: 'Latitude must be a number between -90 and 90' });
  }
  if (lng !== null && !isValidLng(lng)) {
    ctx.addIssue({ code: 'custom', path: ['longitude'], message: 'Longitude must be a number between -180 and 180' });
  }
  if ((lat === null) !== (lng === null)) {
    ctx.addIssue({
      code: 'custom',
      path: [lat === null ? 'latitude' : 'longitude'],
      message: 'Enter both latitude and longitude, or neither',
    });
  }
});

export type LocationFormValues = z.infer<typeof locationSchema>;

export const emptyLocation: LocationFormValues = {
  name: '',
  code: '',
  description: '',
  structureId: '',
  locationLevelId: '',
  parentLocationId: '',
  addressLine1: '',
  addressLine2: '',
  city: '',
  geoAreaId: '',
  postalCode: '',
  countryId: '',
  digitalAddress: '',
  latitude: '',
  longitude: '',
  geofenceZoneId: '',
  phone: '',
  email: '',
  website: '',
  faxNumber: '',
  sequence: 1,
  isActive: true,
};

interface LocationFormProps {
  structures: LocationStructureSummary[];
  levels: LocationLevel[];
  locations: Location[];
  countries: Country[];
  /** Geofence zones the location may be linked to. Managed under Attendance › Geofence Zones. */
  zones?: GeofenceZoneSummary[];
  defaultValues: LocationFormValues;
  onSubmit: (values: LocationFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  /** Exclude this location id from the parent options (edit mode). */
  excludeId?: string;
}

export function LocationForm({
  structures,
  levels,
  locations,
  countries,
  zones = [],
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  excludeId,
}: LocationFormProps) {
  const form = useForm<LocationFormValues>({
    resolver: zodResolver(locationSchema) as any,
    defaultValues,
  });

  const structureId = form.watch('structureId');
  const levelId = form.watch('locationLevelId');
  const parentValue = form.watch('parentLocationId') || NONE;

  // The pin, as the map sees it: only when both fields hold a usable number.
  const latNumber = toNumberOrNull(form.watch('latitude'));
  const lngNumber = toNumberOrNull(form.watch('longitude'));
  const pin =
    latNumber !== null && lngNumber !== null && isValidLat(latNumber) && isValidLng(lngNumber)
      ? { lat: latNumber, lng: lngNumber }
      : null;

  const zoneId = form.watch('geofenceZoneId') || '';
  const zone = zones.find((z) => z.id === zoneId) ?? null;
  const zoneReference = zone
    ? {
        centre:
          zone.centreLatitude != null && zone.centreLongitude != null
            ? { lat: zone.centreLatitude, lng: zone.centreLongitude }
            : null,
        radiusMetres: zone.shape === 'Circle' ? zone.radiusMetres : null,
        polygon: zone.shape === 'Polygon' ? parsePolygonJson(zone.polygonCoordinatesJson) : [],
        label: zone.zoneName,
      }
    : null;

  const setPin = (p: { lat: number; lng: number } | null | undefined) => {
    form.setValue('latitude', p ? String(p.lat) : '', { shouldValidate: true, shouldDirty: true });
    form.setValue('longitude', p ? String(p.lng) : '', { shouldValidate: true, shouldDirty: true });
  };

  const describeZone = (z: GeofenceZoneSummary) => {
    const shape = z.shape === 'Circle' ? (z.radiusMetres ? `${z.radiusMetres} m radius` : 'circle') : 'polygon';
    return `${z.zoneName} · ${shape}${z.isActive ? '' : ' (inactive)'}`;
  };

  const levelsForStructure = levels
    .filter((l) => l.structureId === structureId)
    .sort((a, b) => a.levelNumber - b.levelNumber || a.name.localeCompare(b.name));
  const parentOptions = locations
    .filter((l) => l.structureId === structureId && l.id !== excludeId)
    .sort((a, b) => a.name.localeCompare(b.name));

  const handleStructureChange = (value: string) => {
    form.setValue('structureId', value, { shouldValidate: true });
    // Clear level/parent that no longer belong to the chosen structure.
    if (!levels.some((l) => l.id === levelId && l.structureId === value)) {
      form.setValue('locationLevelId', '', { shouldValidate: true });
    }
    const parentId = form.getValues('parentLocationId');
    if (parentId && !locations.some((l) => l.id === parentId && l.structureId === value)) {
      form.setValue('parentLocationId', '');
    }
  };

  const err = (name: keyof LocationFormValues) =>
    form.formState.errors[name]?.message as string | undefined;

  return (
    <Card>
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Location Details</CardTitle>
        </CardHeader>
        <CardContent className="space-y-6">
          {/* Placement */}
          <section className="space-y-4">
            <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Placement</h3>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              <div className="space-y-2">
                <Label htmlFor="structureId">Structure</Label>
                <Select value={structureId || undefined} onValueChange={handleStructureChange}>
                  <SelectTrigger id="structureId">
                    <SelectValue placeholder="Select a structure" />
                  </SelectTrigger>
                  <SelectContent>
                    {structures.map((s) => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.name}
                        {s.isDefault ? ' (Default)' : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {err('structureId') && <p className="text-sm text-red-500">{err('structureId')}</p>}
              </div>
              <div className="space-y-2">
                <Label htmlFor="locationLevelId">Level</Label>
                <Select
                  value={levelId || undefined}
                  disabled={!structureId}
                  onValueChange={(v) => form.setValue('locationLevelId', v, { shouldValidate: true })}
                >
                  <SelectTrigger id="locationLevelId">
                    <SelectValue placeholder={structureId ? 'Select a level' : 'Select a structure first'} />
                  </SelectTrigger>
                  <SelectContent>
                    {levelsForStructure.length === 0 ? (
                      <div className="px-2 py-1.5 text-sm text-muted-foreground">No levels for this structure.</div>
                    ) : (
                      levelsForStructure.map((l) => (
                        <SelectItem key={l.id} value={l.id}>
                          {l.name} (L{l.levelNumber})
                        </SelectItem>
                      ))
                    )}
                  </SelectContent>
                </Select>
                {err('locationLevelId') && <p className="text-sm text-red-500">{err('locationLevelId')}</p>}
              </div>
              <div className="space-y-2">
                <Label htmlFor="parentLocationId">Parent Location</Label>
                <Select
                  value={parentValue}
                  disabled={!structureId}
                  onValueChange={(v) => form.setValue('parentLocationId', v === NONE ? '' : v)}
                >
                  <SelectTrigger id="parentLocationId">
                    <SelectValue placeholder="None (root location)" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE}>None (root location)</SelectItem>
                    {parentOptions.map((l) => (
                      <SelectItem key={l.id} value={l.id}>
                        {l.name}
                        {l.levelName ? ` · ${l.levelName}` : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </section>

          {/* Details */}
          <section className="space-y-4 border-t pt-6">
            <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Details</h3>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <div className="space-y-2 lg:col-span-2">
                <Label htmlFor="name">Name</Label>
                <Input id="name" placeholder="Tema Head Office" {...form.register('name')} />
                {err('name') && <p className="text-sm text-red-500">{err('name')}</p>}
              </div>
              <div className="space-y-2">
                <Label htmlFor="code">Code</Label>
                <Input id="code" placeholder="THO" {...form.register('code')} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="sequence">Sequence</Label>
                <Input id="sequence" type="number" min={1} {...form.register('sequence')} />
                {err('sequence') && <p className="text-sm text-red-500">{err('sequence')}</p>}
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="description">Description</Label>
              <Textarea id="description" rows={2} placeholder="Optional description" {...form.register('description')} />
            </div>
          </section>

          {/* Address */}
          <section className="space-y-4 border-t pt-6">
            <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Address</h3>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="addressLine1">Address Line 1</Label>
                <Input id="addressLine1" {...form.register('addressLine1')} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="addressLine2">Address Line 2</Label>
                <Input id="addressLine2" {...form.register('addressLine2')} />
              </div>
            </div>
            {/*
              Which administrative area this site stands in. The dropdown labels come from the
              selected country's scheme, so there is no country-specific code here.

              ⚠ City is read-only once a scheme is loaded: the server rewrites it from the chosen
              area, so an editable box would silently discard what you type.
            */}
            <AddressFields
              countryId={form.watch('countryId') || ''}
              onCountryChange={(v) => form.setValue('countryId', v, { shouldDirty: true })}
              geoAreaId={form.watch('geoAreaId') || ''}
              onGeoAreaChange={(v) => form.setValue('geoAreaId', v, { shouldDirty: true })}
              fallback={(schemeLoaded) => (
                <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                  <div className="space-y-2">
                    <Label htmlFor="city">City / Town</Label>
                    <Input
                      id="city"
                      {...form.register('city')}
                      readOnly={schemeLoaded}
                      disabled={schemeLoaded}
                    />
                    {schemeLoaded && (
                      <p className="text-muted-foreground text-xs">Set from the address above.</p>
                    )}
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="postalCode">Postal Code</Label>
                    <Input id="postalCode" {...form.register('postalCode')} />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="digitalAddress">Digital Address</Label>
                    <Input id="digitalAddress" {...form.register('digitalAddress')} />
                  </div>
                </div>
              )}
            />
          </section>

          {/* Map & attendance zone */}
          <section className="space-y-4 border-t pt-6">
            <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Map &amp; Attendance Zone</h3>
            <p className="text-sm text-muted-foreground">
              Pin the site on the map, or type its coordinates. The attendance zone decides where staff
              assigned here may clock in from their phones; punches from a fixed device are not affected.
            </p>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <div className="space-y-2">
                <Label htmlFor="latitude">Latitude</Label>
                <Input id="latitude" inputMode="decimal" placeholder="5.603700" {...form.register('latitude')} />
                {err('latitude') && <p className="text-sm text-red-500">{err('latitude')}</p>}
              </div>
              <div className="space-y-2">
                <Label htmlFor="longitude">Longitude</Label>
                <Input id="longitude" inputMode="decimal" placeholder="-0.187000" {...form.register('longitude')} />
                {err('longitude') && <p className="text-sm text-red-500">{err('longitude')}</p>}
              </div>
              <div className="space-y-2 lg:col-span-2">
                <Label htmlFor="geofenceZoneId">Attendance zone</Label>
                <Select
                  value={zoneId || NONE}
                  onValueChange={(v) => form.setValue('geofenceZoneId', v === NONE ? '' : v, { shouldDirty: true })}
                >
                  <SelectTrigger id="geofenceZoneId">
                    <SelectValue placeholder="None (no geofence check)" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE}>None (no geofence check)</SelectItem>
                    {zones.map((z) => (
                      <SelectItem key={z.id} value={z.id}>
                        {describeZone(z)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  Zones are drawn under{' '}
                  <Link href="/administration/hr/attendance/geofence-zones" className="underline underline-offset-2">
                    Attendance › Geofence Zones
                  </Link>
                  . The selected zone is shown on the map in orange.
                </p>
              </div>
            </div>
            <GeoPicker
              mode="point"
              value={{ centre: pin }}
              onChange={(v) => setPin(v.centre)}
              reference={zoneReference}
              height={300}
            />
          </section>

          {/* Contact */}
          <section className="space-y-4 border-t pt-6">
            <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Contact</h3>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <div className="space-y-2">
                <Label htmlFor="phone">Phone</Label>
                <Input id="phone" {...form.register('phone')} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="email">Email</Label>
                <Input id="email" type="email" {...form.register('email')} />
                {err('email') && <p className="text-sm text-red-500">{err('email')}</p>}
              </div>
              <div className="space-y-2">
                <Label htmlFor="website">Website</Label>
                <Input id="website" {...form.register('website')} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="faxNumber">Fax</Label>
                <Input id="faxNumber" {...form.register('faxNumber')} />
              </div>
            </div>
            <div className="flex items-center justify-between rounded-md border px-3 py-2.5 sm:max-w-xs">
              <Label htmlFor="isActive" className="cursor-pointer">Active</Label>
              <Switch
                id="isActive"
                checked={form.watch('isActive')}
                onCheckedChange={(v) => form.setValue('isActive', v)}
              />
            </div>
          </section>
        </CardContent>
        <CardFooter className="flex justify-end gap-2 border-t pt-6">
          <Button variant="outline" type="button" onClick={onCancel} disabled={submitting}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            {submitLabel}
          </Button>
        </CardFooter>
      </form>
    </Card>
  );
}
