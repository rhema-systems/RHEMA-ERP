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

/** ⚠ A 404 here means "not yours", not "deleted" — the self-service surface does not distinguish. */
export default function EditMyTravelRequestPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);

  const { data: r, isLoading, isError, error } = useQuery({
    queryKey: ['my-travel-request', id],
    queryFn: () => travelService.getMineById(id),
    retry: false,
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

  const locked = ['Approved', 'Completed', 'Cancelled', 'Closed'].includes(r.status);

  return (
    <div className="space-y-6">
      <PageHeader
        title={`Edit ${r.requestNumber}`}
        description={`${r.originCity} → ${r.destinationCity}`}
        backHref={`/me/travel/${id}`}
      />
      {locked ? (
        <EmptyState
          title="This request can no longer be edited"
          description={
            r.status === 'Approved'
              ? 'An approved trip cannot be changed here. If it has changed, withdraw it and raise a new one, or ask the travel desk.'
              : `A request that is ${enumLabel(TRAVEL_REQUEST_STATUS_LABELS, r.status).toLowerCase()} cannot be changed.`
          }
        />
      ) : (
        <TravelRequestForm surface="self" existing={r} />
      )}
    </div>
  );
}
