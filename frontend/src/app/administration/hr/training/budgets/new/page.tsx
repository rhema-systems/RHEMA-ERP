'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  TrainingBudgetForm,
  emptyTrainingBudget,
  type TrainingBudgetFormValues,
} from '@/components/hr/training/TrainingBudgetForm';
import { trainingBudgetService } from '@/services/hr/training-budget.service';

export default function NewTrainingBudgetPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: TrainingBudgetFormValues) => {
    setSubmitting(true);
    try {
      const created = await trainingBudgetService.create({
        budgetCode: values.budgetCode,
        year: values.year,
        quarter: values.quarter ? Number(values.quarter) : null,
        organizationLevelId: values.organizationLevelId || null,
        organizationUnitId: values.organizationUnitId || null,
        currency: values.currency,
        allocatedAmount: values.allocatedAmount,
        glAccountCode: values.glAccountCode || null,
        costCenterCode: values.costCenterCode || null,
        notes: values.notes || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'budgets'] });
      toast({ title: 'Success', description: `Budget ${created.budgetCode} created.` });
      router.push(`/administration/hr/training/budgets/${created.id}`);
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to create budget.', variant: 'destructive' });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Training Budget"
        description="A budget is created as a Draft and must be approved before spend can be recorded against it."
        backHref="/administration/hr/training/budgets"
      />
      <TrainingBudgetForm
        defaultValues={emptyTrainingBudget}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Budget"
        onCancel={() => router.push('/administration/hr/training/budgets')}
      />
    </div>
  );
}
