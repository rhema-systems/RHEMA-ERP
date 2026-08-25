'use client';

/**
 * Area 25 slice 4 — file a new leave request (self only).
 *
 * Create and submit are two backend acts: POST /Leaves lands the row (Draft or Pending)
 * and POST /Leaves/{id}/submit starts the approval workflow. If create succeeds but
 * submit is refused (e.g. no active definition), the request EXISTS unsubmitted — the
 * toast says exactly that and the detail screen offers submit again, rather than
 * pretending the whole thing failed.
 */

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/components/ui/use-toast';
import { Button } from '@/components/ui/button';
import { ArrowLeft } from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import {
  MyLeaveRequestForm,
  emptyMyLeaveRequest,
  myLeaveRequestToPayload,
  type MyLeaveRequestFormValues,
} from '@/components/me/leave/MyLeaveRequestForm';
import { leaveService } from '@/services/hr/leave.service';

export default function NewMyLeaveRequestPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { user } = useAuth();
  const employeeId = user?.employeeId ?? '';
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (values: MyLeaveRequestFormValues, saveAsDraft: boolean) => {
    setSubmitting(true);
    try {
      const created = await leaveService.create(
        myLeaveRequestToPayload(employeeId, values, saveAsDraft),
      );

      if (!saveAsDraft) {
        try {
          await leaveService.submit(created.id);
          toast({ title: 'Submitted', description: 'Your leave request is on its way for approval.' });
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
        toast({ title: 'Draft saved', description: 'Submit it when you are ready.' });
      }

      await queryClient.invalidateQueries({ queryKey: ['me', 'leave-history'] });
      await queryClient.invalidateQueries({ queryKey: ['me', 'leave-balances'] });
      router.push(`/me/leave/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Could not create the request',
        description: error?.message || 'Something went wrong creating the leave request.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <Button variant="ghost" size="sm" asChild className="-ml-2 mb-2">
          <Link href="/me/leave">
            <ArrowLeft className="mr-1 h-4 w-4" /> My Leave
          </Link>
        </Button>
        <h1 className="text-2xl font-bold tracking-tight">New leave request</h1>
      </div>
      <MyLeaveRequestForm
        employeeId={employeeId}
        defaultValues={emptyMyLeaveRequest}
        onSubmit={handleSubmit}
        submitting={submitting}
        onCancel={() => router.push('/me/leave')}
      />
    </div>
  );
}
