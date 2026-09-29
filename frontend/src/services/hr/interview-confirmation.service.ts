// The two "confirm" links that interview emails carry.
//
// ⚠ ANONYMOUS ON PURPOSE, and deliberately NOT `apiService`. The recipient is reading an email:
// a candidate who is not signed in, or an external panelist who has no account at all. Routing
// these through apiService would attach a bearer token that does not exist and, on a 401, bounce
// them to a login page they can never satisfy.
//
// ⚠ No `X-Tenant-Id` either, unlike the public job board. Both endpoints look their row up BY
// TOKEN with no tenant predicate, which is what lets a link work from an email that knows nothing
// about tenants. Adding the header would not help and would imply a tenant the page cannot know.
//
// ⚠ Both endpoints are a GET that MUTATES — they confirm, then null the token so it cannot be
// reused. Two consequences the callers must respect:
//   1. The request must fire ONCE. React's StrictMode double-invokes effects in development, and
//      a second call finds the token already spent and reports "invalid or expired" over a
//      confirmation that had just succeeded. The pages use React Query with retry disabled so the
//      call is made once per token.
//   2. A mail scanner that pre-fetches links will spend the token before the human clicks. The
//      attendance is still recorded correctly — the scanner's GET is what records it — but the
//      person then sees the expired message. That is a property of the token design, not of these
//      pages, and it is why "already confirmed" is presented as a success rather than an error.

const API_BASE = process.env.NEXT_PUBLIC_API_URL || '/api';

/** What the server says after a candidate confirms they will attend. */
export interface InterviewAttendanceConfirmation {
  alreadyConfirmed: boolean;
  candidateName: string;
  jobTitle: string;
  /** ISO date only, e.g. "2026-07-20". */
  scheduledDate: string;
  interviewNumber: string;
}

/** What the server says after a panelist confirms their assignment. */
export interface PanelistAssignmentConfirmation {
  alreadyConfirmed: boolean;
  panelistName: string;
  jobTitle: string;
  scheduledDate: string;
  interviewNumber: string;
  isExternal: boolean;
}

async function confirmFetch<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, { method: 'GET' });
  const text = await response.text();

  let body: unknown = null;
  try {
    body = text ? JSON.parse(text) : null;
  } catch {
    body = null;
  }

  if (!response.ok) {
    const message =
      (body as { message?: string } | null)?.message ??
      (response.status === 429
        ? 'Too many attempts. Please wait a moment and try the link again.'
        : 'This confirmation link could not be used.');
    throw new Error(message);
  }

  return body as T;
}

export const interviewConfirmationService = {
  /** A candidate confirming they will attend their interview. */
  confirmAttendance(token: string): Promise<InterviewAttendanceConfirmation> {
    return confirmFetch<InterviewAttendanceConfirmation>(
      `/job-interviews/confirm-attendance/${encodeURIComponent(token)}`,
    );
  },

  /** A panelist — internal or external — confirming they will sit on the panel. */
  confirmPanelistAssignment(token: string): Promise<PanelistAssignmentConfirmation> {
    return confirmFetch<PanelistAssignmentConfirmation>(
      `/job-interviews/confirm-panelist/${encodeURIComponent(token)}`,
    );
  },
};
