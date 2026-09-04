'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { LocationForm, emptyLocation, type LocationFormValues } from '@/components/hr/location/LocationForm';
import { locationService } from '@/services/hr/location.service';
import { locationLevelService } from '@/services/hr/location-level.service';
import { locationStructureService } from '@/services/hr/location-structure.service';
import { countryService } from '@/services/hr/country.service';
import { geofenceZoneService } from '@/services/hr/attendance-setup.service';
import { toNumberOrNull } from '@/components/hr/common/geo/geo';

const s = (v?: string | null) => (v && v.trim() ? v.trim() : null);

export default function NewLocationPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: structures, isLoading: structuresLoading } = useQuery({
    queryKey: ['hr', 'location-structures', 'summary'],
    queryFn: () => locationStructureService.getSummary(),
  });

  const { data: levels, isLoading: levelsLoading } = useQuery({
    queryKey: ['hr', 'location-levels'],
    queryFn: () => locationLevelService.getAll(),
  });

  const { data: locations, isLoading: locationsLoading } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const { data: zones } = useQuery({
    queryKey: ['hr', 'geofence-zones'],
    queryFn: () => geofenceZoneService.getAll(),
  });

  const handleSubmit = async (values: LocationFormValues) => {
    setSubmitting(true);
    try {
      await locationService.create({
        name: values.name,
        code: values.code ?? '',
        description: s(values.description),
        structureId: values.structureId,
        locationLevelId: values.locationLevelId,
        parentLocationId: values.parentLocationId || null,
        addressLine1: s(values.addressLine1),
        addressLine2: s(values.addressLine2),
        city: s(values.city),
        geoAreaId: s(values.geoAreaId),
        postalCode: s(values.postalCode),
        countryId: values.countryId || null,
        digitalAddress: s(values.digitalAddress),
        latitude: toNumberOrNull(values.latitude),
        longitude: toNumberOrNull(values.longitude),
        geofenceZoneId: values.geofenceZoneId || null,
        phone: s(values.phone),
        email: s(values.email),
        website: s(values.website),
        faxNumber: s(values.faxNumber),
        sequence: values.sequence,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'locations'] });
      toast({ title: 'Success', description: 'Location created.' });
      router.push('/administration/hr/location/locations');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create location.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-5xl mx-auto">
      <PageHeader
        title="New Location"
        description="Add a physical work location."
        backHref="/administration/hr/location/locations"
      />

      {structuresLoading || levelsLoading || locationsLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <LocationForm
          structures={structures ?? []}
          levels={levels ?? []}
          locations={locations ?? []}
          countries={countries ?? []}
          zones={zones ?? []}
          defaultValues={emptyLocation}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Create Location"
          onCancel={() => router.push('/administration/hr/location/locations')}
        />
      )}
    </div>
  );
}
