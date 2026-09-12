'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  TrainingPlanForm,
  emptyTrainingPlan,
  type TrainingPlanFormValues,
} from '@/components/hr/training/TrainingPlanForm';
import { trainingPlanService } from '@/services/hr/training-plan.service';

export default function NewTrainingPlanPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: TrainingPlanFormValues) => {
    setSubmitting(true);
    try {
      const created = await trainingPlanService.create({
        year: values.year,
        organizationLevelId: values.organizationLevelId || null,
        organizationUnitId: values.organizationUnitId || null,
        notes: values.notes || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'plans'] });
      toast({ title: 'Success', description: `Plan ${created.planNumber} created.` });
      router.push(`/administration/hr/training/plans/${created.id}`);
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to create plan.', variant: 'destructive' });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Training Plan"
        description="A plan is created as a Draft; add items and budget lines before submitting for approval."
        backHref="/administration/hr/training/plans"
      />
      <TrainingPlanForm
        defaultValues={emptyTrainingPlan}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Plan"
        onCancel={() => router.push('/administration/hr/training/plans')}
      />
    </div>
  );
}
