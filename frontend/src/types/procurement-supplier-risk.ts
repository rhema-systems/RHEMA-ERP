import type { SupplierDueDiligenceEvidenceReference } from './procurement-supplier-due-diligence';

export type SupplierRiskEligibilityAction =
  | 'AlertOnly'
  | 'EscalationRequired'
  | 'AwardHardStop';
export type SupplierRiskAlertType =
  | 'DataIncomplete'
  | 'MinimumScore'
  | 'Concentration'
  | 'SingleSourceDependency';
export type SupplierRiskAlertStatus = 'Open' | 'Escalated' | 'Resolved';

export interface SupplierRiskSummary {
  supplierCount: number;
  assessedSupplierCount: number;
  currentAssessmentCount: number;
  overdueAssessmentCount: number;
  openAlertCount: number;
  escalatedAlertCount: number;
  awardBlockedSupplierCount: number;
  policyAvailable: boolean;
  policyProfileCode?: string;
  policyProfileVersion?: number;
  exposureWindowMonths?: number;
  concentrationLimitPercent?: number;
  minimumScore?: number;
  eligibilityAction?: SupplierRiskEligibilityAction;
  policyReleaseGate?: string;
}

export interface SupplierRiskListItem {
  id: string;
  assessmentReference: string;
  assessmentSequence: number;
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  assessedAtUtc: string;
  nextReviewDueAtUtc: string;
  riskScore?: number;
  riskBand?: string;
  maximumSpendSharePercent: number;
  singleSourceCategoryCount: number;
  dataComplete: boolean;
  isCurrent: boolean;
  openAlertCount: number;
  escalatedAlertCount: number;
  awardBlocked: boolean;
  integrityHash: string;
}

export interface SupplierRiskDimension {
  dimension: string;
  weightPercent: number;
  score?: number;
  weightedScore?: number;
  source: string;
  missingReason?: string;
}

export interface SupplierSpendExposure {
  currencyCode: string;
  supplierAmount: number;
  tenantAmount: number;
  spendSharePercent: number;
  supplierOrderCount: number;
  tenantOrderCount: number;
}

export interface SupplierCategoryExposure {
  categoryId?: string;
  categoryCode: string;
  categoryName: string;
  currencyCode: string;
  supplierAmount: number;
  categoryAmount: number;
  spendSharePercent: number;
  distinctSupplierCount: number;
  isSingleSource: boolean;
}

export interface SupplierRiskFinding {
  code: string;
  message: string;
  breach: boolean;
  dataGap: boolean;
}

export interface SupplierRiskAlert {
  id: string;
  assessmentId: string;
  alertType: SupplierRiskAlertType;
  status: SupplierRiskAlertStatus;
  ruleCode: string;
  severity: string;
  message: string;
  openedAtUtc: string;
  workflowDefinitionId?: string;
  workflowInstanceId?: string;
  escalatedById?: string;
  escalatedAtUtc?: string;
  escalationReason?: string;
  resolvedById?: string;
  resolvedAtUtc?: string;
  resolutionReason?: string;
  rowVersion: string;
  integrityHash: string;
}

export interface SupplierRiskAssessment extends SupplierRiskListItem {
  periodStartUtc: string;
  periodEndUtc: string;
  policyDecisionId: string;
  policyProfileId: string;
  policyProfileCode: string;
  policyProfileVersion: number;
  policyValueHash: string;
  exposureWindowMonths: number;
  minimumScore: number;
  concentrationLimitPercent: number;
  eligibilityAction: SupplierRiskEligibilityAction;
  minimumScoreBreached: boolean;
  concentrationBreached: boolean;
  singleSourceDependency: boolean;
  eligibilityDecisionHash: string;
  sourceType?: string;
  sourceId?: string;
  sourceReference?: string;
  assessedByUserId: string;
  assessedByName: string;
  dimensions: SupplierRiskDimension[];
  spendExposure: SupplierSpendExposure[];
  categoryExposure: SupplierCategoryExposure[];
  findings: SupplierRiskFinding[];
  alerts: SupplierRiskAlert[];
  decisionKeys: string[];
}

export interface SupplierRiskPage {
  items: SupplierRiskListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface SupplierRiskSearch {
  search?: string;
  businessPartnerId?: string;
  riskBand?: string;
  hasOpenAlerts?: boolean;
  page?: number;
  pageSize?: number;
}

export interface SupplierRiskCurrentState {
  businessPartnerId: string;
  partnerCode?: string;
  partnerName?: string;
  policyAvailable: boolean;
  policyReleaseGate?: string;
  assessment?: SupplierRiskAssessment;
  current: boolean;
  awardBlocked: boolean;
  awardBlockReasons: string[];
}

export interface EvaluateSupplierRisk {
  businessPartnerId: string;
  idempotencyKey: string;
  sourceType?: string;
  sourceId?: string;
  sourceReference?: string;
}

export interface EscalateSupplierRiskAlert {
  workflowDefinitionId: string;
  reason: string;
  rowVersion: string;
  evidence: SupplierDueDiligenceEvidenceReference[];
}

export interface ResolveSupplierRiskAlert {
  reason: string;
  rowVersion: string;
  evidence: SupplierDueDiligenceEvidenceReference[];
}

export interface SupplierRiskSupplierOption {
  id: string;
  code: string;
  name: string;
}

export interface SupplierRiskWorkflowOption {
  id: string;
  name: string;
  version: number;
}
