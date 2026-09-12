'use client';

import { PageHeader } from '@/components/hr/common/PageHeader';
import { TravelPolicyForm } from '@/components/hr/travel/TravelPolicyForm';

/**
 * Drafting a travel policy.
 *
 * The register shipped with a "Draft a policy" button pointing here and this route was never
 * built, so the control was a 404 to anyone who pressed it. Slice 10 removed the button rather
 * than leave a dead control; this is the route it was waiting for.
 */
export default function NewTravelPolicyPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Draft a travel policy"
        description="It caps nothing until a travel administrator approves it."
        backHref="/administration/hr/travel/policies"
      />
      <TravelPolicyForm />
    </div>
  );
}
