/**
 * Performance improvement plans — the corrective half of area 5.
 *
 * **The lifecycle.** Draft → PendingApproval → Active → (InProgress) → Completed / Unsuccessful,
 * or Cancelled. The first three belong to the generic workflow engine: a PIP is served on a named
 * employee, so it is written in draft and approved through a published
 * `PerformanceImprovementPlan` workflow definition before it is in force. Use
 * `WorkflowApprovalActions` / `WorkflowRecordTab` for that, never a bespoke approve button.
 *
 * ⚠ Submit/approve/reject/recall are inoperable until such a definition has been published and
 * `POST api/Workflow/entity-types/seed` re-run — approval authority comes from the definition,
 * not from a role.
 *
 * **Extending is not closing.** `Extended` is the one outcome that leaves the plan running: it
 * needs a `newEndDate` and pushes the end date out instead of writing a completion.
 *
 * **Who can see one.** HR, the employee it is about, the named supervisor and the named HR owner.
 * The org-wide dashboard is HR's alone; `mine` and `supervising` give everyone else their own
 * slice without opening it up.
 *
 * Routes: `api/Pip` (alias of `api/PerformanceImprovementPlans`), `api/PipDashboard`,
 * `api/PipMeeting`.
 */
import type { AuditFields } from './common';
import type { GoalProgressStatus } from './goals';

export type PipStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Active'
  | 'InProgress'
  | 'Completed'
  | 'Unsuccessful'
  | 'Cancelled';

/**
 * A review meeting's stored status (closure D-73): booked = Scheduled, *Record meeting* = Held, the
 * Cancel action = Cancelled. It used to be worked out from the date — past meant held.
 */
export type PipMeetingStatus = 'Scheduled' | 'Held' | 'Cancelled';

/** The numbers behind `PipMeetingStatus` — what the meeting form's `status` carries. */
export const PIP_MEETING_STATUS_VALUES: Record<PipMeetingStatus, number> = {
  Scheduled: 1,
  Held: 2,
  Cancelled: 3,
};

/** A plan that takes no more writes: its outcome recorded, or cancelled (D-73). */
export const PIP_CLOSED_STATUSES: readonly PipStatus[] = ['Completed', 'Unsuccessful', 'Cancelled'];

export type PipOutcome =
  | 'PerformanceImproved'
  | 'Extended'
  | 'Demotion'
  | 'Termination'
  | 'Transferred';

/** Numeric values behind `PipOutcome` — the outcome endpoint takes the int, not the name. */
export const PIP_OUTCOME_VALUES: Record<PipOutcome, number> = {
  PerformanceImproved: 1,
  Extended: 2,
  Demotion: 3,
  Termination: 4,
  Transferred: 5,
};

export const PIP_OUTCOME_OPTIONS: { value: PipOutcome; label: string; description: string }[] = [
  {
    value: 'PerformanceImproved',
    label: 'Performance improved',
    description: 'The plan worked. Closes it as successfully completed.',
  },
  {
    value: 'Extended',
    label: 'Extended',
    description: 'More time needed. Keeps the plan running to a new end date.',
  },
  {
    value: 'Demotion',
    label: 'Demotion',
    description: 'Closes the plan as unsuccessful and hands the action to HR.',
  },
  {
    value: 'Transferred',
    label: 'Transferred',
    description: 'Closes the plan as unsuccessful; the employee moves elsewhere.',
  },
  {
    value: 'Termination',
    label: 'Termination',
    description: 'Closes the plan as unsuccessful and hands the action to HR.',
  },
];

/** The statuses a plan can be moved between directly. Everything else is workflow or outcome. */
export const PIP_MANUAL_STATUS_OPTIONS: { value: PipStatus; label: string }[] = [
  { value: 'InProgress', label: 'In progress' },
  { value: 'Cancelled', label: 'Cancelled' },
];

export interface Pip extends AuditFields {
  tenantId: string;
  pipNumber: string;
  employeeId: string;
  employeeName: string;
  appraisalId?: string | null;
  appraisalNumber?: string | null;
  startDate: string;
  endDate: string;
  status: PipStatus;
  performanceIssues: string;
  expectedStandards: string;
  improvementActions: string;
  supportProvided: string;
  measurementCriteria: string;
  supervisorId: string;
  supervisorName: string;
  hrOwnerId?: string | null;
  hrOwnerName?: string | null;
  /** Free-text working notes. Rejection reasons from the approval workflow are appended here. */
  reviewSchedule?: string | null;
  completionDate?: string | null;
  outcome?: PipOutcome | null;
  outcomeNotes?: string | null;
}

export interface CreatePip {
  employeeId: string;
  appraisalId?: string | null;
  supervisorId: string;
  hrOwnerId?: string | null;
  startDate: string;
  endDate: string;
  performanceIssues: string;
  expectedStandards: string;
  improvementActions: string;
  supportProvided?: string | null;
  measurementCriteria?: string | null;
  reviewSchedule?: string | null;
}

export interface UpdatePip extends CreatePip {
  /** Sent for shape only — the server keeps the stored status, which is the workflow's to set. */
  status: PipStatus;
}

export interface PipPrepare {
  employeeId: string;
  employeeName: string;
  employeePosition: string;
  employeeDepartment: string;
  employeePhotoUrl?: string | null;
  appraisalId?: string | null;
  appraisalCycleName?: string | null;
  appraisalScore?: number | null;
  appraisalGradeLabel?: string | null;
  supervisorId: string;
  supervisorName: string;
  startDate: string;
  endDate: string;
}

export interface PipGoal extends AuditFields {
  tenantId: string;
  pipId: string;
  pipNumber: string;
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  dueDate: string;
  status: GoalProgressStatus;
  progressPercent?: number | null;
  progressNotes?: string | null;
}

export interface PipGoalRequest {
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  dueDate: string;
  status: GoalProgressStatus;
  progressPercent?: number | null;
  progressNotes?: string | null;
}

export interface PipReviewMeeting extends AuditFields {
  tenantId: string;
  pipId: string;
  meetingDate: string;
  status: PipMeetingStatus;
  employeeAttended: boolean;
  progressNotes: string;
  issuesDiscussed?: string | null;
  actionsAgreed?: string | null;
  employeeComments?: string | null;
  conductedById: string;
  conductedByName: string;
}

export interface PipAttachment {
  attachmentId: string;
  fileName: string;
  fileSizeBytes?: number | null;
  description?: string | null;
  uploadDate: string;
  uploadedByName: string;
  publicUrl?: string | null;
}

export interface PipMeetingSummary {
  meetingId: string;
  meetingDate: string;
  /** Stored (D-73). */
  status: PipMeetingStatus;
  /** Means something once the meeting is Held. */
  employeeAttended: boolean;
  conductedByName: string;
  progressNotesPreview: string;
  /** Held. */
  isCompleted: boolean;
}

/** The composite the detail screen reads — plan, goals, attachments and meetings in one call. */
export interface PipDetail {
  pipId: string;
  pipNumber: string;
  employeeId: string;
  employeeName: string;
  employeePosition: string;
  employeeDepartment: string;
  employeePhotoUrl?: string | null;
  appraisalId?: string | null;
  appraisalCycleName?: string | null;
  appraisalScore?: number | null;
  appraisalGradeLabel?: string | null;
  supervisorId: string;
  supervisorName: string;
  hrOwnerId?: string | null;
  hrOwnerName?: string | null;
  startDate: string;
  endDate: string;
  status: PipStatus;
  performanceIssues: string;
  expectedStandards: string;
  improvementActions: string;
  supportProvided: string;
  measurementCriteria: string;
  reviewSchedule?: string | null;
  goals: {
    goalId: string;
    title: string;
    description?: string | null;
    successCriteria?: string | null;
    dueDate: string;
    status: GoalProgressStatus;
    progressPercent?: number | null;
    progressNotes?: string | null;
  }[];
  attachments: PipAttachment[];
  reviewMeetings: PipMeetingSummary[];
  nextScheduledMeeting?: PipMeetingSummary | null;
  outcome?: PipOutcome | null;
  completionDate?: string | null;
  outcomeNotes?: string | null;
}

export interface PipListItem {
  pipId: string;
  pipNumber: string;
  employeeId: string;
  employeeName: string;
  position: string;
  department: string;
  photoUrl?: string | null;
  supervisorName: string;
  hrOwnerName?: string | null;
  startDate: string;
  endDate: string;
  totalDays: number;
  daysRemaining: number;
  periodProgressPercent: number;
  status: PipStatus;
  outcome?: PipOutcome | null;
  completionDate?: string | null;
  isOverdue: boolean;
  totalGoals: number;
  completedGoals: number;
  goalProgressPercent: number;
  totalMeetings: number;
  completedMeetings: number;
  nextMeetingDate?: string | null;
  appraisalId?: string | null;
  appraisalCycleName?: string | null;
  appraisalScore?: number | null;
  appraisalGradeLabel?: string | null;
}

export interface PipDashboard {
  pips: PipListItem[];
  activeCount: number;
  draftCount: number;
  pendingApprovalCount: number;
  overdueCount: number;
  completedThisYearCount: number;
  passedCount: number;
  failedCount: number;
  extendedCount: number;
  terminatedCount: number;
}

/** One goal's line on the meeting form — the progress the meeting agreed. */
export interface PipGoalMeetingUpdate {
  goalId: string;
  goalTitle: string;
  successCriteria?: string | null;
  dueDate: string;
  currentProgressPercent?: number | null;
  currentStatus: GoalProgressStatus;
}

/**
 * The meeting form. Both a read and a write shape — the server hands back the same object it
 * takes, with the PIP context filled in.
 *
 * `status` is the stored `PipMeetingStatus` as a number — 1 Scheduled, 2 Held, 3 Cancelled
 * (closure D-73) — and is read only: Record meeting and Cancel write it. `completedOn` is
 * presentational (the response to Record meeting); nothing stores it.
 */
export interface PipMeetingForm {
  meetingId?: string | null;
  pipId: string;
  pipNumber: string;
  /** The plan's status — meetings are written while it is in force, never once closed (D-73). */
  pipStatus: PipStatus;
  /** The plan's subject — the only person who writes `employeeComments` (P13). */
  employeeId: string;
  employeeName: string;
  employeePosition: string;
  employeePhotoUrl?: string | null;
  pipStartDate: string;
  pipEndDate: string;
  meetingNumber: number;
  totalScheduledMeetings: number;
  meetingDate: string;
  conductedById: string;
  conductedByName: string;
  employeeAttended: boolean;
  absenceReason?: string | null;
  progressNotes: string;
  issuesDiscussed?: string | null;
  actionsAgreed?: string | null;
  employeeComments?: string | null;
  employeeCommentsLastUpdated?: string | null;
  goalUpdates: PipGoalMeetingUpdate[];
  status: number;
  completedOn?: string | null;
}

export interface PipMeetingScheduleItem {
  meetingId: string;
  meetingNumber: number;
  meetingDate: string;
  /** The stored status as a number: 1 Scheduled, 2 Held, 3 Cancelled (D-73). */
  status: number;
  employeeAttended: boolean;
  conductedByName: string;
  progressNotesPreview: string;
}

export interface PipMeetingSchedule {
  pipId: string;
  meetings: PipMeetingScheduleItem[];
  canScheduleMore: boolean;
}

export interface EmployeeSearchResult {
  employeeId: string;
  fullName: string;
  position: string;
  department: string;
  photoUrl?: string | null;
  managerId?: string | null;
  managerName?: string | null;
}
