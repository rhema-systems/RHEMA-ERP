'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  CodeNameLookupForm,
  type CodeNameLookupFormValues,
} from '@/components/hr/training/CodeNameLookupForm';
import { trainingCategoryService } from '@/services/hr/training-category.service';

export default function EditTrainingCategoryPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: category, isLoading, isError } = useQuery({
    queryKey: ['hr', 'training', 'categories', id],
    queryFn: () => trainingCategoryService.getById(id),
    enabled: !!id,
  });

  const handleSubmit = async (values: CodeNameLookupFormValues) => {
    setSubmitting(true);
    try {
      await trainingCategoryService.update(id, {
        code: values.code,
        name: values.name,
        colorHex: values.colorHex || null,
        description: values.description || null,
        isActive: values.isActive,
        sortOrder: values.sortOrder,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'categories'] });
      toast({ title: 'Success', description: 'Training category updated.' });
      router.push('/administration/hr/training/categories');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update category.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Edit Training Category"
        description={category ? category.name : 'Update this category.'}
        backHref="/administration/hr/training/categories"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !category ? (
        <EmptyState title="Category not found" description="This category may have been deleted." />
      ) : (
        <CodeNameLookupForm
          entityLabel="Category"
          codeEditable={false}
          defaultValues={{
            code: category.code,
            name: category.name,
            colorHex: category.colorHex ?? '',
            description: category.description ?? '',
            sortOrder: category.sortOrder,
            isActive: category.isActive,
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/training/categories')}
        />
      )}
    </div>
  );
}
