'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  OrganizationLevelForm,
  emptyOrganizationLevel,
  type OrganizationLevelFormValues,
} from '@/components/hr/organization/OrganizationLevelForm';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { organizationStructureService } from '@/services/hr/organization-structure.service';

export default function NewOrganizationLevelPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: structures, isLoading: structuresLoading } = useQuery({
    queryKey: ['hr', 'organization-structures', 'summary'],
    queryFn: () => organizationStructureService.getSummary(),
  });

  // Preselect the default structure once structures load.
  const defaultValues = useMemo<OrganizationLevelFormValues>(() => {
    const defaultStructure = structures?.find((s) => s.isDefault) ?? structures?.[0];
    return { ...emptyOrganizationLevel, structureId: defaultStructure?.id ?? '' };
  }, [structures]);

  const handleSubmit = async (values: OrganizationLevelFormValues) => {
    setSubmitting(true);
    try {
      await organizationLevelService.create({
        name: values.name,
        code: values.code ?? '',
        description: values.description || null,
        levelNumber: values.levelNumber,
        requiresHead: values.requiresHead,
        allowsDirectEmployees: values.allowsDirectEmployees,
        isActive: values.isActive,
        structureId: values.structureId,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'organization-levels'] });
      toast({ title: 'Success', description: 'Organization level created.' });
      router.push('/administration/hr/organization/levels');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create organization level.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="New Organization Level"
        description="Add a new tier to the organization hierarchy."
        backHref="/administration/hr/organization/levels"
      />

      {structuresLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <OrganizationLevelForm
          key={defaultValues.structureId}
          structures={structures ?? []}
          defaultValues={defaultValues}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Create Level"
          onCancel={() => router.push('/administration/hr/organization/levels')}
        />
      )}
    </div>
  );
}
