import type { ProcurementConfigurationPagedResult } from '@/types/procurement-configuration';

export type ProcurementPolicyLifecycleStatus = 'Draft' | 'Published' | 'Retired';
export type ProcurementPolicyScopeType = 'TenantBaseline' | 'TenantOverride';
export type ProcurementPolicyRuleKind = 'Category' | 'Method' | 'Threshold' | 'Authority' | 'Evidence' | 'Exception' | 'SegregationOfDuties';
export type ProcurementPolicyOverrideAction = 'Add' | 'Replace' | 'Disable';
export type ProcurementCategoryClass = 'Goods' | 'Works' | 'TechnicalServices' | 'ConsultancyServices' | 'GeneralServices';
export type ProcurementMethodType = 'RequestForQuotation' | 'NationalCompetitiveTendering' | 'InternationalCompetitiveTendering' | 'RestrictedTendering' | 'SingleSource' | 'PettyPurchase' | 'FrameworkCallOff' | 'QualityBasedSelection' | 'QualityAndCostBasedSelection';
export type ProcurementEvidenceStage = 'Requisition' | 'Sourcing' | 'Evaluation' | 'Award' | 'Contract' | 'PurchaseOrder' | 'Receipt' | 'Invoice' | 'Payment' | 'Inventory';
export type ProcurementExceptionDisposition = 'Prohibited' | 'ApprovalRequired' | 'Permitted';
export type ProcurementSodEnforcement = 'HardStop' | 'ApprovalRequired' | 'Warning';

export interface ProcurementPolicyValidationIssue {
  code: string;
  message: string;
  ruleKind?: ProcurementPolicyRuleKind;
  ruleId?: string;
  ruleCode?: string;
  severity: string;
}

export interface ProcurementPolicyValidationResult {
  isValid: boolean;
  errors: ProcurementPolicyValidationIssue[];
  warnings: ProcurementPolicyValidationIssue[];
}

export interface ProcurementPolicyRoleOption {
  id: string;
  name: string;
  description?: string;
  isSystemRole: boolean;
  isAssignedToSelectedWorkflow: boolean;
}

export interface ProcurementPolicyRuleValue {
  ruleCode: string;
  priority: number;
  isEnabled: boolean;
  effectiveFrom: string;
  effectiveTo?: string;
  overrideAction: ProcurementPolicyOverrideAction;
  sourceRuleId?: string;
  sourceDecisionKey: string;
  [key: string]: unknown;
}

export interface ProcurementPolicyRule {
  id: string;
  kind: ProcurementPolicyRuleKind;
  ruleCode: string;
  name: string;
  priority: number;
  isEnabled: boolean;
  effectiveFrom: string;
  effectiveTo?: string;
  overrideAction: ProcurementPolicyOverrideAction;
  sourceRuleId?: string;
  sourceDecisionKey: string;
  value: ProcurementPolicyRuleValue;
  rowVersion: string;
}

export interface ProcurementPolicyRevision {
  id: string;
  policySetId: string;
  ruleId?: string;
  ruleKind?: ProcurementPolicyRuleKind;
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

export interface ProcurementPolicySetSummary {
  id: string;
  policyKey: string;
  code: string;
  name: string;
  version: number;
  lifecycleStatus: ProcurementPolicyLifecycleStatus;
  scopeType: ProcurementPolicyScopeType;
  sourceConfigurationProfileId: string;
  sourceConfigurationProfileCode: string;
  sourceConfigurationProfileVersion: number;
  defaultCurrencyCode: string;
  effectiveFrom: string;
  effectiveTo?: string;
  isDefault: boolean;
  ruleCount: number;
  ruleFamilyCount: number;
  isComplete: boolean;
  updatedBy?: string;
  updatedAt: string;
  rowVersion: string;
}

export interface ProcurementPolicySet extends ProcurementPolicySetSummary {
  description?: string;
  changeSummary?: string;
  basePolicySetId?: string;
  supersedesPolicySetId?: string;
  publishedAt?: string;
  publishedById?: string;
  retiredAt?: string;
  retiredById?: string;
  rules: ProcurementPolicyRule[];
  validation: ProcurementPolicyValidationResult;
  recentHistory: ProcurementPolicyRevision[];
}

export interface CreateProcurementPolicySetRequest {
  sourceConfigurationProfileId: string;
  code: string;
  name: string;
  description?: string;
  scopeType: ProcurementPolicyScopeType;
  basePolicySetId?: string;
  defaultCurrencyCode: string;
  effectiveFrom: string;
  effectiveTo?: string;
  changeSummary?: string;
  isDefault: boolean;
}

export interface UpdateProcurementPolicySetRequest {
  name: string;
  description?: string;
  defaultCurrencyCode: string;
  effectiveFrom: string;
  effectiveTo?: string;
  changeSummary?: string;
  isDefault: boolean;
  rowVersion: string;
  reason?: string;
}

export interface SaveProcurementPolicyRuleRequest {
  kind: ProcurementPolicyRuleKind;
  category?: ProcurementPolicyRuleValue;
  method?: ProcurementPolicyRuleValue;
  threshold?: ProcurementPolicyRuleValue;
  authority?: ProcurementPolicyRuleValue;
  evidence?: ProcurementPolicyRuleValue;
  exception?: ProcurementPolicyRuleValue;
  segregationOfDuties?: ProcurementPolicyRuleValue;
  rowVersion?: string;
  reason?: string;
}

export interface ProcurementPolicyLifecycleRequest {
  rowVersion: string;
  reason?: string;
}

export type ProcurementPolicyPagedResult = ProcurementConfigurationPagedResult<ProcurementPolicySetSummary>;
