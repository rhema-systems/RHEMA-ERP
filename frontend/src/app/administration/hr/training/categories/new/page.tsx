'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  CodeNameLookupForm,
  emptyCodeNameLookup,
  type CodeNameLookupFormValues,
} from '@/components/hr/training/CodeNameLookupForm';
import { trainingCategoryService } from '@/services/hr/training-category.service';

export default function NewTrainingCategoryPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: CodeNameLookupFormValues) => {
    setSubmitting(true);
    try {
      await trainingCategoryService.create({
        code: values.code,
        name: values.name,
        colorHex: values.colorHex || null,
        description: values.description || null,
        isActive: values.isActive,
        sortOrder: values.sortOrder,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'categories'] });
      toast({ title: 'Success', description: 'Training category created.' });
      router.push('/administration/hr/training/categories');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create category.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Training Category"
        description="Add a category training programs can be classified under."
        backHref="/administration/hr/training/categories"
      />
      <CodeNameLookupForm
        entityLabel="Category"
        defaultValues={emptyCodeNameLookup}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Category"
        onCancel={() => router.push('/administration/hr/training/categories')}
      />
    </div>
  );
}
