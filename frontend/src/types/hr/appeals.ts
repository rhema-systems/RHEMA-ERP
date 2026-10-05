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
 *     appraisal stays under appeal, a snapshot of the manager's evaluation is frozen for the
 *     before/after comparison, the manager's evaluation reopens, and a deadline is set (HR can
 *     extend it). Once the manager re-submits — or the deadline passes without it — HR makes a
 *     *final* decision on the post-remand review screen (Upheld or Rejected only; Rejected
 *     restores the scores from before the remand).
 *
 * Whether HR may change scores while resolving is `hrCanModifyScores` on the cycle's settings
 * profile, and it is reported on the review payload — do not offer the score fields when it is
 * false, since the server refuses them.
 *
 * **Actors.** The appellant's own reads (`appeal-page-data`, `appeal-status`, `appeal-outcome`)
 * take the employee from the token; the HR reads and the three decisions are role-gated. Nothing
 * here accepts an employee id.
 *
 * **Rows (closure C6).** Every criterion the manager scored can be appealed — a competency, a KPI
 * or one of the employee's goals — and every read names and describes it the same way: its kind
 * (`itemType`), how it is scored (`scoringMethod`), its section and weight, and a **score** that is
 * a rated row's score on its own scale or a measured row's achievement %, with the actual and the
 * target beside it. (KPI appeals were "vestigial" until C6: the page offered competencies only.)
 */
import type { AppraisalStatus, CriterionScoringMethod } from './appraisal-run';

export type AppraisalAppealStatus =
  | 'Submitted'
  | 'UnderReview'
  | 'Remanded'
  | 'Upheld'
  | 'Rejected';

/** What a row is. A template item that is neither a competency nor a KPI is a `Question`. */
export type AppealCriterionKind = 'Competency' | 'KPI' | 'Goal' | 'Question';

// ── The appellant's side ─────────────────────────────────────────────────────────

/**
 * One criterion the employee may appeal. Sent back by `criterionConfigId` — with `templateItemId`
 * too on a template row, as the forms send their rows; the server refuses a pair naming two rows.
 */
export interface AppealableCriterion {
  /** The template item for a template row, the snapshot row for a goal row. */
  criterionKey: string;
  templateItemId?: string | null;
  criterionConfigId?: string | null;
  itemType: AppealCriterionKind;
  scoringMethod: CriterionScoringMethod;
  itemName: string;
  description?: string | null;
  sectionName?: string | null;
  /** The section's weight on the form. */
  sectionWeight?: number | null;
  /** The row's weight within its section. */
  weight: number;
  /** The top of the row's own scale; 100 for a measured row's achievement %. */
  scaleTop: number;
  /** A measured row's target. */
  targetValue?: number | null;
  unit?: string | null;
  /** The manager's score — a rated score, or a measured row's achievement %. Absent when the cycle shows the overall only (B2). */
  score?: number | null;
  actualValue?: number | null;
  /** A measured row's achievement was restated by calibration or an appeal, not read from its actual. */
  achievementOverridden: boolean;
  /** What the row contributes to the manager's evaluation. */
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
  /**
   * Each item carries the manager's score. False when the cycle shows the employee only the
   * overall, grade and narrative (B2): the items are listed without their scores. Before the
   * outcome is released nothing is listed and there is no score.
   */
  scoreBreakdownShown: boolean;
  /** Every criterion the manager scored — competency, KPI and goal rows, in the forms' order. */
  appealableCriteria: AppealableCriterion[];
}

export interface AppealItemSubmission {
  templateItemId?: string | null;
  /** A goal row is named by this alone; a template row by either, or both. */
  criterionConfigId?: string | null;
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
  criterionConfigId?: string | null;
  reason: string;
  /** What the manager scored the item when the appeal was filed (D-38). */
  originalScore?: number | null;
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
  criterionKey: string;
  templateItemId?: string | null;
  criterionConfigId?: string | null;
  itemType: AppealCriterionKind | 'Criterion';
  scoringMethod: CriterionScoringMethod;
  itemName: string;
  sectionName?: string | null;
  weight?: number | null;
  reason: string;
  /**
   * What the manager scored it when the appeal was filed (D-38) — a rated score or an achievement
   * %. Absent when the cycle shows the overall only, or not known (an appeal filed before it was kept).
   */
  originalScore?: number | null;
  /** What it scores now. Absent while a remand withholds it, or when the cycle shows the overall only. */
  currentScore?: number | null;
  /** After the decision: whether the appeal moved it. */
  scoreChanged?: boolean | null;
  targetValue?: number | null;
  unit?: string | null;
  /** A measured row's actual now, beside `currentScore`. */
  actualValue?: number | null;
  achievementOverridden: boolean;
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
  /** Set once decided — a remand is not a decision. */
  resolvedDate?: string | null;
  resolutionNotes?: string | null;
  /** The overall the appeal was filed against. */
  originalScore?: number | null;
  /** The new overall when the decision moved it; null when it did not, or before the decision (A5). */
  adjustedScore?: number | null;
  /** The overall now; null while a remand withholds it. */
  currentOverallScore?: number | null;
  /** False while a remand is open: the scores as they stand are provisional until HR decides. */
  outcomeReleased: boolean;
  /** The appealed items carry the manager's scores — false when the cycle shows the overall only (B2). */
  scoreBreakdownShown: boolean;
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

/** One contested criterion on HR's review, every leg's score on the row's own terms (C6, C9). */
export interface AppealedCriterionReview {
  appealItemId: string;
  /** Null on a goal row, which is named by its snapshot row. */
  templateItemId: string | null;
  criterionConfigId?: string | null;
  /** The criterion's key — the template item, or a goal row's snapshot row. */
  criterionKey: string;
  itemType: AppealCriterionKind | 'Criterion';
  scoringMethod: CriterionScoringMethod;
  itemName: string;
  itemDescription: string;
  sectionName?: string | null;
  /** The row's weight within its section, from the snapshot. */
  weight: number;
  /** The highest new score this row takes: its top band, or 100 — a measured row's new score is an achievement % (D-22). */
  scaleTop: number;
  targetValue?: number | null;
  unit?: string | null;
  appealReason: string;
  /** What the manager scored it when the appeal was filed (D-38). */
  scoreWhenAppealed?: number | null;
  /** A self-evaluation draft is not read (B2). */
  selfScore?: number | null;
  selfActualValue?: number | null;
  /** The submitted peers' average. */
  peerAverageScore?: number | null;
  managerScore?: number | null;
  managerActualValue?: number | null;
  achievementOverridden: boolean;
  selfWeightedScore?: number | null;
  /** What the row contributes to the manager's evaluation — the contribution under appeal. */
  managerWeightedScore?: number | null;
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
  /** A draft self-evaluation — one HR waived — is not read. */
  selfEvaluationSubmitted: boolean;
  peerEvaluationScore?: number | null;
  managerEvaluationScore?: number | null;
  overallScore?: number | null;

  /** From the cycle's settings profile. False means the score fields are refused. */
  hrCanModifyScores: boolean;
  appraisalSettingsId: string;
  appraisalSettingsName: string;

  /** Why the reader may not act on this appeal — they are party to it (D-35) — or null. */
  partyToAppealReason?: string | null;

  // The decision, once made — a decided appeal opens read-only (D-37).
  reviewedByName?: string | null;
  resolvedDate?: string | null;
  resolutionNotes?: string | null;
  originalOverallScore?: number | null;
  /** The new overall when the decision moved it; null when it did not. */
  adjustedScore?: number | null;

  appealedCriteria: AppealedCriterionReview[];
}

/**
 * A score HR restates on an upheld appeal — on a contested criterion only. A measured row's new
 * score is an achievement %, not a new actual (D-22).
 */
export interface CriterionScoreModification {
  /** The criterion restated: its template item, or — for a goal row, which has none — its snapshot row. */
  templateItemId?: string | null;
  criterionConfigId?: string | null;
  newScore: number;
  justification: string;
}

export interface ResolveAppeal {
  /**
   * `Upheld`, `Rejected` or `Remanded` — anything else is refused (422), as is a decision on an
   * appeal already sent back to the manager (closure C4).
   */
  resolutionDecision: Extract<AppraisalAppealStatus, 'Upheld' | 'Rejected' | 'Remanded'>;
  resolutionNotes: string;
  /** Only with `Upheld`, and only on contested criteria: anything else is refused (422). */
  criteriaModifications?: CriterionScoreModification[] | null;
}

// ── Post-remand ──────────────────────────────────────────────────────────────────

/** One criterion before the remand and after the re-evaluation — a measured row compares its actual too. */
export interface CriterionScoreComparison {
  /** Null on a goal row, which is named by its snapshot row. */
  templateItemId: string | null;
  criterionConfigId?: string | null;
  /** The criterion's key — the template item, or a goal row's snapshot row. */
  criterionKey: string;
  itemType: AppealCriterionKind | 'Criterion';
  scoringMethod: CriterionScoringMethod;
  itemName: string;
  itemDescription: string;
  sectionName?: string | null;
  weight: number;
  targetValue?: number | null;
  unit?: string | null;
  wasAppealed: boolean;
  appealReason?: string | null;
  preRemandScore?: number | null;
  preRemandActualValue?: number | null;
  preRemandWeightedScore?: number | null;
  preRemandComments?: string | null;
  postRemandScore?: number | null;
  postRemandActualValue?: number | null;
  postRemandWeightedScore?: number | null;
  postRemandComments?: string | null;
  /** The score, or on a measured row the actual behind it, moved. */
  scoreChanged: boolean;
  scoreDifference?: number | null;
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
  /** The re-evaluation deadline while the manager owes it; null once they have re-evaluated. */
  appealRemandDeadline?: string | null;
  managerReevaluationDate?: string | null;

  // Where the remand stands (closure C3, D-34). The read answers while the manager is still
  // re-evaluating; the comparison is empty until they have.
  /** The manager has not re-evaluated yet. */
  awaitingReevaluation: boolean;
  /** The deadline passed without a re-evaluation. */
  deadlinePassed: boolean;
  /** HR can decide: re-evaluated, or the deadline passed without it (on the original scores). */
  canDecide: boolean;
  /** HR can move the deadline: the manager has not re-evaluated. */
  canExtend: boolean;

  overallAppealReason: string;
  hrRemandJustification: string;

  /** Every criterion the manager scored, KPI and goal rows among them. */
  criteriaComparisons: CriterionScoreComparison[];

  /** The overall the appeal was filed against, and the overall now. */
  preRemandOverallScore: number;
  postRemandOverallScore: number;
  /** The manager's total before the remand, and after the re-evaluation. */
  preRemandManagerScore?: number | null;
  postRemandManagerScore?: number | null;

  hrCanModifyScores: boolean;
}

/**
 * Only `Upheld` or `Rejected` — a remand cannot be remanded again. `Rejected` restores the scores
 * from before the remand; after a lapsed deadline both decisions stand on them (closure C5, D-34).
 */
export interface PostRemandFinalDecision {
  finalDecision: Extract<AppraisalAppealStatus, 'Upheld' | 'Rejected'>;
  hrFinalNotes: string;
}

/** HR moves a remand's re-evaluation deadline to a later day (closure D-34). */
export interface ExtendRemandDeadline {
  /** `yyyy-MM-dd` — the last day; the deadline is the end of it. */
  newDeadline: string;
  reason: string;
}

// ── The appellant's outcome view ─────────────────────────────────────────────────

export interface FinalCriterionScore {
  /** Null on a goal row, which is named by its snapshot row. */
  templateItemId: string | null;
  criterionConfigId?: string | null;
  /** The criterion's key — the template item, or a goal row's snapshot row. */
  criterionKey: string;
  itemType: AppealCriterionKind | 'Criterion';
  scoringMethod: CriterionScoringMethod;
  itemName: string;
  itemDescription: string;
  sectionName?: string | null;
  /** A rated score, or a measured row's achievement %. */
  finalScore?: number | null;
  finalActualValue?: number | null;
  targetValue?: number | null;
  unit?: string | null;
  finalWeightedScore: number;
  weight: number;
  managerComments: string;
  wasAppealed: boolean;
  /** On a contested row: what it scored when the appeal was filed (D-38). */
  scoreWhenAppealed?: number | null;
  /** On a contested row: whether the appeal moved it. */
  changedOnAppeal?: boolean | null;
  /** A measured row whose achievement calibration or an appeal restated to `finalScore` percent. */
  achievementOverridden?: boolean;
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
  /** Each as "<kind>: <name>" — "KPI: …", "Competency: …", "Goal: …". */
  appealedItems: string[];

  hrFinalNotes: string;
  /** Says whether the appeal moved a score — an upheld appeal can leave them all as they were. */
  outcomeMessage: string;

  finalOverallScore: number;
  /** The overall the appeal was filed against; null on appeals filed before it was kept. */
  originalOverallScore?: number | null;
  /** `finalCriteriaScores` is filled — false (and the list empty) when the cycle shows the overall only (B2). */
  scoreBreakdownShown: boolean;
  finalCriteriaScores: FinalCriterionScore[];

  /** The appeal moved the overall, or a score it contested. */
  scoresChangedAfterAppeal: boolean;
}

/** Convenience shape for the appraisal statuses an appeal can leave behind. */
export type AppealableAppraisalStatus = Extract<AppraisalStatus, 'Completed' | 'Appealed'>;

// ── Presenting a score ───────────────────────────────────────────────────────────

/**
 * A row's score as the appeal pages print it: a measured row's achievement as a percentage, a rated
 * row's score on its scale — "—" when there is none.
 */
export function formatCriterionScore(
  score: number | null | undefined,
  scoringMethod: CriterionScoringMethod,
): string {
  if (score == null) return '—';
  const value = Number(score);
  const shown = Number.isInteger(value) ? value.toString() : value.toFixed(1);
  return scoringMethod === 'Measured' ? `${shown} %` : shown;
}

/**
 * A measured row's actual against its target — "92 against a target of 100 %" — or its target alone
 * when the actual is not shown; null when there is neither (a rated row).
 */
export function formatActualAgainstTarget(
  actual: number | null | undefined,
  target: number | null | undefined,
  unit?: string | null,
): string | null {
  const u = unit ? ` ${unit}` : '';
  const n = (v: number) => Number(v).toLocaleString();
  if (actual != null && target != null) return `${n(actual)} against a target of ${n(target)}${u}`;
  if (actual != null) return `${n(actual)}${u}`;
  if (target != null) return `Target ${n(target)}${u}`;
  return null;
}
