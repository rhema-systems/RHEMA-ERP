import type { ProcurementControlEvidenceReferenceKind } from './procurement-control-event';
import type { ProcurementMethodType } from './procurement-policy';

export type ProcurementTenderDocumentTemplateStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Published'
  | 'Retired';

export type ProcurementTenderDocumentSourceType =
  | 'Tender'
  | 'RequestForQuotation';

export type ProcurementTenderDocumentFeeMode = 'Free' | 'Paid';

export type ProcurementTenderDocumentChangeType =
  | 'Addendum'
  | 'SubmissionDeadlineExtension'
  | 'BidValidityExtension';

export type ProcurementTenderDocumentChangeStatus =
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected';

export type ProcurementTenderDocumentRecipientSourceType =
  | 'Issuance'
  | 'TenderBid'
  | 'RequestForQuotationQuote';

export type ProcurementTenderDocumentAcknowledgementOutcome =
  | 'Acknowledged'
  | 'Declined';

export type ProcurementTenderDocumentAllowedAction = string;

export interface ProcurementTenderDocumentSearch {
  search?: string;
  status?: ProcurementTenderDocumentTemplateStatus;
  method?: ProcurementMethodType;
  effectiveAtUtc?: string;
  page?: number;
  pageSize?: number;
}

export interface ProcurementTenderDocumentTemplateSummary {
  templateFamilyCount: number;
  draftCount: number;
  pendingApprovalCount: number;
  publishedCount: number;
  effectiveCount: number;
  retiredCount: number;
}

export interface ProcurementTenderDocumentTemplateListItem {
  id: string;
  templateKey: string;
  templateCode: string;
  name: string;
  documentTypeCode: string;
  version: number;
  status: ProcurementTenderDocumentTemplateStatus;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  isEffective: boolean;
  policySetCode: string;
  policySetVersion: number;
  applicableMethods: ProcurementMethodType[];
  allowedActions: ProcurementTenderDocumentAllowedAction[];
  rowVersion: string;
}

export interface ProcurementTenderDocumentTemplatePage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: ProcurementTenderDocumentTemplateListItem[];
}

export interface ProcurementTenderDocumentTemplate
  extends ProcurementTenderDocumentTemplateListItem {
  description?: string;
  policySetId: string;
  sourceConfigurationProfileId: string;
  contentReference: string;
  contentWorkflowEvidenceDocumentId?: string;
  contentFileUploadRecordId?: string;
  contentChecksumSha256: string;
  workflowDefinitionId: string;
  workflowInstanceId?: string;
  supersedesVersionId?: string;
  changeSummary?: string;
  approvalEvidenceReference?: string;
  reviewComment?: string;
  submittedAtUtc?: string;
  submittedById?: string;
  submittedByName?: string;
  publishedAtUtc?: string;
  publishedById?: string;
  publishedByName?: string;
  retiredAtUtc?: string;
  retiredById?: string;
  retiredByName?: string;
  integrityHash: string;
  blockedReasons: string[];
}

export interface SaveProcurementTenderDocumentTemplate {
  templateCode: string;
  name: string;
  description?: string;
  documentTypeCode: string;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  policySetId: string;
  policySetCode: string;
  policySetVersion: number;
  sourceConfigurationProfileId: string;
  contentReference: string;
  contentWorkflowEvidenceDocumentId?: string;
  contentFileUploadRecordId?: string;
  contentChecksumSha256: string;
  workflowDefinitionId: string;
  applicableMethods: ProcurementMethodType[];
  changeSummary?: string;
  rowVersion?: string;
}

export interface ProcurementTenderDocumentEvidenceReference {
  referenceKind: ProcurementControlEvidenceReferenceKind;
  referenceId?: string;
  reference?: string;
  label?: string;
  requirementKey?: string;
}

export interface ProcurementTenderDocumentLifecycleRequest {
  rowVersion: string;
  comment?: string;
  evidence: ProcurementTenderDocumentEvidenceReference[];
}

export interface CloneProcurementTenderDocumentTemplateRequest {
  rowVersion: string;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  changeSummary: string;
}

export interface ProcurementTenderDocumentWorkflowOption {
  id: string;
  name: string;
  version: number;
  lifecycleStatus: string;
}

export interface ProcurementTenderDocumentPolicyOption {
  id: string;
  code: string;
  name: string;
  version: number;
  sourceConfigurationProfileId: string;
  sourceConfigurationProfileCode: string;
}

export interface AttachProcurementTenderDocumentTemplateContentRequest {
  contentWorkflowEvidenceDocumentId: string;
  rowVersion: string;
}

export interface ProcurementTenderDocumentContentArtifactOption {
  id: string;
  documentName?: string;
  documentType?: string;
  fileName: string;
  filePath: string;
  sha256: string;
  version: number;
  isCurrent: boolean;
  verificationStatus: number;
  malwareScanStatus: number;
  workflowInstanceId: string;
  workflowName: string;
  entityType: string;
  entityId: string;
  stepName: string;
}

export interface ProcurementTenderDocumentReadiness {
  sourceType: ProcurementTenderDocumentSourceType;
  sourceId: string;
  sourceReference: string;
  sourcingCaseId?: string;
  method?: ProcurementMethodType;
  methodRuleId?: string;
  methodRuleCode?: string;
  hasRegister: boolean;
  registerId?: string;
  effectiveTemplateVersionId?: string;
  effectiveTemplateReference?: string;
  effectiveSubmissionDeadlineUtc?: string;
  effectiveBidValidityUntilUtc?: string;
  feeMode?: ProcurementTenderDocumentFeeMode;
  feeAmount?: number;
  currencyCode?: string;
  issuanceCount: number;
  pendingAcknowledgementCount: number;
  pendingChangeCount: number;
  ready: boolean;
  blockedReasons: string[];
  allowedActions: ProcurementTenderDocumentAllowedAction[];
  rowVersion?: string;
}

export interface ProcurementTenderDocumentAcknowledgement {
  id: string;
  correlationId: string;
  issuanceId?: string;
  changeRecipientId?: string;
  businessPartnerId?: string;
  outcome: ProcurementTenderDocumentAcknowledgementOutcome;
  acknowledgedAtUtc: string;
  acknowledgedByUserId: string;
  acknowledgementChannel: string;
  acknowledgementReference: string;
  evidenceReference: string;
  integrityHash: string;
}

export interface ProcurementTenderDocumentIssuance {
  id: string;
  correlationId: string;
  registerId: string;
  templateVersionId: string;
  templateReference: string;
  businessPartnerId?: string;
  recipientKey: string;
  recipientName: string;
  recipientEmail?: string;
  recipientPhone?: string;
  feeMode: ProcurementTenderDocumentFeeMode;
  feeAmount: number;
  currencyCode: string;
  amountPaid: number;
  paymentReference?: string;
  receiptNumber: string;
  issueChannel: string;
  issuedAtUtc: string;
  issuedByUserId: string;
  evidenceReference: string;
  integrityHash: string;
  acknowledgement?: ProcurementTenderDocumentAcknowledgement;
}

export interface ProcurementTenderDocumentChangeRecipient {
  id: string;
  sourceType: ProcurementTenderDocumentRecipientSourceType;
  issuanceId?: string;
  tenderBidId?: string;
  requestForQuotationQuoteId?: string;
  businessPartnerId?: string;
  recipientKey: string;
  recipientName: string;
  recipientEmail?: string;
  recipientPhone?: string;
  dispatchChannel: string;
  dispatchReference: string;
  dispatchedAtUtc: string;
  dispatchEvidenceReference: string;
  integrityHash: string;
  acknowledgement?: ProcurementTenderDocumentAcknowledgement;
}

export interface ProcurementTenderDocumentChange {
  id: string;
  correlationId: string;
  registerId: string;
  sequence: number;
  changeType: ProcurementTenderDocumentChangeType;
  status: ProcurementTenderDocumentChangeStatus;
  previousTemplateVersionId?: string;
  newTemplateVersionId?: string;
  previousValueUtc?: string;
  newValueUtc?: string;
  requiresAcknowledgement: boolean;
  reason: string;
  workflowDefinitionId: string;
  workflowInstanceId?: string;
  workflowOutcome?: string;
  approvalReference?: string;
  evidenceReference: string;
  requestedAtUtc: string;
  requestedByUserId: string;
  decidedAtUtc?: string;
  decidedByUserId?: string;
  dispatchedAtUtc?: string;
  dispatchedByUserId?: string;
  dispatchEvidenceReference?: string;
  integrityHash: string;
  recipients: ProcurementTenderDocumentChangeRecipient[];
  allowedActions: ProcurementTenderDocumentAllowedAction[];
  blockedReasons: string[];
  rowVersion: string;
}

export interface ProcurementTenderDocumentRegister {
  id: string;
  correlationId: string;
  sourceType: ProcurementTenderDocumentSourceType;
  sourceId: string;
  tenderId?: string;
  requestForQuotationId?: string;
  sourceReference: string;
  sourcingCaseId: string;
  methodRuleId: string;
  method: ProcurementMethodType;
  methodRuleCode: string;
  policySetId: string;
  policySetCode: string;
  policySetVersion: number;
  sourceConfigurationProfileId: string;
  initialTemplateVersionId: string;
  effectiveTemplateVersionId: string;
  effectiveTemplateReference: string;
  originalSubmissionDeadlineUtc: string;
  effectiveSubmissionDeadlineUtc: string;
  openingScheduledAtUtc?: string;
  originalBidValidityUntilUtc: string;
  effectiveBidValidityUntilUtc: string;
  feeMode: ProcurementTenderDocumentFeeMode;
  feeAmount: number;
  currencyCode: string;
  boundAtUtc: string;
  boundByUserId: string;
  integrityHash: string;
  issuances: ProcurementTenderDocumentIssuance[];
  changes: ProcurementTenderDocumentChange[];
  blockedReasons: string[];
  allowedActions: ProcurementTenderDocumentAllowedAction[];
  rowVersion: string;
}

export interface BindProcurementTenderDocumentRequest {
  sourceType: ProcurementTenderDocumentSourceType;
  sourceId: string;
  templateVersionId: string;
  submissionDeadlineUtc: string;
  openingScheduledAtUtc?: string;
  bidValidityUntilUtc: string;
  feeMode: ProcurementTenderDocumentFeeMode;
  feeAmount: number;
  currencyCode: string;
}

export interface IssueProcurementTenderDocumentRegisterRequest {
  sourceType: ProcurementTenderDocumentSourceType;
  sourceId: string;
  businessPartnerId?: string;
  recipientName: string;
  recipientEmail?: string;
  recipientPhone?: string;
  amountPaid: number;
  paymentReference?: string;
  receiptNumber: string;
  issueChannel: string;
  evidenceReference: string;
  evidenceWorkflowDocumentId?: string;
  evidenceFileUploadRecordId?: string;
  registerRowVersion: string;
}

export interface CreateProcurementTenderDocumentChangeRequest {
  sourceType: ProcurementTenderDocumentSourceType;
  sourceId: string;
  changeType: ProcurementTenderDocumentChangeType;
  newTemplateVersionId?: string;
  newValueUtc?: string;
  requiresAcknowledgement: boolean;
  reason: string;
  workflowDefinitionId: string;
  evidenceReference: string;
  evidenceWorkflowDocumentId?: string;
  evidenceFileUploadRecordId?: string;
  registerRowVersion: string;
}

export interface DecideProcurementTenderDocumentChangeRequest {
  action: 'Approve' | 'Reject';
  approvalReference: string;
  comments?: string;
  dispatchChannel?: string;
  dispatchReference?: string;
  dispatchEvidenceReference?: string;
  rowVersion: string;
}

export interface AcknowledgeProcurementTenderDocumentChangeRequest {
  issuanceId?: string;
  changeRecipientId?: string;
  outcome: ProcurementTenderDocumentAcknowledgementOutcome;
  acknowledgementChannel: string;
  acknowledgementReference: string;
  evidenceReference: string;
}

export interface ProcurementTenderDocumentEffectiveState {
  registerId: string;
  sourceType: ProcurementTenderDocumentSourceType;
  sourceId: string;
  effectiveTemplateVersionId: string;
  effectiveTemplateReference: string;
  effectiveSubmissionDeadlineUtc: string;
  effectiveBidValidityUntilUtc: string;
  ready: boolean;
  blockedReasons: string[];
}
