export type SupplierPerformanceEligibilityAction =
  | 'AlertOnly'
  | 'EscalationRequired'
  | 'AwardHardStop';
export type SupplierPerformanceDataStatus =
  | 'Complete'
  | 'InsufficientCoverage'
  | 'NoQualifyingActivity';
export type SupplierPerformanceMetric =
  | 'DeliveryTimeliness'
  | 'GrnQuality'
  | 'RejectionRate'
  | 'PriceCompetitiveness'
  | 'Responsiveness'
  | 'ComplaintResolution'
  | 'ContractCompletion';

export interface SupplierPerformanceSummary {
  supplierCount: number;
  scoredSupplierCount: number;
  currentScorecardCount: number;
  belowMinimumCount: number;
  insufficientCoverageCount: number;
  policyAvailable: boolean;
  policyProfileCode?: string;
  policyProfileVersion?: number;
  performanceWindowMonths?: number;
  minimumScore?: number;
  minimumDataCoveragePercent?: number;
  responseTargetHours?: number;
  eligibilityAction?: SupplierPerformanceEligibilityAction;
  policyReleaseGate?: string;
}

export interface SupplierPerformanceListItem {
  id: string;
  scorecardReference: string;
  scorecardSequence: number;
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  calculatedAtUtc: string;
  periodStartUtc: string;
  periodEndUtc: string;
  nextReviewDueAtUtc: string;
  overallScore?: number;
  performanceBand?: string;
  dataStatus: SupplierPerformanceDataStatus;
  dataCoveragePercent: number;
  minimumScoreBreached: boolean;
  isCurrent: boolean;
  integrityHash: string;
}

export interface SupplierPerformanceMeasure {
  metric: SupplierPerformanceMetric;
  weightPercent: number;
  score?: number;
  appliedWeightPercent?: number;
  weightedContribution?: number;
  observationCount: number;
  numerator?: number;
  denominator?: number;
  unit: string;
  source: string;
  missingReason?: string;
}

export interface SupplierPerformanceFinding {
  code: string;
  message: string;
  breach: boolean;
  dataGap: boolean;
}

export interface SupplierPerformanceScorecard
  extends SupplierPerformanceListItem {
  policyDecisionId: string;
  policyProfileId: string;
  policyProfileCode: string;
  policyProfileVersion: number;
  policyValueHash: string;
  performanceWindowMonths: number;
  minimumScore: number;
  minimumDataCoveragePercent: number;
  responseTargetHours: number;
  eligibilityAction: SupplierPerformanceEligibilityAction;
  sourceSnapshotHash: string;
  supplierEligibilityDecisionHash: string;
  riskAssessmentId?: string;
  riskAssessmentIntegrityHash?: string;
  sourceType?: string;
  sourceId?: string;
  sourceReference?: string;
  calculatedByUserId: string;
  calculatedByName: string;
  measures: SupplierPerformanceMeasure[];
  findings: SupplierPerformanceFinding[];
  decisionKeys: string[];
}

export interface SupplierPerformancePage {
  items: SupplierPerformanceListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface SupplierPerformanceSearch {
  search?: string;
  businessPartnerId?: string;
  performanceBand?: string;
  dataStatus?: SupplierPerformanceDataStatus;
  belowMinimum?: boolean;
  page?: number;
  pageSize?: number;
}

export interface SupplierPerformanceCurrentState {
  businessPartnerId: string;
  partnerCode?: string;
  partnerName?: string;
  policyAvailable: boolean;
  policyReleaseGate?: string;
  scorecard?: SupplierPerformanceScorecard;
  current: boolean;
  awardBlocked: boolean;
  awardBlockReasons: string[];
}

export interface CalculateSupplierPerformance {
  businessPartnerId: string;
  idempotencyKey: string;
  sourceType?: string;
  sourceId?: string;
  sourceReference?: string;
}

export interface SupplierPerformanceSupplierOption {
  id: string;
  code: string;
  name: string;
}
