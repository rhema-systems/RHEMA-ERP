/**
 * Interim reviews — the quarterly and mid-year checkpoints between goal setting and the year-end
 * appraisal.
 *
 * **They are generated, not created.** How many exist and how deep they go is a cycle setting:
 * `reviewFrequency` (None / MidYearOnly / Quarterly / Custom) decides how many, and
 * `interimReviewDepth` (LightTouch / FullAppraisal) decides whether each is a conversation with
 * progress notes or a scored appraisal of the period's goals. The events are written when the cycle
 * generates its appraisals, so the only reason to create one by hand is `Custom`.
 *
 * **Not a conversation.** An `AppraisalConversation` (`types/hr/conversations.ts`) is the *meeting*
 * — an agenda, a held date, notes. A review event is the *checkpoint*: what the employee submitted,
 * what progress each goal made, and for a full interim appraisal a period score. A conversation can
 * point at a review event via its `reviewEventId`.
 *
 * **Two shapes, one entity.** `isLightTouch` and `isFullAppraisal` are mirror images set from the
 * cycle's depth setting. A light-touch event runs submit → complete. A full appraisal runs submit →
 * `finalize-full-appraisal`, which scores each goal and aggregates a weighted `overallPeriodScore`.
 *
 * Route: `api/AppraisalReviewEvents`.
 */
import type { AuditFields } from './common';
import type { GoalPeriod, GoalProgressStatus } from './goals';

export type ReviewEventType =
  | 'GoalSetting'
  | 'QuarterlyQ1'
  | 'QuarterlyQ2'
  | 'MidYearReview'
  | 'QuarterlyQ3'
  | 'QuarterlyQ4'
  | 'YearEndReview';

/**
 * `InProgress` exists on the enum but nothing writes it — an event goes Pending →
 * EmployeeSubmitted → Completed. `Cancelled` is written when the appraisal is withdrawn from its
 * cycle (closure D-74), and closes the event as Completed does: it takes no more work.
 */
export type AppraisalReviewStatus =
  | 'Pending'
  | 'InProgress'
  | 'EmployeeSubmitted'
  | 'Completed'
  | 'Cancelled';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

export const REVIEW_EVENT_TYPE_OPTIONS = opts<ReviewEventType>([
  ['GoalSetting', 'Goal setting'],
  ['QuarterlyQ1', 'Quarterly — Q1'],
  ['QuarterlyQ2', 'Quarterly — Q2'],
  ['MidYearReview', 'Mid-year review'],
  ['QuarterlyQ3', 'Quarterly — Q3'],
  ['QuarterlyQ4', 'Quarterly — Q4'],
  ['YearEndReview', 'Year-end review'],
]);

export const REVIEW_STATUS_OPTIONS = opts<AppraisalReviewStatus>([
  ['Pending', 'Not started'],
  ['InProgress', 'In progress'],
  ['EmployeeSubmitted', 'Awaiting manager'],
  ['Completed', 'Completed'],
  ['Cancelled', 'Cancelled'],
]);

export interface AppraisalReviewEvent extends AuditFields {
  tenantId: string;
  appraisalCycleId: string;
  cycleCode?: string | null;
  performanceAppraisalId: string;
  appraisalNumber?: string | null;
  /** The appraisee — the team list is unreadable without it. */
  employeeId: string;
  employeeName?: string | null;
  type: ReviewEventType;
  /** Date-only on the wire: `YYYY-MM-DD`. */
  eventDate: string;
  status: AppraisalReviewStatus;
  isLightTouch: boolean;
  isFullAppraisal: boolean;
  /** Only ever set by `finalize-full-appraisal`; null on a light-touch event. */
  overallPeriodScore?: number | null;
  achievementsSummary?: string | null;
  challengesSummary?: string | null;
  /** The employee's own notes. `managerNotes` is the other side and is written on complete. */
  notes?: string | null;
  managerNotes?: string | null;
  conversationId?: string | null;
  updatedDevelopmentPlanId?: string | null;
}

/** HR only, and only really needed when the cycle's `reviewFrequency` is `Custom`. */
export interface CreateAppraisalReviewEvent {
  appraisalCycleId: string;
  performanceAppraisalId: string;
  type: ReviewEventType;
  eventDate: string;
  isLightTouch: boolean;
  isFullAppraisal: boolean;
}

/**
 * Rescheduling and amending summaries only.
 *
 * ⚠ The cycle, the appraisal, the status and the period score are **not** editable here — the
 * server ignores them. Status moves through `submit` / `complete` / `finalizeFullAppraisal`, which
 * are the calls that run the gates (self-assessment required, goal progress required).
 */
export interface UpdateAppraisalReviewEvent {
  id: string;
  type: ReviewEventType;
  eventDate: string;
  isLightTouch: boolean;
  isFullAppraisal: boolean;
  achievementsSummary?: string | null;
  challengesSummary?: string | null;
  notes?: string | null;
  managerNotes?: string | null;
  conversationId?: string | null;
  updatedDevelopmentPlanId?: string | null;
}

export interface SubmitReviewEvent {
  achievementsSummary?: string | null;
  challengesSummary?: string | null;
}

export interface CompleteReviewEvent {
  notes?: string | null;
  managerNotes?: string | null;
}

// ── Full interim appraisal ───────────────────────────────────────────────────────

export interface InterimGoalScore {
  employeeGoalId: string;
  title: string;
  /** Relative weight within the period score. Zero weights fall back to a plain average. */
  weight: number;
  period: GoalPeriod | string;
  currentProgress?: number | null;
  /** A score already recorded for this goal at this event, so the form can reopen part-scored. */
  existingScore?: number | null;
}

export interface FullInterimAppraisalContext {
  event: AppraisalReviewEvent;
  employeeName?: string | null;
  goals: InterimGoalScore[];
}

export interface InterimGoalScoreInput {
  employeeGoalId: string;
  /** 0–100. */
  score: number;
  note?: string | null;
}

export interface FinalizeFullInterimAppraisal {
  scores: InterimGoalScoreInput[];
  managerNotes?: string | null;
}

// ── Progress recorded at a review ────────────────────────────────────────────────

/**
 * As `CreateGoalProgressEntry` but without `recordedById` — on this route the recorder is taken
 * from the token, and `reviewEventId` comes from the URL.
 *
 * ⚠ Recording progress here also moves the goal itself (percent and execution status), exactly as
 * the goal screen's own progress entry does. It is not a note.
 */
export interface RecordReviewProgressEntry {
  employeeGoalId: string;
  progressPercent?: number | null;
  actualValue?: number | null;
  status: GoalProgressStatus;
  challenges?: string | null;
  notes?: string | null;
}
