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
import { useTravelAccess } from '@/components/hr/travel/useTravelAccess';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';

/**
 * The server accepts an edit only while the request is a Draft or has been returned for revision
 * (travel final closure, lane 1 — finding A1: it used to accept one while the request was out for
 * approval). That rule is repeated here only to explain the refusal before the user retypes the
 * whole form — the server remains the authority.
 *
 * ⚠ It used to say "raise an amendment instead". There is no amendment: `ParentRequestId` has no
 * writer and nothing creates a child request (finding O-10). An approved trip now has **Request
 * change** (decision D-9): it goes back for revision, this form opens on it, and it is approved again.
 */
export default function EditTravelRequestPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const access = useTravelAccess();
  const [changeOpen, setChangeOpen] = useState(false);

  const { data: r, isLoading, isError, error } = useQuery({
    queryKey: ['travel-request', id],
    queryFn: () => travelService.getById(id),
  });

  const requestChange = useMutation({
    mutationFn: (reason: string) => travelService.requestChange(id, reason),
    onSuccess: async () => {
      toast({ title: 'Back for revision', description: 'Edit the trip below, then submit it for approval again.' });
      await queryClient.invalidateQueries({ queryKey: ['travel-request', id] });
      await queryClient.invalidateQueries({ queryKey: ['travel-requests'] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not send the trip back', description: e.message }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }
  if (isError && !r) {
    return (
      <div className="p-6">
        <TravelQueryError error={error} what="this travel request" />
      </div>
    );
  }
  if (!r) {
    return (
      <div className="p-6">
        <EmptyState title="Not found" description="This travel request does not exist." />
      </div>
    );
  }

  const locked = r.status !== 'Draft' && r.status !== 'ReturnedForRevision';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`Edit ${r.requestNumber}`}
        description={`${r.employeeName} · ${r.originCity} → ${r.destinationCity}`}
        backHref={`/hr/travel/${id}`}
      />
      {locked ? (
        <EmptyState
          title="This request cannot be edited now"
          description={
            r.status === 'Submitted'
              ? 'It is out for approval. Recall it, or ask the approver to return it for revision, and then edit it.'
              : r.status === 'Approved'
                ? 'An approved trip is changed by sending it back for revision; it is then approved again. Its bookings, advances and claims stay with it.'
                : `A request that is ${enumLabel(TRAVEL_REQUEST_STATUS_LABELS, r.status).toLowerCase()} cannot be changed.`
          }
          action={
            r.status === 'Approved' && access.canWrite ? (
              <Button onClick={() => setChangeOpen(true)}>
                <FilePenLine className="mr-2 h-4 w-4" /> Request change
              </Button>
            ) : undefined
          }
        />
      ) : (
        <TravelRequestForm surface="desk" existing={r} />
      )}

      <TravelReasonDialog
        open={changeOpen}
        onOpenChange={setChangeOpen}
        title="Request a change to this approved trip"
        description="The trip goes back for revision: edit it here, then submit it for approval again. Its bookings, advances and claims stay with it."
        label="What has changed"
        placeholder="New dates, a different destination, a higher cost…"
        confirmLabel="Send back for revision"
        pending={requestChange.isPending}
        onConfirm={(reason) => requestChange.mutateAsync(reason)}
      />
    </div>
  );
}
