'use client';

import { use } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { TravelRequestForm } from '@/components/hr/travel/TravelRequestForm';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { TRAVEL_REQUEST_STATUS_LABELS, enumLabel } from '@/components/hr/travel/travel-enums';
import { travelService } from '@/services/hr/travel.service';

/**
 * The server refuses an edit once the request is Approved, Completed, Cancelled or Closed. That
 * check is repeated here only to explain the refusal before the user retypes the whole form — the
 * server remains the authority.
 *
 * ⚠ It used to say "raise an amendment instead". There is no amendment: `ParentRequestId` has no
 * writer and nothing creates a child request (travel final closure, finding O-10). Lane 1 gives an
 * approved trip a "Request change" that sends it back for re-approval; until then the honest
 * answer is to cancel and raise a new request.
 */
export default function EditTravelRequestPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);

  const { data: r, isLoading, isError, error } = useQuery({
    queryKey: ['travel-request', id],
    queryFn: () => travelService.getById(id),
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

  const locked = ['Approved', 'Completed', 'Cancelled', 'Closed'].includes(r.status);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`Edit ${r.requestNumber}`}
        description={`${r.employeeName} · ${r.originCity} → ${r.destinationCity}`}
        backHref={`/hr/travel/${id}`}
      />
      {locked ? (
        <EmptyState
          title="This request can no longer be edited"
          description={
            r.status === 'Approved'
              ? 'An approved trip cannot be changed, and there is no way to amend one yet. If the trip has changed, cancel it and raise a new request.'
              : `A request that is ${enumLabel(TRAVEL_REQUEST_STATUS_LABELS, r.status).toLowerCase()} cannot be changed.`
          }
        />
      ) : (
        <TravelRequestForm surface="desk" existing={r} />
      )}
    </div>
  );
}
