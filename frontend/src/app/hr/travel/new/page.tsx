'use client';

import { PageHeader } from '@/components/hr/common/PageHeader';
import { TravelRequestForm } from '@/components/hr/travel/TravelRequestForm';

export default function NewTravelRequestPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Raise a travel request"
        description="The request is created as a draft — submit it separately once the details are settled."
        backHref="/hr/travel"
      />
      <TravelRequestForm surface="desk" />
    </div>
  );
}
