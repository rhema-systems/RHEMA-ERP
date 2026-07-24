export enum ProcurementPrequalificationStatus {
  Draft = 0,
  Advertised = 1,
  Closed = 2,
  UnderEvaluation = 3,
  PendingApproval = 4,
  Approved = 5,
  Rejected = 6,
  Expired = 7,
}

export enum ProcurementPrequalificationApplicationStatus {
  Submitted = 0,
  EvaluatedQualified = 1,
  EvaluatedRejected = 2,
  Approved = 3,
  Rejected = 4,
}

export enum ProcurementQualifiedListEntryStatus {
  Active = 0,
  Expired = 1,
  Revoked = 2,
}

export interface ProcurementPrequalificationSummary {
  id: string;
  reference: string;
  title: string;
  status: ProcurementPrequalificationStatus;
  opensAtUtc: string;
  closesAtUtc: string;
  applicationCount: number;
  qualifiedCount: number;
  expiresAtUtc?: string;
}

export interface ProcurementPrequalificationCriterion {
  id: string;
  code: string;
  name: string;
  description?: string;
  weight: number;
  minimumScore: number;
  isMandatory: boolean;
  requiresEvidence: boolean;
  sortOrder: number;
}

export interface ProcurementPrequalificationApplication {
  id: string;
  applicationNumber: string;
  businessPartnerId: string;
  supplierName: string;
  status: ProcurementPrequalificationApplicationStatus;
  submittedAtUtc: string;
  evaluatedAtUtc?: string;
  totalScore?: number;
  passed?: boolean;
  evaluationRemarks?: string;
  recommendationEvidenceReference?: string;
  rowVersion: string;
  categoryIds: string[];
  evidence: Array<{ criterionCode: string; evidenceReference: string; verificationReference: string }>;
  scores: Array<{
    criterionId: string;
    criterionCode: string;
    criterionName: string;
    score: number;
    meetsRequirement: boolean;
    reason: string;
    evidenceReference?: string;
    evaluatedAtUtc: string;
    evaluatedById: string;
  }>;
}

export interface ProcurementQualifiedListEntry {
  id: string;
  businessPartnerId: string;
  supplierName: string;
  categoryId: string;
  categoryCode: string;
  categoryName: string;
  validFromUtc: string;
  expiresAtUtc: string;
  status: ProcurementQualifiedListEntryStatus;
  approvalReference: string;
  integrityHash: string;
}

export interface ProcurementPrequalificationExercise extends ProcurementPrequalificationSummary {
  description: string;
  validityMonths: number;
  passingScore: number;
  policySetId: string;
  policySetCode: string;
  policySetVersion: number;
  sourceConfigurationProfileId: string;
  workflowDefinitionId: string;
  workflowInstanceId?: string;
  advertisementReference?: string;
  advertisementEvidenceReference?: string;
  advertisedAtUtc?: string;
  closedAtUtc?: string;
  submittedForApprovalAtUtc?: string;
  decisionReference?: string;
  decisionEvidenceReference?: string;
  decisionReason?: string;
  decidedAtUtc?: string;
  integrityHash: string;
  rowVersion: string;
  categories: Array<{ id: string; code: string; name: string }>;
  criteria: ProcurementPrequalificationCriterion[];
  applications: ProcurementPrequalificationApplication[];
  qualifiedEntries: ProcurementQualifiedListEntry[];
  milestones: Array<{ code: string; label: string; completedAtUtc?: string; reference?: string }>;
}

export interface ProcurementPrequalificationReadiness {
  categories: Array<{ id: string; code: string; name: string }>;
  policies: Array<{ id: string; code: string; name: string; version: number; sourceConfigurationProfileId: string }>;
  workflows: Array<{ id: string; name: string; version: number }>;
  suppliers: Array<{ id: string; code: string; name: string }>;
}

export interface CreateProcurementPrequalificationExercise {
  reference: string;
  title: string;
  description: string;
  categoryIds: string[];
  opensAtUtc: string;
  closesAtUtc: string;
  validityMonths: number;
  passingScore: number;
  policySetId: string;
  workflowDefinitionId: string;
  criteria: Array<{
    code: string;
    name: string;
    description?: string;
    weight: number;
    minimumScore: number;
    isMandatory: boolean;
    requiresEvidence: boolean;
    sortOrder: number;
  }>;
}

export interface ProcurementSupplierEligibility {
  businessPartnerId: string;
  categoryId: string;
  evaluatedAtUtc: string;
  eligible: boolean;
  code: string;
  message: string;
  entry?: ProcurementQualifiedListEntry;
}
