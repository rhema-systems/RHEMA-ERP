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

const blank = (v?: string | null) => (v && v.trim() ? v.trim() : null);

export default function NewOrganizationUnitPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: levels, isLoading: levelsLoading } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
  });

  const handleSubmit = async (values: OrganizationUnitFormValues) => {
    setSubmitting(true);
    try {
      await organizationUnitService.create({
        name: values.name,
        code: values.code ?? '',
        accountCode: values.accountCode || null,
        // Round 2, lane B2 - the id is what is stored; the server rewrites accountCode
        // above from the chosen account, so the two cannot disagree.
        financeAccountId: values.financeAccountId || null,
        description: values.description || null,
        organizationLevelId: values.organizationLevelId,
        parentUnitId: values.parentUnitId || null,
        headEmployeeId: values.headEmployeeId || null,
        sequence: values.sequence,
        isActive: values.isActive,
        // The initial placement's history row (round 2, O-3a/O-3b).
        effectiveFrom: values.effectiveFrom || null,
        effectiveTo: values.effectiveTo || null,
        changeReason: blank(values.changeReason),
        notes: blank(values.notes),
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'organization-units'] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'organization-unit-history'] });
      toast({ title: 'Success', description: 'Organization unit created and its initial placement recorded.' });
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

      {levelsLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <OrganizationUnitForm
          levels={levels ?? []}
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
