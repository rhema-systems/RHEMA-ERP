export function isQuoteSubmissionOpen(
  rfqStatus: string | undefined,
  submissionDeadline: string | undefined,
  now = new Date(),
): boolean {
  if (rfqStatus?.toLowerCase() !== 'sent' || !submissionDeadline) return false;

  const deadline = new Date(submissionDeadline);
  return Number.isFinite(deadline.getTime()) && deadline.getTime() > now.getTime();
}

export function getQuoteSubmitLabel(
  hasSubmittedQuote: boolean,
  submissionOpen: boolean,
): string {
  if (!submissionOpen) return 'Submission Closed';
  return hasSubmittedQuote ? 'Update & Resubmit' : 'Submit Quote';
}
