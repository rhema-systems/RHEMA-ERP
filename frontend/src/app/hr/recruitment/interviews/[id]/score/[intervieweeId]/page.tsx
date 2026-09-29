'use client';

import { useParams, useSearchParams } from 'next/navigation';
import { InterviewScorecardForm } from '@/components/hr/recruitment/InterviewScorecardForm';

/**
 * HR's route into a scorecard, reached from the interview's Candidates tab.
 *
 * The form itself lives in `InterviewScorecardForm` and is shared with the panelist's own route at
 * `/me/panel/[interviewId]/score/[intervieweeId]`. ⚠ One form, two routes — never two forms. A
 * second implementation against the same upsert endpoint is how a scorecard gets silently replaced,
 * which is why the portal route was withheld until the form could be shared.
 *
 * `?panelistId=` names the seat, because HR opening this screen is usually filing on somebody's
 * behalf and the server records it as such.
 */
export default function InterviewScorecardPage() {
  const params = useParams();
  const searchParams = useSearchParams();

  const interviewId = params.id as string;
  const intervieweeId = params.intervieweeId as string;

  return (
    <InterviewScorecardForm
      interviewId={interviewId}
      intervieweeId={intervieweeId}
      panelistIdFromQuery={searchParams.get('panelistId')}
      backHref={`/hr/recruitment/interviews/${interviewId}`}
    />
  );
}
