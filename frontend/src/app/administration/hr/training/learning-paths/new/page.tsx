'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  LearningPathForm,
  emptyLearningPath,
  toLearningPathRequest,
  type LearningPathFormValues,
} from '@/components/hr/training/LearningPathForm';
import { learningPathService } from '@/services/hr/learning-path.service';

export default function NewLearningPathPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: LearningPathFormValues) => {
    setSubmitting(true);
    try {
      const created = await learningPathService.create(toLearningPathRequest(values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'learning-paths'] });
      toast({
        title: 'Created',
        description: 'Now add the programmes and put them in order.',
      });
      router.push(`/administration/hr/training/learning-paths/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create the path.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Learning Path"
        description="Create the curriculum, then sequence its programmes."
        backHref="/administration/hr/training/learning-paths"
      />
      <LearningPathForm
        defaultValues={emptyLearningPath}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create path"
        onCancel={() => router.push('/administration/hr/training/learning-paths')}
      />
    </div>
  );
}
