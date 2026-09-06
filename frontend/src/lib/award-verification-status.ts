export type AwardBidderReviewStatus = 'pending' | 'verified' | 'failed';

// The API persists Passed; older clients used Verified for the same outcome.
// Unknown or incomplete states must never be treated as a successful review.
export function getAwardBidderReviewStatus(status?: string): AwardBidderReviewStatus {
  if (status === 'Passed' || status === 'Verified') return 'verified';
  return status === 'Failed' ? 'failed' : 'pending';
}

export function isAwardVerificationClosed(status?: string): boolean {
  return status === 'Completed' || status === 'Cancelled';
}
