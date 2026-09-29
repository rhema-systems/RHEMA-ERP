/**
 * Performance analytics — the read-only view over a cycle that is already running.
 *
 * Two backends sit behind this, and they answer different questions:
 *
 *   `api/HRCycleDashboard/{cycleId}` — *where has this cycle got to?* One pre-aggregated payload
 *   covering the pipeline, the deadlines, the score spread, what the outcomes became, a
 *   per-unit breakdown, an attention list and an activity feed. Everything on the dashboard
 *   comes from this one call; nothing here is composed client-side from list endpoints.
 *
 *   `api/PerformanceAnalytics` — *how do the scores look?* The cycle's rating distribution
 *   (the calibration skew check) and one employee's multi-year trend.
 *
 * Both are HR-scoped reads except the employee trend, which is also readable by the employee
 * and their line manager.
 *
 * The dashboard is read-only with one exception: `nudge`, which raises an in-app notification
 * for whoever owes a single stalled appraisal's current step. Nothing on this screen approves
 * anything — the outcome pipeline links out to the workflow-backed screens instead.
 */
import type {
  AppraisalCycleStatus,
  AppraisalNotificationType,
  PerformanceRating,
} from './appraisal';
import type { CalibrationStatus } from './calibration';
import type { AppraisalSubStatus } from './outcomes';

// ── HR cycle dashboard ───────────────────────────────────────────────────────────

/** How close a phase deadline is. `Passed` means the phase is behind the cycle, not late. */
export type DeadlineState =
  | 'None'
  | 'Safe'
  | 'Approaching'
  | 'Imminent'
  | 'Today'
  | 'Overdue'
  | 'Passed';

export type AttentionReason =
  | 'OverdueAtStep'
  | 'PIPrecommendation'
  | 'TerminationRecommendation'
  | 'AppealFiled'
  | 'AppealOverdue'
  | 'CalibrationAdjustmentLarge'
  | 'ManagerEvalIncomplete'
  | 'LowScore';

export type AttentionSeverity = 'Info' | 'Warning' | 'Critical';

/**
 * One count per pipeline stage.
 *
 * ⚠ Pending and completed are resolved independently: pending is "sitting at this stage right
 * now", completed is "past it". They do not sum to `totalAppraisals` — an appraisal three
 * stages further on counts as completed for all three.
 */
export interface CyclePipelineProgress {
  totalAppraisals: number;
  goalSettingCount: number;
  peerNominationCount: number;
  selfEvalPendingCount: number;
  selfEvalCompletedCount: number;
  peerEvalPendingCount: number;
  peerEvalCompletedCount: number;
  managerEvalPendingCount: number;
  managerEvalCompletedCount: number;
  calibrationPendingCount: number;
  calibrationCompletedCount: number;
  hrReviewPendingCount: number;
  hrReviewCompletedCount: number;
  acknowledgmentPendingCount: number;
  acknowledgmentCompletedCount: number;
  appealCount: number;
  completedCount: number;
  closedCount: number;
}

export interface CycleDeadline {
  stepName: string;
  deadline?: string | null;
  state: DeadlineState;
  /** Negative once the deadline has passed. Null when the phase has no date set. */
  daysRemaining?: number | null;
  appraisalsAffected: number;
  appraisalsCompleted: number;
  isActive: boolean;
}

export interface CycleGradeDistributionItem {
  gradeDefinitionId: string;
  gradeName: string;
  /** Server-assigned slice colour. Kept for parity with the API; the charts colour by role. */
  color: string;
  count: number;
  /** Share of the *graded* population, so the slices total 100. */
  percentage: number;
}

/** What managers ticked on the appraisal form — intent, not outcome. */
export interface CycleRecommendationSummary {
  awardCount: number;
  promotionCount: number;
  incrementCount: number;
  trainingCount: number;
  pipCount: number;
  terminationCount: number;
}

/** One bucket of an outcome stream: a status (or a recommendation type) and its count. */
export interface CycleOutcomeCount {
  /** The enum member name, for linking to a filtered list. */
  key: string;
  label: string;
  count: number;
  /** The rows behind this count are out for approval on the workflow engine. */
  awaitingApproval: boolean;
}

/**
 * What the recommendations actually became.
 *
 * `CycleRecommendationSummary` counts intent; this counts records. The three downstream streams
 * run on the workflow engine, so a cycle can read as finished while its outcomes are queued
 * behind an approver.
 */
export interface CycleOutcomePipeline {
  recommendations: CycleOutcomeCount[];
  recommendationTypes: CycleOutcomeCount[];
  salaryProposals: CycleOutcomeCount[];
  employmentActionProposals: CycleOutcomeCount[];
  improvementPlans: CycleOutcomeCount[];
  totalRaised: number;
  awaitingApproval: number;
}

export interface CycleDepartmentProgress {
  organizationUnitId: string;
  departmentName: string;
  /** The unit's head where one is configured, otherwise the first appraisee's line manager. */
  managerName?: string | null;
  totalAppraisals: number;
  completedSteps: number;
  completionPercent: number;
  overdueCount: number;
  averageScore?: number | null;
  hasCalibrationSession: boolean;
  calibrationSessionStatus?: CalibrationStatus | null;
}

export interface CycleAttentionItem {
  appraisalId: string;
  employeeName: string;
  position: string;
  department: string;
  photoUrl?: string | null;
  managerName: string;
  reason: AttentionReason;
  reasonLabel: string;
  detail?: string | null;
  severity: AttentionSeverity;
  overallScore?: number | null;
  gradeLabel?: string | null;
  daysOverdue: number;
  currentSubStatus: AppraisalSubStatus;
}

export interface CycleActivityItem {
  timestamp: string;
  description: string;
  subjectEmployeeName?: string | null;
  actorName?: string | null;
  eventType: AppraisalNotificationType;
  appraisalId?: string | null;
  /** Rendered server-side ("3 days ago") so every client agrees on the wording. */
  timeAgo: string;
}

export interface HRCycleDashboard {
  cycleId: string;
  cycleName: string;
  year: number;
  cycleStatus: AppraisalCycleStatus;
  startDate: string;
  endDate: string;

  hasPeerReviews: boolean;
  hasCalibration: boolean;
  hasHRReview: boolean;
  hasAppeals: boolean;
  autoLockEnabled: boolean;
  minPeers: number;
  maxPeers: number;
  appealWindowDays: number;

  selfWeight: number;
  peerWeight: number;
  managerWeight: number;

  pipelineProgress: CyclePipelineProgress;
  deadlines: CycleDeadline[];
  gradeDistribution: CycleGradeDistributionItem[];
  /** Appraisals carrying an overall score — not the same population as the graded one. */
  scoredAppraisalCount: number;
  averageScore?: number | null;
  recommendations: CycleRecommendationSummary;
  outcomePipeline: CycleOutcomePipeline;
  departmentBreakdown: CycleDepartmentProgress[];
  attentionItems: CycleAttentionItem[];
  recentActivity: CycleActivityItem[];
}

/** What a nudge reached. `notificationsRaised` is lower than the recipient count when
 *  somebody already has an identical unread nudge — repeats are skipped, not stacked. */
export interface CycleNudgeResult {
  appraisalId: string;
  employeeName: string;
  stepName: string;
  recipients: string[];
  notificationsRaised: number;
}

// ── Score analytics ──────────────────────────────────────────────────────────────

export interface RatingBucket {
  rating: PerformanceRating;
  ratingLabel: string;
  count: number;
  percent: number;
}

/**
 * The cycle's finalised scores mapped onto rating bands, highest first.
 *
 * ⚠ Only appraisals with an `overallScore` are in here, so it is empty until sign-off starts —
 * and `totalRated` will be lower than the cycle's appraisal count for most of the year.
 */
export interface CalibrationDistribution {
  cycleId: string;
  cycleName?: string | null;
  totalRated: number;
  averageScore?: number | null;
  buckets: RatingBucket[];
}

export interface PerformanceTrendPoint {
  year: number;
  appraisalId: string;
  cycleName?: string | null;
  overallScore?: number | null;
  rating?: PerformanceRating | null;
  status?: string | null;
  /** Released to the appraisee (P2); on their own trend the score is null until then. */
  outcomeReleased?: boolean;
}

export interface EmployeePerformanceTrend {
  employeeId: string;
  employeeName?: string | null;
  points: PerformanceTrendPoint[];
}
