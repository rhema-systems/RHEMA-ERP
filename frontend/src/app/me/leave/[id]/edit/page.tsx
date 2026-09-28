'use client';

/**
 * Area 25 slice 4 — edit one of my DRAFT leave requests.
 *
 * PUT /Leaves/{id}/draft refuses anything that is no longer a draft, so a request that
 * moved on while this screen was open fails loudly rather than silently rewriting.
 */

import { use, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { ArrowLeft } from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import {
  MyLeaveRequestForm,
  myLeaveRequestToPayload,
  type MyLeaveRequestFormValues,
} from '@/components/me/leave/MyLeaveRequestForm';
import { leaveService } from '@/services/hr/leave.service';

export default function EditMyLeaveDraftPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { user } = useAuth();
  const employeeId = user?.employeeId ?? '';
  const [submitting, setSubmitting] = useState(false);

  const { data: request, isLoading } = useQuery({
    queryKey: ['me', 'leave-request', id],
    queryFn: () => leaveService.getById(id),
  });

  const handleSubmit = async (values: MyLeaveRequestFormValues) => {
    setSubmitting(true);
    try {
      await leaveService.updateDraft(id, myLeaveRequestToPayload(employeeId, values, true));
      toast({ title: 'Saved', description: 'Your draft was updated.' });
      await queryClient.invalidateQueries({ queryKey: ['me', 'leave-request', id] });
      await queryClient.invalidateQueries({ queryKey: ['me', 'leave-history'] });
      router.push(`/me/leave/${id}`);
    } catch (error: any) {
      toast({
        title: 'Could not save',
        description: error?.message || 'The draft could not be updated.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  if (isLoading || !request) {
    return (
      <div className="mx-auto max-w-3xl space-y-4">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-96" />
      </div>
    );
  }

  if (request.status !== 'Draft') {
    return (
      <div className="mx-auto max-w-3xl space-y-4">
        <h1 className="text-2xl font-bold tracking-tight">This request is not a draft</h1>
        <p className="text-sm text-muted-foreground">
          Only drafts can be edited — this one is {request.status.toLowerCase()}.
        </p>
        <Button asChild variant="outline">
          <Link href={`/me/leave/${id}`}>
            <ArrowLeft className="mr-1 h-4 w-4" /> Back to the request
          </Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <Button variant="ghost" size="sm" asChild className="-ml-2 mb-2">
          <Link href={`/me/leave/${id}`}>
            <ArrowLeft className="mr-1 h-4 w-4" /> {request.requestNumber}
          </Link>
        </Button>
        <h1 className="text-2xl font-bold tracking-tight">Edit draft</h1>
      </div>
      <MyLeaveRequestForm
        employeeId={employeeId}
        isEdit
        defaultValues={{
          leaveTypeId: request.leaveTypeId,
          leaveSubTypeId: request.leaveSubTypeId ?? '',
          startDate: request.startDate,
          endDate: request.endDate,
          reason: request.reason,
          relieverEmployeeId: request.relieverEmployeeId ?? '',
          secondRelieverEmployeeId: request.secondRelieverEmployeeId ?? '',
          relieverNotes: request.relieverNotes ?? '',
          handoverNotes: request.handoverNotes ?? '',
          // Shows the plan banner and holds the leave type to the plan's (guide L-59).
          leavePlanId: request.leavePlanId ?? '',
          chargeExcessToAnnual: request.chargeExcessToAnnual ?? false,
        }}
        onSubmit={handleSubmit}
        submitting={submitting}
        onCancel={() => router.push(`/me/leave/${id}`)}
      />
    </div>
  );
}
