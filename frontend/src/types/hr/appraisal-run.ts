/**
 * The appraisal *run* — area 5's third slice.
 *
 * `types/hr/appraisal.ts` owns everything HR configures before a cycle can run (settings,
 * templates, targets, the cycle itself). This file owns what happens once it is running and
 * the appraisal records exist:
 *
 *   self-evaluation → peer nomination → peer evaluation → manager evaluation → HR review
 *   → employee acknowledgment,  with check-ins running alongside throughout
 *
 * Routes, per controller as always:
 *   api/PerformanceAppraisals   the appraisal record and every evaluation leg on it
 *   api/PeerEvaluations         a peer's own assignments and scoring form
 *   api/PeerNomination          individual nomination CRUD (batch ops live on the appraisal)
 *   api/CheckIns                one-to-ones and the goal updates they produce
 *   api/AppraisalWorkflow       computed phase and role-editability
 *
 * **Scoring model.** Every leg scores the same frozen snapshot. When a cycle generates its
 * appraisals it copies the template's items into `PerformanceAppraisalCriterionConfig` rows
 * with their weights and grade bands, so later edits to the template cannot move a scored
 * appraisal underneath it. The unit everyone scores is therefore `templateItemId`, and each
 * leg's context read returns the same section → item tree with its own scores attached.
 *
 * Enums serialize as strings, so every union below is the enum member name.
 */
import type { AuditFields } from './common';
import type {
  AppraisalCycleStatus,
  AppraisalSettings,
  PeerNominationMode,
} from './appraisal';

// ── Enums ────────────────────────────────────────────────────────────────────────

/**
 * The appraisal record's own lifecycle. Coarser than it looks: everything between opening
 * and HR sign-off is `Active`, and the fine-grained step is `AppraisalPhase`, computed from
 * live state rather than stored.
 *
 * `Open` is a legacy member retained by the ported model and is not produced by this flow.
 */
export type AppraisalStatus =
  | 'Open'
  | 'Draft'
  | 'Active'
  | 'Governance'
  | 'Appealed'
  | 'Completed'
  | 'Closed'
  /** Not appraised — a leaver, or someone generated in error (D-10). Excluded from scores. */
  | 'Withdrawn';

/**
 * The computed step. Derived from what has actually been submitted plus the cycle's settings
 * flags, so a cycle that does not require peer reviews never reports `PeerEvaluation`.
 */
export type AppraisalPhase =
  | 'GoalSetting'
  | 'SelfEvaluation'
  | 'PeerEvaluation'
  | 'ManagerEvaluation'
  | 'Calibration'
  | 'HRReview'
  | 'EmployeeReview'
  | 'Closed';

export type PeerNominationStatus = 'Pending' | 'Approved' | 'Rejected';

export type EvaluatorRole = 'Self' | 'Manager' | 'Peer' | 'HR';

export type CheckInType =
  | 'OneOnOne'
  | 'AdHocFeedback'
  | 'GoalProgressUpdate'
  | 'CoachingSession'
  | 'MidYearCheckIn';

export type GoalProgressStatus =
  | 'NotStarted'
  | 'InProgress'
  | 'OnTrack'
  | 'AtRisk'
  | 'Completed'
  | 'Cancelled';

/** How a KPI item is measured. `Range` scores against a min/max band rather than a single target. */
export type MeasurementType = 'NumericAbsolute' | 'PercentageTarget' | 'Boolean' | 'Range';

export type KpiTargetSource = 'Goal' | 'KpiDefinition' | 'None';

/** The role the workflow service answers editability questions for. Lower-cased on the wire. */
export type AppraisalEditRole = 'employee' | 'manager' | 'peer' | 'hr';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

export const CHECK_IN_TYPE_OPTIONS = opts<CheckInType>([
  ['OneOnOne', 'One-to-one'],
  ['MidYearCheckIn', 'Mid-year check-in'],
  ['GoalProgressUpdate', 'Goal progress update'],
  ['CoachingSession', 'Coaching session'],
  ['AdHocFeedback', 'Ad-hoc feedback'],
]);

export const GOAL_PROGRESS_STATUS_OPTIONS = opts<GoalProgressStatus>([
  ['NotStarted', 'Not started'],
  ['InProgress', 'In progress'],
  ['OnTrack', 'On track'],
  ['AtRisk', 'At risk'],
  ['Completed', 'Completed'],
  ['Cancelled', 'Cancelled'],
]);

/** Human labels for the computed phase, for headers and progress rails. */
export const APPRAISAL_PHASE_LABELS: Record<AppraisalPhase, string> = {
  GoalSetting: 'Goal setting',
  SelfEvaluation: 'Self-evaluation',
  PeerEvaluation: 'Peer evaluation',
  ManagerEvaluation: 'Manager evaluation',
  Calibration: 'Calibration',
  HRReview: 'HR review',
  EmployeeReview: 'Employee acknowledgment',
  Closed: 'Complete',
};

/**
 * Phases in the order they run, for a progress rail. Calibration and HR review can swap
 * (`hrReviewTiming`), and any of them can be switched off in settings — this is the display
 * order, not a claim about which ones apply.
 */
export const APPRAISAL_PHASE_ORDER: AppraisalPhase[] = [
  'GoalSetting',
  'SelfEvaluation',
  'PeerEvaluation',
  'ManagerEvaluation',
  'Calibration',
  'HRReview',
  'EmployeeReview',
  'Closed',
];

// ── The appraisal record ─────────────────────────────────────────────────────────

export interface PerformanceAppraisal extends AuditFields {
  id: string;
  tenantId: string;
  appraisalCycleId: string;
  appraisalCycleCode?: string | null;
  appraisalNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  departmentName?: string | null;
  positionTitle?: string | null;
  year: number;
  startDate: string;
  endDate: string;
  status: AppraisalStatus;
  peerEvaluatorsCount: number;
  /**
   * The outcome is released to the appraisee (performance closure P2). While false, the
   * appraisee's own copy comes back with the score, grade, ranks, recommendations and the
   * manager's narrative blanked by the server; HR and the manager see them throughout.
   */
  outcomeReleased?: boolean;
  overallScore?: number | null;
  /** Set only once an appeal is adjudicated with a score change. */
  adjustedScore?: number | null;
  rankInPosition?: number | null;
  rankInUnit?: number | null;
  overallComments?: string | null;
  strengthsIdentified?: string | null;
  areasForImprovement?: string | null;
  trainingNeeds?: string | null;
  careerAspirations?: string | null;
  recommendPromotion: boolean;
  recommendIncrement: boolean;
  recommendTraining: boolean;
  recommendPIP: boolean;
  recommendTermination: boolean;
  recommendAward: boolean;
  recommendationNotes?: string | null;
  nextAppraisalDate?: string | null;
  isCalibrated: boolean;
  calibrationSessionId?: string | null;
  preCalibrationScore?: number | null;
  overallGradeDefinitionId?: string | null;
  appraisalTemplateId?: string | null;
  developmentPlanId?: string | null;
  employeeAcknowledged: boolean;
  employeeAcknowledgedDate?: string | null;
  employeeAcknowledgmentComments?: string | null;
  hasAppeal: boolean;
  currentAppealStatus?: string | null;
  isRemandedAppeal: boolean;
  appealRemandedDate?: string | null;
  appealRemandDeadline?: string | null;
  isRemandDeadlineExceeded: boolean;
}

/**
 * A row in the employee's own appraisal list.
 *
 * `myRole` is always `Self` here — this list is scoped to appraisals *about* the employee.
 * Peer work owed on other people's appraisals is a separate list
 * (`peerEvaluationService.getMyAssignments`) and is kept separate on purpose, since those
 * rows carry another employee's scores.
 */
export interface MyAppraisal {
  appraisalId: string;
  appraisalNumber: string;
  appraisalCycleId: string;
  appraisalCycleName: string;
  year: number;
  periodStart: string;
  periodEnd: string;
  status: AppraisalStatus;
  myRole: string;
  actionRequired: boolean;
  actionText?: string | null;
  dueDate?: string | null;
  /** Null for the appraisee until {@link outcomeReleased} (P2). */
  overallScore?: number | null;
  /** The outcome is released to the appraisee (P2). */
  outcomeReleased?: boolean;
  isAcknowledged: boolean;
  appealFiled: boolean;
  appealStatus?: string | null;
  canFileAppeal: boolean;
  selfEvaluationComplete: boolean;
  peerEvaluationComplete: boolean;
  isOverdue: boolean;
}

export interface AppraisalPhaseResponse {
  appraisalId: string;
  phase: AppraisalPhase;
}

export interface AppraisalEditableResponse {
  appraisalId: string;
  role: string;
  isEditable: boolean;
}

// ── Shared scoring shapes ────────────────────────────────────────────────────────

/** A grade band on one criterion, from the appraisal-time snapshot. */
export interface EvaluationGradeRange {
  gradeDefinitionId: string;
  gradeName: string;
  gradeDescription?: string | null;
  lowScore: number;
  highScore: number;
}

/**
 * One scoreable line. `templateItemId` is the key every leg scores against and the key every
 * save is posted under.
 *
 * An item is KPI-driven when `kpiDefinitionId` is set — those take an *actual value* measured
 * against `kpiTargetValue`, not a 0–100 score. Everything else takes the 0–100 score.
 */
export interface EvaluationItem {
  templateItemId: string;
  criterionConfigId: string;
  itemName: string;
  itemDescription?: string | null;
  customQuestion?: string | null;
  requireEvidence: boolean;
  itemWeight: number;
  displayOrder: number;
  kpiDefinitionId?: string | null;
  kpiUnit?: string | null;
  measurementType?: MeasurementType | null;
  kpiTargetValue?: number | null;
  kpiMinValue?: number | null;
  kpiMaxValue?: number | null;
  kpiTargetSource?: KpiTargetSource | null;
  gradeRanges: EvaluationGradeRange[];
}

export interface SelfEvaluationItem extends EvaluationItem {
  existingCriterionScoreId?: string | null;
  existingNumericScore?: number | null;
  existingActualValue?: number | null;
  existingNotes?: string | null;
  existingEvidenceLinks?: string | null;
  achievementPercent?: number | null;
  achievedGrade?: string | null;
}

/** Carries the employee's self-score beside the manager's, so the two can be compared in place. */
export interface ManagerEvaluationItem extends EvaluationItem {
  employeeSelfCriterionScoreId?: string | null;
  employeeSelfNumericScore?: number | null;
  employeeSelfActualValue?: number | null;
  employeeSelfNotes?: string | null;
  employeeSelfEvidenceLinks?: string | null;
  employeeSelfAchievementPercent?: number | null;
  employeeSelfAchievedGrade?: string | null;
  managerCriterionScoreId?: string | null;
  managerNumericScore?: number | null;
  managerActualValue?: number | null;
  managerNotes?: string | null;
  managerEvidenceLinks?: string | null;
  managerAchievementPercent?: number | null;
  /** A KPI whose achievement calibration or an appeal restated — `managerAchievementPercent` is the restatement. */
  managerAchievementOverridden?: boolean;
  managerAchievedGrade?: string | null;
  /** True when the item is under an active appeal remand — the row is highlighted. */
  isAppealed: boolean;
}

export interface PeerEvaluationItem extends EvaluationItem {
  existingCriterionScoreId?: string | null;
  existingNumericScore?: number | null;
  existingActualValue?: number | null;
  existingNotes?: string | null;
  existingEvidenceLinks?: string | null;
  weightedScore?: number | null;
  /** False for KPI items when the cycle does not let peers score KPIs — shown read-only. */
  isScoreable: boolean;
}

interface EvaluationSectionBase {
  sectionId: string;
  sectionName: string;
  sectionDescription?: string | null;
  displayOrder: number;
  sectionWeight: number;
}

export interface SelfEvaluationSection extends EvaluationSectionBase {
  items: SelfEvaluationItem[];
  customQuestions: SelfEvaluationCustomQuestion[];
}

export interface ManagerEvaluationSection extends EvaluationSectionBase {
  items: ManagerEvaluationItem[];
}

export interface PeerEvaluationSection extends EvaluationSectionBase {
  items: PeerEvaluationItem[];
}

export interface SelfEvaluationCustomQuestion {
  templateItemId: string;
  questionText: string;
  displayOrder: number;
  existingResponse?: string | null;
  isSubmitted: boolean;
}

/**
 * One scored item on the way back to the server. Send `numericScore` for competency and
 * custom-question items, `actualValue` for KPI-driven ones.
 *
 * ⚠ Only send rows that actually carry a value. A submit (not a draft) is refused if any row
 * arrives with both fields null, because the server reads the payload as "these are the items
 * I have scored" rather than as the whole form.
 */
export interface EvaluationItemInput {
  templateItemId: string;
  numericScore?: number | null;
  actualValue?: number | null;
  notes?: string | null;
  evidenceLinks?: string | null;
}

export interface CustomQuestionResponseInput {
  templateItemId: string;
  responseText?: string | null;
}

/** Year-end assessment of one goal, filled in by both the employee and the manager. */
export interface GoalAssessmentInput {
  goalId: string;
  finalProgressPercent?: number | null;
  finalStatus?: GoalProgressStatus | null;
  finalActualValue?: number | null;
  assessmentNotes?: string | null;
  evidenceLinks?: string | null;
}

// ── Self-evaluation ──────────────────────────────────────────────────────────────

export interface SelfEvaluationContext {
  appraisalId: string;
  appraisalNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  appraisalCycleName: string;
  periodStart: string;
  periodEnd: string;
  status: AppraisalStatus;
  isSelfEvaluationSubmitted: boolean;
  selfEvaluationSubmittedDate?: string | null;
  isEditable: boolean;
  selfEvaluationDeadline?: string | null;
  allowSelfSoftSkillRating: boolean;
  settings?: AppraisalSettings | null;
  sections: SelfEvaluationSection[];
}

export interface SaveSelfEvaluation {
  appraisalId: string;
  /**
   * Overwritten server-side from the token — an employee can only self-evaluate as themselves.
   * Sent anyway so the payload matches the documented DTO.
   */
  employeeId: string;
  itemScores: EvaluationItemInput[];
  isDraft: boolean;
  customQuestionResponses: CustomQuestionResponseInput[];
  goalAssessments: GoalAssessmentInput[];
}

/**
 * ⚠ This endpoint answers 200 with `success: false` for a rejected save as well as 400 —
 * always check the flag, do not assume a 2xx means it saved.
 */
export interface SelfEvaluationResult {
  success: boolean;
  message?: string | null;
  evaluatorEvaluationId?: string | null;
  submittedDate?: string | null;
}

// ── The read-only submitted view ─────────────────────────────────────────────────

export interface SubmittedEvaluationItem {
  templateItemId: string;
  itemName: string;
  itemDescription?: string | null;
  kpiDefinitionId?: string | null;
  itemWeight: number;
  numericScore?: number | null;
  actualValue?: number | null;
  achievedGrade?: string | null;
  notes?: string | null;
  evidenceLinks?: string | null;
  kpiUnit?: string | null;
  kpiTargetValue?: number | null;
  achievementPercent?: number | null;
}

export interface SubmittedEvaluationSection {
  sectionName: string;
  sectionWeight: number;
  displayOrder: number;
  items: SubmittedEvaluationItem[];
}

export interface SubmittedAttachment {
  attachmentId: string;
  fileName: string;
  filePath: string;
  description?: string | null;
  uploadedDate: string;
}

export interface ViewSubmittedEvaluation {
  appraisalId: string;
  appraisalCycleName: string;
  periodStart: string;
  periodEnd: string;
  year: number;
  employeeId: string;
  employeeName: string;
  employeePosition?: string | null;
  department?: string | null;
  submittedDate: string;
  currentStatus: AppraisalStatus;
  requirePeerReviews: boolean;
  peerReviewsInProgress: boolean;
  managerReviewComplete: boolean;
  requireHRReview: boolean;
  hrReviewComplete: boolean;
  allowSelfSoftSkillRating: boolean;
  sections: SubmittedEvaluationSection[];
  attachments: SubmittedAttachment[];
}

// ── Manager evaluation ───────────────────────────────────────────────────────────

export interface TeamAppraisalCycleSummary {
  cycleId: string;
  cycleName: string;
  periodStart: string;
  periodEnd: string;
  status: AppraisalCycleStatus;
  totalEmployees: number;
  evaluatedCount: number;
  pendingCount: number;
  inProgressCount: number;
}

export interface TeamMemberAppraisal {
  appraisalId: string;
  cycleName: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  position?: string | null;
  organizationUnit?: string | null;
  selfEvaluationSubmitted: boolean;
  selfEvaluationSubmittedDate?: string | null;
  managerEvaluationStarted: boolean;
  managerEvaluationSubmitted: boolean;
  managerEvaluationSubmittedDate?: string | null;
  appraisalStatus: AppraisalStatus;
  requireSelfEvaluation: boolean;
  overallScore?: number | null;
  isRemandedAppeal: boolean;
  appealRemandDeadline?: string | null;
}

export interface ManagerEvaluationContext {
  appraisalId: string;
  appraisalNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  position?: string | null;
  department?: string | null;
  cycleId: string;
  appraisalCycleName: string;
  periodStart: string;
  periodEnd: string;
  managerEvaluationDeadline?: string | null;
  status: AppraisalStatus;
  isRemandedAppeal: boolean;
  appealRemandedDate?: string | null;
  appealRemandDeadline?: string | null;
  isRemandDeadlineExceeded: boolean;
  appealedKpiIds: string[];
  appealedTemplateItemIds: string[];
  managerEvaluatorEvaluationId?: string | null;
  isManagerEvaluationSubmitted: boolean;
  managerEvaluationSubmittedDate?: string | null;
  isEditable: boolean;
  selfEvaluationWeight: number;
  managerEvaluationWeight: number;
  peerEvaluationWeight: number;
  settings?: AppraisalSettings | null;
  overallComments?: string | null;
  strengthsIdentified?: string | null;
  areasForImprovement?: string | null;
  trainingNeeds?: string | null;
  careerAspirations?: string | null;
  recommendPromotion: boolean;
  recommendIncrement: boolean;
  recommendTraining: boolean;
  recommendPIP: boolean;
  recommendTermination: boolean;
  recommendationNotes?: string | null;
  sections: ManagerEvaluationSection[];
}

export interface SaveManagerEvaluation {
  appraisalId: string;
  /** Overwritten server-side from the token — see `SaveSelfEvaluation.employeeId`. */
  managerId: string;
  itemScores: EvaluationItemInput[];
  overallNotes?: string | null;
  recommendation?: string | null;
  overallComments?: string | null;
  strengthsIdentified?: string | null;
  areasForImprovement?: string | null;
  trainingNeeds?: string | null;
  careerAspirations?: string | null;
  recommendPromotion: boolean;
  recommendIncrement: boolean;
  recommendTraining: boolean;
  recommendPIP: boolean;
  recommendTermination: boolean;
  recommendationNotes?: string | null;
  isDraft: boolean;
  goalAssessments: GoalAssessmentInput[];
}

/** Same `success: false` on 200 caveat as `SelfEvaluationResult`. */
export interface ManagerEvaluationResult {
  success: boolean;
  message?: string | null;
  evaluatorEvaluationId?: string | null;
  submittedDate?: string | null;
}

// ── Peer nomination ──────────────────────────────────────────────────────────────

export interface PeerNomination extends AuditFields {
  id: string;
  tenantId: string;
  appraisalId: string;
  appraisalNumber?: string | null;
  peerEmployeeId: string;
  peerEmployeeName: string;
  peerEmployeeNumber?: string | null;
  nominatedById: string;
  nominatedByName: string;
  nominationDate: string;
  invitationSentDate?: string | null;
  dueDate?: string | null;
  instructionsToPeer?: string | null;
  nominationStatus: PeerNominationStatus;
  approvedDate?: string | null;
  rejectionReason?: string | null;
}

/**
 * `canSubmit` reports whether the nomination *count* is within the configured min/max — it is
 * about the list, not about approval. `canEdit` is false once the appraisal leaves Draft or
 * Active.
 */
export interface PeerNominationSummary {
  appraisalId: string;
  totalNominations: number;
  pendingCount: number;
  approvedCount: number;
  rejectedCount: number;
  minRequired: number;
  maxAllowed: number;
  canSubmit: boolean;
  canEdit: boolean;
  nominationMode: PeerNominationMode;
  nominations: PeerNomination[];
}

export interface BatchCreatePeerNominations {
  appraisalId: string;
  peerEmployeeIds: string[];
  dueDate?: string | null;
  instructionsToPeer?: string | null;
}

/** Approving is what creates the peers' evaluation records and notifies them. */
export interface ApprovePeerNominations {
  appraisalId: string;
  nominationIds: string[];
  dueDate?: string | null;
}

export interface RejectPeerNominations {
  appraisalId: string;
  nominationIds: string[];
  rejectionReason: string;
}

// ── Peer evaluation ──────────────────────────────────────────────────────────────

export interface PeerEvaluationAssignment {
  evaluationId: string;
  appraisalId: string;
  appraisalCycleName: string;
  appraiseeId: string;
  appraiseeName: string;
  appraiseePosition: string;
  appraiseeOrganizationUnit: string;
  /** Free text from the server: `Not Started` | `In Progress` | `Submitted`. */
  status: string;
  startedDate?: string | null;
  submittedDate?: string | null;
  dueDate?: string | null;
  evaluatorWeight: number;
}

/**
 * `isAnonymous` says whether the *appraisee* will see who said what — the peer always sees
 * the appraisee's name, and the manager always sees the peer's.
 */
export interface PeerEvaluationDetail {
  evaluationId: string;
  appraisalId: string;
  appraisalCycleName: string;
  appraiseeName: string;
  appraiseePosition: string;
  appraiseeOrganizationUnit: string;
  peerEvaluationWeight: number;
  allowPeerKpiEvaluation: boolean;
  isAnonymous: boolean;
  isSubmitted: boolean;
  dueDate?: string | null;
  sections: PeerEvaluationSection[];
}

export interface SavePeerEvaluation {
  evaluationId: string;
  itemScores: EvaluationItemInput[];
}

// ── The manager's view of peer feedback ──────────────────────────────────────────

export interface PeerCompetencyScore {
  criterionScoreId: string;
  criteriaName: string;
  criteriaDescription?: string | null;
  weight: number;
  numericScore: number;
  weightedScore: number;
  comments?: string | null;
  achievedGrade?: string | null;
}

export interface PeerKpiEvaluation {
  kpiEvaluationRecordId: string;
  kpiName: string;
  kpiDescription?: string | null;
  targetValue?: number | null;
  actualValue?: number | null;
  achievementPercent?: number | null;
  unit?: string | null;
  notes?: string | null;
  achievedGrade?: string | null;
}

export interface PeerEvaluatorDetail {
  evaluationId: string;
  evaluatorId: string;
  evaluatorName: string;
  evaluatorEmployeeNumber?: string | null;
  evaluatorPosition?: string | null;
  isSubmitted: boolean;
  submittedDate?: string | null;
  totalScore?: number | null;
  competencyScores: PeerCompetencyScore[];
  kpiEvaluations: PeerKpiEvaluation[];
}

export interface ManagerPeerEvaluationReview {
  appraisalId: string;
  isAnonymous: boolean;
  allowKpiEvaluation: boolean;
  totalPeerEvaluators: number;
  submittedEvaluations: number;
  peerEvaluations: PeerEvaluatorDetail[];
}

// ── HR review ────────────────────────────────────────────────────────────────────

export interface CompetencyScoreSummary {
  criteriaName: string;
  description?: string | null;
  numericScore?: number | null;
  weight: number;
  weightedScore?: number | null;
  comments?: string | null;
}

export interface KpiScoreSummary {
  kpiName: string;
  description?: string | null;
  targetValue?: number | null;
  actualValue?: number | null;
  achievementPercentage?: number | null;
  /** The achievement was restated by calibration or an appeal — it is not actual ÷ target. */
  achievementOverridden?: boolean;
  unit?: string | null;
  notes?: string | null;
}

export interface EvaluationSummary {
  competencyScores: CompetencyScoreSummary[];
  kpiScores: KpiScoreSummary[];
  generalComments?: string | null;
  totalScore?: number | null;
}

export interface PeerCompetencyScoreSummary {
  criteriaName: string;
  averageScore?: number | null;
  responseCount: number;
}

export interface PeerKpiScoreSummary {
  kpiName: string;
  averageTarget?: number | null;
  averageActual?: number | null;
  responseCount: number;
}

export interface PeerEvaluationSummary {
  isAnonymous: boolean;
  allowKpiEvaluation: boolean;
  competencyScores: PeerCompetencyScoreSummary[];
  kpiScores: PeerKpiScoreSummary[];
}

/**
 * Everything HR needs to sign an appraisal off: the preconditions, a compliance check on the
 * weights, each leg's scores, and the weighted breakdown that produces the final score.
 *
 * `canProceedToHRReview` is the gate — false means one of self / manager / the minimum peer
 * count is still outstanding, and finalising will be refused with 422.
 */
export interface HRReview {
  appraisalId: string;
  appraisalNumber: string;
  employeeId: string;
  employeeName: string;
  position: string;
  organizationUnit: string;
  appraisalCycleName: string;
  status: AppraisalStatus;
  /**
   * The outcome is released to the appraisee (P2). While false, the appraisee's own copy has no
   * manager evaluation, peer summary, manager/peer/final score, grade or HR remarks.
   */
  outcomeReleased?: boolean;
  isSelfEvaluationComplete: boolean;
  isManagerEvaluationComplete: boolean;
  requiresPeerReviews: boolean;
  arePeerReviewsComplete: boolean;
  requiredPeerReviews: number;
  completedPeerReviews: number;
  canProceedToHRReview: boolean;
  weightTotalValid: boolean;
  totalWeight: number;
  isCycleActive: boolean;
  selfEvaluation?: EvaluationSummary | null;
  managerEvaluation?: EvaluationSummary | null;
  peerEvaluationSummary?: PeerEvaluationSummary | null;
  selfWeight: number;
  managerWeight: number;
  peerWeight: number;
  selfScore?: number | null;
  managerScore?: number | null;
  peerScore?: number | null;
  finalScore?: number | null;
  finalGrade?: string | null;
  isFinalized: boolean;
  hrRemarks?: string | null;
  finalizedDate?: string | null;
  finalizedByName?: string | null;
  employeeAcknowledgedDate?: string | null;
  hasAppeal: boolean;
  currentAppealStatus?: string | null;
  isRemandedAppeal: boolean;
  appealRemandedDate?: string | null;
  appealRemandDeadline?: string | null;
  isRemandDeadlineExceeded: boolean;
}

export interface HRReviewListItem {
  appraisalId: string;
  appraisalNumber: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  position: string;
  organizationUnit: string;
  appraisalCycleName: string;
  cycleYear: number;
  isSelfEvaluationComplete: boolean;
  isManagerEvaluationComplete: boolean;
  requiredPeerReviews: number;
  completedPeerReviews: number;
  arePeerReviewsComplete: boolean;
  isReadyForHRReview: boolean;
  /** `Not Started` | `In Review` | `Finalized`. */
  hrReviewStatus: string;
  isFinalized: boolean;
  finalizedDate?: string | null;
  finalizedByName?: string | null;
  finalScore?: number | null;
  finalGrade?: string | null;
}

export interface ApproveAppraisal {
  hrRemarks?: string | null;
}

export interface ReturnAppraisal {
  hrRemarks: string;
}

// ── Check-ins ────────────────────────────────────────────────────────────────────

/**
 * `privateNotes` are the conductor's own record and are returned by the API — do not show
 * them on any screen the employee sees.
 */
export interface CheckIn extends AuditFields {
  id: string;
  tenantId: string;
  appraisalCycleId: string;
  cycleCode?: string | null;
  employeeId: string;
  employeeName: string;
  conductedById: string;
  conductedByName: string;
  checkInType: CheckInType;
  title: string;
  scheduledDate: string;
  conductedDate?: string | null;
  agenda?: string | null;
  sharedNotes?: string | null;
  privateNotes?: string | null;
  actionItems?: string | null;
  followUpDate?: string | null;
  employeeComments?: string | null;
}

export interface CreateCheckIn {
  appraisalCycleId: string;
  employeeId: string;
  conductedById: string;
  checkInType: CheckInType;
  title: string;
  scheduledDate: string;
  agenda?: string | null;
}

export interface UpdateCheckIn extends CreateCheckIn {
  id: string;
  conductedDate?: string | null;
  sharedNotes?: string | null;
  privateNotes?: string | null;
  actionItems?: string | null;
  followUpDate?: string | null;
  employeeComments?: string | null;
}

export interface CompleteCheckIn {
  sharedNotes?: string | null;
  privateNotes?: string | null;
  actionItems?: string | null;
}

/**
 * A goal's status as recorded in a check-in. Saving one **also moves the goal itself** —
 * the percent, the reported status, and `flaggedAtRisk` are applied to the `EmployeeGoal`,
 * so flagging a goal here is what puts it in the at-risk reports.
 */
export interface CheckInGoalUpdate extends AuditFields {
  id: string;
  tenantId: string;
  checkInId: string;
  employeeGoalId: string;
  goalTitle: string;
  updatedProgress?: number | null;
  updatedStatus: GoalProgressStatus;
  flaggedAtRisk: boolean;
  note?: string | null;
}

export interface CreateCheckInGoalUpdate {
  checkInId: string;
  employeeGoalId: string;
  updatedProgress?: number | null;
  updatedStatus: GoalProgressStatus;
  flaggedAtRisk: boolean;
  note?: string | null;
}

export interface UpdateCheckInGoalUpdate extends CreateCheckInGoalUpdate {
  id: string;
}

// ── Helpers ──────────────────────────────────────────────────────────────────────

/** True when the item takes an actual value measured against a target rather than a 0–100 score. */
export const isKpiItem = (item: EvaluationItem): boolean => Boolean(item.kpiDefinitionId);

/**
 * Resolves a 0–100 score to its grade band name, matching the server's own resolution.
 * Bands are inclusive at both ends; returns null when nothing covers the score.
 */
export function resolveGrade(
  score: number | null | undefined,
  ranges: EvaluationGradeRange[],
): string | null {
  if (score === null || score === undefined) return null;
  return ranges.find((r) => score >= r.lowScore && score <= r.highScore)?.gradeName ?? null;
}

/**
 * KPI achievement as a percentage of target — the same rule as the server's
 * `AppraisalScoring.KpiAchievementPercent`, so the preview shows what will be scored: an actual
 * above the ceiling counts as the ceiling; with a floor, achievement runs from the floor (0 %)
 * to the target (100 %); and the result is clamped to 0–100, because hitting target is full
 * marks. The stored value is always the server's — this only saves a round trip while the user
 * types. It used to divide actual by target and nothing else, so it showed 150 % for a score
 * the server caps at 100 % and ignored the floor altogether (performance closure A12).
 */
export function kpiAchievementPercent(
  actual: number | null | undefined,
  target: number | null | undefined,
  min?: number | null,
  max?: number | null,
): number | null {
  if (actual === null || actual === undefined) return null;
  if (!target) return null;

  let value = actual;
  if (max !== null && max !== undefined && value > max) value = max;

  let percent: number;
  if (min !== null && min !== undefined && target !== min) {
    if (value <= min) return 0;
    percent = ((value - min) / (target - min)) * 100;
  } else {
    percent = (value / target) * 100;
  }

  return Math.round(Math.min(100, Math.max(0, percent)) * 10) / 10;
}

/**
 * The top of a rated item's own scale: its highest grade band, or 100 when it has none. Scores
 * above it are refused by the server (A11) — bands may stop below 100.
 */
export function scaleTop(ranges: EvaluationGradeRange[]): number {
  return ranges.length > 0 ? Math.max(...ranges.map((r) => r.highScore)) : 100;
}

/**
 * Turns a leg's sections into the payload the save endpoints want: only the items that carry
 * a value, keyed by `templateItemId`.
 *
 * This filtering is required, not a nicety — a submit is refused if any row arrives with both
 * `numericScore` and `actualValue` null.
 */
export function toItemScores(
  values: Record<string, { numericScore?: number | null; actualValue?: number | null; notes?: string | null; evidenceLinks?: string | null }>,
): EvaluationItemInput[] {
  return Object.entries(values)
    .filter(([, v]) => v && (v.numericScore !== null && v.numericScore !== undefined
      ? true
      : v.actualValue !== null && v.actualValue !== undefined))
    .map(([templateItemId, v]) => ({
      templateItemId,
      numericScore: v.numericScore ?? null,
      actualValue: v.actualValue ?? null,
      notes: v.notes?.trim() ? v.notes : null,
      evidenceLinks: v.evidenceLinks?.trim() ? v.evidenceLinks : null,
    }));
}

/** Counts scoreable items and how many carry a value — the "12 of 18 scored" progress line. */
export function countScored(
  items: EvaluationItem[],
  values: Record<string, { numericScore?: number | null; actualValue?: number | null }>,
  isScoreable: (item: EvaluationItem) => boolean = () => true,
): { total: number; scored: number } {
  const scoreable = items.filter(isScoreable);
  const scored = scoreable.filter((item) => {
    const v = values[item.templateItemId];
    if (!v) return false;
    return isKpiItem(item)
      ? v.actualValue !== null && v.actualValue !== undefined
      : v.numericScore !== null && v.numericScore !== undefined;
  });
  return { total: scoreable.length, scored: scored.length };
}

/**
 * The employee's written answer to their own appraisal, gated on the cycle's
 * `allowEmployeeResponse` setting.
 *
 * ⚠ There is no author field, and that is not an omission in this type — `AppraisalEmployeeResponse`
 * has no author COLUMN. The response is keyed to the appraisal alone, and the appraisal names its
 * employee; what makes the record true is that the write route refuses anyone else. Filing one from
 * the HR desk is a separate, deliberately unwired route for transcribing a paper response.
 */
export interface AppraisalEmployeeResponse {
  id: string;
  appraisalId: string;
  templateItemId?: string | null;
  templateItemName?: string | null;
  responseText: string;
  responseDate: string;
  responseStatus: string;
  submittedDate?: string | null;
}
