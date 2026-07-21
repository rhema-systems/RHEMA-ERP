export type ProcurementConfigurationProfileStatus = 'Draft' | 'Published' | 'Retired';
export type ProcurementConfigurationDecisionStatus = 'Draft' | 'Proposed' | 'Approved' | 'Rejected';
export type ProcurementConfigurationApprovalStatus = 'Pending' | 'Approved' | 'Rejected' | 'NotRequired';
export type ProcurementConfigurationEvidenceStatus = 'Missing' | 'Attached' | 'Verified';

export interface ProcurementConfigurationValidationIssue {
  code: string;
  message: string;
  decisionKey?: string;
  severity: string;
}

export interface ProcurementConfigurationValidationResult {
  isValid: boolean;
  errors: ProcurementConfigurationValidationIssue[];
  warnings: ProcurementConfigurationValidationIssue[];
}

export interface ProcurementConfigurationEvidenceLink {
  id: string;
  decisionId: string;
  evidenceType: string;
  fileUploadRecordId?: string;
  filePath?: string;
  originalFileName?: string;
  contentType?: string;
  fileSize?: number;
  virusScanStatus?: string;
  externalReference?: string;
  checksum?: string;
  uploadedAt: string;
  uploadedById: string;
}

export interface ProcurementConfigurationDecision {
  id: string;
  decisionKey: string;
  displayName: string;
  description: string;
  ownerGroup: string;
  schemaVersion: number;
  status: ProcurementConfigurationDecisionStatus;
  approvalStatus: ProcurementConfigurationApprovalStatus;
  evidenceStatus: ProcurementConfigurationEvidenceStatus;
  value: Record<string, unknown>;
  decisionDate?: string;
  effectiveFrom?: string;
  effectiveTo?: string;
  approvedById?: string;
  approvedAt?: string;
  approvalWorkflowInstanceId?: string;
  approvalReference?: string;
  sourceLineage?: string;
  notes?: string;
  requiresApproval: boolean;
  requiresEvidence: boolean;
  isComplete: boolean;
  rowVersion: string;
  evidence: ProcurementConfigurationEvidenceLink[];
}

export interface ProcurementConfigurationProfileSummary {
  id: string;
  profileKey: string;
  profileCode: string;
  name: string;
  version: number;
  lifecycleStatus: ProcurementConfigurationProfileStatus;
  effectiveFrom: string;
  effectiveTo?: string;
  isDefault: boolean;
  completeDecisionCount: number;
  totalDecisionCount: number;
  isComplete: boolean;
  updatedBy?: string;
  updatedAt: string;
  rowVersion: string;
}

export interface ProcurementConfigurationRevision {
  id: string;
  profileId: string;
  decisionId?: string;
  action: string;
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

export interface ProcurementConfigurationProfile extends ProcurementConfigurationProfileSummary {
  changeSummary?: string;
  supersedesProfileId?: string;
  publishedAt?: string;
  publishedById?: string;
  retiredAt?: string;
  retiredById?: string;
  decisions: ProcurementConfigurationDecision[];
  validation: ProcurementConfigurationValidationResult;
  recentHistory: ProcurementConfigurationRevision[];
}

export interface ProcurementDecisionSchema {
  decisionKey: string;
  displayName: string;
  description: string;
  ownerGroup: string;
  schemaVersion: number;
  valueType: string;
  requiresApproval: boolean;
  requiresEvidence: boolean;
  requiresRenewedApproval: boolean;
}

export interface ProcurementConfigurationPagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateProcurementConfigurationProfileRequest {
  profileCode: string;
  name: string;
  effectiveFrom: string;
  effectiveTo?: string;
  changeSummary?: string;
  isDefault: boolean;
}

export interface SaveProcurementConfigurationDecisionRequest {
  schemaVersion: number;
  ownerGroup: string;
  status: ProcurementConfigurationDecisionStatus;
  approvalStatus: ProcurementConfigurationApprovalStatus;
  value: Record<string, unknown>;
  decisionDate?: string;
  approvalWorkflowInstanceId?: string;
  approvalReference?: string;
  sourceLineage?: string;
  notes?: string;
  rowVersion: string;
  reason?: string;
}

export interface UpdateProcurementConfigurationProfileRequest {
  name: string;
  effectiveFrom: string;
  effectiveTo?: string;
  changeSummary?: string;
  isDefault: boolean;
  rowVersion: string;
  reason?: string;
}

export interface ProcurementConfigurationLifecycleRequest {
  rowVersion: string;
  reason?: string;
}

export interface LinkProcurementConfigurationEvidenceRequest {
  evidenceType: string;
  filePath?: string;
  externalReference?: string;
  checksum?: string;
  referenceMetadataJson?: string;
  decisionRowVersion: string;
  reason?: string;
}

export type ProcurementDecisionFieldType = 'text' | 'textarea' | 'number' | 'date' | 'boolean' | 'select' | 'textList';

export interface ProcurementDecisionFieldDefinition {
  key: string;
  label: string;
  type: ProcurementDecisionFieldType;
  required?: boolean;
  placeholder?: string;
  options?: Array<{ value: string; label: string }>;
  min?: number;
  max?: number;
  step?: number;
}

export interface ProcurementDecisionFormDefinition {
  decisionKey: string;
  fields: ProcurementDecisionFieldDefinition[];
  defaults: Record<string, unknown>;
}
