'use client';

import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { interviewConfirmationService } from '@/services/hr/interview-confirmation.service';
import {
  InterviewConfirmationCard,
  InterviewConfirmationPending,
  formatInterviewDate,
} from '@/components/hr/recruitment/InterviewConfirmationCard';

/**
 * Where a panelist lands from "Confirm my assignment" in their panel-assignment email.
 *
 * ⚠ A TOP-LEVEL route with no shell, and not under `/careers` or `/external-portal`. A panelist is
 * neither a job candidate nor a portal user: they are an internal manager, or an EXTERNAL expert
 * who has no account in the system at all. Wrapping this in the careers header would greet a
 * director with "Create an account", and putting it behind the portal's AuthGuard would shut the
 * external panelist out of the one thing they were asked to do.
 *
 * ⚠ This page did not exist until 2026-09-22 — the email had pointed at
 * `/interviews/confirm-panelist/{token}` from the start and nothing was ever built there, so the
 * button 404'd in every panel assignment. The route is kept exactly as the email already spells
 * it, so no template had to change.
 */
export default function ConfirmPanelistAssignmentPage() {
  const params = useParams();
  const token = (params?.token as string) ?? '';

  const confirmation = useQuery({
    queryKey: ['interviews', 'confirm-panelist', token],
    queryFn: () => interviewConfirmationService.confirmPanelistAssignment(token),
    enabled: !!token,
    // ⚠ The endpoint spends the token — see the service. One call per token, never a retry.
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    refetchOnMount: false,
    staleTime: Infinity,
    gcTime: Infinity,
  });

  if (confirmation.isPending) {
    return (
      <div className="min-h-screen bg-background px-4">
        <InterviewConfirmationPending message="Confirming your assignment…" />
      </div>
    );
  }

  if (confirmation.isError) {
    return (
      <div className="min-h-screen bg-background px-4">
        <InterviewConfirmationCard
          tone="error"
          heading="This link could not be used"
          message={
            (confirmation.error as Error)?.message ??
            'This confirmation link is invalid or has expired.'
          }
          footer={
            <p className="text-center text-sm text-muted-foreground">
              If the interview is still ahead, reply to the assignment email and the recruitment
              team will confirm you by hand.
            </p>
          }
        />
      </div>
    );
  }

  const result = confirmation.data;

  return (
    <div className="min-h-screen bg-background px-4">
      <InterviewConfirmationCard
        tone={result.alreadyConfirmed ? 'already' : 'success'}
        heading={result.alreadyConfirmed ? 'Already confirmed' : 'Assignment confirmed'}
        message={
          result.alreadyConfirmed
            ? 'You had already confirmed this panel assignment. There is nothing more to do.'
            : 'Thank you. The recruitment team has been told you will sit on this panel.'
        }
        details={[
          { label: 'Panelist', value: result.panelistName },
          { label: 'Role', value: result.jobTitle },
          { label: 'Date', value: formatInterviewDate(result.scheduledDate) ?? '' },
          { label: 'Interview', value: result.interviewNumber },
        ]}
        footer={
          <p className="text-center text-sm text-muted-foreground">
            The time, format and location are in your assignment email. If the session is moved you
            will be written to again.
          </p>
        }
      />
    </div>
  );
}
