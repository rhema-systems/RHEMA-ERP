'use client';

/**
 * Create and submit are two backend acts: POST /Leaves lands the row (Draft or Pending)
 * and POST /Leaves/{id}/submit starts the approval workflow. Creating without submitting
 * leaves a Pending row with no workflow instance that nobody can approve, so a non-draft
 * create always follows through — and says so honestly when only the hand-off fails.
 */

import { useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
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
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: LeaveRequestFormValues, saveAsDraft: boolean) => {
    setSubmitting(true);
    try {
      const created = await leaveService.create(leaveRequestFormToPayload(values, saveAsDraft));

      if (!saveAsDraft) {
        try {
          await leaveService.submit(created.id);
          toast({ title: 'Submitted', description: 'The leave request is on its way for approval.' });
        } catch (submitError: any) {
          // The request exists; only the workflow hand-off failed. Say so honestly.
          toast({
            title: 'Saved, but not submitted',
            description:
              submitError?.message ||
              'The request was saved but could not be submitted for approval. You can retry from the request page.',
            variant: 'destructive',
          });
        }
      } else {
        toast({
          title: 'Draft saved',
          description: 'Submit it when ready to start the approval workflow.',
        });
      }

      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-requests'] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-balances'] });
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

  // Raised from an approved plan: the plans screen links here with the planned dates and people,
  // and `planId` is what joins the two records (closure plan L-9 / R-1). Everything is still
  // editable — a plan is an intention, and intentions move.
  const p = (key: string) => searchParams?.get(key) ?? '';
  const fromPlan: LeaveRequestFormValues = {
    ...emptyLeaveRequest,
    employeeId: p('employeeId'),
    leaveTypeId: p('leaveTypeId'),
    leaveSubTypeId: p('leaveSubTypeId'),
    startDate: p('startDate'),
    endDate: p('endDate'),
    relieverEmployeeId: p('relieverId'),
    secondRelieverEmployeeId: p('secondRelieverId'),
    leavePlanId: p('planId'),
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="New Leave Request"
        description="Request leave for an employee."
        backHref="/hr/leave/requests"
      />
      <LeaveRequestForm
        defaultValues={p('planId') ? fromPlan : emptyLeaveRequest}
        onSubmit={handleSubmit}
        submitting={submitting}
        onCancel={() => router.push('/hr/leave/requests')}
      />
    </div>
  );
}
