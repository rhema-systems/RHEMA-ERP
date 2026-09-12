'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  TrainingProgramForm,
  emptyTrainingProgram,
  type TrainingProgramFormValues,
} from '@/components/hr/training/TrainingProgramForm';
import { trainingProgramService } from '@/services/hr/training-program.service';
import { trainingCategoryService } from '@/services/hr/training-category.service';
import { trainingProgramGroupService } from '@/services/hr/training-program-group.service';

export default function NewTrainingProgramPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: categories } = useQuery({
    queryKey: ['hr', 'training', 'categories', 'active'],
    queryFn: () => trainingCategoryService.getAll(true),
  });
  const { data: groups } = useQuery({
    queryKey: ['hr', 'training', 'program-groups', 'active'],
    queryFn: () => trainingProgramGroupService.getAll(true),
  });

  const handleSubmit = async (values: TrainingProgramFormValues) => {
    setSubmitting(true);
    try {
      const created = await trainingProgramService.create({
        ...values,
        description: values.description || '',
        categoryOptionId: values.categoryOptionId || null,
        programGroupId: values.programGroupId || null,
        prerequisites: values.prerequisites || null,
        learningObjectives: values.learningObjectives || null,
        certificateName: values.certificateName || null,
        serviceBondTerms: values.serviceBondTerms || null,
      } as any);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'programs'] });
      toast({ title: 'Success', description: 'Training program created.' });
      router.push(`/administration/hr/training/programs/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create program.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Training Program"
        description="Add a program to the training catalog."
        backHref="/administration/hr/training/programs"
      />
      <TrainingProgramForm
        defaultValues={emptyTrainingProgram}
        categories={categories ?? []}
        groups={groups ?? []}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Program"
        onCancel={() => router.push('/administration/hr/training/programs')}
      />
    </div>
  );
}
