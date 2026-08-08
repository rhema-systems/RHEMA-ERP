'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  LocationLevelForm,
  type LocationLevelFormValues,
} from '@/components/hr/location/LocationLevelForm';
import { locationLevelService } from '@/services/hr/location-level.service';
import { locationStructureService } from '@/services/hr/location-structure.service';

export default function EditLocationLevelPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: level, isLoading, isError } = useQuery({
    queryKey: ['hr', 'location-levels', id],
    queryFn: () => locationLevelService.getById(id),
    enabled: !!id,
  });

  const { data: structures } = useQuery({
    queryKey: ['hr', 'location-structures', 'summary'],
    queryFn: () => locationStructureService.getSummary(),
  });

  const handleSubmit = async (values: LocationLevelFormValues) => {
    setSubmitting(true);
    try {
      await locationLevelService.update(id, {
        id,
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
      toast({ title: 'Success', description: 'Location level updated.' });
      router.push('/administration/hr/location/levels');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update location level.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="Edit Location Level"
        description={level ? level.name : 'Update this tier of the location hierarchy.'}
        backHref="/administration/hr/location/levels"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !level ? (
        <EmptyState title="Level not found" description="This location level may have been deleted." />
      ) : (
        <LocationLevelForm
          structures={structures ?? []}
          defaultValues={{
            name: level.name,
            code: level.code ?? '',
            description: level.description ?? '',
            levelNumber: level.levelNumber,
            structureId: level.structureId,
            requiresAddress: level.requiresAddress,
            requiresContactInfo: level.requiresContactInfo,
            allowsEmployeeAssignment: level.allowsEmployeeAssignment,
            isActive: level.isActive,
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/location/levels')}
        />
      )}
    </div>
  );
}
