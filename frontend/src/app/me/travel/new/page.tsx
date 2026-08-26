'use client';

import { PageHeader } from '@/components/hr/common/PageHeader';
import { TravelRequestForm } from '@/components/hr/travel/TravelRequestForm';

/**
 * Requesting your own travel. There is no traveller field: this posts to `api/staff-travel/me`,
 * which takes no employee id anywhere and stamps you from the token.
 */
export default function NewMyTravelRequestPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Request travel"
        description="Saved as a draft. Submit it when you are ready for approval."
        backHref="/me/travel"
      />
      <TravelRequestForm surface="self" />
    </div>
  );
}
