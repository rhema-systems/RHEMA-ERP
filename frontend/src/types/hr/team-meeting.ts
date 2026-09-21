/**
 * Teams and committees, slice F2 — the minute book, the reviews, and the dashboard.
 *
 * ⚠ Enums are **string** unions: the API registers `JsonStringEnumConverter`, so a numeric union
 * would compile and match nothing.
 *
 * Round 2, lane F2 (plan § 6.6).
 */

import type { TeamObjectiveStatus, TeamTaskPriority, TeamTorStatus } from './team-activity';

/** `TeamMeetingKind`. A site visit produces decisions exactly as a meeting does. */
export type TeamMeetingKind = 'Meeting' | 'Workshop' | 'SiteVisit' | 'Other';

/** `TeamMeetingStatus`. */
export type TeamMeetingStatus = 'Scheduled' | 'Held' | 'Cancelled';

/** `TeamReviewStatus`. A submitted review is immutable; the lead acknowledges, not approves. */
export type TeamReviewStatus = 'Draft' | 'Submitted' | 'Acknowledged';

export const TEAM_MEETING_KIND_OPTIONS: { value: TeamMeetingKind; label: string }[] = [
  { value: 'Meeting', label: 'Meeting' },
  { value: 'Workshop', label: 'Workshop' },
  { value: 'SiteVisit', label: 'Site visit' },
  { value: 'Other', label: 'Other' },
];

export const TEAM_MEETING_STATUS_LABELS: Record<TeamMeetingStatus, string> = {
  Scheduled: 'Scheduled',
  Held: 'Held',
  Cancelled: 'Cancelled',
};

export const TEAM_REVIEW_STATUS_LABELS: Record<TeamReviewStatus, string> = {
  Draft: 'Draft',
  Submitted: 'Submitted',
  Acknowledged: 'Acknowledged',
};

// ── Meetings ────────────────────────────────────────────────────────────────

export interface TeamMeeting {
  id: string;
  teamId: string;
  kind: TeamMeetingKind;
  title: string;
  /** DateTime */
  scheduledAt: string;
  heldAt?: string | null;
  venue?: string | null;
  status: TeamMeetingStatus;
  chairMemberId?: string | null;
  chairName?: string | null;
  /** ⚠ Read-only: the signed minutes arrive through the upload gate. */
  hasMinutesDocument: boolean;
  minutesFileName?: string | null;
  attendeeCount: number;
  /**
   * How many invitees actually came.
   *
   * ⚠ Counts only attendance KNOWN to be true. Before a meeting is held every attendance is null,
   * so this is 0 — which reads correctly as "nobody has come yet", not "nobody came".
   */
  attendedCount: number;
  decisionCount: number;
  /** Decisions turned into a task. The rest are still only minutes. */
  decisionsWithTaskCount: number;
}

export interface TeamMeetingDetail extends TeamMeeting {
  agenda?: string | null;
  minutes?: string | null;
  cancelledReason?: string | null;
  minutesMimeType?: string | null;
  minutesFileSizeBytes?: number | null;
  attendees: TeamMeetingAttendee[];
  decisions: TeamMeetingDecision[];
}

export interface CreateTeamMeetingRequest {
  kind: TeamMeetingKind;
  title: string;
  scheduledAt: string;
  venue?: string | null;
  agenda?: string | null;
  chairMemberId?: string | null;
  /** ⚠ A REPLACE SET on update — an invitee omitted is an invitee removed. */
  attendeeMemberIds: string[];
}

export type UpdateTeamMeetingRequest = CreateTeamMeetingRequest;

export interface HoldTeamMeetingRequest {
  heldAt?: string | null;
  minutes?: string | null;
  /** ⚠ NOT a replace set — anyone omitted keeps what was already recorded. */
  attendance: TeamMeetingAttendanceEntry[];
}

export interface TeamMeetingAttendanceEntry {
  memberId: string;
  attended?: boolean | null;
  apology: boolean;
  notes?: string | null;
}

export interface TeamMeetingAttendee {
  id: string;
  meetingId: string;
  memberId: string;
  memberName?: string | null;
  employeeId?: string | null;
  /** ⚠ Null means "not yet known", not "absent". */
  attended?: boolean | null;
  apology: boolean;
  notes?: string | null;
}

export interface TeamMeetingDecision {
  id: string;
  meetingId: string;
  displayOrder: number;
  text: string;
  responsibleMemberId?: string | null;
  responsibleName?: string | null;
  dueDate?: string | null;
  /** The task raised from this decision, if one has been. */
  raisedTaskId?: string | null;
  raisedTaskTitle?: string | null;
}

export interface CreateTeamMeetingDecisionRequest {
  text: string;
  displayOrder: number;
  responsibleMemberId?: string | null;
  dueDate?: string | null;
}

export type UpdateTeamMeetingDecisionRequest = CreateTeamMeetingDecisionRequest;

/**
 * ⚠ Only the title, objective and priority are supplied. The ASSIGNEE and DUE DATE come from the
 * decision — an action item that quietly acquired a different owner from the one the meeting named
 * would leave the minute saying one thing and the board another.
 */
export interface RaiseTaskFromDecisionRequest {
  title?: string | null;
  objectiveId?: string | null;
  priority: TeamTaskPriority;
}

// ── Reviews ─────────────────────────────────────────────────────────────────

export interface TeamReview {
  id: string;
  teamId: string;
  periodStart: string;
  periodEnd: string;
  reviewedByName?: string | null;
  overallRating?: number | null;
  status: TeamReviewStatus;
  acknowledgedByName?: string | null;
  acknowledgedOn?: string | null;
  lineCount: number;
}

export interface TeamReviewDetail extends TeamReview {
  summary?: string | null;
  recommendations?: string | null;
  acknowledgementNote?: string | null;
  lines: TeamReviewLine[];
}

export interface CreateTeamReviewRequest {
  periodStart: string;
  periodEnd: string;
  overallRating?: number | null;
  summary?: string | null;
  recommendations?: string | null;
  // ⚠ No reviewedById — the reviewer is the token's employee.
}

export type UpdateTeamReviewRequest = CreateTeamReviewRequest;

export interface TeamReviewLine {
  id: string;
  reviewId: string;
  objectiveId: string;
  objectiveTitle?: string | null;
  /** ⚠ What the objective stood at WHEN THE LINE WAS WRITTEN, not now. */
  progressAtReview: number;
  rating?: number | null;
  comment?: string | null;
}

export interface UpsertTeamReviewLineRequest {
  objectiveId: string;
  rating?: number | null;
  comment?: string | null;
  // ⚠ No progressAtReview — the server snapshots it, so a review cannot claim a figure the
  // objective never held.
}

export interface AcknowledgeTeamReviewRequest {
  note?: string | null;
}

// ── Dashboard ───────────────────────────────────────────────────────────────

export interface TeamDashboard {
  teamId: string;
  teamName: string;

  termsOfReferenceId?: string | null;
  termsVersion?: number | null;
  termsStatus?: TeamTorStatus | null;
  termsDaysUntilExpiry?: number | null;
  /** ⚠ A committee operating with no approved charter — the most useful thing here. */
  hasNoApprovedTerms: boolean;

  objectivesActive: number;
  objectivesCompleted: number;
  objectivesOverdue: number;
  averageProgressPercent: number;
  objectiveWeightTotal: number;
  objectiveWeightsBalanced: boolean;

  tasksOpen: number;
  tasksOverdue: number;
  tasksDueThisWeek: number;
  tasksBlocked: number;
  /** Open work nobody owns. */
  tasksUnassigned: number;

  nextMeetingId?: string | null;
  nextMeetingTitle?: string | null;
  nextMeetingAt?: string | null;
  lastMeetingHeldAt?: string | null;
  /** Decisions with somebody responsible that never became a task. */
  unactionedDecisions: number;

  lastReviewPeriodEnd?: string | null;
  lastReviewRating?: number | null;
  lastReviewStatus?: TeamReviewStatus | null;
}

// ── The sweep ───────────────────────────────────────────────────────────────

export interface TeamReminderRun {
  id: string;
  startedAt: string;
  completedAt?: string | null;
  trigger: string;
  remindersQueued: number;
}

export interface TeamReminderDispatch {
  id: string;
  runId: string;
  kind: string;
  itemType: string;
  entityId: string;
  teamId: string;
  reference: string;
  dueDate?: string | null;
  daysRemaining: number;
  routedToEmployeeId?: string | null;
  createdAt: string;
}

export interface TeamReminderRunResult {
  runId: string;
  remindersQueued: number;
  teamsSwept: number;
  byKind: Record<string, number>;
}

/** Re-exported so a screen importing the dashboard need not reach into two modules. */
export type { TeamObjectiveStatus, TeamTorStatus };
