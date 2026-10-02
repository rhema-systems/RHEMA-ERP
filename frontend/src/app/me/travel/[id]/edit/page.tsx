'use client';

import { use, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FilePenLine, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { TravelRequestForm } from '@/components/hr/travel/TravelRequestForm';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { TravelReasonDialog } from '@/components/hr/travel/TravelReasonDialog';
import { TRAVEL_REQUEST_STATUS_LABELS, enumLabel } from '@/components/hr/travel/travel-enums';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';

/**
 * ⚠ A 404 here means "not yours", not "deleted" — the self-service surface does not distinguish.
 *
 * Only a Draft or a request returned for revision can be edited (lane 1, finding A1). An approved
 * trip offers **Request change** here (decision D-9): it comes back to the traveller, this form opens
 * on it, and it is approved again.
 */
export default function EditMyTravelRequestPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [changeOpen, setChangeOpen] = useState(false);

  const { data: r, isLoading, isError, error } = useQuery({
    queryKey: ['my-travel-request', id],
    queryFn: () => travelService.getMineById(id),
    retry: false,
  });

  const requestChange = useMutation({
    mutationFn: (reason: string) => travelService.requestChangeMine(id, reason),
    onSuccess: async () => {
      toast({ title: 'Back with you to change', description: 'Edit the trip below, then send it for approval again.' });
      await queryClient.invalidateQueries({ queryKey: ['my-travel-request', id] });
      await queryClient.invalidateQueries({ queryKey: ['my-travel-requests'] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not ask for the change', description: e.message }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }
  // A 404 is "not yours" and keeps its own wording; any other failure with nothing loaded says
  // the read failed.
  if (!r && isError && (error as { status?: number } | null)?.status !== 404) {
    return (
      <div className="p-6">
        <TravelQueryError error={error} what="this travel request" />
      </div>
    );
  }
  if (!r) {
    return (
      <div className="p-6">
        <EmptyState
          title="Not available"
          description="This travel request is not one of yours."
        />
      </div>
    );
  }

  const locked = r.status !== 'Draft' && r.status !== 'ReturnedForRevision';

  return (
    <div className="space-y-6">
      <PageHeader
        title={`Edit ${r.requestNumber}`}
        description={`${r.originCity} → ${r.destinationCity}`}
        backHref={`/me/travel/${id}`}
      />
      {locked ? (
        <EmptyState
          title="This request cannot be edited now"
          description={
            r.status === 'Submitted'
              ? 'It is out for approval. Recall it first, or ask your approver to return it to you for revision.'
              : r.status === 'Approved'
                ? 'An approved trip is changed by asking for a change: it comes back to you to edit, then goes for approval again.'
                : `A request that is ${enumLabel(TRAVEL_REQUEST_STATUS_LABELS, r.status).toLowerCase()} cannot be changed.`
          }
          action={
            r.status === 'Approved' ? (
              <Button onClick={() => setChangeOpen(true)}>
                <FilePenLine className="mr-2 h-4 w-4" /> Request change
              </Button>
            ) : undefined
          }
        />
      ) : (
        <TravelRequestForm surface="self" existing={r} />
      )}

      <TravelReasonDialog
        open={changeOpen}
        onOpenChange={setChangeOpen}
        title="Ask for a change to this approved trip"
        description="It comes back to you to edit here, then goes for approval again. Bookings and any advance stay with the trip."
        label="What has changed"
        placeholder="New dates, a different destination, a higher cost…"
        confirmLabel="Ask for the change"
        pending={requestChange.isPending}
        onConfirm={(reason) => requestChange.mutateAsync(reason)}
      />
    </div>
  );
}
