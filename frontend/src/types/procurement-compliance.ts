import type {
  ProcurementCategoryClass,
  ProcurementEvidenceStage,
  ProcurementExceptionDisposition,
  ProcurementMethodType,
  ProcurementPolicyOverrideAction,
  ProcurementPolicyRuleKind,
  ProcurementPolicyScopeType,
} from '@/types/procurement-policy';

export type ProcurementComplianceOutcome = 'Allowed' | 'ReviewRequired' | 'Blocked';
export type ProcurementComplianceFindingSeverity = 'Information' | 'Warning' | 'HardStop';
export type ProcurementComplianceRouteStepType = 'MethodWorkflow' | 'Authority' | 'ExceptionApproval';

export interface ProcurementComplianceDecisionRequest {
  policySetId?: string;
  policyCode?: string;
  category: ProcurementCategoryClass;
  serviceClass?: string;
  amount: number;
  currencyCode: string;
  requestedMethod?: ProcurementMethodType;
  sourceType: string;
  sourceReference: string;
  atUtc?: string;
  actorUserId?: string;
  actorRoles: string[];
  sourceOwnerUserId?: string;
  sourceOwnerRoles: string[];
  entityType: string;
  action: string;
  exceptionType?: string;
  justificationProvided: boolean;
  exceptionApprovalReference?: string;
  evidenceReferenceKeys: string[];
}

export interface ProcurementCompliancePolicyOption {
  policySetId: string;
  policyCode: string;
  policyName: string;
  version: number;
  scopeType: ProcurementPolicyScopeType;
  currencyCode: string;
  effectiveFrom: string;
  effectiveTo?: string;
  isDefault: boolean;
}

export interface ProcurementCompliancePolicySelection extends ProcurementCompliancePolicyOption {
  policyKey: string;
  sourceConfigurationProfileId: string;
  basePolicySetId?: string;
  selectionReason: string;
}

export interface ProcurementComplianceFinding {
  code: string;
  message: string;
  severity: ProcurementComplianceFindingSeverity;
  ruleId?: string;
  ruleCode?: string;
  ruleKind?: ProcurementPolicyRuleKind;
  sourceDecisionKey?: string;
}

export interface ProcurementComplianceMethodCandidate {
  method: ProcurementMethodType;
  isAllowed: boolean;
  requiresCompetition: boolean;
  minimumQuotationCount: number;
  workflowDefinitionId?: string;
  applicabilityConditions?: string;
  methodRuleCode: string;
  thresholdRuleCode?: string;
  statutoryReference?: string;
  priority: number;
  matchesAmount: boolean;
  explanation: string;
}

export interface ProcurementComplianceAuthority {
  ruleId: string;
  ruleCode: string;
  authorityName: string;
  authorityRole: string;
  sequence: number;
  quorum: number;
  isObserver: boolean;
  escalationAuthority?: string;
  workflowDefinitionId?: string;
  sourceDecisionKey: string;
}

export interface ProcurementComplianceEvidence {
  ruleId: string;
  ruleCode: string;
  evidenceName: string;
  stage: ProcurementEvidenceStage;
  sharedRequirementKey?: string;
  isMandatory: boolean;
  requiresVerification: boolean;
  maximumAgeDays?: number;
  sourceDecisionKey: string;
}

export interface ProcurementComplianceRouteStep {
  stepType: ProcurementComplianceRouteStepType;
  sequence: number;
  name: string;
  responsibleRole: string;
  quorum: number;
  workflowDefinitionId?: string;
  escalationAuthority?: string;
  ruleId: string;
  ruleCode: string;
  sourceDecisionKey: string;
}

export interface ProcurementComplianceException {
  ruleId: string;
  ruleCode: string;
  exceptionName: string;
  exceptionType: string;
  disposition: ProcurementExceptionDisposition;
  justificationRequired: boolean;
  evidenceRequired: boolean;
  postAwardFilingRequired: boolean;
  approverRole: string;
  workflowDefinitionId?: string;
  maximumDurationDays?: number;
  sourceDecisionKey: string;
}

export interface ProcurementComplianceCategoryRequirement {
  ruleId: string;
  ruleCode: string;
  name: string;
  requiresSpecification: boolean;
  specificationTemplateCode?: string;
  sourceDecisionKey: string;
}

export interface ProcurementComplianceRuleReference {
  policySetId: string;
  policyCode: string;
  policyVersion: number;
  ruleId: string;
  ruleKind: ProcurementPolicyRuleKind;
  ruleCode: string;
  ruleName: string;
  sourceDecisionKey: string;
  sourceRuleId?: string;
  overrideAction: ProcurementPolicyOverrideAction;
  matchReason: string;
}

export interface ProcurementComplianceTraceStep {
  sequence: number;
  stage: string;
  result: string;
  ruleCodes: string[];
}

export interface ProcurementComplianceDecision {
  evaluationId: string;
  evaluatedAtUtc: string;
  policyDateUtc: string;
  correlationId: string;
  evaluationOnly: boolean;
  outcome: ProcurementComplianceOutcome;
  isAllowed: boolean;
  canProceed: boolean;
  policy: ProcurementCompliancePolicySelection;
  category: ProcurementCategoryClass;
  serviceClass?: string;
  amount: number;
  currencyCode: string;
  sourceType: string;
  sourceReference: string;
  actorUserId: string;
  actorRoles: string[];
  requestedMethod?: ProcurementMethodType;
  selectedMethod?: ProcurementMethodType;
  methodCandidates: ProcurementComplianceMethodCandidate[];
  categoryRequirements: ProcurementComplianceCategoryRequirement[];
  requiredAuthorities: ProcurementComplianceAuthority[];
  requiredEvidence: ProcurementComplianceEvidence[];
  route: ProcurementComplianceRouteStep[];
  applicableExceptions: ProcurementComplianceException[];
  selectedException?: ProcurementComplianceException;
  hardStops: ProcurementComplianceFinding[];
  reviewRequirements: ProcurementComplianceFinding[];
  warnings: ProcurementComplianceFinding[];
  matchedRules: ProcurementComplianceRuleReference[];
  trace: ProcurementComplianceTraceStep[];
}

export interface ProcurementComplianceSimulatorForm {
  policySetId: string;
  category: ProcurementCategoryClass;
  serviceClass: string;
  amount: string;
  currencyCode: string;
  requestedMethod: ProcurementMethodType | 'Auto';
  sourceType: string;
  sourceReference: string;
  atDate: string;
  actorUserId: string;
  actorRoles: string;
  sourceOwnerUserId: string;
  sourceOwnerRoles: string;
  entityType: string;
  action: string;
  exceptionType: string;
  justificationProvided: boolean;
  exceptionApprovalReference: string;
  evidenceReferenceKeys: string;
}
