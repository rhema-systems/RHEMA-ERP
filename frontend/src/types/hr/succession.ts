/**
 * Area 13 — Succession planning & talent management.
 *
 * ⚠ Every type here was written from the C# DTO in `SuccessionPlanDTOs.cs`, not from the endpoint's
 * name. The area-12 lesson: a TypeScript type invented from a route is fiction that type-checks.
 *
 * ⚠ Enums arrive as **strings** ("Draft", "Critical", "HighRisk"), and the API accepts strings on
 * the way back in. The DTOs also carry `*Name` twins for each one — `status`/`statusName` hold the
 * same text — so prefer the plain field and treat the twin as redundant.
 */

export type SuccessionPlanStatus =
  | 'Draft'
  | 'UnderReview'
  | 'Approved'
  | 'Rejected'
  | 'Completed'
  | 'Archived';

export type PositionCriticality = 'Low' | 'Medium' | 'High' | 'Critical';

/** ⚠ Reads backwards from the others: `HighRisk` is the bad end, `NoRisk` the good one. */
export type SuccessionRisk = 'HighRisk' | 'MediumRisk' | 'LowRisk' | 'NoRisk';

export type ReadinessLevel =
  | 'ReadyNow'
  | 'ReadyIn12Months'
  | 'ReadyIn24Months'
  | 'ReadyIn36PlusMonths'
  | 'NotReady';

export type VacancyReason =
  | 'Retirement'
  | 'Resignation'
  | 'Promotion'
  | 'Transfer'
  | 'Termination'
  | 'Restructure'
  | 'Death'
  | 'Other';

export interface SuccessionPlanSummary {
  id: string;
  planNumber: string;
  planName: string;
  planYear: number;
  versionNumber: number;
  isActiveVersion: boolean;
  positionId: string;
  positionTitle: string;
  currentIncumbentName?: string | null;
  status: SuccessionPlanStatus;
  statusName: string;
  criticality: PositionCriticality;
  criticalityName: string;
  riskLevel: SuccessionRisk;
  riskLevelName: string;
  hasReadyNowSuccessor: boolean;
  numberOfIdentifiedSuccessors: number;
  hasEmergencySuccessor: boolean;
  nextReviewDate?: string | null;
}

export type CandidateType = 'Internal' | 'External' | 'Emergency';

/** ⚠ Same backwards reading as {@link SuccessionRisk}: `HighRisk` is bad, `Secure` is good. */
export type RetentionRisk = 'HighRisk' | 'MediumRisk' | 'LowRisk' | 'Secure';

export type PotentialRating = 'LowPotential' | 'MediumPotential' | 'HighPotential';

export type PerformanceRating =
  | 'Unsatisfactory'
  | 'BelowExpectations'
  | 'MeetsExpectations'
  | 'ExceedsExpectations'
  | 'Outstanding';

export type ActionType = 'Development' | 'Recruitment' | 'Retention' | 'Assessment' | 'Other';
export type ActionPriority = 'Critical' | 'High' | 'Medium' | 'Low';
export type ActionStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'Overdue' | 'Cancelled';

export interface SuccessionCompetencyRequirement {
  id: string;
  successionPlanId: string;
  competencyId: string;
  competencyCode: string;
  competencyName: string;
  competencyCategory: string;
  requiredLevel: number;
  /** The top of the scale `requiredLevel` is measured against — never assume 5. */
  proficiencyScaleMax: number;
}

/**
 * ⚠ Note what this does NOT have: no `successionPlanId` (it only ever arrives nested inside its
 * plan), and readiness is `currentReadiness`, not `readiness`.
 */
export interface SuccessionCandidateSummary {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  age?: number | null;
  serviceYearsLeft?: number | null;
  type: CandidateType;
  rank: number;
  currentReadiness: ReadinessLevel;
  isEmergencyOnly: boolean;
  latestPerformanceRating?: PerformanceRating | null;
  potentialRating?: PotentialRating | null;
  retentionRisk: RetentionRisk;
  isRecommended: boolean;
  isSelected: boolean;
  successionCompleted: boolean;
  feedbackCount: number;
  supportCount: number;
  opposeCount: number;
}

export interface SuccessionActionSummary {
  id: string;
  actionDescription: string;
  type: ActionType;
  priority: ActionPriority;
  status: ActionStatus;
  dueDate?: string | null;
  responsiblePersonName?: string | null;
}

export interface SuccessionDocument {
  id: string;
  successionPlanId?: string | null;
  planNumber?: string | null;
  documentName: string;
  documentType: string;
  documentUrl: string;
  description?: string | null;
  candidateId?: string | null;
  candidateEmployeeName?: string | null;
  talentPoolMemberId?: string | null;
  talentPoolMemberName?: string | null;
  uploadDate: string;
  uploadedById: string;
  uploadedByName: string;
  fileSizeBytes: number;
  fileHash?: string | null;
  isConfidential: boolean;
  retentionDate?: string | null;
}

export interface SuccessionPlan {
  id: string;
  tenantId: string;
  planNumber: string;
  planName: string;
  description?: string | null;

  positionId: string;
  positionTitle: string;

  currentIncumbentId?: string | null;
  currentIncumbentName?: string | null;
  currentIncumbentNumber?: string | null;
  /** Derived server-side from the tenant's HR policy settings, not stored. */
  incumbentAge?: number | null;
  incumbentServiceYearsLeft?: number | null;
  incumbentEffectiveRetirementDate?: string | null;

  planYear: number;
  versionNumber: number;
  isActiveVersion: boolean;
  supersededByPlanId?: string | null;
  supersededByPlanNumber?: string | null;

  status: SuccessionPlanStatus;
  statusName: string;
  criticality: PositionCriticality;
  criticalityName: string;
  riskLevel: SuccessionRisk;
  riskLevelName: string;
  riskAssessmentNotes?: string | null;
  businessImpactIfVacant?: string | null;

  incumbentRetirementDate?: string | null;
  anticipatedVacancyDate?: string | null;
  anticipatedVacancyReason?: VacancyReason | null;
  anticipatedVacancyReasonName?: string | null;
  incumbentSuccessionNotes?: string | null;

  hasReadyNowSuccessor: boolean;
  numberOfIdentifiedSuccessors: number;
  hasEmergencySuccessor: boolean;

  emergencySuccessorId?: string | null;
  emergencySuccessorName?: string | null;
  emergencyProtocol?: string | null;

  targetSuccessionDate?: string | null;
  estimatedTimeToReadyMonths?: number | null;

  reviewedById?: string | null;
  reviewedByName?: string | null;
  reviewDate?: string | null;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvalDate?: string | null;

  reviewFrequencyMonths: number;
  nextReviewDate?: string | null;

  competencyRequirements: SuccessionCompetencyRequirement[];
  candidates: SuccessionCandidateSummary[];
  actions: SuccessionActionSummary[];
  documents: SuccessionDocument[];

  createdAt: string;
  createdBy: string;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface CreateSuccessionPlan {
  planName: string;
  description?: string | null;
  positionId: string;
  currentIncumbentId?: string | null;
  planYear: number;
  criticality: PositionCriticality;
  riskLevel: SuccessionRisk;
  riskAssessmentNotes?: string | null;
  businessImpactIfVacant?: string | null;
  incumbentRetirementDate?: string | null;
  anticipatedVacancyDate?: string | null;
  anticipatedVacancyReason?: VacancyReason | null;
  incumbentSuccessionNotes?: string | null;
  emergencySuccessorId?: string | null;
  emergencyProtocol?: string | null;
  targetSuccessionDate?: string | null;
  estimatedTimeToReadyMonths?: number | null;
  reviewFrequencyMonths: number;
  nextReviewDate?: string | null;
}

export interface UpdateSuccessionPlan extends CreateSuccessionPlan {
  id: string;
}

/**
 * ⚠ Carries no actor. The reviewer is the signed-in user, taken from the token — `reviewedById`
 * was removed from the DTO in slice 1 because a caller-declared reviewer is not a reviewer.
 */
export interface ReviewSuccessionPlan {
  planId: string;
  reviewNotes?: string | null;
  newStatus: SuccessionPlanStatus;
}

/** ⚠ Carries no actor, for the reason on {@link ReviewSuccessionPlan}. */
export interface ApproveSuccessionPlan {
  planId: string;
  approvalNotes?: string | null;
}

export interface RiskHeatmapCell {
  riskLevel: SuccessionRisk;
  criticality: PositionCriticality;
  count: number;
}

/** ⚠ Keyed by `planId`, not by position — despite the name. */
export interface PositionCoverageRow {
  planId: string;
  positionTitle: string;
  currentIncumbentName?: string | null;
  riskLevel: SuccessionRisk;
  successorCount: number;
}

export interface SuccessionDashboard {
  totalActivePlans: number;
  totalCriticalPositions: number;
  positionsWithReadyNowSuccessor: number;
  highRiskPositions: number;
  positionsWithoutSuccessors: number;
  positionsWithoutEmergencyCover: number;
  coveragePercentage: number;
  riskHeatmap: RiskHeatmapCell[];
  withReadyNow: PositionCoverageRow[];
  withoutReadyNow: PositionCoverageRow[];
  totalTalentPoolMembers: number;
  talentReadyNow: number;
}

/**
 * ⚠ Returns an empty list today: no competencies exist until area 17 lands. A screen that reads it
 * must render "none defined yet" rather than treating empty as a loading state.
 */
export interface CompetencyLookup {
  id: string;
  name: string;
  code: string;
  competencyCategory: string;
  proficiencyScaleMax: number;
}
