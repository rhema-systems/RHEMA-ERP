export type CivilProfileStatus = 'Draft' | 'Published' | 'Retired';
export type CivilDecisionStatus =
  'Draft' | 'Proposed' | 'Approved' | 'Rejected';
export type CivilApprovalStatus = 'Pending' | 'Approved' | 'Rejected';
export type CivilEvidenceStatus = 'Missing' | 'Attached' | 'Verified';

export interface CivilValidationIssue {
  code: string;
  message: string;
  configurationKey?: string;
  severity: string;
}
export interface CivilValidationResult {
  isValid: boolean;
  errors: CivilValidationIssue[];
  warnings: CivilValidationIssue[];
}
export interface CivilEvidence {
  id: string;
  decisionId: string;
  evidenceType: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference?: string;
  documentTitle?: string;
  versionNumber?: string;
  checksum?: string;
  linkedAt: string;
  linkedById: string;
}
export interface CivilDecision {
  id: string;
  configurationKey: string;
  displayName: string;
  description: string;
  ownerGroup: string;
  schemaVersion: number;
  status: CivilDecisionStatus;
  approvalStatus: CivilApprovalStatus;
  evidenceStatus: CivilEvidenceStatus;
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
  evidence: CivilEvidence[];
}
export interface CivilProfileSummary {
  id: string;
  profileKey: string;
  profileCode: string;
  name: string;
  version: number;
  lifecycleStatus: CivilProfileStatus;
  effectiveFrom: string;
  effectiveTo?: string;
  isDefault: boolean;
  completeDecisionCount: number;
  totalDecisionCount: number;
  isComplete: boolean;
  updatedAt: string;
  rowVersion: string;
}
export interface CivilRevision {
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
export interface CivilProfile extends CivilProfileSummary {
  changeSummary?: string;
  supersedesProfileId?: string;
  publishedAt?: string;
  retiredAt?: string;
  decisions: CivilDecision[];
  validation: CivilValidationResult;
  recentHistory: CivilRevision[];
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
export interface CivilDecisionSchema {
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
export interface CivilLookups {
  sources: Record<string, LookupOption[]>;
}
export interface CivilPagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export interface CreateCivilProfileRequest {
  name: string;
  effectiveFrom: string;
  effectiveTo?: string;
  changeSummary?: string;
  isDefault: boolean;
}
export interface UpdateCivilProfileRequest extends CreateCivilProfileRequest {
  rowVersion: string;
  reason?: string;
}
export interface SaveCivilDecisionRequest {
  schemaVersion: number;
  value: Record<string, unknown>;
  sourceLineage?: string;
  notes?: string;
  rowVersion: string;
  reason?: string;
}
export interface CivilDecisionActionRequest {
  rowVersion: string;
  reason?: string;
  approvalReference?: string;
}
export interface CivilLifecycleRequest {
  rowVersion: string;
  reason: string;
}
export interface LinkCivilEvidenceRequest {
  evidenceType: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  checksum?: string;
  decisionRowVersion: string;
  reason?: string;
}
