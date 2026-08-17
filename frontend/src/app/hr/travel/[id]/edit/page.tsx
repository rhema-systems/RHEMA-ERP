'use client';

import { use } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { TravelRequestForm } from '@/components/hr/travel/TravelRequestForm';
import { travelService } from '@/services/hr/travel.service';

/**
 * The server refuses an edit once the request is Approved, Completed, Cancelled or Closed. That
 * check is repeated here only to explain the refusal before the user retypes the whole form — the
 * server remains the authority.
 */
export default function EditTravelRequestPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);

  const { data: r, isLoading } = useQuery({
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
          description={`A request in status "${r.status}" is settled. Raise an amendment instead.`}
        />
      ) : (
        <TravelRequestForm surface="desk" existing={r} />
      )}
    </div>
  );
}
