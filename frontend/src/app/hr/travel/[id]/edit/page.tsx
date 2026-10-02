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
 * The server accepts an edit only while the request is a Draft or has been returned for revision
 * (travel final closure, lane 1 — finding A1: it used to accept one while the request was out for
 * approval). That rule is repeated here only to explain the refusal before the user retypes the
 * whole form — the server remains the authority.
 *
 * ⚠ It used to say "raise an amendment instead". There is no amendment: `ParentRequestId` has no
 * writer and nothing creates a child request (finding O-10). Slice 1b gives an approved trip a
 * "Request change" that sends it back for re-approval.
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
                ? 'An approved trip cannot be edited. Use Request change on the trip to send it back for re-approval.'
                : `A request that is ${enumLabel(TRAVEL_REQUEST_STATUS_LABELS, r.status).toLowerCase()} cannot be changed.`
          }
        />
      ) : (
        <TravelRequestForm surface="desk" existing={r} />
      )}
    </div>
  );
}
