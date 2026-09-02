import type { CivilEngineeringDevelopmentApprovalDocument, CivilEngineeringDevelopmentApprovalLookupOption } from './civil-engineering-development-approval-file';

export type CivilEngineeringPermittingOutcome = 'RecommendApproval' | 'RecommendApprovalWithConditions' | 'ReturnForCorrection' | 'RecommendRejection';
export type CivilEngineeringPermittingEngineeringReviewStage = 'CorrectionRequested' | 'PendingHodDecision' | 'HodApproved' | 'HodRejected' | 'HodReturned';

export type CivilEngineeringPermittingEngineeringReviewLookups = {
  commentCategories: CivilEngineeringDevelopmentApprovalLookupOption[];
  documents: CivilEngineeringDevelopmentApprovalDocument[];
  allowedOutcomes: CivilEngineeringPermittingOutcome[];
};

export type SubmitCivilEngineeringPermittingEngineeringReviewRequest = {
  clientRequestId: string;
  commentCategoryId: string;
  reviewComment: string;
  recommendedOutcome: CivilEngineeringPermittingOutcome;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
};

export type CivilEngineeringPermittingEngineeringReview = {
  id: string;
  developmentApprovalFileId: string;
  sourceHandoffId: string;
  reviewerName: string;
  commentCategoryLabel: string;
  reviewComment: string;
  recommendedOutcome: CivilEngineeringPermittingOutcome;
  stage: CivilEngineeringPermittingEngineeringReviewStage;
  status: string;
  approvalStatus: string;
  documentReference?: string | null;
  workflowInstanceId?: string | null;
  reviewedAt: string;
  rowVersion: string;
};
