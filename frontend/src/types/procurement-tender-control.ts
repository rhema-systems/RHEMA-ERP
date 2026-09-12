export enum ProcurementTenderControlStatus {
  Advertised = 'Advertised',
  Opened = 'Opened',
  TechnicalEvaluated = 'TechnicalEvaluated',
  FinancialEvaluated = 'FinancialEvaluated',
  PendingApproval = 'PendingApproval',
  Approved = 'Approved',
  Rejected = 'Rejected',
  Awarded = 'Awarded',
  Contracted = 'Contracted',
  Accepted = 'Accepted',
}

export enum ProcurementTenderSubmissionDisposition {
  OnTimeAccepted = 'OnTimeAccepted',
  LateRejected = 'LateRejected',
}

export enum ProcurementMethodType {
  RequestForQuotation = 'RequestForQuotation',
  NationalCompetitiveTendering = 'NationalCompetitiveTendering',
  InternationalCompetitiveTendering = 'InternationalCompetitiveTendering',
  RestrictedTendering = 'RestrictedTendering',
  SingleSource = 'SingleSource',
  PettyPurchase = 'PettyPurchase',
  FrameworkCallOff = 'FrameworkCallOff',
  QualityBasedSelection = 'QualityBasedSelection',
  QualityAndCostBasedSelection = 'QualityAndCostBasedSelection',
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
  approvalRequired?: boolean;
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  method: ProcurementMethodType;
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
