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

// ── Candidates (slice 4) ─────────────────────────────────────────────────────

export type GapStatus = 'Gap' | 'Met' | 'Exceeded';
export type FeedbackDisposition = 'Support' | 'Neutral' | 'Oppose';

export interface SuccessionCandidateGap {
  id: string;
  candidateId: string;
  competencyId: string;
  competencyCode: string;
  competencyName: string;
  competencyCategory: string;
  competencyCategoryName: string;
  requiredLevel: number;
  currentLevel: number;
  /** Server-computed. Do not recalculate it in the UI — the two would drift. */
  gapSize: number;
  status: GapStatus;
  statusName: string;
  gapNotes?: string | null;
  addressed: boolean;
  addressedDate?: string | null;
  addressedByActivityId?: string | null;
  addressedByActivityName?: string | null;
}

export interface SuccessionCandidateFeedback {
  id: string;
  candidateId: string;
  reviewerId: string;
  reviewerName: string;
  note: string;
  disposition: FeedbackDisposition;
  dispositionName: string;
  createdAt: string;
}

export interface SuccessionDevelopmentActivitySummary {
  id: string;
  activityName: string;
  type: string;
  status: string;
  plannedStartDate: string;
  plannedEndDate?: string | null;
  competencyGained: boolean;
}

export interface SuccessionCandidate {
  id: string;
  tenantId: string;
  successionPlanId: string;
  planNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  employeePosition?: string | null;
  employeeDepartment?: string | null;
  dateOfBirth?: string | null;
  age?: number | null;
  yearsOfService?: number | null;
  retirementDate?: string | null;
  serviceYearsLeft?: number | null;
  talentPoolMemberId?: string | null;
  talentPoolName?: string | null;
  type: CandidateType;
  typeName: string;
  rank: number;
  currentReadiness: ReadinessLevel;
  currentReadinessName: string;
  readyByDate?: string | null;
  monthsToReady?: number | null;
  isEmergencyOnly: boolean;
  latestPerformanceRating?: PerformanceRating | null;
  latestPerformanceRatingName?: string | null;
  potentialRating?: PotentialRating | null;
  potentialRatingName?: string | null;
  talentReviewRatingId?: string | null;
  strengths?: string | null;
  developmentGaps?: string | null;
  developmentPlan?: string | null;
  yearsInCurrentRole: number;
  yearsWithCompany: number;
  hasRelevantExperience: boolean;
  relevantExperienceDetails?: string | null;
  willingToRelocate: boolean;
  availableForPromotion: boolean;
  availableFrom?: string | null;
  retentionRisk: RetentionRisk;
  retentionRiskName: string;
  riskMitigationPlan?: string | null;
  assessedById?: string | null;
  assessedByName?: string | null;
  assessmentDate?: string | null;
  assessmentNotes?: string | null;
  /** ⚠ The gate on selection: only a recommended candidate can be selected. */
  isRecommended: boolean;
  recommendationNotes?: string | null;
  recommendationDate?: string | null;
  recommendedById?: string | null;
  recommendedByName?: string | null;
  isSelected: boolean;
  selectionDate?: string | null;
  successionCompleted: boolean;
  successionDate?: string | null;
  developmentActivities: SuccessionDevelopmentActivitySummary[];
  competencyGaps: SuccessionCandidateGap[];
  feedback: SuccessionCandidateFeedback[];
}

export interface CreateSuccessionCandidate {
  successionPlanId: string;
  employeeId: string;
  talentPoolMemberId?: string | null;
  type: CandidateType;
  rank: number;
  currentReadiness: ReadinessLevel;
  readyByDate?: string | null;
  monthsToReady?: number | null;
  isEmergencyOnly: boolean;
  latestPerformanceRating?: PerformanceRating | null;
  potentialRating?: PotentialRating | null;
  talentReviewRatingId?: string | null;
  strengths?: string | null;
  developmentGaps?: string | null;
  developmentPlan?: string | null;
  yearsInCurrentRole: number;
  yearsWithCompany: number;
  hasRelevantExperience: boolean;
  relevantExperienceDetails?: string | null;
  willingToRelocate: boolean;
  availableForPromotion: boolean;
  availableFrom?: string | null;
  retentionRisk: RetentionRisk;
  riskMitigationPlan?: string | null;
}

/** ⚠ No `successionPlanId` and no `employeeId` — neither can be changed after nomination. */
export type UpdateSuccessionCandidate = Omit<
  CreateSuccessionCandidate,
  'successionPlanId' | 'employeeId'
>;

/**
 * ⚠ Carries no assessor and no date. Both were removed in slice 4: an assessment naming someone
 * else as the assessor was stored, and since `isRecommended` gates selection, that was a way to
 * manufacture a recommendation in a colleague's name.
 */
export interface AssessCandidate {
  candidateId: string;
  assessmentNotes?: string | null;
  isRecommended: boolean;
  recommendationNotes?: string | null;
}

export interface CandidateRankUpdate {
  id: string;
  rank: number;
}

export interface CreateSuccessionCandidateFeedback {
  note: string;
  disposition: FeedbackDisposition;
}

export interface CreateSuccessionCandidateGap {
  candidateId: string;
  competencyId: string;
  requiredLevel: number;
  currentLevel: number;
  gapNotes?: string | null;
}

// ── Development activities (slice 5) ─────────────────────────────────────────

export type DevelopmentActivityType =
  | 'Training'
  | 'Mentoring'
  | 'JobRotation'
  | 'ProjectAssignment'
  | 'Shadowing'
  | 'ActingRole'
  | 'ExternalExperience'
  | 'Certification'
  | 'Other';

export type DevelopmentActivityStatus =
  | 'Planned'
  | 'InProgress'
  | 'Completed'
  | 'Deferred'
  | 'Cancelled';

export interface SuccessionDevelopmentMilestone {
  id: string;
  activityId: string;
  milestoneName: string;
  targetDate: string;
  completedDate?: string | null;
  isCompleted: boolean;
  notes?: string | null;
}

export interface SuccessionDevelopmentActivity {
  id: string;
  tenantId: string;
  /** Exactly one of these two is set — an activity belongs to a candidate OR a pool member. */
  candidateId?: string | null;
  candidateEmployeeName?: string | null;
  talentPoolMemberId?: string | null;
  talentPoolMemberName?: string | null;
  activityName: string;
  type: DevelopmentActivityType;
  description?: string | null;
  plannedStartDate: string;
  plannedEndDate?: string | null;
  actualStartDate?: string | null;
  actualEndDate?: string | null;
  status: DevelopmentActivityStatus;
  outcome?: string | null;
  competencyGained: boolean;
  /** ⚠ Money. `currencyCode` is validated against Finance's currency master — see slice 5. */
  estimatedCost?: number | null;
  actualCost?: number | null;
  currencyCode?: string | null;
  supervisorId?: string | null;
  supervisorName?: string | null;
  externalProviderContactId?: string | null;
  externalProviderName?: string | null;
  notes?: string | null;
  milestones: SuccessionDevelopmentMilestone[];
  addressedGaps: SuccessionCandidateGap[];
}

export interface CreateSuccessionDevelopmentActivity {
  candidateId?: string | null;
  talentPoolMemberId?: string | null;
  activityName: string;
  type: DevelopmentActivityType;
  description?: string | null;
  plannedStartDate: string;
  plannedEndDate?: string | null;
  status: DevelopmentActivityStatus;
  estimatedCost?: number | null;
  currencyCode?: string | null;
  supervisorId?: string | null;
  externalProviderContactId?: string | null;
  externalProviderName?: string | null;
  notes?: string | null;
}

export interface UpdateSuccessionDevelopmentActivity {
  id: string;
  activityName: string;
  type: DevelopmentActivityType;
  description?: string | null;
  plannedStartDate: string;
  plannedEndDate?: string | null;
  actualStartDate?: string | null;
  actualEndDate?: string | null;
  status: DevelopmentActivityStatus;
  outcome?: string | null;
  competencyGained: boolean;
  estimatedCost?: number | null;
  actualCost?: number | null;
  currencyCode?: string | null;
  supervisorId?: string | null;
  externalProviderContactId?: string | null;
  externalProviderName?: string | null;
  notes?: string | null;
}

export interface CreateSuccessionDevelopmentMilestone {
  activityId: string;
  milestoneName: string;
  targetDate: string;
  notes?: string | null;
}

// ── Talent pools (slice 6) ───────────────────────────────────────────────────

export interface TalentPoolTypeDefinition {
  id: string;
  name: string;
  description?: string | null;
  colorHex?: string | null;
  isActive: boolean;
  displayOrder: number;
}

export interface TalentPoolMemberSummary {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  employeePosition?: string | null;
  rank: number;
  readiness: ReadinessLevel;
  readinessName: string;
  latestPerformanceRating?: PerformanceRating | null;
  latestPotentialRating?: PotentialRating | null;
  isActive: boolean;
  enrolledDate: string;
}

export interface TalentPoolMember {
  id: string;
  tenantId: string;
  talentPoolId: string;
  talentPoolName: string;
  talentPoolTypeName: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  employeePosition?: string | null;
  employeeDepartment?: string | null;
  rank: number;
  readiness: ReadinessLevel;
  readyByDate?: string | null;
  justification?: string | null;
  strengths?: string | null;
  developmentGaps?: string | null;
  enrolledDate: string;
  /** ⚠ Server-set from the token. Not on the create payload — see {@link CreateTalentPoolMember}. */
  nominatedById?: string | null;
  nominatedByName?: string | null;
  nominationNotes?: string | null;
  lastReviewDate?: string | null;
  nextReviewDate?: string | null;
  reviewNotes?: string | null;
  latestPerformanceRating?: PerformanceRating | null;
  latestPotentialRating?: PotentialRating | null;
  ratingLastUpdated?: string | null;
  removedDate?: string | null;
  removalReason?: string | null;
  isActive: boolean;
  developmentActivities: SuccessionDevelopmentActivitySummary[];
  documents: SuccessionDocument[];
}

export interface TalentPool {
  id: string;
  tenantId: string;
  name: string;
  description?: string | null;
  poolTypeId: string;
  poolTypeName: string;
  poolTypeColor?: string | null;
  targetPositionId?: string | null;
  targetPositionTitle?: string | null;
  targetSize: number;
  validFrom?: string | null;
  validTo?: string | null;
  isActive: boolean;
  ownerId: string;
  ownerName: string;
  /** ⚠ Counts ACTIVE members only, and is derived server-side from the loaded collection. */
  currentMemberCount: number;
  members: TalentPoolMemberSummary[];
}

export interface TalentPoolSummary {
  id: string;
  name: string;
  poolTypeId: string;
  poolTypeName: string;
  poolTypeColor?: string | null;
  targetSize: number;
  isActive: boolean;
  ownerName: string;
  currentMemberCount: number;
  validFrom?: string | null;
  validTo?: string | null;
}

export interface CreateTalentPool {
  name: string;
  description?: string | null;
  poolTypeId: string;
  targetPositionId?: string | null;
  targetSize: number;
  validFrom?: string | null;
  validTo?: string | null;
  isActive: boolean;
  /** The manager who owns the pool — a business assignment, not an actor claim. */
  ownerId: string;
}

export interface UpdateTalentPool extends CreateTalentPool {
  id: string;
}

/**
 * ⚠ Carries no `nominatedById`. Who nominated someone into a pool is the signed-in user; the field
 * arrived on the body and was honoured until slice 6.
 *
 * ⚠ Re-adding a previously removed employee **revives their old membership** rather than creating a
 * second one, so their original enrolment and removal history survive.
 */
export interface CreateTalentPoolMember {
  talentPoolId: string;
  employeeId: string;
  rank: number;
  readiness: ReadinessLevel;
  readyByDate?: string | null;
  justification?: string | null;
  strengths?: string | null;
  developmentGaps?: string | null;
  /** Caller-set on purpose: a desk may legitimately back-record when someone joined. */
  enrolledDate: string;
  nominationNotes?: string | null;
}

export interface RemoveTalentPoolMember {
  removalReason: string;
}

// ── Talent reviews and the nine box (slice 7) ────────────────────────────────

export interface TalentReviewRatingSummary {
  id: string;
  employeeId: string;
  employeeName: string;
  employeePosition?: string | null;
  performance: PerformanceRating;
  potential: PotentialRating;
  previousPerformance?: PerformanceRating | null;
  previousPotential?: PotentialRating | null;
  calibrationConfirmed: boolean;
}

export interface TalentReviewRating {
  id: string;
  tenantId: string;
  sessionId: string;
  sessionName: string;
  reviewYear: number;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  employeePosition?: string | null;
  talentPoolMemberId?: string | null;
  talentPoolName?: string | null;
  performance: PerformanceRating;
  potential: PotentialRating;
  previousRatingSessionId?: string | null;
  previousSessionName?: string | null;
  previousPerformance?: PerformanceRating | null;
  previousPotential?: PotentialRating | null;
  justification?: string | null;
  keyStrengths?: string | null;
  developmentPriorities?: string | null;
  /** Which manager gave the score. Caller-set: a fact being recorded, not an act by the caller. */
  ratedById?: string | null;
  ratedByName?: string | null;
  calibrationConfirmed: boolean;
  /** ⚠ Server-set from the token. Confirming publishes the placement to the talent pool member. */
  calibrationConfirmedById?: string | null;
  calibrationConfirmedByName?: string | null;
  calibrationConfirmedDate?: string | null;
  calibrationNotes?: string | null;
}

export interface TalentReviewSession {
  id: string;
  tenantId: string;
  sessionName: string;
  reviewYear: number;
  sessionDate: string;
  location?: string | null;
  /** Who chaired. Caller-set — a desk may record a session someone else ran. */
  facilitatedById?: string | null;
  facilitatedByName?: string | null;
  agenda?: string | null;
  sessionNotes?: string | null;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  /** ⚠ Once true the session is FROZEN — no new ratings, no edits, not even uncalibrated ones. */
  isFinalized: boolean;
  finalizedDate?: string | null;
  /** ⚠ Server-set. This was a required body field the client could not know, and it 500'd. */
  finalizedById?: string | null;
  finalizedByName?: string | null;
  ratings: TalentReviewRatingSummary[];
}

export interface TalentReviewSessionSummary {
  id: string;
  sessionName: string;
  reviewYear: number;
  sessionDate: string;
  facilitatedByName?: string | null;
  organizationUnitName?: string | null;
  isFinalized: boolean;
  ratingCount: number;
}

export interface CreateTalentReviewSession {
  sessionName: string;
  reviewYear: number;
  sessionDate: string;
  location?: string | null;
  facilitatedById?: string | null;
  agenda?: string | null;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
}

export interface CreateTalentReviewRating {
  sessionId: string;
  employeeId: string;
  talentPoolMemberId?: string | null;
  performance: PerformanceRating;
  potential: PotentialRating;
  justification?: string | null;
  keyStrengths?: string | null;
  developmentPriorities?: string | null;
  ratedById?: string | null;
}

/** ⚠ Carries no confirmer and no date — both come from the token and the clock. */
export interface ConfirmCalibration {
  ratingId: string;
  calibrationNotes?: string | null;
}

/** ⚠ Carries no actor. `finalizedById` used to be required here and was unknowable. */
export interface FinalizeTalentReviewSession {
  sessionId: string;
  sessionNotes?: string | null;
}

/**
 * What the grid can suggest before anyone types — decision D-4.
 *
 * ⚠ There is no `suggestedPotential`, and there never will be: `PotentialRating` does not exist in
 * area 5. One axis of the nine box has no source outside a human judgement, which is what settled
 * D-4 — the grid could not have been a projection of appraisal data even if we had wanted it.
 */
export interface TalentRatingSuggestion {
  employeeId: string;
  suggestedPerformance?: PerformanceRating | null;
  sourceAppraisalId?: string | null;
  sourceAppraisalNumber?: string | null;
  sourceOverallScore?: number | null;
  sourceAppraisalDate?: string | null;
  previousPerformance?: PerformanceRating | null;
  previousPotential?: PotentialRating | null;
  previousSessionName?: string | null;
}
