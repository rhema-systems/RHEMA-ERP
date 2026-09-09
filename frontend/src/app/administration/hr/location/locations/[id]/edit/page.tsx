'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { LocationContactsPanel } from '@/components/hr/location/LocationContactsPanel';
import { LocationForm, type LocationFormValues } from '@/components/hr/location/LocationForm';
import { locationService } from '@/services/hr/location.service';
import { locationLevelService } from '@/services/hr/location-level.service';
import { locationStructureService } from '@/services/hr/location-structure.service';
import { countryService } from '@/services/hr/country.service';
import { geofenceZoneService } from '@/services/hr/attendance-setup.service';
import { toNumberOrNull } from '@/components/hr/common/geo/geo';

const s = (v?: string | null) => (v && v.trim() ? v.trim() : null);

export default function EditLocationPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: location, isLoading, isError } = useQuery({
    queryKey: ['hr', 'locations', id],
    queryFn: () => locationService.getById(id),
    enabled: !!id,
  });

  const { data: structures } = useQuery({
    queryKey: ['hr', 'location-structures', 'summary'],
    queryFn: () => locationStructureService.getSummary(),
  });

  const { data: levels } = useQuery({
    queryKey: ['hr', 'location-levels'],
    queryFn: () => locationLevelService.getAll(),
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
      await locationService.update(id, {
        id,
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
      toast({ title: 'Success', description: 'Location updated.' });
      router.push('/administration/hr/location/locations');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update location.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-5xl mx-auto">
      <PageHeader
        title="Edit Location"
        description={location ? location.name : 'Update this location.'}
        backHref="/administration/hr/location/locations"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !location ? (
        <EmptyState title="Location not found" description="This location may have been deleted." />
      ) : (
        <LocationForm
          structures={structures ?? []}
          levels={levels ?? []}
          countries={countries ?? []}
          zones={zones ?? []}
          excludeId={id}
          defaultValues={{
            name: location.name,
            code: location.code ?? '',
            description: location.description ?? '',
            structureId: location.structureId,
            locationLevelId: location.locationLevelId,
            parentLocationId: location.parentLocationId ?? '',
            addressLine1: location.addressLine1 ?? '',
            addressLine2: location.addressLine2 ?? '',
            city: location.city ?? '',
            geoAreaId: location.geoAreaId ?? '',
            postalCode: location.postalCode ?? '',
            countryId: location.countryId ?? '',
            digitalAddress: location.digitalAddress ?? '',
            latitude: location.latitude != null ? String(location.latitude) : '',
            longitude: location.longitude != null ? String(location.longitude) : '',
            geofenceZoneId: location.geofenceZoneId ?? '',
            phone: location.phone ?? '',
            email: location.email ?? '',
            website: location.website ?? '',
            faxNumber: location.faxNumber ?? '',
            sequence: location.sequence,
            isActive: location.isActive,
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/location/locations')}
        />
      )}

      {/* Who to ring here. Four endpoints that nothing in the product has ever called. */}
      {!isLoading && location && <LocationContactsPanel locationId={id} />}
    </div>
  );
}
