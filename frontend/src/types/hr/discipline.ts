/**
 * Staff discipline — area 9. Backend routes under `api/discipline`.
 *
 * Every union below was extracted from `HREnums.cs` rather than inferred from the field name.
 * Six of area 8's unions were wrong because they were guessed, and a wrong member is invisible until
 * a user picks it: the request serialises, the server rejects the value, and the screen reports a
 * validation error nobody can explain.
 */

// ── Enumerations ─────────────────────────────────────────────────────────────

export type StaffOffenseSeverity = 'Minor' | 'Moderate' | 'Serious' | 'GrossMisconduct';

/**
 * The case lifecycle. Unlike the staff-movement ladder, every member here is genuinely assigned by
 * the services — there are no decorative statuses in this enum.
 */
export type DisciplinaryStatus =
  | 'Draft'
  | 'Reported'
  | 'UnderReview'
  | 'UnderInvestigation'
  | 'InvestigationComplete'
  | 'HearingScheduled'
  | 'HearingConducted'
  | 'AwaitingDecision'
  | 'DecisionMade'
  | 'UnderAppeal'
  | 'Closed'
  | 'Dismissed'
  | 'OnHold';

export type DisciplinaryWarningType = 'Verbal' | 'Written' | 'Final';

export type DisciplinaryFinePaymentStatus = 'Pending' | 'PartiallyPaid' | 'FullyPaid' | 'Waived';

export type DisciplinaryActionStepStatus =
  | 'Pending' | 'InProgress' | 'Completed' | 'Cancelled' | 'Skipped';

export type DisciplineAppealStatus =
  | 'Filed' | 'UnderReview' | 'HearingScheduled' | 'HearingConducted'
  | 'AwaitingDecision' | 'DecisionMade' | 'Dismissed';

export type DisciplineAppealOutcomeType = 'Upheld' | 'Overturned' | 'Reduced';

export type DisciplinaryDocumentCategory =
  | 'Evidence' | 'NotificationLetter' | 'Statement' | 'Report'
  | 'HearingMinutes' | 'DecisionLetter' | 'AppealDocument' | 'LegalDocument' | 'Other';

export type DisciplinaryDocumentScope = 'Case' | 'ActionStep' | 'Appeal';

export type DisciplinaryNotificationType =
  | 'ShowCause' | 'HearingNotice' | 'InvestigationNotice' | 'DecisionLetter'
  | 'WarningLetter' | 'SuspensionNotice' | 'TerminationLetter' | 'AppealOutcomeNotice' | 'Other';

export type DisciplineCorrectiveActionStatus =
  | 'Pending' | 'InProgress' | 'Completed' | 'Overdue' | 'Cancelled';

export type DisciplineLegalRiskLevel = 'None' | 'Low' | 'Medium' | 'High' | 'Critical';

export type EmployeeTerminationType =
  | 'InvoluntaryForCause' | 'InvoluntaryPerformance' | 'InvoluntaryRedundancy'
  | 'VoluntaryResignation' | 'VoluntaryRetirement' | 'MutualAgreement'
  | 'ContractExpiry' | 'Death' | 'Other';

export type DisciplinaryRepresentativeType =
  | 'LegalCounsel' | 'FamilyMember' | 'WorkplaceColleague' | 'UnionRepresentative' | 'Other';

// ── Option lists for pickers ─────────────────────────────────────────────────

export const SEVERITY_OPTIONS: { value: StaffOffenseSeverity; label: string }[] = [
  { value: 'Minor', label: 'Minor' },
  { value: 'Moderate', label: 'Moderate' },
  { value: 'Serious', label: 'Serious' },
  { value: 'GrossMisconduct', label: 'Gross misconduct' },
];

export const WARNING_TYPE_OPTIONS: { value: DisciplinaryWarningType; label: string }[] = [
  { value: 'Verbal', label: 'Verbal warning' },
  { value: 'Written', label: 'Written warning' },
  { value: 'Final', label: 'Final warning' },
];

/**
 * The statuses a case can be filtered by in the register. Ordered as the lifecycle runs rather than
 * alphabetically, so the filter reads as a progression.
 */
export const CASE_STATUS_OPTIONS: { value: DisciplinaryStatus; label: string }[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Reported', label: 'Reported' },
  { value: 'UnderReview', label: 'Under review' },
  { value: 'UnderInvestigation', label: 'Under investigation' },
  { value: 'InvestigationComplete', label: 'Investigation complete' },
  { value: 'HearingScheduled', label: 'Hearing scheduled' },
  { value: 'HearingConducted', label: 'Hearing conducted' },
  { value: 'AwaitingDecision', label: 'Awaiting decision' },
  { value: 'DecisionMade', label: 'Decision made' },
  { value: 'UnderAppeal', label: 'Under appeal' },
  { value: 'OnHold', label: 'On hold' },
  { value: 'Closed', label: 'Closed' },
  { value: 'Dismissed', label: 'Dismissed' },
];

/** Statuses that mean the case is finished; used to grey rows and hide actions. */
export const TERMINAL_CASE_STATUSES: DisciplinaryStatus[] = ['Closed', 'Dismissed'];

// ── Lookups ──────────────────────────────────────────────────────────────────

export interface StaffOffenseSummary {
  id: string;
  offenseCode: string;
  offenseName: string;
  isActive: boolean;
}

export interface StaffOffenseProcedure {
  id: string;
  offenseId: string;
  stepName: string;
  stepDescription: string;
  sequence: number;
  expectedCompletionDays?: number | null;
}

export interface StaffOffense extends StaffOffenseSummary {
  offenseDescription: string;
  offenseProcedures: StaffOffenseProcedure[];
}

/**
 * Who may issue a given sanction — FR-HR-080 (heads of department are limited to verbal warnings)
 * and FR-HR-092 (the MD signs terminations). Ordered by increasing authority.
 */
export type DisciplinaryActionAuthority = 'HeadOfDepartment' | 'Hr' | 'Management';

export const ACTION_AUTHORITY_OPTIONS: { value: DisciplinaryActionAuthority; label: string; hint: string }[] = [
  {
    value: 'HeadOfDepartment',
    label: 'Head of department',
    hint: 'A head of department may issue this directly. FR-HR-080 puts verbal warnings here.',
  },
  {
    value: 'Hr',
    label: 'HR',
    hint: 'HR issues it. The default, and the safe setting for a catalog entry nobody has reviewed.',
  },
  {
    value: 'Management',
    label: 'Management',
    hint: 'Requires management sign-off. FR-HR-092 puts terminations here.',
  },
];

export interface DisciplinaryActionTypeSummary {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  minimumAuthority: DisciplinaryActionAuthority;
  minimumAuthorityName: string;
}

// ── Case ─────────────────────────────────────────────────────────────────────

/**
 * The register row.
 *
 * The five `has*` flags are computed server-side from the case's sanction navigations. They are
 * trustworthy across every list endpoint as of area 9 slice 1 — before that only the by-employee
 * read loaded those navigations, so the flags read false everywhere else and the register's badges
 * disagreed with the case detail.
 */
export interface DisciplinaryCaseSummary {
  id: string;
  caseNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  offenseName: string;
  severity: StaffOffenseSeverity;
  severityName: string;
  status: DisciplinaryStatus;
  statusName: string;
  incidentDate: string;
  reportedDate: string;
  hasWarning: boolean;
  hasSuspension: boolean;
  hasFine: boolean;
  hasTermination: boolean;
  appealFiled: boolean;
  closedDate?: string | null;
}

export interface DisciplinaryCase {
  id: string;
  tenantId: string;
  caseNumber: string;
  status: DisciplinaryStatus;
  statusName: string;

  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;

  staffOffenseId: string;
  offenseName: string;
  offenseCode?: string | null;
  severity: StaffOffenseSeverity;
  severityName: string;

  incidentDate: string;
  incidentDescription: string;

  reportedById: string;
  reportedByName: string;
  reportedDate: string;
  reportedToId?: string | null;
  reportedToName?: string | null;

  requiresInvestigation: boolean;
  hearingRequired: boolean;

  actionTypeId?: string | null;
  actionTypeName?: string | null;
  actionDetails?: string | null;
  decisionDate?: string | null;
  decisionById?: string | null;
  decisionByName?: string | null;
  decisionRationale?: string | null;

  closedDate?: string | null;
  closureNotes?: string | null;
  closedById?: string | null;
  closedByName?: string | null;

  hasWarning: boolean;
  hasSuspension: boolean;
  hasFine: boolean;
  hasTermination: boolean;
  /**
   * Whether a reduction in rank cites this case. A demotion is NOT a sub-entity here — it is a staff
   * movement, because reducing someone's rank means moving them to a different post with its own
   * grade, unit and salary. Area 8 owns that record and the write to the employee; this flag and the
   * list below only report that one exists.
   */
  hasDemotion: boolean;
  linkedDemotions: DisciplinaryLinkedDemotion[];

  // One-to-one sub-entities; null when that penalty or stage does not apply.
  investigation?: DisciplineInvestigation | null;
  hearing?: DisciplineHearing | null;
  warning?: DisciplineWarning | null;
  suspension?: DisciplineSuspension | null;
  fine?: DisciplineFine | null;
  termination?: DisciplineTermination | null;
  separation?: DisciplineSeparation | null;
  appeal?: DisciplineAppeal | null;
  correctiveAction?: DisciplineCorrectiveAction | null;

  actionSteps: DisciplineActionStep[];
  witnesses: DisciplineWitnessSummary[];
  documents: DisciplineDocumentSummary[];
  notes: DisciplineNoteSummary[];
  notifications: DisciplineNotificationSummary[];
  legalReviews: DisciplineLegalReviewSummary[];
}

/**
 * `reportedById` is optional: the server fills it from the caller's token, and only HR may name
 * someone else — recording a report that was made to them. Every other caller leaves it unset.
 */
export interface CreateDisciplinaryCaseRequest {
  employeeId: string;
  staffOffenseId: string;
  severity: StaffOffenseSeverity;
  incidentDate: string;
  incidentDescription: string;
  reportedById?: string;
  reportedDate: string;
  reportedToId?: string | null;
  caseNumber?: string;
  requiresInvestigation: boolean;
  hearingRequired: boolean;
}

export interface UpdateDisciplinaryCaseRequest {
  id: string;
  staffOffenseId: string;
  severity: StaffOffenseSeverity;
  incidentDate: string;
  incidentDescription: string;
  reportedById?: string;
  reportedDate: string;
  reportedToId?: string | null;
  requiresInvestigation: boolean;
  hearingRequired: boolean;
}

/**
 * `decisionById` is absent by design — who decided a case is testimony and comes from the token.
 * Same for `closedById` on the closure payload.
 */
export interface RecordDecisionRequest {
  caseId: string;
  actionTypeId: string;
  actionDetails?: string | null;
  decisionRationale?: string | null;
  decisionDate: string;
}

export interface CloseCaseRequest {
  caseId: string;
  closedDate: string;
  closureNotes?: string | null;
}

// ── Sub-entities ─────────────────────────────────────────────────────────────
//
// ⚠ Every field below was extracted from the DTOs in StaffDisciplineDTOs.cs, not inferred from the
// entity or the field's likely name. Several of these were guessed when the case-detail tabs were
// first written in slice 1 and were WRONG — `findings`/`recommendations` instead of
// `investigationFindings`/`evidenceCollected`, `isPaid` instead of `suspensionWithPay`, a
// `suspensionReason` that does not exist. A wrong field name is not a compile error against an
// interface you also wrote: it renders as undefined, the tab shows "—", and the screen looks like
// it is working on a record with nothing in it.

export interface DisciplineInvestigation {
  id: string;
  disciplinaryActionId: string;
  caseNumber: string;
  investigatorId?: string | null;
  investigatorName?: string | null;
  investigationStartDate?: string | null;
  investigationEndDate?: string | null;
  investigationFindings?: string | null;
  evidenceCollected?: string | null;
}

export interface DisciplineHearing {
  id: string;
  disciplinaryActionId: string;
  caseNumber: string;
  employeeId: string;
  employeeName: string;
  hearingDate?: string | null;
  hearingVenue?: string | null;
  hearingNotes?: string | null;
  hearingOfficerId?: string | null;
  hearingOfficerName?: string | null;
  employeeAttendedHearing: boolean;
  employeeStatement?: string | null;
  employeeHadRepresentation: boolean;
  representativeType?: DisciplinaryRepresentativeType | null;
  representativeTypeName?: string | null;
  representativeEmployeeId?: string | null;
  representativeEmployeeName?: string | null;
  representativeName?: string | null;
  representativePosition?: string | null;
  representativeContactInfo?: string | null;
}

export interface DisciplineWarning {
  id: string;
  disciplinaryActionId: string;
  caseNumber: string;
  employeeId: string;
  employeeName: string;
  warningType: DisciplinaryWarningType;
  warningTypeName: string;
  warningExpiryDate?: string | null;
  /** Server-computed against the expiry date — do not re-derive it in the client. */
  isExpired: boolean;
  warningLetterReference?: string | null;
}

export interface DisciplineSuspension {
  id: string;
  disciplinaryActionId: string;
  caseNumber: string;
  employeeId: string;
  employeeName: string;
  suspensionStartDate?: string | null;
  suspensionEndDate?: string | null;
  /** Note the sense: TRUE means the suspension is WITH pay. FR-HR-179's ladder names the unpaid one. */
  suspensionWithPay: boolean;
  suspensionDays?: number | null;
}

export interface DisciplineFine {
  id: string;
  disciplinaryActionId: string;
  caseNumber: string;
  employeeId: string;
  employeeName: string;
  fineAmount?: number | null;
  finePaymentStatus?: DisciplinaryFinePaymentStatus | null;
  finePaymentStatusName?: string | null;
  fineDueDate?: string | null;
  finePaidAmount?: number | null;
  finePaymentDate?: string | null;
  outstandingBalance?: number | null;
}

export interface DisciplineTermination {
  id: string;
  disciplinaryActionId: string;
  caseNumber: string;
  employeeId: string;
  employeeName: string;
  type: EmployeeTerminationType;
  typeName: string;
  isEligibleForRehire: boolean;
  eligibleForRehireDate?: string | null;
  rehireRestrictions?: string | null;
  finalPaycheckProcessed: boolean;
  finalPaycheckDate?: string | null;
  finalPaycheckAmount?: number | null;
  separationNotes?: string | null;
}

export interface DisciplineSeparation {
  id: string;
  disciplinaryActionId: string;
  caseNumber: string;
  employeeId: string;
  employeeName: string;
  exitInterviewCompleted: boolean;
  exitInterviewDate?: string | null;
  exitInterviewNotes?: string | null;
  exitInterviewerId?: string | null;
  exitInterviewerName?: string | null;
  equipmentReturned: boolean;
  equipmentReturnedDate?: string | null;
  missingEquipment?: string | null;
  accessRevoked: boolean;
  accessRevokedDate?: string | null;
  accessRevokedById?: string | null;
  accessRevokedByName?: string | null;
  finalPayrollProcessed: boolean;
  finalPayrollDate?: string | null;
  benefitsTerminated: boolean;
  benefitsTerminationDate?: string | null;
  exitChecklistCompleted: boolean;
  exitChecklistCompletedDate?: string | null;
  additionalNotes?: string | null;
  /** Server-computed over the six checklist flags. */
  checklistCompletionPercent: number;
}

export interface DisciplineAppeal {
  id: string;
  disciplinaryActionId: string;
  caseNumber: string;
  employeeId: string;
  employeeName: string;
  filedDate: string;
  reason: string;
  appealStatus: DisciplineAppealStatus;
  appealStatusName: string;
  appealOfficerId?: string | null;
  appealOfficerName?: string | null;
  hearingDate?: string | null;
  hearingVenue?: string | null;
  hearingNotes?: string | null;
  appealOutcome?: DisciplineAppealOutcomeType | null;
  appealOutcomeName?: string | null;
  appealOutcomeNotes?: string | null;
  appealOutcomeDate?: string | null;
  appealOutcomeById?: string | null;
  appealOutcomeByName?: string | null;
}

export interface DisciplineCorrectiveActionItem {
  id: string;
  correctiveActionId: string;
  description: string;
  targetDate: string;
  completedDate?: string | null;
  status: DisciplineCorrectiveActionStatus;
  completionNotes?: string | null;
}

export interface DisciplineCorrectiveAction {
  id: string;
  disciplinaryActionId: string;
  caseNumber: string;
  employeeId: string;
  employeeName: string;
  supervisorId: string;
  supervisorName: string;
  objective: string;
  startDate: string;
  reviewDate: string;
  completedDate?: string | null;
  status: DisciplineCorrectiveActionStatus;
  statusName: string;
  notes?: string | null;
  isOverdue: boolean;
  itemCount: number;
  completedItemCount: number;
  items: DisciplineCorrectiveActionItem[];
}

export interface DisciplineActionStep {
  id: string;
  disciplinaryActionId: string;
  offenseProcedureId: string;
  stepName: string;
  stepDescription?: string | null;
  sequence: number;
  expectedCompletionDays?: number | null;
  dueDate?: string | null;
  startedDate?: string | null;
  completedDate?: string | null;
  status: DisciplinaryActionStepStatus;
  statusName: string;
  actionedById?: string | null;
  actionedByName?: string | null;
  notes?: string | null;
  isOverdue: boolean;
  documents: DisciplineDocumentSummary[];
}

/** Note there is no employeeName here — the summary carries the witness's own `name` either way. */
export interface DisciplineWitnessSummary {
  id: string;
  name: string;
  isEmployee: boolean;
  employeeId?: string | null;
  hasStatement: boolean;
  statement?: string | null;
  statementDate?: string | null;
}

export interface DisciplineDocumentSummary {
  id: string;
  fileName: string;
  filePath: string;
  category: DisciplinaryDocumentCategory;
  categoryName: string;
  scope: DisciplinaryDocumentScope;
  scopeName: string;
  actionStepId?: string | null;
  appealId?: string | null;
  uploadDate: string;
  uploadedByName: string;
}

/** The summary carries an EXCERPT, not the full note — the full text is on the note read. */
export interface DisciplineNoteSummary {
  id: string;
  createdByEmployeeName: string;
  noteExcerpt: string;
  isConfidential: boolean;
  noteDate: string;
}

export interface DisciplineNotificationSummary {
  id: string;
  notificationType: DisciplinaryNotificationType;
  notificationTypeName: string;
  sentDate: string;
  isAcknowledged: boolean;
  isFollowupSent: boolean;
}

export interface DisciplineLegalReviewSummary {
  id: string;
  referredToLegalDate: string;
  legalRiskLevel: DisciplineLegalRiskLevel;
  legalRiskLevelName: string;
  legalReviewCompleteDate?: string | null;
  requiresExternalCounsel: boolean;
  isConfidential: boolean;
}

/**
 * A reduction in rank citing a disciplinary case. Read-only here: the movement is area 8's record
 * and owns the destination post, the approval route and the write to the employee.
 */
export interface DisciplinaryLinkedDemotion {
  demotionId: string;
  movementId: string;
  movementNumber: string;
  movementStatus: string;
  gradeLevelDecrease: number;
  effectiveDate?: string | null;
  newPositionTitle?: string | null;
}

// ── Statutory clocks ─────────────────────────────────────────────────────────

/**
 * How a case stands against FR-HR-177's 48-hour written query and FR-HR-178's four-week
 * investigation.
 *
 * Reported, not enforced: a breach is a fact about what already happened, and refusing the next step
 * cannot undo it — it would only stop the case being dealt with. The figures are computed server-side
 * on every read, so the client must never re-derive them; two definitions of "overdue" over the same
 * record is how a screen and its queue end up disagreeing.
 */
export interface DisciplineProcessClock {
  queryDueAt: string;
  queryIssuedAt?: string | null;
  queryIssued: boolean;
  queryAcknowledged: boolean;
  queryAcknowledgedAt?: string | null;
  queryBreached: boolean;
  queryHoursLate?: number | null;

  investigationRequired: boolean;
  investigationOpened: boolean;
  investigationStartedAt?: string | null;
  investigationDueAt?: string | null;
  investigationCompletedAt?: string | null;
  investigationBreached: boolean;
  investigationDaysLate?: number | null;

  // Natural justice. Unlike the two clocks above, THIS ONE BLOCKS — a decision is refused until the
  // employee has been queried and given a chance to answer. Surfaced so the screen can explain the
  // refusal in advance rather than letting the user discover it by pressing the button.
  queryResponseClosesAt?: string | null;
  queryOpportunityWaived: boolean;
  queryOpportunityWaivedAt?: string | null;
  queryOpportunityWaivedReason?: string | null;
  queryOpportunityWaivedByName?: string | null;
  canProposeDecision: boolean;
  /** The server's own words, so the screen and the refusal cannot drift apart. */
  decisionBlockedReason?: string | null;

  advisories: string[];
}

// ── Dashboard ────────────────────────────────────────────────────────────────

export interface DisciplinaryCaseAlert {
  caseId: string;
  caseNumber: string;
  employeeName: string;
  offenseName: string;
  severity: StaffOffenseSeverity;
  severityName: string;
  status: DisciplinaryStatus;
  statusName: string;
  incidentDate: string;
  daysOpen: number;
}

/**
 * Flat counters, not grouped collections — mirrors `StaffDisciplineDashboardDto` exactly. The
 * severity split and the stage split are separate scalar fields rather than a by-status array, so
 * the tiles read them directly.
 */
export interface DisciplineDashboard {
  totalOpenCases: number;
  casesUnderInvestigation: number;
  casesAwaitingHearing: number;
  casesAwaitingDecision: number;
  casesWithActiveAppeal: number;
  casesClosedThisMonth: number;

  activeWarnings: number;
  activeSuspensions: number;
  activeFines: number;
  terminationsPendingProcessing: number;

  minorCases: number;
  moderateCases: number;
  seriousCases: number;
  grossMisconductCases: number;

  overdueActionSteps: number;
  overdueCorrectiveActionItems: number;
  outstandingFines: number;

  recentOpenCases: DisciplinaryCaseAlert[];
  overdueCases: DisciplinaryCaseAlert[];
  computedAt: string;
}
