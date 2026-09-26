'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  LeaveRequestForm,
  leaveRequestFormToPayload,
  type LeaveRequestFormValues,
} from '@/components/hr/leave/LeaveRequestForm';
import { leaveService } from '@/services/hr/leave.service';

export default function EditLeaveRequestPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: r, isLoading, isError } = useQuery({
    queryKey: ['hr', 'leave-requests', id],
    queryFn: () => leaveService.getById(id),
    enabled: !!id,
  });

  const handleSubmit = async (values: LeaveRequestFormValues) => {
    setSubmitting(true);
    try {
      // The draft endpoint keeps it a draft; submitting is a separate workflow action.
      await leaveService.updateDraft(id, leaveRequestFormToPayload(values, true));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-requests'] });
      toast({ title: 'Success', description: 'Draft updated.' });
      router.push(`/hr/leave/requests/${id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update the draft.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="Edit Leave Request"
        description={r ? r.requestNumber : 'Update this draft.'}
        backHref={`/hr/leave/requests/${id}`}
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !r ? (
        <EmptyState title="Leave request not found" description="It may have been removed." />
      ) : r.status !== 'Draft' ? (
        <EmptyState
          title="This request is no longer a draft"
          description="Once submitted, a request is changed through the approval workflow rather than edited."
        />
      ) : (
        <LeaveRequestForm
          isEdit
          defaultValues={{
            employeeId: r.employeeId,
            leaveTypeId: r.leaveTypeId,
            leaveSubTypeId: r.leaveSubTypeId ?? '',
            startDate: r.startDate?.slice(0, 10) ?? '',
            endDate: r.endDate?.slice(0, 10) ?? '',
            reason: r.reason ?? '',
            relieverEmployeeId: r.relieverEmployeeId ?? '',
            secondRelieverEmployeeId: r.secondRelieverEmployeeId ?? '',
            relieverNotes: r.relieverNotes ?? '',
            handoverNotes: r.handoverNotes ?? '',
            // Shows the plan banner and holds the leave type to the plan's (guide L-59).
            leavePlanId: r.leavePlanId ?? '',
            chargeExcessToAnnual: r.chargeExcessToAnnual ?? false,
          }}
          onSubmit={handleSubmit}
          submitting={submitting}
          onCancel={() => router.push(`/hr/leave/requests/${id}`)}
        />
      )}
    </div>
  );
}
