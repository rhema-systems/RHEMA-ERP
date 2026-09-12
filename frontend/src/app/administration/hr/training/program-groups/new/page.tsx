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
import { trainingProgramGroupService } from '@/services/hr/training-program-group.service';

export default function NewTrainingProgramGroupPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: CodeNameLookupFormValues) => {
    setSubmitting(true);
    try {
      await trainingProgramGroupService.create({
        code: values.code,
        name: values.name,
        colorHex: values.colorHex || null,
        description: values.description || null,
        isActive: values.isActive,
        sortOrder: values.sortOrder,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'program-groups'] });
      toast({ title: 'Success', description: 'Program group created.' });
      router.push('/administration/hr/training/program-groups');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create program group.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Program Group"
        description="Add a curriculum cluster that programs can belong to."
        backHref="/administration/hr/training/program-groups"
      />
      <CodeNameLookupForm
        entityLabel="Group"
        defaultValues={emptyCodeNameLookup}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Group"
        onCancel={() => router.push('/administration/hr/training/program-groups')}
      />
    </div>
  );
}
