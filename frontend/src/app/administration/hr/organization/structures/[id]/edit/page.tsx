'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  OrganizationStructureForm,
  type OrganizationStructureFormValues,
} from '@/components/hr/organization/OrganizationStructureForm';
import { organizationStructureService } from '@/services/hr/organization-structure.service';

export default function EditOrganizationStructurePage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: structure, isLoading, isError } = useQuery({
    queryKey: ['hr', 'organization-structures', id],
    queryFn: () => organizationStructureService.getById(id),
    enabled: !!id,
  });

  const handleSubmit = async (values: OrganizationStructureFormValues) => {
    setSubmitting(true);
    try {
      await organizationStructureService.update(id, {
        id,
        name: values.name,
        code: values.code ?? '',
        description: values.description || null,
        isDefault: values.isDefault,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'organization-structures'] });
      toast({ title: 'Success', description: 'Organization structure updated.' });
      router.push('/administration/hr/organization/structures');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update structure.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Edit Organization Structure"
        description={structure ? structure.name : 'Update this structure.'}
        backHref="/administration/hr/organization/structures"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !structure ? (
        <EmptyState title="Structure not found" description="This structure may have been deleted." />
      ) : (
        <OrganizationStructureForm
          defaultValues={{
            name: structure.name,
            code: structure.code ?? '',
            description: structure.description ?? '',
            isDefault: structure.isDefault,
            isActive: structure.isActive,
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/organization/structures')}
        />
      )}
    </div>
  );
}
