export type SupplierOnboardingFeeMode = 'Free' | 'Paid';
export type SupplierOnboardingTokenStatus =
  | 'AwaitingPayment'
  | 'Active'
  | 'Expired';
export type SupplierOnboardingPaymentStatus =
  | 'NotRequired'
  | 'Pending'
  | 'Posted'
  | 'Reconciled'
  | 'Exempt'
  | 'Failed';
export type SupplierOnboardingExemptionStatus =
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected';

export interface SupplierOnboardingTokenSearch {
  search?: string;
  status?: SupplierOnboardingTokenStatus;
  paymentStatus?: SupplierOnboardingPaymentStatus;
  page?: number;
  pageSize?: number;
}

export interface SupplierOnboardingTokenSummary {
  totalCount: number;
  awaitingPaymentCount: number;
  activeCount: number;
  expiredCount: number;
  pendingReconciliationCount: number;
  postedAmount: number;
}

export interface SupplierOnboardingTokenListItem {
  id: string;
  registrationId: string;
  registrationNumber: string;
  applicantName: string;
  tokenReference: string;
  maskedToken: string;
  generation: number;
  status: SupplierOnboardingTokenStatus;
  paymentStatus: SupplierOnboardingPaymentStatus;
  feeMode: SupplierOnboardingFeeMode;
  feeAmount: number;
  taxAmount: number;
  totalAmount: number;
  currencyCode: string;
  issuedAtUtc: string;
  activatedAtUtc?: string;
  expiredAtUtc?: string;
  sourceConfigurationProfileCode: string;
  sourceConfigurationProfileVersion: number;
  rowVersion: string;
}

export interface SupplierOnboardingPayment {
  id: string;
  paymentMethodId: string;
  paymentMethodCode: string;
  paymentMethodName: string;
  paymentReference?: string;
  feeAmount: number;
  taxAmount: number;
  totalAmount: number;
  currencyCode: string;
  status: SupplierOnboardingPaymentStatus;
  paidAtUtc: string;
  postedAtUtc?: string;
  postingEventId?: string;
  journalEntryId?: string;
  receiptNumber?: string;
  receiptIssuedAtUtc?: string;
  reconciledAtUtc?: string;
  reconciliationReference?: string;
  reconciliationNotes?: string;
  failureReason?: string;
  rowVersion: string;
  integrityHash: string;
}

export interface SupplierOnboardingEvidenceReference {
  referenceKind: 'Document' | 'ExternalReference' | 'ControlEvent';
  reference: string;
  label?: string;
  requirementKey?: string;
}

export interface SupplierOnboardingExemption {
  id: string;
  reason: string;
  status: SupplierOnboardingExemptionStatus;
  workflowDefinitionId: string;
  workflowInstanceId?: string;
  requestedById: string;
  requestedAtUtc: string;
  decidedById?: string;
  decidedAtUtc?: string;
  decisionComment?: string;
  evidence: SupplierOnboardingEvidenceReference[];
  rowVersion: string;
  integrityHash: string;
}

export interface SupplierOnboardingToken extends SupplierOnboardingTokenListItem {
  feeType: string;
  taxPercent: number;
  paymentChannels: string[];
  receiptNumberFormat: string;
  exemptionRule: string;
  refundRule: string;
  renewalRule: string;
  sourceConfigurationProfileId: string;
  sourceConfigurationDecisionId: string;
  revenueAccountId?: string;
  taxAccountId?: string;
  exemptionWorkflowDefinitionId?: string;
  decisionSnapshotHash: string;
  integrityHash: string;
  expiryReason?: string;
  reissueReason?: string;
  reissuedAtUtc?: string;
  payments: SupplierOnboardingPayment[];
  exemptions: SupplierOnboardingExemption[];
}

export interface SupplierOnboardingTokenPage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: SupplierOnboardingTokenListItem[];
}

export interface SupplierOnboardingTokenIssueResult {
  token: SupplierOnboardingToken;
  plaintextToken?: string;
}

export interface SupplierOnboardingPaymentMethodOption {
  id: string;
  code: string;
  name: string;
  requiresReference: boolean;
  isPostingReady: boolean;
}

export interface RecordSupplierOnboardingPayment {
  paymentMethodId: string;
  paymentReference?: string;
  rowVersion: string;
}

export interface SupplierOnboardingExemptionRequest {
  reason: string;
  evidence: SupplierOnboardingEvidenceReference[];
  rowVersion: string;
}

export interface SupplierOnboardingExemptionDecision {
  approve: boolean;
  comment: string;
  evidence: SupplierOnboardingEvidenceReference[];
  rowVersion: string;
}
