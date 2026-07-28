'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  StaffLevelForm,
  emptyStaffLevel,
  type StaffLevelFormValues,
} from '@/components/hr/staff-level/StaffLevelForm';
import { staffLevelService } from '@/services/hr/staff-level.service';

export default function NewStaffLevelPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: StaffLevelFormValues) => {
    setSubmitting(true);
    try {
      await staffLevelService.create({
        name: values.name,
        code: values.code ?? '',
        rank: values.rank,
        description: values.description || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'staff-levels'] });
      toast({ title: 'Success', description: 'Staff level created.' });
      router.push('/administration/hr/staff-levels');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create staff level.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Staff Level"
        description="Add a ranked staff-level classification."
        backHref="/administration/hr/staff-levels"
      />
      <StaffLevelForm
        defaultValues={emptyStaffLevel}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Staff Level"
        onCancel={() => router.push('/administration/hr/staff-levels')}
        showActive={false}
      />
    </div>
  );
}
