import type { ProcurementMethodType } from './procurement-policy';

export type ProcurementAwardReadinessSourceType =
  | 'RequestForQuotation'
  | 'Tender'
  | 'ExceptionalSourcing';

export type ProcurementAwardReadinessDecisionStatus = 'Blocked' | 'Ready';

export type ProcurementAwardReadinessPrerequisiteGroup =
  | 'Source'
  | 'Recommendation'
  | 'Evaluation'
  | 'ScoreIntegrity'
  | 'SupplierEligibility'
  | 'Prequalification'
  | 'VerificationAndDueDiligence'
  | 'AuthorityAndWorkflow'
  | 'Evidence';

export type ProcurementAwardReadinessPrerequisiteStatus =
  | 'Passed'
  | 'Failed'
  | 'NotApplicable';

export interface EvaluateProcurementAwardReadinessRequest {
  idempotencyKey: string;
  expectedRecommendedSubjectIds: string[];
  expectedBusinessPartnerIds: string[];
  expectedSourceIntegrityHash?: string;
}

export interface ProcurementAwardReadinessRecommendation {
  subjectType: string;
  subjectIds: string[];
  businessPartnerIds: string[];
  reason?: string;
  evidenceReference?: string;
  recommendedAtUtc?: string;
  recommendedByUserId?: string;
}

export interface ProcurementAwardReadinessScoreAttempt {
  scoreSheetId: string;
  committeeControlId: string;
  meetingId: string;
  appointmentId: string;
  phase: 'Technical' | 'Financial' | 'Combined';
  scoreSubjectType: string;
  scoreSubjectId: string;
  attempt: number;
  status: 'Locked' | 'Recalled';
  submittedAtUtc: string;
  submittedByUserId: string;
  submittedByName: string;
  evidenceReference: string;
  integrityHash: string;
  recallIds: string[];
}

export interface ProcurementAwardReadinessEvaluation {
  evaluationType: string;
  evaluationId: string;
  phase: 'Technical' | 'Financial' | 'Combined';
  status: string;
  completedAtUtc?: string;
  evidenceReference?: string;
  integrityHash?: string;
  scoreAttempts: ProcurementAwardReadinessScoreAttempt[];
}

export interface ProcurementAwardReadinessPrequalification {
  entryId: string;
  exerciseId: string;
  applicationId: string;
  categoryId: string;
  status: 'Active' | 'Expired' | 'Revoked';
  validFromUtc: string;
  expiresAtUtc: string;
  approvalReference: string;
  approvalEvidenceReference: string;
  integrityHash: string;
}

export interface ProcurementAwardReadinessSupplier {
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  isEligible: boolean;
  validationCode: string;
  errors: string[];
  warnings: string[];
  prequalification: ProcurementAwardReadinessPrequalification[];
}

export interface ProcurementAwardReadinessVerification {
  verificationId: string;
  verificationBidderId: string;
  tenderBidId: string;
  businessPartnerId: string;
  templateId?: string;
  verificationStatus: string;
  bidderStatus: string;
  completedAtUtc?: string;
  itemResultIds: string[];
  documentIds: string[];
  snapshotIntegrityHash: string;
}

export interface ProcurementAwardReadinessAuthority {
  methodRuleId?: string;
  methodRuleCode?: string;
  authorityRouteId?: string;
  authorityRouteReference?: string;
  workflowDefinitionId?: string;
  workflowInstanceId?: string;
  workflowStatus?: string;
  approvalReference?: string;
  approvedAtUtc?: string;
  approvedByUserId?: string;
  approvalActorUserIds: string[];
}

export interface ProcurementAwardReadinessEvidence {
  requirementKey: string;
  label: string;
  referenceId?: string;
  reference?: string;
  isAvailable: boolean;
}

export interface ProcurementAwardReadinessPrerequisiteItem {
  code: string;
  label: string;
  status: ProcurementAwardReadinessPrerequisiteStatus;
  message: string;
  remediation?: string;
  lineageType?: string;
  lineageId?: string;
  lineageHash?: string;
}

export interface ProcurementAwardReadinessPrerequisiteGroupResult {
  group: ProcurementAwardReadinessPrerequisiteGroup;
  status: ProcurementAwardReadinessPrerequisiteStatus;
  items: ProcurementAwardReadinessPrerequisiteItem[];
}

export interface ProcurementAwardReadinessTimelineEntry {
  eventType: string;
  occurredAtUtc: string;
  actorUserId?: string;
  reference?: string;
  integrityHash?: string;
}

export interface ProcurementEvaluatorAwardApproverLineage {
  family: string;
  evaluationId?: string;
  committeeControlId?: string;
  appointmentId?: string;
  scoreSheetId?: string;
  scoreSubjectType?: string;
  scoreSubjectId?: string;
  phase?: 'Technical' | 'Financial' | 'Combined';
  attempt?: number;
  scoreStatus?: 'Locked' | 'Recalled';
  isRetainedAttempt: boolean;
  isRecalledAttempt: boolean;
  evaluatorUserId: string;
  evaluatedAtUtc?: string;
  integrityHash?: string;
}

export interface ProcurementEvaluatorAwardApproverSodStatus {
  sourceType: ProcurementAwardReadinessSourceType;
  sourceId: string;
  sourceReference: string;
  allowed: boolean;
  code: string;
  message: string;
  currentActorUserId: string;
  currentActorName: string;
  currentActorRoles: string[];
  evaluatorUserIds: string[];
  independentApprovalActorUserIds: string[];
  evaluatorLineage: ProcurementEvaluatorAwardApproverLineage[];
  readinessDecisionId?: string;
  readinessDecisionSequence?: number;
  readinessSourceIntegrityHash?: string;
  readinessIntegrityHash?: string;
  readinessDecisionIsCurrent?: boolean;
  sodDecisionId: string;
  sodControlCode?: string;
  sodPolicySetId?: string;
  sodPolicyCode?: string;
  sodPolicyVersion?: number;
  sodRuleId?: string;
  sodRuleCode?: string;
  sodSourceDecisionKey?: string;
  sourceMethodRuleId?: string;
  sourceMethodRuleCode?: string;
  correlationId: string;
  evaluatedAtUtc: string;
}

export interface ProcurementAwardReadinessDecision {
  id: string;
  decisionSequence: number;
  sourceType: ProcurementAwardReadinessSourceType;
  sourceId: string;
  sourceReference: string;
  method: ProcurementMethodType;
  status: ProcurementAwardReadinessDecisionStatus;
  isReady: boolean;
  isCurrent: boolean;
  sourceIntegrityHash: string;
  integrityHash: string;
  idempotencyKey: string;
  correlationId: string;
  evaluatedAtUtc: string;
  evaluatedByUserId: string;
  evaluatedByName: string;
  recommendation: ProcurementAwardReadinessRecommendation;
  evaluations: ProcurementAwardReadinessEvaluation[];
  suppliers: ProcurementAwardReadinessSupplier[];
  verifications: ProcurementAwardReadinessVerification[];
  authority: ProcurementAwardReadinessAuthority;
  evidence: ProcurementAwardReadinessEvidence[];
  prerequisiteGroups: ProcurementAwardReadinessPrerequisiteGroupResult[];
  timeline: ProcurementAwardReadinessTimelineEntry[];
  blockedReasons: string[];
  allowedActions: string[];
}
