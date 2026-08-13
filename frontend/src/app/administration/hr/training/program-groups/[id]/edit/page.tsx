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
import { trainingProgramGroupService } from '@/services/hr/training-program-group.service';

export default function EditTrainingProgramGroupPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: group, isLoading, isError } = useQuery({
    queryKey: ['hr', 'training', 'program-groups', id],
    queryFn: () => trainingProgramGroupService.getById(id),
    enabled: !!id,
  });

  const handleSubmit = async (values: CodeNameLookupFormValues) => {
    setSubmitting(true);
    try {
      await trainingProgramGroupService.update(id, {
        code: values.code,
        name: values.name,
        colorHex: values.colorHex || null,
        description: values.description || null,
        isActive: values.isActive,
        sortOrder: values.sortOrder,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'program-groups'] });
      toast({ title: 'Success', description: 'Program group updated.' });
      router.push('/administration/hr/training/program-groups');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update program group.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Edit Program Group"
        description={group ? group.name : 'Update this group.'}
        backHref="/administration/hr/training/program-groups"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !group ? (
        <EmptyState title="Group not found" description="This group may have been deleted." />
      ) : (
        <CodeNameLookupForm
          entityLabel="Group"
          codeEditable={false}
          defaultValues={{
            code: group.code,
            name: group.name,
            colorHex: group.colorHex ?? '',
            description: group.description ?? '',
            sortOrder: group.sortOrder,
            isActive: group.isActive,
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/training/program-groups')}
        />
      )}
    </div>
  );
}
