'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  MentoringProgramForm,
  emptyMentoringProgram,
  toMentoringProgramRequest,
  type MentoringProgramFormValues,
} from '@/components/hr/training/MentoringProgramForm';
import { mentoringService } from '@/services/hr/mentoring.service';

export default function NewMentoringProgramPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: MentoringProgramFormValues) => {
    setSubmitting(true);
    try {
      const created = await mentoringService.createProgram(toMentoringProgramRequest(values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'mentoring', 'programs'] });
      toast({ title: 'Created', description: 'Now pair mentors with mentees inside it.' });
      router.push(`/administration/hr/training/mentoring/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create the programme.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Mentoring Programme"
        description="Create the scheme, then add its pairs."
        backHref="/administration/hr/training/mentoring"
      />
      <MentoringProgramForm
        defaultValues={emptyMentoringProgram}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create programme"
        onCancel={() => router.push('/administration/hr/training/mentoring')}
      />
    </div>
  );
}
