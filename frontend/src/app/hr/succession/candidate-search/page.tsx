'use client';

import { PageHeader } from '@/components/hr/common/PageHeader';
import { CandidateSearchPanel } from '@/components/hr/succession/CandidateSearchPanel';

/**
 * Finish-plan lane 4: the criteria candidate search, standalone. The same panel sits inside a
 * plan's Candidates tab pre-filled with that plan's post; here HR asks the open question — who in
 * the organisation could step up, for any post — before there is a plan to attach them to.
 */
export default function SuccessionCandidateSearchPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Find Succession Candidates"
        description="Search the organisation by target post, age band, service left, performance and competency. Scored against the post's requirements when one is chosen."
      />
      <CandidateSearchPanel />
    </div>
  );
}
