'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  ComplianceRequirementForm,
  emptyComplianceRequirement,
  toRequirementRequest,
  type ComplianceRequirementFormValues,
} from '@/components/hr/training/ComplianceRequirementForm';
import { trainingComplianceService } from '@/services/hr/training-compliance.service';

export default function NewComplianceRequirementPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: ComplianceRequirementFormValues) => {
    setSubmitting(true);
    try {
      const created = await trainingComplianceService.createRequirement(toRequirementRequest(values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'compliance'] });
      toast({
        title: 'Created',
        description: 'Nobody is assigned yet — assign employees from the requirement.',
      });
      router.push(`/administration/hr/training/compliance/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create the requirement.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Compliance Requirement"
        description="Define training a population must hold and how often it renews."
        backHref="/administration/hr/training/compliance"
      />
      <ComplianceRequirementForm
        defaultValues={emptyComplianceRequirement}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create requirement"
        onCancel={() => router.push('/administration/hr/training/compliance')}
      />
    </div>
  );
}
