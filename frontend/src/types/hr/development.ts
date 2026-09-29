/**
 * Employee development plans — the growth half of a cycle: what someone is working on becoming
 * good at, the objectives that make that concrete, and the manager feedback recorded against it.
 *
 * **Not the same thing as an improvement plan.** A development plan is voluntary and forward
 * looking; a PIP (`types/hr/pip.ts`) is a corrective employment record with an approval workflow
 * and an outcome. They share nothing but the word "plan".
 *
 * **Deliberately off the workflow engine.** Employee and manager both write to a development
 * plan, feedback arrives at any time and objectives move independently of the plan's own status,
 * so there is no single-writer approval lifecycle for a workflow definition to own. Activating a
 * plan is the manager agreeing to support it, not an approval gate.
 *
 * Routes: `api/DevelopmentPlans`, `api/DevelopmentPlanFeedback`.
 */
import type { AuditFields } from './common';

export type DevelopmentPlanStatus = 'Draft' | 'Active' | 'OnHold' | 'Completed' | 'Cancelled';

export type DevelopmentObjectiveStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'Cancelled';

export type DevelopmentFeedbackType =
  | 'GeneralComment'
  | 'ProgressUpdate'
  | 'RiskFlag'
  | 'ReviewNote';

export const DEVELOPMENT_PLAN_STATUS_OPTIONS: { value: DevelopmentPlanStatus; label: string }[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Active', label: 'Active' },
  { value: 'OnHold', label: 'On hold' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Cancelled', label: 'Cancelled' },
];

export const DEVELOPMENT_OBJECTIVE_STATUS_OPTIONS: {
  value: DevelopmentObjectiveStatus;
  label: string;
}[] = [
  { value: 'NotStarted', label: 'Not started' },
  { value: 'InProgress', label: 'In progress' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Cancelled', label: 'Cancelled' },
];

export const DEVELOPMENT_FEEDBACK_TYPE_OPTIONS: {
  value: DevelopmentFeedbackType;
  label: string;
}[] = [
  { value: 'GeneralComment', label: 'General comment' },
  { value: 'ProgressUpdate', label: 'Progress update' },
  { value: 'RiskFlag', label: 'Risk flag' },
  { value: 'ReviewNote', label: 'Review note' },
];

export interface DevelopmentPlan extends AuditFields {
  tenantId: string;
  employeeId: string;
  employeeName: string;
  appraisalCycleId?: string | null;
  cycleCode?: string | null;
  cycleName?: string | null;
  title: string;
  /** DateOnly on the wire: `YYYY-MM-DD`, with no time part and no timezone. */
  startDate: string;
  endDate?: string | null;
  planStatus: DevelopmentPlanStatus;
  overallNotes?: string | null;
  /**
   * The login that wrote the plan (performance closure P10); null for a plan older than the
   * stamp. The employee may complete, cancel or delete only a plan they wrote themselves — the
   * server refuses the rest with 403.
   */
  authorUserId?: string | null;

  /** Rollup over the plan's objectives, computed server-side so a list needs one call. */
  objectiveCount: number;
  completedObjectiveCount: number;
  averageProgressPercent: number;
}

export interface CreateDevelopmentPlan {
  employeeId: string;
  appraisalCycleId?: string | null;
  title: string;
  startDate: string;
  endDate?: string | null;
  /** Honoured by the server — a plan saved as Draft stays a draft until it is activated. */
  planStatus?: DevelopmentPlanStatus;
  overallNotes?: string | null;
}

/**
 * The edit form's body. The server ignores `employeeId` and `planStatus` on an update
 * (performance closure P10): the plan stays with its employee, and its status moves only through
 * `PATCH …/status`.
 */
export interface UpdateDevelopmentPlan extends CreateDevelopmentPlan {
  id: string;
  planStatus: DevelopmentPlanStatus;
}

export interface DevelopmentObjective extends AuditFields {
  tenantId: string;
  developmentPlanId: string;
  title: string;
  description?: string | null;
  actions?: string | null;
  targetDate?: string | null;
  progressPercent: number;
  progressNotes?: string | null;
  objectiveStatus: DevelopmentObjectiveStatus;
  updatedInReviewEventId?: string | null;
}

export interface CreateDevelopmentObjective {
  developmentPlanId: string;
  title: string;
  description?: string | null;
  actions?: string | null;
  targetDate?: string | null;
  objectiveStatus?: DevelopmentObjectiveStatus;
}

export interface UpdateDevelopmentObjective extends CreateDevelopmentObjective {
  id: string;
  progressPercent: number;
  progressNotes?: string | null;
  objectiveStatus: DevelopmentObjectiveStatus;
}

export interface UpdateObjectiveProgress {
  progressPercent: number;
  notes?: string | null;
  status: DevelopmentObjectiveStatus;
}

export interface DevelopmentPlanFeedback {
  id: string;
  tenantId: string;
  developmentPlanId: string;
  managerId: string;
  feedbackType: DevelopmentFeedbackType;
  comment: string;
  createdAt: string;
  createdBy: string;
}

export interface CreateDevelopmentPlanFeedback {
  developmentPlanId: string;
  feedbackType: DevelopmentFeedbackType;
  comment: string;
  /**
   * Ignored by the server, which stamps the signed-in employee. Present only because the DTO
   * requires the field; do not try to write feedback under someone else's name.
   */
  managerId?: string;
}
