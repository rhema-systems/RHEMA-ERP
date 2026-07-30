import type { ProcurementControlEvidenceReferenceKind } from './procurement-control-event';

export type SupplierDueDiligenceReviewType = 'Initial' | 'Annual';
export type SupplierDueDiligenceStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Expired'
  | 'Superseded';
export type SupplierDueDiligenceOutcome = 'Pending' | 'Clear' | 'Adverse';
export type SupplierDueDiligenceCheckType =
  | 'PpaDebarment'
  | 'GraTaxClearance'
  | 'Sanctions'
  | 'BankVerification'
  | 'FinancialStability'
  | 'Reputation';
export type SupplierDueDiligenceCheckStatus =
  | 'Pending'
  | 'Clear'
  | 'Adverse'
  | 'NotApplicable';

export interface SupplierDueDiligenceSearch {
  search?: string;
  businessPartnerId?: string;
  status?: SupplierDueDiligenceStatus;
  reviewType?: SupplierDueDiligenceReviewType;
  dueOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export interface SupplierDueDiligenceSummary {
  totalReviews: number;
  draftCount: number;
  pendingApprovalCount: number;
  currentApprovedCount: number;
  dueOrExpiredCount: number;
  adverseCount: number;
  suppliersWithoutCurrentReview: number;
  policyAvailable: boolean;
  policyProfileCode?: string;
  policyProfileVersion?: number;
  reviewFrequencyMonths?: number;
  policyReleaseGate?: string;
}

export interface SupplierDueDiligenceListItem {
  id: string;
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  reviewReference: string;
  cycleNumber: number;
  reviewType: SupplierDueDiligenceReviewType;
  status: SupplierDueDiligenceStatus;
  outcome: SupplierDueDiligenceOutcome;
  reviewPeriodStartUtc: string;
  reviewPeriodEndUtc: string;
  nextReviewDueAtUtc?: string;
  isCurrent: boolean;
  isDueOrExpired: boolean;
  policyProfileCode: string;
  policyProfileVersion: number;
  reviewFrequencyMonths: number;
  clearCheckCount: number;
  adverseCheckCount: number;
  evidenceCount: number;
  allowedActions: string[];
  rowVersion: string;
}

export interface SupplierDueDiligencePage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: SupplierDueDiligenceListItem[];
}

export interface SupplierDueDiligenceEvidence {
  id: string;
  referenceKind: ProcurementControlEvidenceReferenceKind;
  workflowEvidenceDocumentId?: string;
  fileUploadRecordId?: string;
  reference: string;
  label?: string;
  requirementKey: string;
  integrityHash: string;
}

export interface SupplierDueDiligenceCheck {
  id: string;
  checkType: SupplierDueDiligenceCheckType;
  status: SupplierDueDiligenceCheckStatus;
  sourceName: string;
  sourceReference: string;
  checkedAtUtc?: string;
  validUntilUtc?: string;
  reviewedById?: string;
  reviewerName?: string;
  notes?: string;
  integrityHash: string;
  evidence: SupplierDueDiligenceEvidence[];
}

export interface SupplierDueDiligence extends SupplierDueDiligenceListItem {
  policyDecisionId: string;
  policyProfileId: string;
  policyValueHash: string;
  policySnapshotJson: string;
  workflowDefinitionId: string;
  workflowInstanceId?: string;
  supersedesReviewId?: string;
  supersededByReviewId?: string;
  notes?: string;
  reviewComment?: string;
  submittedById?: string;
  submittedAtUtc?: string;
  approvedById?: string;
  approvedAtUtc?: string;
  rejectedById?: string;
  rejectedAtUtc?: string;
  expiredAtUtc?: string;
  integrityHash: string;
  checks: SupplierDueDiligenceCheck[];
}

export interface SupplierDueDiligenceEvidenceReference {
  referenceKind: ProcurementControlEvidenceReferenceKind;
  referenceId?: string;
  reference?: string;
  label?: string;
  requirementKey?: string;
}

export interface SaveSupplierDueDiligenceCheck {
  checkType: SupplierDueDiligenceCheckType;
  status: SupplierDueDiligenceCheckStatus;
  sourceName: string;
  sourceReference: string;
  checkedAtUtc?: string;
  validUntilUtc?: string;
  notes?: string;
  evidence: SupplierDueDiligenceEvidenceReference[];
}

export interface CreateSupplierDueDiligence {
  businessPartnerId: string;
  reviewType: SupplierDueDiligenceReviewType;
  workflowDefinitionId: string;
  notes?: string;
}

export interface UpdateSupplierDueDiligence {
  rowVersion: string;
  notes?: string;
  checks: SaveSupplierDueDiligenceCheck[];
}

export interface SupplierDueDiligenceLifecycleRequest {
  rowVersion: string;
  comment: string;
  evidence: SupplierDueDiligenceEvidenceReference[];
}

export interface SupplierDueDiligenceWorkflowOption {
  id: string;
  name: string;
  version: number;
}

export interface SupplierDueDiligenceSupplierOption {
  id: string;
  code: string;
  name: string;
  hasCurrentReview: boolean;
  nextReviewDueAtUtc?: string;
}

export interface SupplierDueDiligenceCurrentState {
  businessPartnerId: string;
  policyAvailable: boolean;
  hasApprovedReview: boolean;
  isCurrent: boolean;
  isClear: boolean;
  code: string;
  message: string;
  reviewId?: string;
  reviewReference?: string;
  reviewPeriodEndUtc?: string;
  earliestCheckExpiryUtc?: string;
  integrityHash?: string;
  policyDecisionId?: string;
  policyValueHash?: string;
  checks: SupplierDueDiligenceCheck[];
}
