'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  LocationLevelForm,
  emptyLocationLevel,
  type LocationLevelFormValues,
} from '@/components/hr/location/LocationLevelForm';
import { locationLevelService } from '@/services/hr/location-level.service';
import { locationStructureService } from '@/services/hr/location-structure.service';

export default function NewLocationLevelPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: structures, isLoading: structuresLoading } = useQuery({
    queryKey: ['hr', 'location-structures', 'summary'],
    queryFn: () => locationStructureService.getSummary(),
  });

  const defaultValues = useMemo<LocationLevelFormValues>(() => {
    const def = structures?.find((s) => s.isDefault) ?? structures?.[0];
    return { ...emptyLocationLevel, structureId: def?.id ?? '' };
  }, [structures]);

  const handleSubmit = async (values: LocationLevelFormValues) => {
    setSubmitting(true);
    try {
      await locationLevelService.create({
        name: values.name,
        code: values.code ?? '',
        description: values.description || null,
        levelNumber: values.levelNumber,
        requiresAddress: values.requiresAddress,
        requiresContactInfo: values.requiresContactInfo,
        allowsEmployeeAssignment: values.allowsEmployeeAssignment,
        isActive: values.isActive,
        structureId: values.structureId,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'location-levels'] });
      toast({ title: 'Success', description: 'Location level created.' });
      router.push('/administration/hr/location/levels');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create location level.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="New Location Level"
        description="Add a new tier to the location hierarchy."
        backHref="/administration/hr/location/levels"
      />

      {structuresLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <LocationLevelForm
          key={defaultValues.structureId}
          structures={structures ?? []}
          defaultValues={defaultValues}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Create Level"
          onCancel={() => router.push('/administration/hr/location/levels')}
        />
      )}
    </div>
  );
}
