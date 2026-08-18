/**
 * Probation & confirmation (FRD §A1.4 — FR-HR-031, FR-HR-032, FR-HR-140).
 *
 * ⚠ Every type here was written by reading the C# DTO, not the endpoint name. A type inferred from
 * a route is fiction that type-checks: area 12 shipped two of them — a review typed `approve:
 * boolean` when the API takes `newStatus`, and a dashboard field typed as a number when it is a
 * list. Both compiled. See `hr-travel-area-survey`.
 */

/** `ProbationStatus` — Active(1) Completed(2) Terminated(3) PendingConfirmation(4) ConfirmationApproved(5). */
export type ProbationStatus =
  | 'Active'
  | 'Completed'
  | 'Terminated'
  | 'PendingConfirmation'
  | 'ConfirmationApproved';

export type ProbationReviewStatus = 'Scheduled' | 'Completed' | 'Missed' | 'Rescheduled';

export type ProbationPerformanceRating =
  | 'Outstanding'
  | 'ExceedsExpectations'
  | 'MeetsExpectations'
  | 'BelowExpectations'
  | 'Unsatisfactory';

export type ProbationReviewRecommendation = 'Confirm' | 'Extend' | 'Terminate' | 'ContinueMonitoring';

export type OathAdministrationMethod = 'Affirmed' | 'Administered';

/** `ProbationPeriodSummaryDto` — the register row. */
export interface ProbationPeriodSummary {
  id: string;
  employeeName: string;
  employeeNumber: string;
  startDate: string;
  currentEndDate: string;
  durationMonths: number;
  status: number;
  statusName: ProbationStatus;
  extensionCount: number;
  reviewCount: number;
}

/** `ProbationPeriodDto`. */
export interface ProbationPeriod {
  id: string;
  tenantId: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  contractDetailId: string;
  startDate: string;
  /** The end date originally set. Unchanged by extensions — compare with {@link currentEndDate}. */
  originalEndDate: string;
  currentEndDate: string;
  durationMonths: number;
  status: number;
  statusName: ProbationStatus;
  outcomeNotes: string | null;
  extensionCount: number;
  reviewCount: number;
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

/** `ProbationPeriodDetailDto` — the by-id read with its reviews. */
export interface ProbationPeriodDetail extends ProbationPeriod {
  reviews: ProbationReviewSummary[];
}

/** `ProbationReviewSummaryDto`. */
export interface ProbationReviewSummary {
  id: string;
  reviewNumber: number;
  scheduledDate: string;
  actualDate: string | null;
  status: number;
  statusName: ProbationReviewStatus;
  recommendation: number | null;
  recommendationName: ProbationReviewRecommendation | null;
  reviewedByName: string;
  hrApproved: boolean;
}

/** `ProbationReviewDto` — the full review. */
export interface ProbationReview {
  id: string;
  tenantId: string;
  probationPeriodId: string;
  employeeName: string;
  employeeNumber: string;
  reviewNumber: number;
  scheduledDate: string;
  actualDate: string | null;
  status: number;
  statusName: ProbationReviewStatus;
  performanceRating: number | null;
  performanceRatingName: ProbationPerformanceRating | null;
  conductRating: number | null;
  conductRatingName: ProbationPerformanceRating | null;
  attitudeRating: number | null;
  attitudeRatingName: ProbationPerformanceRating | null;
  strengthsObserved: string | null;
  areasForImprovement: string | null;
  reviewerComments: string | null;
  employeeResponse: string | null;
  recommendation: number | null;
  recommendationName: ProbationReviewRecommendation | null;
  proposedExtensionMonths: number | null;
  reviewedById: string;
  reviewedByName: string;
  secondReviewerId: string | null;
  secondReviewerName: string | null;
  employeeAcknowledged: boolean;
  employeeAcknowledgementDate: string | null;
  hrApproved: boolean;
  hrApprovedById: string | null;
  hrApprovedByName: string | null;
  hrApprovalDate: string | null;
  signedDocumentPath: string | null;
}

/** `ProbationExtensionDto` — one row of the audit trail. */
export interface ProbationExtension {
  id: string;
  probationPeriodId: string;
  previousEndDate: string;
  newEndDate: string;
  extensionMonths: number;
  reason: string;
  extendedById: string;
  extendedByName: string | null;
  extendedDate: string;
  comments: string | null;
  createdAt: string;
}

/**
 * `ProbationPolicyDto` — what a create form should read before it renders.
 *
 * ⚠ `expectedDurationMonths` is the answer, not a suggestion: when `isEnforced` is true (permanent
 * staff), sending a different duration is refused. The form should show it and let the user leave
 * it alone.
 */
export interface ProbationPolicy {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  staffLevelId: string | null;
  staffLevelName: string | null;
  staffLevelCode: string | null;
  employmentType: number;
  employmentTypeName: string;
  expectedDurationMonths: number;
  /** 'Position' or 'PolicyDefault'. */
  source: string;
  positionProbationMonths: number | null;
  policyDefaultMonths: number;
  isEnforced: boolean;
  endLeadDays: number;
  /** Null when no confirming authority covers this employee — show that, do not hide it. */
  confirmingAuthorityEmployeeId: string | null;
  confirmingAuthorityName: string | null;
  confirmingAuthorityScope: string | null;
}

/** `CreateProbationPeriodDto`. ⚠ `durationMonths` is OPTIONAL — omit it to apply the category length. */
export interface CreateProbationPeriod {
  employeeId: string;
  contractDetailId: string;
  startDate: string;
  durationMonths?: number | null;
  outcomeNotes?: string | null;
}

/** `CreateProbationReviewDto`. */
export interface CreateProbationReview {
  probationPeriodId: string;
  reviewNumber: number;
  scheduledDate: string;
  reviewedById: string;
  secondReviewerId?: string | null;
}

/** `SubmitProbationReviewDto` — the reviewer's assessment. */
export interface SubmitProbationReview {
  reviewId: string;
  actualDate: string;
  performanceRating?: ProbationPerformanceRating | null;
  conductRating?: ProbationPerformanceRating | null;
  attitudeRating?: ProbationPerformanceRating | null;
  strengthsObserved?: string | null;
  areasForImprovement?: string | null;
  reviewerComments?: string | null;
  /** Required by the API: a review without one is refused. */
  recommendation: ProbationReviewRecommendation;
  /** Required when `recommendation` is 'Extend'. */
  proposedExtensionMonths?: number | null;
  signedDocumentPath?: string | null;
}

/** `AcknowledgeProbationReviewDto` — the subject's own act. No actor field: it comes from the token. */
export interface AcknowledgeProbationReview {
  reviewId: string;
  employeeResponse?: string | null;
}

/** `ApproveProbationReviewDto`. ⚠ Carries only comments — the approver and date are server-side. */
export interface ApproveProbationReview {
  comments?: string | null;
}

/** `CreateProbationExtensionDto`. */
export interface CreateProbationExtension {
  probationPeriodId: string;
  newEndDate: string;
  extensionMonths: number;
  reason: string;
  comments?: string | null;
}

/** `TerminateProbationPeriodDto`, also used for reject/recall bodies. */
export interface TerminateProbationPeriod {
  probationId: string;
  notes?: string | null;
}

/** `ProbationConfirmationLetterDto` — rendered on demand, never stored. */
export interface ProbationConfirmationLetter {
  probationId: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  positionTitle: string;
  confirmationDate: string;
  subject: string;
  /** A self-contained HTML document, suitable for an iframe or print-to-PDF. */
  htmlBody: string;
}

// ── Confirming authority (FR-HR-032, decision D-2) ───────────────────────────

/** `ProbationConfirmingAuthorityDto`. */
export interface ProbationConfirmingAuthority {
  id: string;
  organizationUnitId: string | null;
  organizationUnitName: string | null;
  staffLevelId: string | null;
  staffLevelName: string | null;
  authorityEmployeeId: string;
  authorityEmployeeName: string;
  authorityEmployeeNumber: string;
  isActive: boolean;
  notes: string | null;
  /** 3 = unit + level, 2 = unit, 1 = level, 0 = tenant default. Highest match wins. */
  specificity: number;
  scope: string;
}

export interface CreateProbationConfirmingAuthority {
  organizationUnitId?: string | null;
  staffLevelId?: string | null;
  authorityEmployeeId: string;
  isActive?: boolean;
  notes?: string | null;
}

export interface UpdateProbationConfirmingAuthority {
  authorityEmployeeId: string;
  isActive: boolean;
  notes?: string | null;
}

/** `ResolvedConfirmingAuthorityDto`. ⚠ `isResolved` false is a real answer — surface it. */
export interface ResolvedConfirmingAuthority {
  employeeId: string;
  employeeName: string;
  authorityEmployeeId: string | null;
  authorityEmployeeName: string | null;
  matchedRuleId: string | null;
  matchedScope: string | null;
  matchedSpecificity: number | null;
  isResolved: boolean;
  unresolvedReason: string | null;
}

// ── Reminders (FR-HR-032 / FR-HR-140) ────────────────────────────────────────

export type ProbationReminderKind =
  | 'ConfirmationFormDue'
  | 'ProbationEndingSoon'
  | 'ProbationOverdue'
  | 'ReviewOverdue'
  | 'ReviewUnacknowledged';

export interface ProbationReminderRun {
  id: string;
  startedAt: string;
  completedAt: string | null;
  trigger: string;
  remindersQueued: number;
}

export interface ProbationReminderRunResult {
  runId: string;
  remindersQueued: number;
  byKind: Record<string, number>;
}

export interface ProbationReminderPreviewItem {
  kind: ProbationReminderKind;
  itemType: string;
  entityId: string;
  probationPeriodId: string;
  reference: string;
  dueDate: string | null;
  daysRemaining: number;
  escalationTier: number;
  /** Null means nothing was routed — show it as unassigned work, not as blank. */
  routedToEmployeeId: string | null;
  routedToName: string | null;
  dedupeKey: string;
}

export interface ProbationReminderLogEntry {
  id: string;
  runId: string;
  kind: ProbationReminderKind;
  itemType: string;
  entityId: string;
  probationPeriodId: string;
  reference: string;
  dueDate: string | null;
  daysRemaining: number;
  escalationTier: number;
  routedToEmployeeId: string | null;
  dispatchedAt: string;
}

// ── Oath of secrecy (FR-HR-030) ──────────────────────────────────────────────

/** `EmployeeOathOfSecrecyDto`. */
export interface EmployeeOathOfSecrecy {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  method: number;
  methodName: OathAdministrationMethod;
  /** The exact wording sworn, snapshotted at the time. */
  oathText: string;
  swornOn: string;
  recordedAt: string;
  witnessedById: string | null;
  witnessedByName: string | null;
  recordedById: string;
  recordedByName: string;
  /** True only for an oath affirmed in the system. The hash itself is never returned. */
  hasSignature: boolean;
  fileUploadRecordId: string | null;
  documentRecordId: string | null;
  fileName: string | null;
  mimeType: string | null;
  fileSizeBytes: number | null;
  hasScan: boolean;
  notes: string | null;
}

/** ⚠ No employee id: the person affirming is the person on the token. */
export interface AffirmOathOfSecrecy {
  oathText?: string | null;
  notes?: string | null;
}

/** ⚠ A witness is required — the API refuses without one. */
export interface RecordAdministeredOath {
  employeeId: string;
  witnessedById: string;
  swornOn: string;
  oathText?: string | null;
  notes?: string | null;
}

export interface OathOutstandingEmployee {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  dateEmployed: string | null;
  isOnProbation: boolean;
}
