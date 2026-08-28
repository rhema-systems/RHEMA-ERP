export type AuditLifecycleActionType =
  'LegalHoldPlaced' | 'LegalHoldReleased' | 'Archived' | 'Restored';

export type AuditOperationKind =
  | 'Other'
  | 'Create'
  | 'Update'
  | 'Approve'
  | 'Reject'
  | 'Override'
  | 'Post'
  | 'Reverse'
  | 'Dispatch'
  | 'Receive';

export interface AuditLifecycleAction {
  id: string;
  sequenceNumber: number;
  action: AuditLifecycleActionType;
  reason: string;
  correlationId: string;
  archiveReference?: string;
  occurredAtUtc: string;
  retainUntilUtc: string;
  actorUserId: string;
  actorName: string;
  integrityHash: string;
  integrityValid: boolean;
}

export interface AuditRecordGovernance {
  storeKey: string;
  storeName: string;
  recordId: string;
  reference: string;
  sourceOccurredAtUtc: string;
  retainUntilUtc: string;
  retentionDays: number;
  isImmutable: boolean;
  isLegalHold: boolean;
  isArchived: boolean;
  archiveReference?: string;
  actions: AuditLifecycleAction[];
}

export interface AuditLifecycleCommand {
  reason: string;
  requestKey: string;
  correlationId?: string;
}

export interface AuditEventCoverageDefinition {
  module: string;
  operation: AuditOperationKind;
  control: string;
  emittedActions: string[];
}

export interface AuditEventCoverageReport {
  verifiedAtUtc: string;
  requiredOperations: AuditOperationKind[];
  definitions: AuditEventCoverageDefinition[];
  missingOperations: AuditOperationKind[];
  isComplete: boolean;
}
