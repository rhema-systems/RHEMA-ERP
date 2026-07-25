import type {
  ProcurementControlEventResult,
  ProcurementControlEvidenceReferenceKind,
} from './procurement-control-event';

export type ProcurementSpecificationTemplateKind =
  | 'Goods'
  | 'Works'
  | 'Services';
export type ProcurementSpecificationTemplateStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Published'
  | 'Retired';

export interface ProcurementSpecificationTemplateSearch {
  search?: string;
  kind?: ProcurementSpecificationTemplateKind;
  status?: ProcurementSpecificationTemplateStatus;
  effectiveOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ProcurementSpecificationTemplateSummary {
  templateFamilyCount: number;
  draftCount: number;
  pendingApprovalCount: number;
  publishedCount: number;
  effectiveCount: number;
  byKind: Record<string, number>;
}

export interface ProcurementSpecificationTemplateListItem {
  id: string;
  templateKey: string;
  templateCode: string;
  name: string;
  kind: ProcurementSpecificationTemplateKind;
  version: number;
  status: ProcurementSpecificationTemplateStatus;
  isDefault: boolean;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  isEffective: boolean;
  isPublicationReady: boolean;
  workflowDefinitionName?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
  rowVersion: string;
}

export interface ProcurementSpecificationTemplatePage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: ProcurementSpecificationTemplateListItem[];
}

export interface ProcurementSpecificationTemplateValidationIssue {
  code: string;
  field: string;
  message: string;
}

export interface ProcurementSpecificationTemplateEvidence {
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

export interface ProcurementSpecificationTemplateTimelineEvent {
  id: string;
  action: string;
  result: ProcurementControlEventResult;
  actorName: string;
  reason?: string;
  occurredAtUtc: string;
  integrityHash: string;
  evidence: ProcurementSpecificationTemplateEvidence[];
}

export interface ProcurementSpecificationTemplate
  extends ProcurementSpecificationTemplateListItem {
  description?: string;
  changeSummary?: string;
  purpose: string;
  functionalAndPerformanceRequirements: string;
  processAndMaterialsRequirements: string;
  dimensionsAndMarkingRequirements: string;
  testingAndInspectionRequirements: string;
  applicableStandards: string;
  deliverables: string;
  acceptanceCriteria: string;
  workflowDefinitionId?: string;
  workflowDefinitionVersion?: number;
  workflowInstanceId?: string;
  supersedesTemplateId?: string;
  submittedAtUtc?: string;
  submittedById?: string;
  submittedByName?: string;
  publishedAtUtc?: string;
  publishedById?: string;
  publishedByName?: string;
  retiredAtUtc?: string;
  retiredById?: string;
  retiredByName?: string;
  reviewComment?: string;
  revisionNumber: number;
  validationIssues: ProcurementSpecificationTemplateValidationIssue[];
  timeline: ProcurementSpecificationTemplateTimelineEvent[];
}

export interface SaveProcurementSpecificationTemplate {
  templateCode: string;
  name: string;
  description?: string;
  kind: ProcurementSpecificationTemplateKind;
  isDefault: boolean;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  changeSummary?: string;
  purpose: string;
  functionalAndPerformanceRequirements: string;
  processAndMaterialsRequirements: string;
  dimensionsAndMarkingRequirements: string;
  testingAndInspectionRequirements: string;
  applicableStandards: string;
  deliverables: string;
  acceptanceCriteria: string;
  workflowDefinitionId?: string;
  rowVersion?: string;
}

export interface ProcurementSpecificationEvidenceReference {
  referenceKind: ProcurementControlEvidenceReferenceKind;
  referenceId?: string;
  reference?: string;
  label?: string;
  requirementKey?: string;
}

export interface ProcurementSpecificationTemplateLifecycleRequest {
  rowVersion: string;
  comment?: string;
  evidence?: ProcurementSpecificationEvidenceReference[];
}

export interface CloneProcurementSpecificationTemplateRequest {
  rowVersion: string;
  effectiveFromUtc: string;
  effectiveToUtc?: string;
  changeSummary: string;
}

export interface ProcurementSpecificationWorkflowOption {
  id: string;
  name: string;
  version: number;
  entityTypeName: string;
}
