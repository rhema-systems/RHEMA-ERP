'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  WorkScheduleForm,
  emptyWorkSchedule,
  normalizeWorkSchedule,
  type WorkScheduleFormOutput,
} from '@/components/hr/attendance/WorkScheduleForm';
import { workScheduleService } from '@/services/hr/attendance-setup.service';

export default function NewWorkSchedulePage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (values: WorkScheduleFormOutput) => {
    setSaving(true);
    try {
      const created = await workScheduleService.create(normalizeWorkSchedule(values) as any);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'work-schedules'] });
      toast({ title: 'Created', description: `"${created.scheduleName}" was created.` });
      // Straight to the editor so shifts can be added while the schedule is fresh in mind.
      router.push(`/administration/hr/attendance/work-schedules/${created.id}/edit`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create work schedule.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Work Schedule"
        description="Define the standard hours attendance will be measured against."
        backHref="/administration/hr/attendance/work-schedules"
      />
      <WorkScheduleForm
        defaultValues={emptyWorkSchedule}
        submitLabel="Create schedule"
        saving={saving}
        onSubmit={handleSubmit}
        onCancel={() => router.push('/administration/hr/attendance/work-schedules')}
      />
    </div>
  );
}
