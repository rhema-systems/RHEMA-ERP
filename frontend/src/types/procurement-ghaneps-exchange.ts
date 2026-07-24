export type ProcurementGhanepsSourceType =
  | 'RequestForQuotation'
  | 'Tender'
  | 'ExceptionalSourcing';

export type ProcurementGhanepsEventFamily =
  | 'TenderPublication'
  | 'TenderReference'
  | 'AwardNotification';

export type ProcurementGhanepsExchangeDirection =
  | 'Export'
  | 'Import'
  | 'Bidirectional';

export type ProcurementGhanepsContentType =
  | 'application/json'
  | 'text/csv'
  | 'application/csv'
  | 'application/xml'
  | 'text/xml'
  | 'text/plain';

export type ProcurementGhanepsExchangeStatus =
  | 'Prepared'
  | 'Transferred'
  | 'PendingAcknowledgement'
  | 'Acknowledged'
  | 'Failed'
  | 'Reconciled'
  | 'ReconciliationException';

export type ProcurementGhanepsAttemptOutcome = 'Succeeded' | 'Failed';
export type ProcurementGhanepsAcknowledgementOutcome =
  | 'Accepted'
  | 'Rejected';
export type ProcurementGhanepsReconciliationOutcome =
  | 'Matched'
  | 'Mismatch'
  | 'Resolved';

export interface ProcurementGhanepsExchangeMappingOption {
  mappingKey: string;
  eventFamily: ProcurementGhanepsEventFamily;
  direction: ProcurementGhanepsExchangeDirection;
  externalEventCode: string;
  templateReference: string;
  schemaReference: string;
  payloadVersion: string;
  referenceField: string;
  payloadContentType: ProcurementGhanepsContentType;
  acknowledgementContentType: ProcurementGhanepsContentType;
  acknowledgementPermissionCode: string;
  reconciliationPermissionCode: string;
  acknowledgementRequired: boolean;
  reconciliationRequired: boolean;
  maximumRetryAttempts: number;
}

export interface ProcurementGhanepsExchangeOptions {
  sourceType: ProcurementGhanepsSourceType;
  sourceId: string;
  sourceReference: string;
  sourceVariant: string;
  configurationProfileId: string;
  configurationProfileCode: string;
  configurationProfileVersion: number;
  configurationDecisionId: string;
  exchangeProfileCode: string;
  configurationValueHash: string;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  frequency: string;
  owner: string;
  acknowledgementRule: string;
  reconciliationRule: string;
  mappings: ProcurementGhanepsExchangeMappingOption[];
  allowedActions: string[];
  blockedReasons: string[];
}

export interface ProcurementGhanepsExchangePayload {
  id: string;
  version: number;
  direction: ProcurementGhanepsExchangeDirection;
  templateReference: string;
  schemaReference: string;
  externalPayloadVersion: string;
  contentType: ProcurementGhanepsContentType;
  fileName?: string;
  payloadContent: string;
  payloadChecksumSha256: string;
  recordedAtUtc: string;
  recordedByUserId: string;
  recordedByName: string;
  evidenceReference?: string;
  integrityHash: string;
}

export interface ProcurementGhanepsExchangeAttempt {
  id: string;
  payloadId: string;
  attemptNumber: number;
  isRetry: boolean;
  supersedesAttemptId?: string;
  outcome: ProcurementGhanepsAttemptOutcome;
  transportReference?: string;
  failureCode?: string;
  failureMessage?: string;
  requestFingerprint: string;
  attemptedAtUtc: string;
  attemptedByUserId: string;
  attemptedByName: string;
  evidenceReference?: string;
  integrityHash: string;
}

export interface ProcurementGhanepsExchangeAcknowledgement {
  id: string;
  attemptId: string;
  payloadId: string;
  sequence: number;
  outcome: ProcurementGhanepsAcknowledgementOutcome;
  acknowledgementReference: string;
  externalStatusCode?: string;
  contentType: ProcurementGhanepsContentType;
  acknowledgementContent: string;
  acknowledgementChecksumSha256: string;
  requestFingerprint: string;
  acknowledgedAtUtc: string;
  acknowledgedByUserId: string;
  acknowledgedByName: string;
  evidenceReference: string;
  integrityHash: string;
}

export interface ProcurementGhanepsExchangeReconciliation {
  id: string;
  attemptId: string;
  payloadId: string;
  sequence: number;
  outcome: ProcurementGhanepsReconciliationOutcome;
  expectedReference: string;
  actualReference?: string;
  expectedChecksumSha256: string;
  actualChecksumSha256?: string;
  requestFingerprint: string;
  notes?: string;
  reconciledAtUtc: string;
  reconciledByUserId: string;
  reconciledByName: string;
  evidenceReference: string;
  integrityHash: string;
}

export interface ProcurementGhanepsExchangeHistoryItem {
  exchangeEventId: string;
  eventFamily: ProcurementGhanepsEventFamily;
  eventReference: string;
  kind: string;
  recordId: string;
  sequence: number;
  outcome: string;
  reference?: string;
  evidenceReference?: string;
  actorUserId: string;
  actorName: string;
  occurredAtUtc: string;
  integrityHash: string;
}

export interface ProcurementGhanepsExchangeEvent {
  id: string;
  sourceType: ProcurementGhanepsSourceType;
  sourceId: string;
  sourceReference: string;
  sourceVariant: string;
  sourceOccurredAtUtc: string;
  sourceIntegrityHash: string;
  eventFamily: ProcurementGhanepsEventFamily;
  direction: ProcurementGhanepsExchangeDirection;
  mappingKey: string;
  externalEventCode: string;
  eventReference: string;
  referenceField: string;
  payloadContentType: ProcurementGhanepsContentType;
  acknowledgementContentType: ProcurementGhanepsContentType;
  acknowledgementPermissionCode: string;
  reconciliationPermissionCode: string;
  configurationProfileId: string;
  configurationProfileCode: string;
  configurationProfileVersion: number;
  configurationDecisionId: string;
  exchangeProfileCode: string;
  configurationValueHash: string;
  mappingIntegrityHash: string;
  configurationEffectiveFromUtc: string;
  configurationEffectiveToUtc?: string;
  frequency: string;
  owner: string;
  acknowledgementRule: string;
  reconciliationRule: string;
  acknowledgementRequired: boolean;
  reconciliationRequired: boolean;
  maximumRetryAttempts: number;
  status: ProcurementGhanepsExchangeStatus;
  requestFingerprint: string;
  correlationId: string;
  preparedByUserId: string;
  preparedByName: string;
  preparedAtUtc: string;
  evidenceReference?: string;
  integrityHash: string;
  rowVersion: string;
  payloads: ProcurementGhanepsExchangePayload[];
  attempts: ProcurementGhanepsExchangeAttempt[];
  acknowledgements: ProcurementGhanepsExchangeAcknowledgement[];
  reconciliations: ProcurementGhanepsExchangeReconciliation[];
  history: ProcurementGhanepsExchangeHistoryItem[];
  allowedActions: string[];
  blockedReasons: string[];
}

export interface ProcurementGhanepsExchangeOverview {
  sourceType: ProcurementGhanepsSourceType;
  sourceId: string;
  sourceReference: string;
  sourceVariant: string;
  events: ProcurementGhanepsExchangeEvent[];
}

export interface ProcurementGhanepsPayloadRequest {
  sourceType: ProcurementGhanepsSourceType;
  sourceId: string;
  eventFamily: ProcurementGhanepsEventFamily;
  mappingKey: string;
  eventReference: string;
  payloadContent: string;
  fileName?: string;
  expectedPayloadChecksumSha256?: string;
  evidenceReference?: string;
  idempotencyKey: string;
}

export type PrepareProcurementGhanepsExportRequest =
  ProcurementGhanepsPayloadRequest;

export interface RecordProcurementGhanepsImportRequest
  extends ProcurementGhanepsPayloadRequest {
  transportReference: string;
}

export interface RecordProcurementGhanepsAttemptRequest {
  payloadId: string;
  outcome: ProcurementGhanepsAttemptOutcome;
  transportReference?: string;
  failureCode?: string;
  failureMessage?: string;
  evidenceReference?: string;
  idempotencyKey: string;
  expectedRowVersion: string;
}

export interface RetryProcurementGhanepsExchangeRequest {
  payloadId?: string;
  outcome: ProcurementGhanepsAttemptOutcome;
  transportReference?: string;
  failureCode?: string;
  failureMessage?: string;
  replacementPayloadContent?: string;
  fileName?: string;
  expectedPayloadChecksumSha256?: string;
  evidenceReference?: string;
  idempotencyKey: string;
  expectedRowVersion: string;
}

export interface RecordProcurementGhanepsAcknowledgementRequest {
  outcome: ProcurementGhanepsAcknowledgementOutcome;
  acknowledgementReference: string;
  externalStatusCode?: string;
  acknowledgementContent: string;
  expectedAcknowledgementChecksumSha256?: string;
  evidenceReference: string;
  idempotencyKey: string;
  expectedRowVersion: string;
}

export interface ReconcileProcurementGhanepsExchangeRequest {
  resolveExistingMismatch: boolean;
  actualReference: string;
  actualChecksumSha256: string;
  notes?: string;
  evidenceReference: string;
  idempotencyKey: string;
  expectedRowVersion: string;
}
