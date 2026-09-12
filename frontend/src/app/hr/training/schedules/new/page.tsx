'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  TrainingScheduleForm,
  emptyTrainingSchedule,
  toScheduleRequest,
  type TrainingScheduleFormValues,
} from '@/components/hr/training/TrainingScheduleForm';
import { trainingScheduleService } from '@/services/hr/training-schedule.service';

export default function NewTrainingSchedulePage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: TrainingScheduleFormValues) => {
    setSubmitting(true);
    try {
      const created = await trainingScheduleService.create(toScheduleRequest(values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'schedules'] });
      toast({ title: 'Created', description: `Schedule ${created.scheduleNumber} was created.` });
      router.push(`/hr/training/schedules/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create schedule.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Training Schedule"
        description="Schedule a run of a catalog programme."
        backHref="/hr/training/schedules"
      />
      <TrainingScheduleForm
        defaultValues={emptyTrainingSchedule}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create schedule"
        onCancel={() => router.push('/hr/training/schedules')}
      />
    </div>
  );
}
