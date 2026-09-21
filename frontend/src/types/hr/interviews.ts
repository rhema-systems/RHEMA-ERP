/**
 * Recruitment — slice C: interviews, the question bank, presets and scorecards.
 *
 * Three controllers: `api/job-interviews`, `api/interview-question-bank` (setup) and
 * `api/interview-question-presets` (setup).
 *
 * ⚠ **The interview controller's authorization is per record, not per role**, which is unlike every
 * other recruitment area. An interview is readable by HR *or by a panelist sitting on that
 * interview* — the panel are ordinary employees, and they have to open the session, read the
 * questions and file a scorecard. Anything structural (scheduling, the panel, the question plan,
 * the whole-tenant list reads) is HR's. The two setup controllers are flat HR-only.
 *
 * The practical consequence for the UI: a panelist reaches their sessions through
 * `me/panelist-slots`, never through the list endpoints, and gets a 403 from the list endpoints if
 * they try. Screens that serve both audiences must branch on the caller's role rather than assume
 * the HR reads will answer.
 */

import type { HrPagedResult } from './recruitment';

export type { HrPagedResult };

// ── enums (string unions matching the backend's JSON) ──────────────────────

export const INTERVIEW_STATUSES = [
  'Scheduled',
  'Rescheduled',
  'InProgress',
  'Completed',
  'NoShow',
  'Cancelled',
] as const;
export type JobInterviewStatus = (typeof INTERVIEW_STATUSES)[number];

/** Statuses from which nothing further can be done. The server owns the real rules. */
export const TERMINAL_INTERVIEW_STATUSES: readonly JobInterviewStatus[] = ['Completed', 'Cancelled'];

export const INTERVIEW_TYPES = [
  'Screening',
  'OneOnOne',
  'Panel',
  'Technical',
  'CompetencyBased',
  'CaseStudy',
  'Presentation',
] as const;
export type JobInterviewType = (typeof INTERVIEW_TYPES)[number];

export const INTERVIEW_MODES = ['InPerson', 'Phone', 'Video', 'Hybrid'] as const;
export type InterviewMode = (typeof INTERVIEW_MODES)[number];

export const PANELIST_ROLES = ['Chair', 'Member', 'TechnicalAssessor', 'Observer'] as const;
export type JobInterviewPanelistRole = (typeof PANELIST_ROLES)[number];

export const INTERVIEW_OUTCOMES = [
  'HighlyRecommended',
  'Recommended',
  'Acceptable',
  'NotRecommended',
  'ProceedToNextRound',
  'Rejected',
  'OnHold',
] as const;
export type JobInterviewOutcome = (typeof INTERVIEW_OUTCOMES)[number];

export const INTERVIEW_RECOMMENDATIONS = [
  'StrongHire',
  'Hire',
  'Neutral',
  'NoHire',
  'StrongNoHire',
] as const;
export type JobInterviewRecommendation = (typeof INTERVIEW_RECOMMENDATIONS)[number];

/**
 * `Recommendation` is stored as a nullable int on a draft so it can be saved before the panelist has
 * made up their mind. These are the 1-based ordinals of INTERVIEW_RECOMMENDATIONS.
 */
export const RECOMMENDATION_ORDINALS: Record<JobInterviewRecommendation, number> = {
  StrongHire: 1,
  Hire: 2,
  Neutral: 3,
  NoHire: 4,
  StrongNoHire: 5,
};

export function recommendationFromOrdinal(value: number | null | undefined) {
  if (!value) return undefined;
  return INTERVIEW_RECOMMENDATIONS[value - 1];
}

// ── question bank ──────────────────────────────────────────────────────────

export interface InterviewQuestionType {
  id: string;
  tenantId: string;
  typeName: string;
  code?: string | null;
  description?: string | null;
  isActive: boolean;
  questionCount?: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface InterviewQuestionTypeSummary {
  id: string;
  typeName: string;
  code?: string | null;
  description?: string | null;
  isActive: boolean;
  questionCount?: number;
}

export interface CreateInterviewQuestionType {
  typeName: string;
  code?: string | null;
  description?: string | null;
  isActive: boolean;
}

export interface UpdateInterviewQuestionType extends CreateInterviewQuestionType {
  id: string;
}

export interface InterviewQuestion {
  id: string;
  tenantId: string;
  questionTypeId: string;
  questionTypeName?: string | null;
  questionText: string;
  /** Multiplier applied to the normalised score — see `weightedScore` on a score entry. */
  weight: number;
  minScore: number;
  maxScore: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateInterviewQuestion {
  questionTypeId: string;
  questionText: string;
  weight: number;
  minScore: number;
  maxScore: number;
  isActive: boolean;
}

export interface UpdateInterviewQuestion extends CreateInterviewQuestion {
  id: string;
}

// ── presets ────────────────────────────────────────────────────────────────

export interface InterviewQuestionPresetItem {
  id: string;
  presetId: string;
  questionTypeId: string;
  questionTypeName?: string | null;
  /** Minimum number of this type that must be scored before a card can be signed off. */
  requiredQuestionCount: number;
  /** How many are drawn from the bank to form the panel's pool. */
  allowedPoolSize: number;
  displayOrder: number;
}

export interface InterviewQuestionPreset {
  id: string;
  tenantId: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  items: InterviewQuestionPresetItem[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface InterviewQuestionPresetSummary {
  id: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  itemCount: number;
}

export interface CreateInterviewQuestionPresetItem {
  questionTypeId: string;
  requiredQuestionCount: number;
  allowedPoolSize: number;
  displayOrder: number;
}

export interface CreateInterviewQuestionPreset {
  name: string;
  description?: string | null;
  isActive: boolean;
  items: CreateInterviewQuestionPresetItem[];
}

/**
 * ⚠ **Replace-set payload.** `items` is the whole set: an item omitted here is deleted server-side.
 * Send the complete list every time, never a delta. Same convention as position entitlements.
 */
export interface UpdateInterviewQuestionPreset extends CreateInterviewQuestionPreset {
  id: string;
  items: (CreateInterviewQuestionPresetItem & { id?: string })[];
}

// ── interviews ─────────────────────────────────────────────────────────────

export interface JobInterview {
  id: string;
  tenantId: string;
  interviewNumber: string;
  jobVacancyId: string;
  vacancyNumber: string;
  jobTitle: string;
  round: number;
  type: JobInterviewType;
  mode: InterviewMode;
  status: JobInterviewStatus;
  scheduledDate: string;
  startTime: string;
  endTime: string;
  locationOrLink?: string | null;
  instructions?: string | null;
  rescheduleReason?: string | null;
  /** Set the first time an interview is rescheduled; null on one that never moved. */
  originalDate?: string | null;
  cancellationReason?: string | null;
  intervieweeCount: number;
  panelistCount: number;
  questionPresetId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface JobInterviewSummary {
  id: string;
  interviewNumber: string;
  jobVacancyId: string;
  vacancyNumber: string;
  jobTitle: string;
  round: number;
  type: JobInterviewType;
  typeName: string;
  mode: InterviewMode;
  modeName: string;
  status: JobInterviewStatus;
  statusName: string;
  scheduledDate: string;
  startTime: string;
  endTime: string;
  locationOrLink?: string | null;
  intervieweeCount: number;
  panelistCount: number;
  candidateNames: string[];
  panelistNames: string[];
  questionPresetId?: string | null;
}

export interface JobInterviewDetail extends JobInterview {
  interviewees: JobInterviewee[];
  panelists: JobInterviewPanelist[];
  externalPanelists: JobInterviewExternalPanelist[];
  questions: JobInterviewQuestionPlan[];
}

/** Optional per-candidate slot inside the session window. */
export interface ApplicationSlotEntry {
  applicationId: string;
  slotStartTime?: string | null;
  slotEndTime?: string | null;
}

export interface CreateJobInterview {
  jobVacancyId: string;
  round: number;
  type: JobInterviewType;
  mode: InterviewMode;
  scheduledDate: string;
  startTime: string;
  endTime: string;
  locationOrLink?: string | null;
  instructions?: string | null;
  applicationIds?: string[];
  applicationSlots?: ApplicationSlotEntry[];
  panelistEmployeeIds?: string[];
  externalPanelistAssociateIds?: string[];
  questionPresetId?: string | null;
}

/**
 * ⚠ **No `status` field, deliberately.** It used to be copied straight onto the entity, so a plain
 * save could cancel an interview with no reason or mark one complete that never happened. Status is
 * owned by reschedule / cancel / complete, which carry the side effects — rotating the candidates'
 * confirmation tokens, re-sending invitations, recording a reason.
 */
export interface UpdateJobInterview {
  id: string;
  round: number;
  type: JobInterviewType;
  mode: InterviewMode;
  scheduledDate: string;
  startTime: string;
  endTime: string;
  locationOrLink?: string | null;
  instructions?: string | null;
  questionPresetId?: string | null;
}

export interface RescheduleJobInterview {
  interviewId: string;
  newDate: string;
  newStartTime: string;
  newEndTime: string;
  locationOrLink?: string | null;
  rescheduleReason: string;
}

export interface CancelJobInterview {
  interviewId: string;
  cancellationReason: string;
}

// ── panel ──────────────────────────────────────────────────────────────────

export interface JobInterviewPanelist {
  id: string;
  jobInterviewId: string;
  interviewNumber: string;
  employeeId: string;
  employeeName: string;
  employeePositionTitle?: string | null;
  role: JobInterviewPanelistRole;
  isRequired: boolean;
  attended?: boolean | null;
  noShowReason?: string | null;
  invitationSentDate?: string | null;
  isConfirmed: boolean;
  confirmationDate?: string | null;
}

export interface JobInterviewExternalPanelist {
  id: string;
  jobInterviewId: string;
  interviewNumber: string;
  associateId: string;
  associateName: string;
  associateOrganization?: string | null;
  role: JobInterviewPanelistRole;
  isRequired: boolean;
  attended?: boolean | null;
  noShowReason?: string | null;
  invitationSentDate?: string | null;
  isConfirmed: boolean;
  confirmationDate?: string | null;
}

export interface AddJobInterviewPanelist {
  jobInterviewId: string;
  employeeId: string;
  role: JobInterviewPanelistRole;
  isRequired: boolean;
}

export interface AddJobInterviewExternalPanelist {
  jobInterviewId: string;
  associateId: string;
  role: JobInterviewPanelistRole;
  isRequired: boolean;
}

export interface UpdateJobInterviewPanelist {
  id: string;
  role: JobInterviewPanelistRole;
  isRequired: boolean;
}

export interface UpdateJobInterviewExternalPanelist {
  id: string;
  role: JobInterviewPanelistRole;
  isRequired: boolean;
}

// ── candidates in the session ──────────────────────────────────────────────

export interface JobInterviewee {
  id: string;
  jobInterviewId: string;
  interviewNumber: string;
  jobApplicationId: string;
  applicationNumber: string;
  candidateName: string;
  candidateEmail: string;
  jobCandidateId?: string | null;
  slotStartTime?: string | null;
  slotEndTime?: string | null;
  invitationSentDate?: string | null;
  /** The candidate's own reply to the invitation, by emailed token. */
  confirmedAttendance?: boolean | null;
  confirmationDate?: string | null;
  /** What actually happened on the day. */
  candidateAttended?: boolean | null;
  noShowReason?: string | null;
  outcome?: JobInterviewOutcome | null;
}

export interface AddJobInterviewee {
  jobInterviewId: string;
  jobApplicationId: string;
}

// ── question plans ─────────────────────────────────────────────────────────

export interface JobInterviewSelectedQuestion {
  id: string;
  jobInterviewQuestionId: string;
  questionDetailId: string;
  questionText: string;
  weight: number;
  minScore: number;
  maxScore: number;
  displayOrder: number;
}

export interface JobInterviewQuestionPlan {
  id: string;
  jobInterviewId: string;
  interviewNumber: string;
  questionTypeId: string;
  questionTypeName: string;
  requiredQuestionCount: number;
  allowedPoolSize: number;
  displayOrder: number;
  selectedQuestions: JobInterviewSelectedQuestion[];
}

export interface CreateJobInterviewQuestionPlan {
  jobInterviewId: string;
  questionTypeId: string;
  requiredQuestionCount: number;
  allowedPoolSize: number;
  displayOrder: number;
}

export interface UpdateJobInterviewQuestionPlan {
  id: string;
  requiredQuestionCount: number;
  allowedPoolSize: number;
  displayOrder: number;
}

export interface QuestionPreviewItem {
  questionDetailId: string;
  questionText: string;
  weight: number;
  minScore: number;
  maxScore: number;
  displayOrder: number;
}

export interface QuestionPlanPreview {
  planId: string;
  questionTypeId: string;
  questionTypeName: string;
  requiredQuestionCount: number;
  allowedPoolSize: number;
  displayOrder: number;
  /** Active questions of this type in the bank — the draw can never exceed this. */
  availableQuestionCount: number;
  /** False when the bank cannot supply `requiredQuestionCount`; the panel would run short. */
  meetsRequiredCount: boolean;
  questions: QuestionPreviewItem[];
}

export interface CommitInterviewQuestions {
  plans: { planId: string; questionDetailIds: string[] }[];
}

// ── scorecards ─────────────────────────────────────────────────────────────

export interface JobInterviewScoreEntry {
  id: string;
  scoreSummaryId: string;
  questionDetailId: string;
  questionText: string;
  rawScore: number;
  /** Server-computed as (rawScore / maxScore) × weight — never sent by the client. */
  weightedScore: number;
  remarks?: string | null;
}

export interface JobInterviewScoreSummary {
  id: string;
  jobIntervieweeId: string;
  candidateName: string;
  applicationNumber: string;
  internalPanelistId?: string | null;
  internalPanelistName?: string | null;
  externalPanelistId?: string | null;
  externalPanelistName?: string | null;
  totalRawScore: number;
  totalWeightedScore: number;
  recommendation: JobInterviewRecommendation;
  comments?: string | null;
  evaluationDate: string;
  isFinalized: boolean;
  finalizedDate?: string | null;
}

export interface JobInterviewScoreSummaryDetail extends JobInterviewScoreSummary {
  scoreEntries: JobInterviewScoreEntry[];
}

export interface CreateJobInterviewScoreEntry {
  questionDetailId: string;
  rawScore: number;
  remarks?: string | null;
}

/**
 * ⚠ Posting this a second time for the same panelist **replaces** the existing card rather than
 * adding one — there is no update endpoint, and the server refuses once the card is finalised.
 * `rawScore` must sit inside each question's own min/max band or the whole card is refused.
 */
export interface CreateJobInterviewScoreSummary {
  jobIntervieweeId: string;
  internalPanelistId?: string | null;
  externalPanelistId?: string | null;
  recommendation: JobInterviewRecommendation;
  comments?: string | null;
  evaluationDate: string;
  scoreEntries: CreateJobInterviewScoreEntry[];
}

// ── drafts ─────────────────────────────────────────────────────────────────

export interface DraftScoreEntry {
  questionDetailId: string;
  rawScore?: number | null;
  remarks?: string | null;
}

export interface InterviewScoreDraft {
  id: string;
  jobInterviewId: string;
  jobIntervieweeId: string;
  internalPanelistId?: string | null;
  externalPanelistId?: string | null;
  scoreEntries: DraftScoreEntry[];
  comments?: string | null;
  /** Nullable ordinal, so a draft can be saved before a recommendation is chosen. */
  recommendation?: number | null;
  lastModified: string;
}

export interface SaveInterviewScoreDraft {
  jobIntervieweeId: string;
  internalPanelistId?: string | null;
  externalPanelistId?: string | null;
  scoreEntries: DraftScoreEntry[];
  comments?: string | null;
  recommendation?: number | null;
}

// ── availability ───────────────────────────────────────────────────────────

export interface PanelistInterviewConflict {
  interviewId: string;
  interviewNumber: string;
  jobTitle: string;
  scheduledDate: string;
  startTime: string;
  endTime: string;
  status: JobInterviewStatus;
}

export interface PanelistLeaveConflict {
  startDate: string;
  endDate: string;
  status: string;
}

export interface PanelistTravelConflict {
  requestNumber: string;
  startDate: string;
  endDate: string;
  status: string;
}

export interface PanelistAvailability {
  /** Employee id for internal panelists, associate id for external ones. */
  employeeId: string;
  employeeName: string;
  isExternal: boolean;
  hasConflicts: boolean;
  interviewConflicts: PanelistInterviewConflict[];
  leaveConflicts: PanelistLeaveConflict[];
  travelConflicts: PanelistTravelConflict[];
}

/** Advisory only — the server never refuses a booking because of a clash. */
export interface PanelistAvailabilityCheck {
  hasConflicts: boolean;
  panelists: PanelistAvailability[];
}

export interface PanelistAvailabilityQuery {
  panelistIds?: string[];
  externalPanelistIds?: string[];
  date: string;
  start: string;
  end: string;
  excludeInterviewId?: string;
}

// ── invitations ────────────────────────────────────────────────────────────

export interface InterviewInviteResultItem {
  applicationId: string;
  candidateName: string;
  sent: boolean;
  error?: string | null;
}

export interface SendInterviewInvitesResult {
  totalRequested: number;
  sent: number;
  skipped: number;
  results: InterviewInviteResultItem[];
}

export interface PanelistNotificationResultItem {
  personId: string;
  name: string;
  isExternal: boolean;
  sent: boolean;
  error?: string | null;
}

export interface SendPanelistNotificationsResult {
  totalRequested: number;
  sent: number;
  skipped: number;
  results: PanelistNotificationResultItem[];
}

/**
 * `null` broadcasts to everyone in that group; `[]` skips the group entirely; a list notifies only
 * those members. The distinction between null and empty is load-bearing.
 */
export interface SendPanelistNotifications {
  employeeIds?: string[] | null;
  externalAssociateIds?: string[] | null;
}

// ── external associates (the external-panelist picker) ─────────────────────

// ⚠ Re-exported, not restated. The local copy declared five of the seven keys the endpoint
// actually returns — `associateNumber` and `phoneNumber` were simply missing, so the picker could
// not have shown either even though both were on the wire. Transcribed from a live payload in
// slice 8 and now kept in one place.
export type { ExternalAssociateSearchResult } from './external-associate';

// ── slot apportionment (round 4, lane C) ───────────────────────────────────

/** A rest period inside the interview window that no candidate may be booked into. */
export interface InterviewBreak {
  /** `HH:mm:ss`. */
  start: string;
  /** `HH:mm:ss`. */
  end: string;
  /** Printed on the timetable — "Lunch", "Panel conference". */
  label?: string | null;
}

export interface ApportionSlotsRequest {
  slotMinutes: number;
  bufferMinutes: number;
  breaks: InterviewBreak[];
  /**
   * The candidates to place, in the order they should be seen. Omit to place everyone currently
   * booked in, in the order they were added.
   */
  applicationIds?: string[] | null;
}

export interface InterviewSlotAssignment {
  intervieweeId: string;
  jobApplicationId: string;
  candidateName: string;
  applicationNumber: string;
  /** 1-based position in the day. Zero for a candidate who did not fit. */
  ordinal: number;
  slotStartTime: string;
  slotEndTime: string;
}

/**
 * The timetable, and the fact the screen leads with: whether everybody actually fits.
 *
 * ⚠ `unplaced` is not an error list. It is the answer to "can we see all of them today?", and it
 * carries the candidates in the order they would have been seen — so a recruiter can decide who
 * moves rather than being told only that somebody must.
 */
export interface InterviewSlotPlan {
  interviewId: string;
  scheduledDate: string;
  windowStart: string;
  windowEnd: string;
  slotMinutes: number;
  bufferMinutes: number;
  breaks: InterviewBreak[];
  slots: InterviewSlotAssignment[];
  unplaced: InterviewSlotAssignment[];
  allFit: boolean;
  /** First free time of day after the window. Null when everybody fits. The DATE is the user's. */
  firstFreeAfterWindow?: string | null;
  summary: string;
}
