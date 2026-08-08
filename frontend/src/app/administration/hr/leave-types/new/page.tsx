'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  LeaveTypeForm,
  emptyLeaveType,
  leaveTypeFormToRequest,
  type LeaveTypeFormValues,
} from '@/components/hr/leave/LeaveTypeForm';
import { leaveTypeService } from '@/services/hr/leave-type.service';

export default function NewLeaveTypePage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: LeaveTypeFormValues) => {
    setSubmitting(true);
    try {
      const created = await leaveTypeService.create(leaveTypeFormToRequest(values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-types'] });
      toast({ title: 'Success', description: 'Leave type created.' });
      // Straight to the detail page: sub-types, allocations and accrual policies are
      // set up there and are what makes the type usable.
      router.push(`/administration/hr/leave-types/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create leave type.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="New Leave Type"
        description="Define a kind of leave employees can request."
        backHref="/administration/hr/leave-types"
      />
      <LeaveTypeForm
        defaultValues={emptyLeaveType}
        onSubmit={handleSubmit}
        submitting={submitting}
        submitLabel="Create Leave Type"
        onCancel={() => router.push('/administration/hr/leave-types')}
        showActive={false}
      />
    </div>
  );
}
