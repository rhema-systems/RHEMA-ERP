export type ProcurementControlEventResult =
  | 'Succeeded'
  | 'Rejected'
  | 'Allowed'
  | 'Denied'
  | 'ReviewRequired'
  | 'Failed'
  | 'Warning';

export type ProcurementControlEvidenceReferenceKind =
  | 'WorkflowEvidenceDocument'
  | 'FileUploadRecord'
  | 'ExternalReference';

export interface ProcurementControlEventSearch {
  eventType?: string;
  action?: string;
  result?: ProcurementControlEventResult;
  ruleCode?: string;
  sourceType?: string;
  sourceReference?: string;
  correlationId?: string;
  actorUserId?: string;
  fromUtc?: string;
  toUtc?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface ProcurementControlEventPage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: ProcurementControlEvent[];
}

export interface ProcurementControlEventSummary {
  totalCount: number;
  allowedCount: number;
  deniedOrRejectedCount: number;
  failedCount: number;
  evidenceLinkedCount: number;
  latestOccurredAtUtc?: string;
  byEventType: Record<string, number>;
}

export interface ProcurementControlEvent {
  id: string;
  tenantId: string;
  eventKey: string;
  schemaVersion: number;
  eventType: string;
  action: string;
  result: ProcurementControlEventResult;
  ruleCode?: string;
  ruleId?: string;
  ruleVersion?: string;
  decisionKeys: string[];
  sourceType: string;
  sourceId?: string;
  sourceReference: string;
  actorUserId: string;
  actorName: string;
  actorRoles: string[];
  reason?: string;
  inputValuesJson?: string;
  resultValuesJson?: string;
  beforeJson?: string;
  afterJson?: string;
  correlationId: string;
  causationId?: string;
  occurredAtUtc: string;
  recordedAtUtc: string;
  integrityHash: string;
  integrityValid: boolean;
  evidence: ProcurementControlEventEvidence[];
}

export interface ProcurementControlEventEvidence {
  id: string;
  referenceKind: ProcurementControlEvidenceReferenceKind;
  referenceId?: string;
  reference: string;
  label?: string;
  requirementKey?: string;
  fileName?: string;
  sha256?: string;
  verificationStatus?: string;
  referenceAvailable: boolean;
}

export interface ProcurementControlEventIntegrity {
  checkedCount: number;
  validCount: number;
  invalidCount: number;
  isValid: boolean;
  verifiedAtUtc: string;
  issues: ProcurementControlEventIntegrityIssue[];
}

export interface ProcurementControlEventIntegrityIssue {
  eventId: string;
  eventKey: string;
  expectedHash: string;
  actualHash: string;
}
