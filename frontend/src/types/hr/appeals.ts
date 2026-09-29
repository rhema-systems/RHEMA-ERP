/**
 * Appraisal appeals — area 5's fourth slice, alongside calibration and outcomes.
 *
 * All routes hang off `api/PerformanceAppraisals`, keyed by the **appraisal** id rather than the
 * appeal id, because there is at most one appeal per appraisal.
 *
 * **The path.** A completed, acknowledged appraisal can be appealed by its subject. HR picks the
 * appeal up (Submitted → UnderReview) and then rules:
 *
 *   - **Upheld** — HR agrees. Final. Scores may have been changed first.
 *   - **Rejected** — HR confirms the original scores. Final.
 *   - **Remanded** — HR sends it back to the manager to re-evaluate. Not a verdict: the
 *     appraisal rolls back to Active, a snapshot of the manager's evaluation is frozen for the
 *     before/after comparison, and a deadline is set. Once the manager re-submits, HR makes a
 *     *final* decision on the post-remand review screen (Upheld or Rejected only).
 *
 * Whether HR may change scores while resolving is `hrCanModifyScores` on the cycle's settings
 * profile, and it is reported on the review payload — do not offer the score fields when it is
 * false, since the server refuses them.
 *
 * **Actors.** The appellant's own reads (`appeal-page-data`, `appeal-status`, `appeal-outcome`)
 * take the employee from the token; the HR reads and the three decisions are role-gated. Nothing
 * here accepts an employee id.
 *
 * KPI-level appeals are vestigial: `EmployeeKpiTarget` was replaced by `EmployeeGoal`, so
 * `appealableKpis` always comes back empty and KPI score modifications are ignored. Competency
 * (template item) appeals are the live path.
 */
import type { AppraisalStatus } from './appraisal-run';

export type AppraisalAppealStatus =
  | 'Submitted'
  | 'UnderReview'
  | 'Remanded'
  | 'Upheld'
  | 'Rejected';

// ── The appellant's side ─────────────────────────────────────────────────────────

export interface AppealableCompetency {
  templateItemId: string;
  itemName: string;
  description?: string | null;
  numericScore?: number | null;
  weight: number;
  weightedScore?: number | null;
}

export interface AppealableKpi {
  employeeKpiTargetId: string;
  kpiName: string;
  description?: string | null;
  targetValue?: number | null;
  actualValue?: number | null;
  achievementPercentage?: number | null;
  unit?: string | null;
  weight: number;
  weightedScore?: number | null;
}

/** What the signed-in employee may appeal, and whether they still can. */
export interface AppealPageData {
  appraisalId: string;
  appraisalNumber: string;
  cycleName: string;
  finalScore?: number | null;
  finalGrade?: string | null;
  canAppeal: boolean;
  /** Populated whenever `canAppeal` is false — show it rather than an empty form. */
  cannotAppealReason?: string | null;
  appealableKpis: AppealableKpi[];
  appealableCompetencies: AppealableCompetency[];
}

export interface AppealItemSubmission {
  templateItemId?: string | null;
  employeeKpiTargetId?: string | null;
  reason: string;
}

export interface SubmitAppeal {
  appraisalId: string;
  overallReason?: string | null;
  /** At least one, or the server rejects the submission. */
  appealedItems: AppealItemSubmission[];
}

export interface AppraisalAppealItem {
  id: string;
  tenantId: string;
  appraisalAppealId: string;
  templateItemId?: string | null;
  reason: string;
}

export interface AppraisalAppeal {
  id: string;
  tenantId: string;
  performanceAppraisalId: string;
  appealReason: string;
  status: AppraisalAppealStatus;
  submittedDate: string;
  items: AppraisalAppealItem[];
}

export interface AppealedItemView {
  itemId: string;
  itemType: string;
  itemName: string;
  reason: string;
  originalScore?: number | null;
  targetValue?: number | null;
  actualValue?: number | null;
}

/** The appellant's read-only view while the appeal is open, and after. */
export interface AppealStatusView {
  appealId: string;
  appraisalId: string;
  appraisalNumber: string;
  cycleName: string;
  status: AppraisalAppealStatus;
  submittedDate: string;
  appealReason: string;
  reviewedByName?: string | null;
  resolvedDate?: string | null;
  resolutionNotes?: string | null;
  originalScore?: number | null;
  /** The post-appeal score. Equals `originalScore` when nothing was changed. */
  adjustedScore?: number | null;
  appealedItems: AppealedItemView[];
}

// ── HR's side ────────────────────────────────────────────────────────────────────

export interface AppealListItem {
  appealId: string;
  appraisalId: string;
  appraisalNumber: string;
  employeeName: string;
  employeeNumber: string;
  cycleName: string;
  cycleId: string;
  submittedDate: string;
  appealedItemsCount: number;
  status: AppraisalAppealStatus;
}

export interface AppealedCriterionReview {
  appealItemId: string;
  /** Null on a goal row, which is named by its snapshot row. */
  templateItemId: string | null;
  criterionConfigId?: string | null;
  /** The criterion's key — the template item, or a goal row's snapshot row. */
  criterionKey: string;
  itemName: string;
  itemDescription: string;
  weight: number;
  appealReason: string;
  selfScore?: number | null;
  peerAverageScore?: number | null;
  managerScore?: number | null;
  selfWeightedScore?: number | null;
  peerWeightedScore?: number | null;
  managerWeightedScore?: number | null;
  finalWeightedScore: number;
  managerComments?: string | null;
  selfComments?: string | null;
}

export interface AppealedKpiReview {
  appealItemId: string;
  employeeKpiTargetId: string;
  kpiName: string;
  kpiDescription: string;
  weight: number;
  appealReason: string;
  targetValue: number;
  actualValue?: number | null;
  measurementUnit: string;
  selfScore?: number | null;
  peerAverageScore?: number | null;
  managerScore?: number | null;
  selfWeightedScore?: number | null;
  peerWeightedScore?: number | null;
  managerWeightedScore?: number | null;
  finalWeightedScore: number;
  managerComments?: string | null;
  selfComments?: string | null;
}

export interface AppealReview {
  appealId: string;
  appraisalId: string;
  appraisalNumber: string;
  submittedDate: string;
  status: AppraisalAppealStatus;
  overallAppealReason: string;

  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  positionTitle?: string | null;
  organizationUnitName?: string | null;

  cycleName: string;
  cycleId: string;
  cycleStartDate: string;
  cycleEndDate: string;

  selfEvaluationScore?: number | null;
  peerEvaluationScore?: number | null;
  managerEvaluationScore?: number | null;
  overallScore: number;

  /** From the cycle's settings profile. False means the score fields are refused. */
  hrCanModifyScores: boolean;
  appraisalSettingsId: string;
  appraisalSettingsName: string;

  appealedCriteria: AppealedCriterionReview[];
  appealedKpis: AppealedKpiReview[];
}

export interface CriterionScoreModification {
  /** The criterion restated: its template item, or — for a goal row, which has none — its snapshot row. */
  templateItemId?: string | null;
  criterionConfigId?: string | null;
  newScore: number;
  justification: string;
}

export interface KpiScoreModification {
  employeeKpiTargetId: string;
  newActualValue: number;
  justification: string;
}

export interface ResolveAppeal {
  /** `Upheld`, `Rejected` or `Remanded`. */
  resolutionDecision: AppraisalAppealStatus;
  resolutionNotes: string;
  criteriaModifications?: CriterionScoreModification[] | null;
  kpiModifications?: KpiScoreModification[] | null;
}

// ── Post-remand ──────────────────────────────────────────────────────────────────

export interface CriterionScoreComparison {
  /** Null on a goal row, which is named by its snapshot row. */
  templateItemId: string | null;
  criterionConfigId?: string | null;
  /** The criterion's key — the template item, or a goal row's snapshot row. */
  criterionKey: string;
  itemName: string;
  itemDescription: string;
  weight: number;
  wasAppealed: boolean;
  appealReason?: string | null;
  preRemandScore?: number | null;
  preRemandWeightedScore?: number | null;
  preRemandComments?: string | null;
  postRemandScore?: number | null;
  postRemandWeightedScore?: number | null;
  postRemandComments?: string | null;
  scoreChanged: boolean;
  scoreDifference?: number | null;
}

export interface KpiScoreComparison {
  employeeKpiTargetId: string;
  kpiName: string;
  kpiDescription: string;
  weight: number;
  wasAppealed: boolean;
  appealReason?: string | null;
  targetValue: number;
  measurementUnit: string;
  preRemandActualValue?: number | null;
  preRemandAchievementPercent?: number | null;
  preRemandWeightedScore?: number | null;
  preRemandComments?: string | null;
  postRemandActualValue?: number | null;
  postRemandAchievementPercent?: number | null;
  postRemandWeightedScore?: number | null;
  postRemandComments?: string | null;
  scoreChanged: boolean;
  actualValueDifference?: number | null;
}

export interface PostRemandReview {
  appraisalId: string;
  appraisalNumber: string;
  appealId: string;
  appealStatus: AppraisalAppealStatus;

  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  positionTitle?: string | null;
  organizationUnitName?: string | null;

  cycleName: string;
  cycleId: string;
  cycleStartDate: string;
  cycleEndDate: string;

  appealSubmittedDate: string;
  appealRemandedDate: string;
  appealRemandDeadline: string;
  managerReevaluationDate?: string | null;

  overallAppealReason: string;
  hrRemandJustification: string;

  criteriaComparisons: CriterionScoreComparison[];
  kpiComparisons: KpiScoreComparison[];

  preRemandOverallScore: number;
  postRemandOverallScore: number;

  hrCanModifyScores: boolean;
}

/** Only `Upheld` or `Rejected` — a remand cannot be remanded again. */
export interface PostRemandFinalDecision {
  finalDecision: Extract<AppraisalAppealStatus, 'Upheld' | 'Rejected'>;
  hrFinalNotes: string;
}

// ── The appellant's outcome view ─────────────────────────────────────────────────

export interface FinalCriterionScore {
  /** Null on a goal row, which is named by its snapshot row. */
  templateItemId: string | null;
  criterionConfigId?: string | null;
  /** The criterion's key — the template item, or a goal row's snapshot row. */
  criterionKey: string;
  itemName: string;
  itemDescription: string;
  finalScore?: number | null;
  finalWeightedScore: number;
  weight: number;
  managerComments: string;
  wasAppealed: boolean;
  /** A KPI whose achievement calibration or an appeal restated to `finalScore` percent. */
  achievementOverridden?: boolean;
}

export interface FinalKpiScore {
  employeeKpiTargetId: string;
  kpiName: string;
  kpiDescription: string;
  targetValue: number;
  finalActualValue?: number | null;
  finalAchievementPercent: number;
  finalWeightedScore: number;
  weight: number;
  measurementUnit: string;
  managerComments: string;
  wasAppealed: boolean;
}

export interface EmployeeAppealOutcome {
  appraisalId: string;
  appraisalNumber: string;
  cycleName: string;
  cycleStartDate: string;
  cycleEndDate: string;
  positionTitle: string;
  organizationUnitName: string;

  appealStatus: AppraisalAppealStatus;
  appealResolvedDate: string;
  isUpheld: boolean;
  isRejected: boolean;

  appealSubmittedDate: string;
  employeeAppealReason: string;
  appealedItems: string[];

  hrFinalNotes: string;
  outcomeMessage: string;

  finalOverallScore: number;
  /** The overall the appeal was filed against; null on appeals filed before it was kept. */
  originalOverallScore?: number | null;
  finalCriteriaScores: FinalCriterionScore[];
  finalKpiScores: FinalKpiScore[];

  scoresChangedAfterAppeal: boolean;
}

/** Convenience shape for the appraisal statuses an appeal can leave behind. */
export type AppealableAppraisalStatus = Extract<AppraisalStatus, 'Completed' | 'Appealed'>;
