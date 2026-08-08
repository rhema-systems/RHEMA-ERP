'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  LeaveRequestForm,
  emptyLeaveRequest,
  leaveRequestFormToPayload,
  type LeaveRequestFormValues,
} from '@/components/hr/leave/LeaveRequestForm';
import { leaveService } from '@/services/hr/leave.service';

export default function NewLeaveRequestPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: LeaveRequestFormValues, saveAsDraft: boolean) => {
    setSubmitting(true);
    try {
      const created = await leaveService.create(leaveRequestFormToPayload(values, saveAsDraft));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-requests'] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-balances'] });
      toast({
        title: 'Success',
        description: saveAsDraft
          ? 'Draft saved. Submit it when ready to start the approval workflow.'
          : 'Leave request submitted for approval.',
      });
      router.push(`/hr/leave/requests/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create the leave request.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="New Leave Request"
        description="Request leave for an employee."
        backHref="/hr/leave/requests"
      />
      <LeaveRequestForm
        defaultValues={emptyLeaveRequest}
        onSubmit={handleSubmit}
        submitting={submitting}
        onCancel={() => router.push('/hr/leave/requests')}
      />
    </div>
  );
}
