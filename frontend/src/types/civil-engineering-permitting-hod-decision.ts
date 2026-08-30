import type { CivilEngineeringPermittingOutcome } from './civil-engineering-permitting-engineering-review';

export type CivilEngineeringPermittingHodDecisionOutcome = 'Approve' | 'Reject' | 'ReturnForCorrection';

export type CivilEngineeringPermittingHodDecisionQueueItem = {
  engineeringReviewId: string;
  developmentApprovalFileId: string;
  fileNumber: string;
  applicationReference: string;
  applicantName: string;
  projectLabel: string;
  reviewerName: string;
  commentCategoryLabel: string;
  reviewComment: string;
  recommendedOutcome: CivilEngineeringPermittingOutcome;
  documentReference?: string | null;
  reviewedAt: string;
  handoffDueDate: string;
  rowVersion: string;
};

export type DecideCivilEngineeringPermittingReviewRequest = { clientRequestId: string; outcome: CivilEngineeringPermittingHodDecisionOutcome; reason?: string | null };
