import type { ProcurementPolicyOverrideAction, ProcurementSodEnforcement } from '@/types/procurement-policy';

export interface ProcurementSodRequiredControl {
  code: string;
  name: string;
  initiatorRole: string;
  conflictingRole: string;
  entityType: string;
  action: string;
  explanation: string;
  sourceRequirement: string;
  sourceDecisionKey: string;
  isConfigured: boolean;
  isEffective: boolean;
  isHardStop: boolean;
  ruleId?: string;
  ruleCode?: string;
  enforcement?: ProcurementSodEnforcement;
  sourceRuleId?: string;
  overrideAction?: ProcurementPolicyOverrideAction;
  configurationIssue?: string;
}

export interface ProcurementSodCoverage {
  evaluatedAtUtc: string;
  currentActorUserId: string;
  currentActorRoles: string[];
  status: 'Complete' | 'Incomplete' | 'NoEffectivePolicy' | 'AmbiguousPolicy';
  isComplete: boolean;
  policySetId?: string;
  policyCode?: string;
  policyName?: string;
  policyVersion?: number;
  controls: ProcurementSodRequiredControl[];
}

export interface ProcurementSodGuardRequest {
  controlCode: string;
  sourceType: string;
  sourceReference: string;
  prohibitedActorUserIds: string[];
}

export interface ProcurementSodGuardDecision {
  decisionId: string;
  correlationId: string;
  evaluatedAtUtc: string;
  allowed: boolean;
  isHardStop: boolean;
  wasAudited: boolean;
  code: string;
  message: string;
  actorUserId: string;
  actorRoles: string[];
  controlCode: string;
  sourceType: string;
  sourceReference: string;
  policySetId?: string;
  policyCode?: string;
  policyVersion?: number;
  ruleId?: string;
  ruleCode?: string;
  sourceDecisionKey?: string;
  sourceRequirement?: string;
}

export interface ProcurementSodProvisionResult {
  policySetId: string;
  createdCount: number;
  existingCount: number;
  createdControlCodes: string[];
  existingControlCodes: string[];
}

export interface ProcurementSodBypassAudit {
  id: string;
  timestamp: string;
  actorUserId: string;
  actorName: string;
  controlCode: string;
  sourceType: string;
  sourceReference: string;
  correlationId: string;
  message: string;
  ruleId?: string;
  ruleCode?: string;
}

export interface ProcurementSodGuardForm {
  controlCode: string;
  sourceType: string;
  sourceReference: string;
  prohibitedActorUserIds: string;
}
