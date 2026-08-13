'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  NeedsAssessmentForm,
  emptyNeedsAssessment,
  type NeedsAssessmentFormValues,
} from '@/components/hr/training/NeedsAssessmentForm';
import { trainingNeedsAssessmentService } from '@/services/hr/training-needs-assessment.service';

export default function NewNeedsAssessmentPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: NeedsAssessmentFormValues) => {
    setSubmitting(true);
    try {
      const created = await trainingNeedsAssessmentService.create({
        employeeId: values.employeeId,
        year: values.year,
        source: values.source,
        identifiedGaps: values.identifiedGaps,
        priority: values.priority,
        identifiedById: values.identifiedById,
        identifiedDate: values.identifiedDate,
        additionalNotes: values.additionalNotes || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'needs-assessments'] });
      toast({ title: 'Success', description: 'Training needs assessment created.' });
      router.push(`/administration/hr/training/needs-assessments/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create assessment.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Training Needs Assessment"
        description="Record a training need identified for an employee."
        backHref="/administration/hr/training/needs-assessments"
      />
      <NeedsAssessmentForm
        mode="create"
        defaultValues={emptyNeedsAssessment}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Assessment"
        onCancel={() => router.push('/administration/hr/training/needs-assessments')}
      />
    </div>
  );
}
