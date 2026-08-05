'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  OrganizationStructureForm,
  emptyOrganizationStructure,
  type OrganizationStructureFormValues,
} from '@/components/hr/organization/OrganizationStructureForm';
import { organizationStructureService } from '@/services/hr/organization-structure.service';

export default function NewOrganizationStructurePage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: OrganizationStructureFormValues) => {
    setSubmitting(true);
    try {
      await organizationStructureService.create({
        name: values.name,
        code: values.code ?? '',
        description: values.description || null,
        isDefault: values.isDefault,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'organization-structures'] });
      toast({ title: 'Success', description: 'Organization structure created.' });
      router.push('/administration/hr/organization/structures');
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
        title="New Organization Structure"
        description="Create a structure to hold levels and units."
        backHref="/administration/hr/organization/structures"
      />
      <OrganizationStructureForm
        defaultValues={emptyOrganizationStructure}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Structure"
        onCancel={() => router.push('/administration/hr/organization/structures')}
        showActive={false}
      />
    </div>
  );
}
