export type QsProfileStatus = 'Draft' | 'Published' | 'Retired';
export type QsDecisionStatus = 'Draft' | 'Proposed' | 'Approved' | 'Rejected';
export type QsApprovalStatus = 'Pending' | 'Approved' | 'Rejected';
export type QsEvidenceStatus = 'Missing' | 'Attached' | 'Verified';

export interface QsValidationIssue {
  code: string;
  message: string;
  decisionKey?: string;
  severity: string;
}
export interface QsValidationResult {
  isValid: boolean;
  errors: QsValidationIssue[];
  warnings: QsValidationIssue[];
}
export interface QsEvidence {
  id: string;
  decisionId: string;
  evidenceType: string;
  centralDocumentRecordId?: string;
  centralDocumentVersionId?: string;
  documentReference?: string;
  documentTitle?: string;
  versionNumber?: string;
  externalReference?: string;
  checksum?: string;
  linkedAt: string;
  linkedById: string;
}
export interface QsDecision {
  id: string;
  decisionKey: string;
  displayName: string;
  description: string;
  ownerGroup: string;
  schemaVersion: number;
  status: QsDecisionStatus;
  approvalStatus: QsApprovalStatus;
  evidenceStatus: QsEvidenceStatus;
  value: Record<string, unknown>;
  decisionDate?: string;
  effectiveFrom?: string;
  effectiveTo?: string;
  approvedById?: string;
  approvedAt?: string;
  approvalReference?: string;
  sourceLineage?: string;
  notes?: string;
  isComplete: boolean;
  rowVersion: string;
  evidence: QsEvidence[];
}
export interface QsProfileSummary {
  id: string;
  profileKey: string;
  profileCode: string;
  name: string;
  version: number;
  lifecycleStatus: QsProfileStatus;
  effectiveFrom: string;
  effectiveTo?: string;
  isDefault: boolean;
  completeDecisionCount: number;
  totalDecisionCount: number;
  isComplete: boolean;
  updatedAt: string;
  rowVersion: string;
}
export interface QsRevision {
  id: string;
  profileId: string;
  decisionId?: string;
  sourceType: string;
  sourceId: string;
  action: string;
  operation: string;
  result: string;
  correlationId: string;
  actorUserId: string;
  actorName: string;
  actorRoles?: string;
  reason?: string;
  before?: Record<string, unknown>;
  after?: Record<string, unknown>;
  timestamp: string;
}
export interface QsProfile extends QsProfileSummary {
  changeSummary?: string;
  supersedesProfileId?: string;
  publishedAt?: string;
  retiredAt?: string;
  decisions: QsDecision[];
  validation: QsValidationResult;
  recentHistory: QsRevision[];
}
export type ControlledFieldType =
  | 'boolean'
  | 'number'
  | 'date'
  | 'select'
  | 'multiselect'
  | 'lookup'
  | 'multilookup';
export interface ControlledFieldSchema {
  name: string;
  label: string;
  control: ControlledFieldType;
  required: boolean;
  lookupSource?: string;
  lookupGroup?: string;
  options: string[];
  minimum?: number;
  maximum?: number;
  helpText?: string;
}
export interface QsDecisionSchema {
  decisionKey: string;
  configurationKey: string;
  displayName: string;
  description: string;
  ownerGroup: string;
  schemaVersion: number;
  fields: ControlledFieldSchema[];
}
export interface LookupOption {
  value: string;
  label: string;
  group?: string;
}
export interface QsLookups {
  sources: Record<string, LookupOption[]>;
}
export interface QsPagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export interface CreateQsProfileRequest {
  name: string;
  effectiveFrom: string;
  effectiveTo?: string;
  changeSummary?: string;
  isDefault: boolean;
}
export interface UpdateQsProfileRequest extends CreateQsProfileRequest {
  rowVersion: string;
  reason?: string;
}
export interface SaveQsDecisionRequest {
  schemaVersion: number;
  value: Record<string, unknown>;
  sourceLineage?: string;
  notes?: string;
  rowVersion: string;
  reason?: string;
}
export interface QsDecisionActionRequest {
  rowVersion: string;
  reason?: string;
  approvalReference?: string;
}
export interface QsLifecycleRequest {
  rowVersion: string;
  reason: string;
}
export interface LinkQsEvidenceRequest {
  evidenceType: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  externalReference?: string;
  checksum?: string;
  decisionRowVersion: string;
  reason?: string;
}
