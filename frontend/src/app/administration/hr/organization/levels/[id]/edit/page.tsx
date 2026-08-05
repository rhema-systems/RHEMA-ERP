'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  OrganizationLevelForm,
  type OrganizationLevelFormValues,
} from '@/components/hr/organization/OrganizationLevelForm';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { organizationStructureService } from '@/services/hr/organization-structure.service';

export default function EditOrganizationLevelPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: level, isLoading, isError } = useQuery({
    queryKey: ['hr', 'organization-levels', id],
    queryFn: () => organizationLevelService.getById(id),
    enabled: !!id,
  });

  const { data: structures } = useQuery({
    queryKey: ['hr', 'organization-structures', 'summary'],
    queryFn: () => organizationStructureService.getSummary(),
  });

  const handleSubmit = async (values: OrganizationLevelFormValues) => {
    setSubmitting(true);
    try {
      await organizationLevelService.update(id, {
        id,
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
      toast({ title: 'Success', description: 'Organization level updated.' });
      router.push('/administration/hr/organization/levels');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update organization level.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="Edit Organization Level"
        description={level ? level.name : 'Update this tier of the organization hierarchy.'}
        backHref="/administration/hr/organization/levels"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !level ? (
        <EmptyState
          title="Level not found"
          description="This organization level may have been deleted."
        />
      ) : (
        <OrganizationLevelForm
          structures={structures ?? []}
          defaultValues={{
            name: level.name,
            code: level.code ?? '',
            levelNumber: level.levelNumber,
            structureId: level.structureId,
            description: level.description ?? '',
            requiresHead: level.requiresHead,
            allowsDirectEmployees: level.allowsDirectEmployees,
            isActive: level.isActive,
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/organization/levels')}
        />
      )}
    </div>
  );
}
