'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  LocationStructureForm,
  emptyLocationStructure,
  type LocationStructureFormValues,
} from '@/components/hr/location/LocationStructureForm';
import { locationStructureService } from '@/services/hr/location-structure.service';

export default function NewLocationStructurePage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: LocationStructureFormValues) => {
    setSubmitting(true);
    try {
      await locationStructureService.create({
        name: values.name,
        code: values.code ?? '',
        description: values.description || null,
        isDefault: values.isDefault,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'location-structures'] });
      toast({ title: 'Success', description: 'Location structure created.' });
      router.push('/administration/hr/location/structures');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create structure.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Location Structure"
        description="Create a structure to hold location levels and locations."
        backHref="/administration/hr/location/structures"
      />
      <LocationStructureForm
        defaultValues={emptyLocationStructure}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Structure"
        onCancel={() => router.push('/administration/hr/location/structures')}
        showActive={false}
      />
    </div>
  );
}
