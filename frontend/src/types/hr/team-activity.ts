/**
 * Teams and committees — what a team is chartered to do, what it has undertaken, and who is doing it.
 *
 * Round 2, lane F1 (plan § 1.4, § 6.6). Transcribed from `TeamActivityDTOs.cs`.
 *
 * ⚠ Enums are **string** unions: the API registers `JsonStringEnumConverter`, so a numeric union
 * would compile and match nothing. Every member below is read off the C# enum rather than guessed
 * from the field name — the trap that put three legal forms into slice 1 that did not exist, with
 * `tsc` silent about all of them.
 */

/** `TeamTorStatus`. Approved is IMMUTABLE — changing it means a new version. */
export type TeamTorStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Superseded';

/** `TeamObjectiveStatus`. */
export type TeamObjectiveStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Active'
  | 'OnHold'
  | 'Completed'
  | 'Cancelled';

/** `TeamObjectiveProgressMode`. Under `FromTasks` the percentage is derived and cannot be typed. */
export type TeamObjectiveProgressMode = 'FromTasks' | 'Manual';

/** `TeamTaskPriority`. */
export type TeamTaskPriority = 'Low' | 'Normal' | 'High' | 'Urgent';

/** `TeamTaskStatus`. Blocked requires a reason. */
export type TeamTaskStatus = 'NotStarted' | 'InProgress' | 'Blocked' | 'Completed' | 'Cancelled';

export const TEAM_TOR_STATUS_LABELS: Record<TeamTorStatus, string> = {
  Draft: 'Draft',
  PendingApproval: 'Pending approval',
  Approved: 'Approved',
  Superseded: 'Superseded',
};

export const TEAM_OBJECTIVE_STATUS_LABELS: Record<TeamObjectiveStatus, string> = {
  Draft: 'Draft',
  PendingApproval: 'Pending approval',
  Active: 'Active',
  OnHold: 'On hold',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
};

export const TEAM_TASK_STATUS_LABELS: Record<TeamTaskStatus, string> = {
  NotStarted: 'Not started',
  InProgress: 'In progress',
  Blocked: 'Blocked',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
};

export const TEAM_TASK_PRIORITY_OPTIONS: { value: TeamTaskPriority; label: string }[] = [
  { value: 'Low', label: 'Low' },
  { value: 'Normal', label: 'Normal' },
  { value: 'High', label: 'High' },
  { value: 'Urgent', label: 'Urgent' },
];

export const TEAM_OBJECTIVE_PROGRESS_MODE_OPTIONS: {
  value: TeamObjectiveProgressMode;
  label: string;
  hint: string;
}[] = [
  {
    value: 'FromTasks',
    label: 'Counted from tasks',
    hint: 'The percentage is the share of this objective’s tasks that are done. It cannot be typed.',
  },
  {
    value: 'Manual',
    label: 'Entered by hand',
    hint: 'For an objective whose progress is not a count of anything. The typed figure is the only truth.',
  },
];

// ── Terms of reference ──────────────────────────────────────────────────────

export interface TeamTermsOfReference {
  id: string;
  teamId: string;
  version: number;
  status: TeamTorStatus;
  /** DateOnly */
  effectiveFrom: string;
  effectiveTo?: string | null;
  approvedByName?: string | null;
  approvedOn?: string | null;
  /** ⚠ Read-only: the signed charter arrives through the upload gate. */
  hasDocument: boolean;
  documentFileName?: string | null;
  /** Server-computed, so the badge and the nightly sweep cannot disagree about the same charter. */
  daysUntilExpiry?: number | null;
}

export interface TeamTermsOfReferenceDetail extends TeamTermsOfReference {
  previousVersionId?: string | null;
  purpose: string;
  scope?: string | null;
  authority?: string | null;
  membershipRules?: string | null;
  meetingCadence?: string | null;
  reportingLine?: string | null;
  deliverables?: string | null;
  notes?: string | null;
  documentMimeType?: string | null;
  documentFileSizeBytes?: number | null;
  /**
   * Why an approver sent this version back (lane F3). Null when it has never been refused.
   *
   * ⚠ On the DETAIL only — a refusal is read once and acted on, and a column of them on the
   * version table would make a committee's history read as a list of complaints.
   */
  rejectionReason?: string | null;
}

export interface CreateTeamTermsOfReferenceRequest {
  purpose: string;
  scope?: string | null;
  authority?: string | null;
  membershipRules?: string | null;
  meetingCadence?: string | null;
  reportingLine?: string | null;
  deliverables?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  notes?: string | null;
}

export type UpdateTeamTermsOfReferenceRequest = CreateTeamTermsOfReferenceRequest;

// ── Objectives ──────────────────────────────────────────────────────────────

export interface TeamObjective {
  id: string;
  teamId: string;
  code?: string | null;
  title: string;
  status: TeamObjectiveStatus;
  progressPercent: number;
  progressMode: TeamObjectiveProgressMode;
  weight?: number | null;
  startDate: string;
  dueDate?: string | null;
  ownerMemberId?: string | null;
  ownerName?: string | null;
  taskCount: number;
  completedTaskCount: number;
  isOverdue: boolean;
}

export interface TeamObjectiveDetail extends TeamObjective {
  description?: string | null;
  measure?: string | null;
  targetValue?: number | null;
  unit?: string | null;
  outcomeSummary?: string | null;
  completedOn?: string | null;
  cancelledReason?: string | null;
  /**
   * Why an approver refused to let this objective start (lane F3).
   *
   * ⚠ Not the same as `cancelledReason`: refused-before-it-began and stopped-part-way are
   * different facts, and one column cannot tell them apart a year later.
   */
  rejectionReason?: string | null;
}

export interface CreateTeamObjectiveRequest {
  code?: string | null;
  title: string;
  description?: string | null;
  measure?: string | null;
  targetValue?: number | null;
  unit?: string | null;
  weight?: number | null;
  startDate: string;
  dueDate?: string | null;
  ownerMemberId?: string | null;
  progressMode: TeamObjectiveProgressMode;
  /**
   * ⚠ Only send under `Manual`. Under `FromTasks` the server REFUSES a supplied figure with 422
   * rather than ignoring it — a field that accepts input and discards it is worse than one that
   * says no.
   */
  progressPercent?: number | null;
}

export type UpdateTeamObjectiveRequest = CreateTeamObjectiveRequest;

export interface TeamObjectiveStatusChangeRequest {
  /** Required to complete below 100 %. */
  outcomeSummary?: string | null;
  /** Required to cancel. */
  cancelledReason?: string | null;
}

export interface TeamObjectiveWeightTotal {
  total: number;
  isBalanced: boolean;
}

// ── Tasks ───────────────────────────────────────────────────────────────────

export interface TeamTask {
  id: string;
  teamId: string;
  objectiveId?: string | null;
  objectiveTitle?: string | null;
  title: string;
  status: TeamTaskStatus;
  priority: TeamTaskPriority;
  startDate?: string | null;
  dueDate?: string | null;
  assigneeMemberId?: string | null;
  assigneeName?: string | null;
  /** So a screen can tell whether the task is the viewer's own. */
  assigneeEmployeeId?: string | null;
  blockedReason?: string | null;
  isOverdue: boolean;
  checklistTotal: number;
  checklistDone: number;
  attachmentCount: number;
}

export interface TeamTaskDetail extends TeamTask {
  description?: string | null;
  completedOn?: string | null;
  completionNotes?: string | null;
  sourceMeetingDecisionId?: string | null;
  checklistItems: TeamTaskChecklistItem[];
  attachments: TeamTaskAttachment[];
}

export interface CreateTeamTaskRequest {
  objectiveId?: string | null;
  title: string;
  description?: string | null;
  assigneeMemberId?: string | null;
  priority: TeamTaskPriority;
  startDate?: string | null;
  dueDate?: string | null;
}

export type UpdateTeamTaskRequest = CreateTeamTaskRequest;

export interface TeamTaskStatusChangeRequest {
  status: TeamTaskStatus;
  /** ⚠ Required when moving to Blocked. */
  blockedReason?: string | null;
  completionNotes?: string | null;
}

export interface TeamTaskChecklistItem {
  id: string;
  taskId: string;
  displayOrder: number;
  text: string;
  isDone: boolean;
  doneByName?: string | null;
  doneAt?: string | null;
}

export interface CreateTeamTaskChecklistItemRequest {
  text: string;
  displayOrder: number;
}

export interface UpdateTeamTaskChecklistItemRequest {
  text: string;
  displayOrder: number;
  isDone: boolean;
}

export interface TeamTaskAttachment {
  id: string;
  taskId: string;
  title?: string | null;
  fileName?: string | null;
  mimeType?: string | null;
  fileSizeBytes?: number | null;
  uploadedByName?: string | null;
  createdAt: string;
}
