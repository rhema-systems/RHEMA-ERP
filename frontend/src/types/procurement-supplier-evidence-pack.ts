import type { ProcurementControlEvidenceReferenceKind } from './procurement-control-event';

export type SupplierRegistrationCategory = 'Goods' | 'Works' | 'Services';
export type SupplierEvidencePackStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Published'
  | 'Retired';
export type SupplierEvidenceRequirementKind =
  | 'Document'
  | 'Classification'
  | 'DocumentAndClassification';
export type SupplierEvidenceValidityMode =
  | 'NotApplicable'
  | 'CurrentOnSubmission'
  | 'MinimumRemainingDays';

export interface SupplierEvidencePackSearch {
  search?: string;
  category?: SupplierRegistrationCategory;
  status?: SupplierEvidencePackStatus;
  effectiveAtUtc?: string;
  page?: number;
  pageSize?: number;
}

export interface SupplierEvidencePackSummary {
  familyCount: number;
  draftCount: number;
  pendingApprovalCount: number;
  publishedCount: number;
  effectiveCount: number;
  retiredCount: number;
  effectiveByCategory: Partial<Record<SupplierRegistrationCategory, number>>;
}

export interface SupplierEvidenceRequirement {
  id: string;
  requirementCode: string;
  name: string;
  description?: string;
  kind: SupplierEvidenceRequirementKind;
  documentType?: string;
  isMandatory: boolean;
  classificationScheme?: string;
  allowedClassifications: string[];
  validityMode: SupplierEvidenceValidityMode;
  minimumRemainingDays?: number;
  approvalStepOrder: number;
  approvalStepName: string;
  maxFileSizeBytes: number;
  allowedMimeTypes: string[];
  integrityHash: string;
}

export interface SaveSupplierEvidenceRequirement {
  requirementCode: string;
  name: string;
  description?: string;
  kind: SupplierEvidenceRequirementKind;
  documentType?: string;
  isMandatory: boolean;
  classificationScheme?: string;
  allowedClassifications: string[];
  validityMode: SupplierEvidenceValidityMode;
  minimumRemainingDays?: number;
  approvalStepOrder: number;
  approvalStepName: string;
  maxFileSizeBytes: number;
  allowedMimeTypes: string[];
}

export interface SupplierEvidencePackListItem {
  id: string;
  packKey: string;
  packCode: string;
  name: string;
  category: SupplierRegistrationCategory;
  version: number;
  status: SupplierEvidencePackStatus;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  isEffective: boolean;
  requirementCount: number;
  mandatoryRequirementCount: number;
  sourceConfigurationProfileCode: string;
  sourceConfigurationProfileVersion: number;
  allowedActions: string[];
  rowVersion: string;
}

export interface SupplierEvidencePack extends SupplierEvidencePackListItem {
  description?: string;
  sourceConfigurationProfileId: string;
  workflowDefinitionId: string;
  workflowInstanceId?: string;
  supersedesVersionId?: string;
  changeSummary?: string;
  reviewComment?: string;
  submittedById?: string;
  submittedAtUtc?: string;
  publishedById?: string;
  publishedAtUtc?: string;
  retiredById?: string;
  retiredAtUtc?: string;
  integrityHash: string;
  requirements: SupplierEvidenceRequirement[];
  blockedReasons: string[];
}

export interface SupplierEvidencePackPage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: SupplierEvidencePackListItem[];
}

export interface SaveSupplierEvidencePack {
  packCode: string;
  name: string;
  description?: string;
  category: SupplierRegistrationCategory;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  sourceConfigurationProfileId: string;
  workflowDefinitionId: string;
  changeSummary?: string;
  requirements: SaveSupplierEvidenceRequirement[];
  rowVersion?: string;
}

export interface SupplierEvidencePackOption {
  id: string;
  code: string;
  name: string;
  version: number;
  steps?: { order: number; name: string }[];
}

export interface SupplierEvidenceReference {
  referenceKind: ProcurementControlEvidenceReferenceKind;
  referenceId?: string;
  reference?: string;
  label?: string;
  requirementKey?: string;
}

export interface SupplierEvidencePackLifecycleRequest {
  rowVersion: string;
  comment?: string;
  evidence: SupplierEvidenceReference[];
}

export interface CloneSupplierEvidencePackRequest {
  rowVersion: string;
  changeSummary: string;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
}

export interface SupplierEvidenceRequirementReadiness {
  requirementCode: string;
  name: string;
  isMandatory: boolean;
  isSatisfied: boolean;
  kind: SupplierEvidenceRequirementKind;
  documentType?: string;
  classificationScheme?: string;
  allowedClassifications: string[];
  validityMode: SupplierEvidenceValidityMode;
  minimumRemainingDays?: number;
  maxFileSizeBytes: number;
  allowedMimeTypes: string[];
  approvalStepName: string;
  approvalStepOrder: number;
  matchedDocumentName?: string;
  classificationCode?: string;
  expiresAtUtc?: string;
  issues: string[];
}

export interface SupplierEvidenceReadiness {
  registrationId: string;
  category?: SupplierRegistrationCategory;
  packVersionId?: string;
  packCode?: string;
  packVersion?: number;
  isBound: boolean;
  isReady: boolean;
  evaluatedAtUtc: string;
  requirements: SupplierEvidenceRequirementReadiness[];
  blockingReasons: string[];
}
