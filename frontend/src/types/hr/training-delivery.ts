// Types for HR Training & Learning — Slice 3 (Scheduling & Delivery): schedules, sessions,
// nominations, waitlist, attendance, feedback, follow-up assessments, completions and training
// requests. Mirrors the same-named DTOs in ErpSystem.Core.DTOs.HR.TrainingDTOs.
//
// Split out of `training.ts` (already ~865 lines covering slices 1–2) rather than appended, so the
// setup/catalog types and the delivery types stay separately navigable. Shared enums that slice 1
// already defined (TrainingPriority, TrainerEngagementType) are imported rather than redeclared.

import type { TrainingPriority, TrainerEngagementType } from './training';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings; option lists use the backend [Description] text) ──────────

export type ScheduleStatus =
  | 'Planned'
  | 'RegistrationOpen'
  | 'RegistrationClosed'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled'
  | 'Postponed';

export const SCHEDULE_STATUS_OPTIONS = opts<ScheduleStatus>([
  ['Planned', 'Planned'],
  ['RegistrationOpen', 'Registration Open'],
  ['RegistrationClosed', 'Registration Closed'],
  ['InProgress', 'In Progress'],
  ['Completed', 'Completed'],
  ['Cancelled', 'Cancelled'],
  ['Postponed', 'Postponed'],
]);

export type NominationType = 'Self' | 'Supervisor' | 'HR';

export const NOMINATION_TYPE_OPTIONS = opts<NominationType>([
  ['Self', 'Self-Nomination'],
  ['Supervisor', 'Supervisor Nomination'],
  ['HR', 'HR Nomination'],
]);

export type NominationStatus =
  | 'Draft'
  | 'Submitted'
  | 'SupervisorReview'
  | 'HrReview'
  | 'Approved'
  | 'Rejected'
  | 'Waitlisted'
  | 'Confirmed'
  | 'Withdrawn';

export const NOMINATION_STATUS_OPTIONS = opts<NominationStatus>([
  ['Draft', 'Draft'],
  ['Submitted', 'Submitted'],
  ['SupervisorReview', 'Supervisor Review'],
  ['HrReview', 'HR Review'],
  ['Approved', 'Approved'],
  ['Rejected', 'Rejected'],
  ['Waitlisted', 'Waitlisted'],
  ['Confirmed', 'Confirmed'],
  ['Withdrawn', 'Withdrawn'],
]);

export type TrainingWaitlistStatus =
  | 'Active'
  | 'Offered'
  | 'Accepted'
  | 'Declined'
  | 'Expired'
  | 'Removed';

export const TRAINING_WAITLIST_STATUS_OPTIONS = opts<TrainingWaitlistStatus>([
  ['Active', 'Active'],
  ['Offered', 'Offered'],
  ['Accepted', 'Accepted'],
  ['Declined', 'Declined'],
  ['Expired', 'Expired'],
  ['Removed', 'Removed'],
]);

export type TrainingCompletionStatus =
  | 'NotStarted'
  | 'InProgress'
  | 'Completed'
  | 'Failed'
  | 'Incomplete'
  | 'Exempted';

export const TRAINING_COMPLETION_STATUS_OPTIONS = opts<TrainingCompletionStatus>([
  ['NotStarted', 'Not Started'],
  ['InProgress', 'In Progress'],
  ['Completed', 'Completed'],
  ['Failed', 'Failed'],
  ['Incomplete', 'Incomplete'],
  ['Exempted', 'Exempted'],
]);

export type TrainingRequestStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Cancelled';

export const TRAINING_REQUEST_STATUS_OPTIONS = opts<TrainingRequestStatus>([
  ['Draft', 'Draft'],
  ['Submitted', 'Submitted'],
  ['Approved', 'Approved'],
  ['Rejected', 'Rejected'],
  ['Cancelled', 'Cancelled'],
]);

export type TrainingAssessmentType =
  | 'PreTraining'
  | 'PostTraining'
  | 'FollowUp30Day'
  | 'FollowUp60Day'
  | 'FollowUp90Day';

export const TRAINING_ASSESSMENT_TYPE_OPTIONS = opts<TrainingAssessmentType>([
  ['PreTraining', 'Pre-Training'],
  ['PostTraining', 'Post-Training'],
  ['FollowUp30Day', '30-Day Follow-Up'],
  ['FollowUp60Day', '60-Day Follow-Up'],
  ['FollowUp90Day', '90-Day Follow-Up'],
]);

// ── Training Schedule (mirrors TrainingScheduleDto) ────────────────────────────────────────

export interface TrainingSession {
  id: string;
  scheduleId: string;
  scheduleNumber: string;
  topic: string;
  date: string;
  startTime?: string | null;
  endTime?: string | null;
  description?: string | null;
}

export interface TrainingSessionRequest {
  scheduleId: string;
  topic: string;
  date: string;
  startTime?: string | null;
  endTime?: string | null;
  description?: string | null;
}

export interface TrainingSchedule {
  id: string;
  scheduleNumber: string;
  programId: string;
  programCode: string;
  programName: string;
  startDate: string;
  endDate: string;
  venue?: string | null;
  venueAddress?: string | null;
  onlineLink?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  trainerProfileId?: string | null;
  trainerName?: string | null;
  vendorId?: string | null;
  vendorName?: string | null;
  maxParticipants: number;
  priority: TrainingPriority;
  confirmedParticipantsCount: number;
  slotsAvailable: number;
  registrationOpenDate: string;
  registrationCloseDate: string;
  status: ScheduleStatus;
  actualCost: number;
  budgetNotes?: string | null;
  trainingBudgetId?: string | null;
  budgetCode?: string | null;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvalDate?: string | null;
  completionDate?: string | null;
  completionNotes?: string | null;
  cancellationReason?: string | null;
  cancelledDate?: string | null;
  /** Id only — TrainingSchedule.CancelledById has no paired navigation, so the API cannot resolve a name. */
  cancelledById?: string | null;
  sessions: TrainingSession[];
}

export interface TrainingScheduleSummary {
  id: string;
  scheduleNumber: string;
  programName: string;
  programCode: string;
  startDate: string;
  endDate: string;
  venue?: string | null;
  trainerName?: string | null;
  vendorName?: string | null;
  maxParticipants: number;
  priority: TrainingPriority;
  confirmedParticipantsCount: number;
  status: ScheduleStatus;
  registrationCloseDate: string;
}

/** A schedule must name a trainer or a vendor — the API rejects neither with a 422. */
export interface TrainingScheduleRequest {
  programId: string;
  startDate: string;
  endDate: string;
  venue?: string | null;
  venueAddress?: string | null;
  onlineLink?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  trainerProfileId?: string | null;
  vendorId?: string | null;
  maxParticipants: number;
  priority: TrainingPriority;
  registrationOpenDate: string;
  registrationCloseDate: string;
  actualCost: number;
  budgetNotes?: string | null;
  trainingBudgetId?: string | null;
}

export interface TrainerScheduleConflict {
  scheduleId: string;
  scheduleNumber: string;
  programName: string;
  startDate: string;
  endDate: string;
  priority: TrainingPriority;
  status: ScheduleStatus;
}

export interface TrainerBlockedPeriod {
  fromDate: string;
  toDate: string;
  engagementType?: TrainerEngagementType | null;
  notes?: string | null;
}

export interface TrainerAvailabilityCheck {
  trainerProfileId: string;
  hasConflicts: boolean;
  conflictingSchedules: TrainerScheduleConflict[];
  blockedPeriods: TrainerBlockedPeriod[];
}

// ── Training Nomination (mirrors TrainingNominationDto) ────────────────────────────────────

export interface TrainingNomination {
  id: string;
  nominationNumber: string;
  scheduleId: string;
  scheduleNumber: string;
  /** Needed to issue a certificate against this nomination without a second round trip. */
  programId: string;
  programName: string;
  trainingStartDate: string;
  trainingEndDate: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  employeeDepartment?: string | null;
  employeePosition?: string | null;
  type: NominationType;
  nominatedById?: string | null;
  nominatedByName?: string | null;
  nominationDate: string;
  justification?: string | null;
  trainingNeedsAssessmentId?: string | null;
  status: NominationStatus;
  supervisorApprovedById?: string | null;
  supervisorApprovedByName?: string | null;
  supervisorApprovalDate?: string | null;
  supervisorComments?: string | null;
  hrApprovedById?: string | null;
  hrApprovedByName?: string | null;
  hrApprovalDate?: string | null;
  hrComments?: string | null;
  rejectedDate?: string | null;
  rejectionReason?: string | null;
  actualCost?: number | null;
  employeeContributed: boolean;
  employeeContribution?: number | null;
  hasCompletionRecord: boolean;
  completionRecord?: TrainingCompletionSummary | null;
}

export interface TrainingNominationSummary {
  id: string;
  nominationNumber: string;
  programName: string;
  /** Needed to act on a row (mark attendance, record a completion) without refetching each nomination. */
  employeeId: string;
  scheduleId: string;
  employeeName: string;
  employeeNumber: string;
  type: NominationType;
  status: NominationStatus;
  nominationDate: string;
  trainingStartDate: string;
}

export interface TrainingNominationRequest {
  scheduleId: string;
  employeeId: string;
  type: NominationType;
  nominationDate: string;
  justification?: string | null;
  trainingNeedsAssessmentId?: string | null;
  saveAsDraft?: boolean;
}

export interface BulkNominationRequest {
  scheduleId: string;
  employeeIds: string[];
  type: NominationType;
  justification?: string | null;
  saveAsDraft?: boolean;
}

export interface BulkNominationSkip {
  employeeId: string;
  reason: string;
}

/** Skipped rows carry their own reason (already nominated, schedule full, …) — surface them. */
export interface BulkNominationResult {
  requestedCount: number;
  createdCount: number;
  skipped: BulkNominationSkip[];
}

/** ApproverRole is [Required] server-side — the chain is Supervisor then HR. */
export interface ApproveNominationRequest {
  nominationId: string;
  approverRole: 'Supervisor' | 'HR';
  comments?: string | null;
}

export interface RejectNominationRequest {
  nominationId: string;
  rejectionReason: string;
}

// ── Waitlist (mirrors TrainingWaitlistDto) ─────────────────────────────────────────────────

export interface TrainingWaitlistEntry {
  id: string;
  scheduleId: string;
  scheduleNumber: string;
  programName: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  position: number;
  addedDate: string;
  status: TrainingWaitlistStatus;
  offerDate?: string | null;
  offerExpiryDate?: string | null;
  responseDate?: string | null;
  offerAccepted?: boolean | null;
  notes?: string | null;
  /** Set only by the explicit promote action — accepting an offer does not enrol on its own. */
  createdNominationId?: string | null;
  createdNominationNumber?: string | null;
}

export interface AddToWaitlistRequest {
  scheduleId: string;
  employeeId: string;
  notes?: string | null;
}

export interface OfferWaitlistPositionRequest {
  waitlistId: string;
  offerDate: string;
  offerExpiryDate: string;
}

export interface RespondToWaitlistOfferRequest {
  waitlistId: string;
  offerAccepted: boolean;
  responseDate: string;
}

// ── Attendance / Feedback / Follow-up ──────────────────────────────────────────────────────

export interface TrainingAttendance {
  id: string;
  scheduleId: string;
  scheduleNumber: string;
  programName: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  nominationId?: string | null;
  nominationNumber?: string | null;
  attendanceDate: string;
  isPresent: boolean;
  checkInTime?: string | null;
  checkOutTime?: string | null;
  absenceReason?: string | null;
  notes?: string | null;
  markedById?: string | null;
  markedByName?: string | null;
  markedAt?: string | null;
}

/** One row of a bulk register: the date and the marker are supplied once for the whole call. */
export interface AttendanceEntry {
  employeeId: string;
  isPresent: boolean;
  checkInTime?: string | null;
  checkOutTime?: string | null;
  absenceReason?: string | null;
}

export interface MarkAttendanceRequest {
  scheduleId: string;
  employeeId: string;
  nominationId?: string | null;
  attendanceDate: string;
  isPresent: boolean;
  checkInTime?: string | null;
  checkOutTime?: string | null;
  absenceReason?: string | null;
  notes?: string | null;
}

/**
 * One piece of training feedback.
 *
 * Verified against the live payload in area 25 slice 14 (`probe-slice14-feedback.mjs`, 27 keys) —
 * the trainee's own `feedback/mine` returns exactly this, so the portal reuses it rather than
 * growing a parallel type.
 *
 * ⚠ Unlike the recruitment self-reads of slice 13b, the portal deliberately gets the SAME shape
 * the desk does, and the difference is worth stating: a job application carries the recruiter's
 * assessment OF the candidate, which is not theirs to read; training feedback is the employee's
 * own words about a course. Nothing here is written about them by anyone else, so there is
 * nothing to withhold.
 *
 * ⚠ `programName` and `employeeName` arrive through navigations and came back EMPTY on the first
 * slice-14 probe run — present but blank, because the new self-read Included `Schedule` without
 * `Schedule.Program` or `Employee`. If they are ever blank again, that is the server, not this.
 */
export interface TrainingFeedback {
  id: string;
  scheduleId: string;
  scheduleNumber: string;
  programName: string;
  employeeId: string;
  employeeName: string;
  nominationId?: string | null;
  contentRelevanceRating?: number | null;
  /** The only rating that is about the trainer — it is what feeds TrainerProfile.AverageRating. */
  trainerKnowledgeRating?: number | null;
  deliveryMethodRating?: number | null;
  materialQualityRating?: number | null;
  venueFacilitiesRating?: number | null;
  overallSatisfactionRating?: number | null;
  averageRating?: number | null;
  strengthsOfTraining?: string | null;
  areasForImprovement?: string | null;
  suggestionsForFuture?: string | null;
  additionalComments?: string | null;
  wouldRecommend: boolean;
  likelihoodToApply?: number | null;
  expectedApplicationOnJob?: string | null;
  barriersToApplication?: string | null;
  feedbackDate: string;
}

export interface SubmitTrainingFeedbackRequest {
  scheduleId: string;
  /** Must be the caller's own unless they hold `HR.Training.Write` (W3). */
  employeeId: string;
  nominationId?: string | null;
  contentRelevanceRating?: number | null;
  trainerKnowledgeRating?: number | null;
  deliveryMethodRating?: number | null;
  materialQualityRating?: number | null;
  venueFacilitiesRating?: number | null;
  overallSatisfactionRating?: number | null;
  strengthsOfTraining?: string | null;
  areasForImprovement?: string | null;
  suggestionsForFuture?: string | null;
  additionalComments?: string | null;
  wouldRecommend: boolean;
  likelihoodToApply?: number | null;
  expectedApplicationOnJob?: string | null;
  barriersToApplication?: string | null;
  /**
   * Optional — the server DTO defaults it to the moment the request is handled, which is what the
   * portal relies on. It is settable rather than server-stamped because HR (with
   * `HR.Training.Write`) may be entering feedback collected on paper on an earlier date; a trainee
   * filing their own has no reason to send it, and the portal does not.
   */
  feedbackDate?: string;
}

export interface TrainingFollowUpAssessment {
  id: string;
  scheduleId: string;
  scheduleNumber: string;
  programName: string;
  employeeId: string;
  employeeName: string;
  nominationId?: string | null;
  assessmentType: TrainingAssessmentType;
  assessmentDate: string;
  keyLearningsTaken?: string | null;
  conceptsStillUnclear?: string | null;
  frequencyOfUse?: number | null;
  howSkillsApplied?: string | null;
  barriersToApplication?: string | null;
  supportNeeded?: string | null;
  managerId?: string | null;
  managerName?: string | null;
  managerObservationNotes?: string | null;
  managerSubmittedDate?: string | null;
  recommendFurtherTraining: boolean;
  recommendedFollowUp?: string | null;
}

export interface SubmitFollowUpAssessmentRequest {
  scheduleId: string;
  employeeId: string;
  nominationId?: string | null;
  assessmentType: TrainingAssessmentType;
  assessmentDate: string;
  keyLearningsTaken?: string | null;
  conceptsStillUnclear?: string | null;
  frequencyOfUse?: number | null;
  howSkillsApplied?: string | null;
  barriersToApplication?: string | null;
  supportNeeded?: string | null;
  recommendFurtherTraining: boolean;
  recommendedFollowUp?: string | null;
}

export interface SubmitManagerObservationRequest {
  assessmentId: string;
  managerObservationNotes: string;
}

// ── Completion (mirrors TrainingCompletionDto) ─────────────────────────────────────────────

export interface TrainingCompletion {
  id: string;
  nominationId: string;
  nominationNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  programName: string;
  completionDate: string;
  status: TrainingCompletionStatus;
  finalScore?: number | null;
  preAssessmentScore?: number | null;
  postAssessmentScore?: number | null;
  isPassed: boolean;
  isVerifiedByManager: boolean;
  verifiedById?: string | null;
  verifiedByName?: string | null;
  verificationDate?: string | null;
  verificationNotes?: string | null;
}

export interface TrainingCompletionSummary {
  id: string;
  completionDate: string;
  status: TrainingCompletionStatus;
  finalScore?: number | null;
  isPassed: boolean;
  isVerifiedByManager: boolean;
}

export interface RecordTrainingCompletionRequest {
  nominationId: string;
  employeeId: string;
  completionDate: string;
  status: TrainingCompletionStatus;
  finalScore?: number | null;
  preAssessmentScore?: number | null;
  postAssessmentScore?: number | null;
  isPassed: boolean;
}

export interface VerifyTrainingCompletionRequest {
  completionId: string;
  verificationNotes?: string | null;
}

// ── Training Request (mirrors TrainingRequestDto) ──────────────────────────────────────────

export interface TrainingRequest {
  id: string;
  requestNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  requestedTrainingTitle: string;
  description?: string | null;
  justification?: string | null;
  requestDate: string;
  status: TrainingRequestStatus;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvalDate?: string | null;
  rejectionReason?: string | null;
  linkedProgramId?: string | null;
  linkedProgramName?: string | null;
  linkedProgramCode?: string | null;
}

export interface TrainingRequestSummary {
  id: string;
  requestNumber: string;
  employeeName: string;
  requestedTrainingTitle: string;
  requestDate: string;
  status: TrainingRequestStatus;
  linkedProgramName?: string | null;
}

export interface TrainingRequestCreate {
  employeeId: string;
  requestedTrainingTitle: string;
  description?: string | null;
  justification?: string | null;
  requestDate: string;
  linkedProgramId?: string | null;
}

export interface TrainingRequestUpdate {
  id: string;
  requestedTrainingTitle: string;
  description?: string | null;
  justification?: string | null;
  linkedProgramId?: string | null;
}

/** Approving may also link the free-text request to a catalog programme. */
export interface ApproveTrainingRequestRequest {
  requestId: string;
  linkedProgramId?: string | null;
}

export interface RejectTrainingRequestRequest {
  requestId: string;
  rejectionReason: string;
}

/**
 * A commitment that overlaps a schedule's dates.
 *
 * ⚠ `source` is one of `Leave`, `Travel` or `Training`; the check reports each separately rather
 * than collapsing them, because "on leave" and "already booked on another course" need different
 * answers from the person nominating.
 */
export interface NomineeConflict {
  employeeId: string;
  source: 'Leave' | 'Travel' | 'Training' | string;
  description: string;
  fromDate: string;
  toDate: string;
}

/**
 * What a bulk completion pass did.
 *
 * ⚠ `skipped` is the important half: the endpoint drops rows rather than failing, so a screen that
 * ignores this reports success for work it did not do. The only skip reason the service produces
 * today is "Completion already recorded."
 */
export interface BulkCompletionResult {
  requestedCount: number;
  createdCount: number;
  skipped: { nominationId: string; reason: string }[];
}
