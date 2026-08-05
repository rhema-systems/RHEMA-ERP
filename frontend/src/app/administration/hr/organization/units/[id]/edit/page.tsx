'use client';

import { useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  OrganizationUnitForm,
  type OrganizationUnitFormValues,
} from '@/components/hr/organization/OrganizationUnitForm';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';

export default function EditOrganizationUnitPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: unit, isLoading, isError } = useQuery({
    queryKey: ['hr', 'organization-units', id],
    queryFn: () => organizationUnitService.getById(id),
    enabled: !!id,
  });

  const { data: levels } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
  });

  const { data: units } = useQuery({
    queryKey: ['hr', 'organization-units', 'summary'],
    queryFn: () => organizationUnitService.getSummary(),
  });

  // Exclude the unit itself from the parent options.
  const parentOptions = useMemo(
    () => (units ?? []).filter((u) => u.id !== id),
    [units, id],
  );

  const handleSubmit = async (values: OrganizationUnitFormValues) => {
    setSubmitting(true);
    try {
      await organizationUnitService.update(id, {
        id,
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
      toast({ title: 'Success', description: 'Organization unit updated.' });
      router.push('/administration/hr/organization/units');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update organization unit.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="Edit Organization Unit"
        description={unit ? unit.name : 'Update this node of the organization hierarchy.'}
        backHref="/administration/hr/organization/units"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !unit ? (
        <EmptyState
          title="Unit not found"
          description="This organization unit may have been deleted."
        />
      ) : (
        <OrganizationUnitForm
          levels={levels ?? []}
          units={parentOptions}
          isEdit
          initialHeadLabel={unit.headEmployeeName}
          defaultValues={{
            name: unit.name,
            code: unit.code ?? '',
            accountCode: unit.accountCode ?? '',
            description: unit.description ?? '',
            organizationLevelId: unit.organizationLevelId,
            parentUnitId: unit.parentUnitId ?? '',
            headEmployeeId: unit.headEmployeeId ?? '',
            sequence: unit.sequence,
            isActive: unit.isActive,
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/organization/units')}
        />
      )}
    </div>
  );
}
