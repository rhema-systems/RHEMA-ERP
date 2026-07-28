'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  OrganizationUnitForm,
  emptyOrganizationUnit,
  type OrganizationUnitFormValues,
} from '@/components/hr/organization/OrganizationUnitForm';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';

export default function NewOrganizationUnitPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: levels, isLoading: levelsLoading } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
  });

  const { data: units, isLoading: unitsLoading } = useQuery({
    queryKey: ['hr', 'organization-units', 'summary'],
    queryFn: () => organizationUnitService.getSummary(),
  });

  const handleSubmit = async (values: OrganizationUnitFormValues) => {
    setSubmitting(true);
    try {
      await organizationUnitService.create({
        name: values.name,
        code: values.code ?? '',
        accountCode: values.accountCode || null,
        description: values.description || null,
        organizationLevelId: values.organizationLevelId,
        parentUnitId: values.parentUnitId || null,
        headEmployeeId: values.headEmployeeId || null,
        sequence: values.sequence,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'organization-units'] });
      toast({ title: 'Success', description: 'Organization unit created.' });
      router.push('/administration/hr/organization/units');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create organization unit.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="New Organization Unit"
        description="Add a node to the organization hierarchy."
        backHref="/administration/hr/organization/units"
      />

      {levelsLoading || unitsLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <OrganizationUnitForm
          levels={levels ?? []}
          units={units ?? []}
          defaultValues={emptyOrganizationUnit}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Create Unit"
          onCancel={() => router.push('/administration/hr/organization/units')}
        />
      )}
    </div>
  );
}
