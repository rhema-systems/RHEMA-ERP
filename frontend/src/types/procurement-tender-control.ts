export enum ProcurementTenderControlStatus {
  Advertised = 0,
  Opened = 1,
  TechnicalEvaluated = 2,
  FinancialEvaluated = 3,
  PendingApproval = 4,
  Approved = 5,
  Rejected = 6,
  Awarded = 7,
  Contracted = 8,
  Accepted = 9,
}

export enum ProcurementTenderSubmissionDisposition {
  OnTimeAccepted = 0,
  LateRejected = 1,
}

export interface ProcurementTenderDocumentIssue {
  id: string;
  businessPartnerId?: string;
  recipientName: string;
  amountPaid: number;
  paymentReference?: string;
  issueReceiptNumber: string;
  issuedAtUtc: string;
  evidenceReference: string;
  integrityHash: string;
}

export interface ProcurementTenderSubmissionReceipt {
  id: string;
  tenderBidId: string;
  businessPartnerId: string;
  businessPartnerName: string;
  receiptNumber: string;
  receivedAtUtc: string;
  disposition: ProcurementTenderSubmissionDisposition;
  openedAtUtc?: string;
  bidAmount: number;
  currency: string;
  integrityHash: string;
}

export interface ProcurementTenderTechnicalResult {
  bidId: string;
  score: number;
  qualified: boolean;
}

export interface ProcurementTenderMilestone {
  code: string;
  label: string;
  completedAtUtc?: string;
  reference?: string;
}

export interface ProcurementTenderControl {
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  method: number;
  methodRuleCode: string;
  authorityRouteReference: string;
  ppaApprovalRequired: boolean;
  status: ProcurementTenderControlStatus;
  advertisementReference: string;
  publicationChannel: string;
  tenderDocumentReference: string;
  tenderDocumentVersion: string;
  documentFee: number;
  advertisementEvidenceReference: string;
  advertisedAtUtc: string;
  submissionDeadlineUtc: string;
  openingScheduledAtUtc: string;
  openedAtUtc?: string;
  technicalEvaluatedAtUtc?: string;
  financialEvaluatedAtUtc?: string;
  minimumTechnicalScore: number;
  technicalWeight: number;
  financialWeight: number;
  recommendedBidId?: string;
  workflowInstanceId?: string;
  authorityApprovalReference?: string;
  ppaApprovalReference?: string;
  awardBidId?: string;
  awardReference?: string;
  contractReference?: string;
  bidderAcceptanceReference?: string;
  integrityHash: string;
  rowVersion: string;
  documentIssues: ProcurementTenderDocumentIssue[];
  submissionReceipts: ProcurementTenderSubmissionReceipt[];
  technicalResults: ProcurementTenderTechnicalResult[];
  milestones: ProcurementTenderMilestone[];
}

export interface PublishProcurementTender {
  advertisementReference: string;
  publicationChannel: string;
  tenderDocumentReference: string;
  tenderDocumentVersion: string;
  documentFee: number;
  advertisementEvidenceReference: string;
  submissionDeadlineUtc: string;
  openingScheduledAtUtc: string;
}

export interface IssueProcurementTenderDocument {
  businessPartnerId?: string;
  recipientName: string;
  recipientEmail?: string;
  recipientPhone?: string;
  amountPaid: number;
  paymentReference?: string;
  issueReceiptNumber: string;
  evidenceReference: string;
}
