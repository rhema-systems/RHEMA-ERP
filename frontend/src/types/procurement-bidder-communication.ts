export type ProcurementBidderCommunicationSourceType =
  | 'RequestForQuotation'
  | 'Tender'
  | 'ExceptionalSourcing';

export type ProcurementBidderCommunicationAwardFamily =
  | 'RequestForQuotation'
  | 'FormalTender'
  | 'ExceptionalSourcing'
  | 'LegacyTenderAward';
export type ProcurementBidderOutcome = 'Successful' | 'Unsuccessful';
export type ProcurementBidderDispatchChannel =
  | 'Email'
  | 'Sms'
  | 'SupplierPortal'
  | 'PhysicalDelivery'
  | 'Other';
export type ProcurementBidderDeliveryOutcome =
  | 'Sent'
  | 'Delivered'
  | 'Failed'
  | 'Returned';
export type ProcurementBidderAcknowledgementOutcome =
  | 'Received'
  | 'Accepted'
  | 'Disputed';
export type ProcurementBidderAppealOutcome =
  | 'Upheld'
  | 'Dismissed'
  | 'Withdrawn';
export type ProcurementTenderSecurityInstrumentType =
  | 'BidBond'
  | 'BankGuarantee'
  | 'InsuranceBond'
  | 'CashDeposit'
  | 'Other';
export type ProcurementTenderSecurityActionType =
  | 'Released'
  | 'Returned'
  | 'Forfeited';

export interface ProcurementBidderDelivery {
  id: string;
  sequence: number;
  outcome: ProcurementBidderDeliveryOutcome;
  occurredAtUtc: string;
  providerReference: string;
  detail?: string;
  evidenceReference: string;
  integrityHash: string;
}

export interface ProcurementBidderAcknowledgement {
  id: string;
  sequence: number;
  outcome: ProcurementBidderAcknowledgementOutcome;
  acknowledgedAtUtc: string;
  acknowledgedByUserId: string;
  acknowledgedByBusinessPartnerId?: string;
  acknowledgementChannel: string;
  acknowledgementReference: string;
  evidenceReference: string;
  integrityHash: string;
}

export interface ProcurementBidderLetterDispatch {
  id: string;
  sequence: number;
  channel: ProcurementBidderDispatchChannel;
  destination: string;
  dispatchReference: string;
  dispatchEvidenceReference: string;
  dispatchedAtUtc: string;
  dispatchedByUserId: string;
  dispatchedByName: string;
  integrityHash: string;
  deliveries: ProcurementBidderDelivery[];
  acknowledgements: ProcurementBidderAcknowledgement[];
}

export interface ProcurementBidderLetterVersion {
  id: string;
  version: number;
  templateVersionId: string;
  templateReference: string;
  templateChecksumSha256: string;
  contentReference: string;
  contentChecksumSha256: string;
  workflowDefinitionId: string;
  workflowInstanceId: string;
  approvalReference: string;
  approvalEvidenceReference: string;
  approvedAtUtc: string;
  approvedByUserId: string;
  approvedByName: string;
  integrityHash: string;
  dispatches: ProcurementBidderLetterDispatch[];
}

export interface ProcurementBidderAppealDecision {
  id: string;
  outcome: ProcurementBidderAppealOutcome;
  reason: string;
  workflowDefinitionId: string;
  workflowInstanceId: string;
  decisionReference: string;
  evidenceReference: string;
  decidedAtUtc: string;
  decidedByUserId: string;
  decidedByName: string;
  integrityHash: string;
}

export interface ProcurementBidderAppeal {
  id: string;
  sequence: number;
  grounds: string;
  evidenceReference: string;
  filedAtUtc: string;
  filedByUserId: string;
  filedByBusinessPartnerId?: string;
  integrityHash: string;
  decision?: ProcurementBidderAppealDecision;
}

export interface ProcurementTenderSecurityAction {
  id: string;
  sequence: number;
  actionType: ProcurementTenderSecurityActionType;
  workflowDefinitionId: string;
  workflowInstanceId: string;
  actionReference: string;
  reason: string;
  evidenceReference: string;
  actionedAtUtc: string;
  actionedByUserId: string;
  actionedByName: string;
  integrityHash: string;
}

export interface ProcurementTenderSecurityInstrument {
  id: string;
  tenderBidId?: string;
  requestForQuotationQuoteId?: string;
  instrumentType: ProcurementTenderSecurityInstrumentType;
  instrumentReference: string;
  issuerName: string;
  amount: number;
  currencyCode: string;
  issuedAtUtc: string;
  expiresAtUtc: string;
  evidenceReference: string;
  registeredAtUtc: string;
  integrityHash: string;
  currentAction?: ProcurementTenderSecurityActionType;
  actions: ProcurementTenderSecurityAction[];
  allowedActions: string[];
  blockedReasons: string[];
}

export interface ProcurementBidderCommunicationRecipient {
  id: string;
  businessPartnerId: string;
  outcome: ProcurementBidderOutcome;
  bidOrQuoteIds: string[];
  partnerCode: string;
  partnerName: string;
  recipientEmail?: string;
  recipientPhone?: string;
  lineageHash: string;
  integrityHash: string;
  letterVersions: ProcurementBidderLetterVersion[];
  appeals: ProcurementBidderAppeal[];
  securityInstruments: ProcurementTenderSecurityInstrument[];
  allowedActions: string[];
}

export interface ProcurementBidderCommunicationOverview {
  id: string;
  sourceType: ProcurementBidderCommunicationSourceType;
  sourceId: string;
  sourceReference: string;
  awardFamily: ProcurementBidderCommunicationAwardFamily;
  awardId: string;
  awardReference: string;
  awardedAtUtc: string;
  awardReadinessDecisionId: string;
  awardReadinessDecisionSequence: number;
  awardReadinessIntegrityHash: string;
  awardReadinessSourceIntegrityHash: string;
  awardReadinessIsCurrent: boolean;
  standstillStartsAtUtc: string;
  standstillEndsAtUtc: string;
  appealWindowEndsAtUtc: string;
  standstillAuthorityReference: string;
  standstillElapsed: boolean;
  appealWindowOpen: boolean;
  hasOpenAppeals: boolean;
  initializedAtUtc: string;
  initializedByUserId: string;
  initializedByName: string;
  recipientSnapshotHash: string;
  integrityHash: string;
  rowVersion: string;
  recipients: ProcurementBidderCommunicationRecipient[];
  allowedActions: string[];
  blockedReasons: string[];
}

export interface InitializeProcurementBidderCommunicationRequest {
  sourceType: ProcurementBidderCommunicationSourceType;
  sourceId: string;
  standstillEndsAtUtc: string;
  appealWindowEndsAtUtc: string;
  standstillAuthorityReference: string;
  expectedAwardReadinessDecisionId: string;
  expectedAwardReadinessIntegrityHash: string;
  idempotencyKey: string;
}

export interface ApproveProcurementBidderLetterRequest {
  templateVersionId: string;
  contentReference: string;
  contentChecksumSha256: string;
  workflowInstanceId: string;
  approvalReference: string;
  approvalEvidenceReference: string;
  approvalWorkflowEvidenceDocumentId?: string;
  approvalFileUploadRecordId?: string;
  expectedRegisterIntegrityHash: string;
  idempotencyKey: string;
}

export interface DispatchProcurementBidderLetterRequest {
  channel: ProcurementBidderDispatchChannel;
  destination: string;
  dispatchReference: string;
  dispatchEvidenceReference: string;
  dispatchWorkflowEvidenceDocumentId?: string;
  dispatchFileUploadRecordId?: string;
  idempotencyKey: string;
}

export interface RecordProcurementBidderDeliveryRequest {
  outcome: ProcurementBidderDeliveryOutcome;
  occurredAtUtc: string;
  providerReference: string;
  detail?: string;
  evidenceReference: string;
  idempotencyKey: string;
}

export interface AcknowledgeProcurementBidderDispatchRequest {
  outcome: ProcurementBidderAcknowledgementOutcome;
  acknowledgementChannel: string;
  acknowledgementReference: string;
  evidenceReference: string;
  idempotencyKey: string;
}

export interface FileProcurementBidderAppealRequest {
  grounds: string;
  evidenceReference: string;
  evidenceWorkflowDocumentId?: string;
  evidenceFileUploadRecordId?: string;
  idempotencyKey: string;
}

export interface ResolveProcurementBidderAppealRequest {
  outcome: ProcurementBidderAppealOutcome;
  reason: string;
  workflowInstanceId: string;
  decisionReference: string;
  evidenceReference: string;
  evidenceWorkflowDocumentId?: string;
  evidenceFileUploadRecordId?: string;
  idempotencyKey: string;
}

export interface RegisterProcurementTenderSecurityRequest {
  tenderBidId?: string;
  requestForQuotationQuoteId?: string;
  instrumentType: ProcurementTenderSecurityInstrumentType;
  instrumentReference: string;
  issuerName: string;
  amount: number;
  currencyCode: string;
  issuedAtUtc: string;
  expiresAtUtc: string;
  evidenceReference: string;
  evidenceWorkflowDocumentId?: string;
  evidenceFileUploadRecordId?: string;
  idempotencyKey: string;
}

export interface ActOnProcurementTenderSecurityRequest {
  actionType: ProcurementTenderSecurityActionType;
  workflowInstanceId: string;
  actionReference: string;
  reason: string;
  evidenceReference: string;
  evidenceWorkflowDocumentId?: string;
  evidenceFileUploadRecordId?: string;
  expectedLatestActionIntegrityHash?: string;
  idempotencyKey: string;
}
