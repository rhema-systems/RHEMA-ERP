'use client';

import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { interviewConfirmationService } from '@/services/hr/interview-confirmation.service';
import {
  InterviewConfirmationCard,
  InterviewConfirmationPending,
  formatInterviewDate,
} from '@/components/hr/recruitment/InterviewConfirmationCard';

/**
 * Where a candidate lands from "Confirm attendance" in their interview invitation.
 *
 * ⚠ ANONYMOUS, and that is the whole point — it sits under `/careers`, whose layout carries no
 * AuthGuard, rather than under `/external-portal`, which requires a login. Somebody reading an
 * invitation on their phone will not sign in first, and a confirm link that demands it is a
 * confirm link nobody uses.
 *
 * ⚠ This page did not exist until 2026-09-22. The email had pointed at
 * `/careers/portal/confirm-interview/{token}` since the feature was written, and there was no such
 * route — so "Confirm attendance" was a 404 in every invitation and every reschedule the system
 * had ever sent, while the API half sat live and anonymous the entire time.
 */
export default function ConfirmInterviewAttendancePage() {
  const params = useParams();
  const token = (params?.token as string) ?? '';

  const confirmation = useQuery({
    queryKey: ['careers', 'confirm-attendance', token],
    queryFn: () => interviewConfirmationService.confirmAttendance(token),
    enabled: !!token,
    // ⚠ The endpoint spends the token. Retrying turns one success into a failure report, and
    // refetching on focus would do the same the moment the reader switches back to the tab.
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    refetchOnMount: false,
    staleTime: Infinity,
    gcTime: Infinity,
  });

  if (confirmation.isPending) {
    return <InterviewConfirmationPending message="Confirming your attendance…" />;
  }

  if (confirmation.isError) {
    return (
      <InterviewConfirmationCard
        tone="error"
        heading="This link could not be used"
        message={
          (confirmation.error as Error)?.message ??
          'This confirmation link is invalid or has expired.'
        }
        footer={
          <p className="text-center text-sm text-muted-foreground">
            If your interview is still ahead of you, reply to the invitation email and the
            recruitment team will confirm you by hand.
          </p>
        }
      />
    );
  }

  const result = confirmation.data;

  return (
    <InterviewConfirmationCard
      tone={result.alreadyConfirmed ? 'already' : 'success'}
      heading={result.alreadyConfirmed ? 'Already confirmed' : 'Attendance confirmed'}
      message={
        result.alreadyConfirmed
          ? 'You had already confirmed this interview. There is nothing more to do.'
          : 'Thank you. The recruitment team has been told you will attend.'
      }
      details={[
        { label: 'Candidate', value: result.candidateName },
        { label: 'Role', value: result.jobTitle },
        { label: 'Date', value: formatInterviewDate(result.scheduledDate) ?? '' },
        { label: 'Interview', value: result.interviewNumber },
      ]}
      footer={
        <div className="flex flex-col gap-2 text-center">
          <p className="text-sm text-muted-foreground">
            The time and place are in your invitation email. If anything changes we will write to
            you again.
          </p>
          <Button asChild variant="outline">
            <Link href="/careers">Browse other openings</Link>
          </Button>
        </div>
      }
    />
  );
}
