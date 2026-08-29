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

export const LEGAL_RISK_OPTIONS: { value: DisciplineLegalRiskLevel; label: string }[] = [
  { value: 'None', label: 'None' },
  { value: 'Low', label: 'Low' },
  { value: 'Medium', label: 'Medium' },
  { value: 'High', label: 'High' },
  { value: 'Critical', label: 'Critical' },
];

/**
 * ⚠ `Skipped` is reachable only through `action-steps/{id}/skip`, which demands a reason, and
 * `Completed` only through `.../complete`. Both are listed here because the status has to be
 * RENDERED, but the edit form must not offer either as a plain dropdown choice — setting
 * `Completed` through the update route bypasses `CompleteStepAsync`, so the step would read as
 * done while `completedDate` and `actionedById` stayed empty.
 */
export const ACTION_STEP_STATUS_OPTIONS: { value: DisciplinaryActionStepStatus; label: string }[] = [
  { value: 'Pending', label: 'Pending' },
  { value: 'InProgress', label: 'In progress' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Cancelled', label: 'Cancelled' },
  { value: 'Skipped', label: 'Skipped' },
];

/** What the action-step edit form may set directly. The other two are transitions, not values. */
export const ACTION_STEP_EDITABLE_STATUSES: { value: DisciplinaryActionStepStatus; label: string }[] = [
  { value: 'Pending', label: 'Pending' },
  { value: 'InProgress', label: 'In progress' },
  { value: 'Cancelled', label: 'Cancelled' },
];

export const CORRECTIVE_ACTION_STATUS_OPTIONS: { value: DisciplineCorrectiveActionStatus; label: string }[] = [
  { value: 'Pending', label: 'Pending' },
  { value: 'InProgress', label: 'In progress' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Cancelled', label: 'Cancelled' },
];

/**
 * A corrective action plan the API will refuse to edit.
 *
 * ⚠ Unlike most of HR this rule IS enforced server-side — `CorrectiveActionService.UpdateAsync`
 * throws "A completed or cancelled corrective action plan cannot be edited." The screen mirrors it
 * so the refusal is visible before the user types, rather than arriving as a toast afterwards.
 */
export const LOCKED_CORRECTIVE_ACTION_STATUSES: DisciplineCorrectiveActionStatus[] = [
  'Completed',
  'Cancelled',
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
  /** Server-rendered `status.ToString()`. Read-only. */
  statusName?: string;
  completionNotes?: string | null;
  /**
   * Server-computed: past its target date and neither completed nor cancelled. ⚠ Computed against
   * the SERVER's clock at serialisation time, so do not recompute it in the browser — a client in
   * another timezone would disagree with the list it is looking at.
   */
  isOverdue?: boolean;
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
  /** The notice's text (area 25 slice 9): these rows ride the case detail the SUBJECT can read,
   * and acknowledging a notice whose words you cannot read defeats the notice. */
  content: string;
  isAcknowledged: boolean;
  acknowledgedDate: string | null;
  isFollowupSent: boolean;
}

/**
 * The FULL legal review, as `cases/{id}/legal-reviews` and `legal-reviews/{id}` return it.
 *
 * ⚠ **Not what the case detail carries.** `StaffDisciplineCaseDetailDto.LegalReviews` is a
 * `...SummaryDto` — seven fields, no advice, no counsel, no costs. A panel that renders the case
 * detail's copy shows a row with nothing in it; the dedicated read is the one with the record.
 */
export interface DisciplineLegalReview {
  id: string;
  tenantId: string;
  disciplinaryActionId: string;
  caseNumber: string;
  referredToLegalDate: string;
  /** ⚠ Stamped from the caller's token — the create payload has no such field. */
  referredById?: string | null;
  referredByName?: string | null;
  legalReviewCompleteDate?: string | null;
  legalRiskLevel: DisciplineLegalRiskLevel;
  legalRiskLevelName: string;
  legalAdvice?: string | null;
  requiresExternalCounsel: boolean;
  externalCounselId?: string | null;
  externalCounselName?: string | null;
  externalCounselFirm?: string | null;
  externalCounselReviewDate?: string | null;
  externalCounselOpinion?: string | null;
  legalCostsIncurred?: number | null;
  isConfidential: boolean;
}

/**
 * ⚠ No `referredById`. "Who referred this case to legal" is a statement about the actor, so it is
 * stamped from the token in `ReferAsync`. It used to be accepted from the body and copied onto the
 * entity verbatim — any HR user could record a colleague as the referrer — and was removed when
 * this screen was built. Sending one now is ignored, not honoured.
 *
 * ⚠ No `legalAdvice` either: advice is what comes BACK from the referral, so it only exists on the
 * update payload. A referral form asking for the advice it is about to request would be nonsense.
 */
export interface CreateDisciplineLegalReview {
  referredToLegalDate: string;
  legalRiskLevel: DisciplineLegalRiskLevel;
  requiresExternalCounsel: boolean;
  externalCounselId?: string | null;
  externalCounselName?: string | null;
  externalCounselFirm?: string | null;
  /** Defaults to TRUE on the DTO — a legal review is confidential unless someone says otherwise. */
  isConfidential: boolean;
}

/** ⚠ `id` is required and must match the route — the controller compares them. */
export interface UpdateDisciplineLegalReview {
  id: string;
  legalRiskLevel: DisciplineLegalRiskLevel;
  legalReviewCompleteDate?: string | null;
  legalAdvice?: string | null;
  requiresExternalCounsel: boolean;
  externalCounselId?: string | null;
  externalCounselName?: string | null;
  externalCounselFirm?: string | null;
  externalCounselReviewDate?: string | null;
  externalCounselOpinion?: string | null;
  legalCostsIncurred?: number | null;
  isConfidential: boolean;
}

/**
 * ⚠ The action step's update payload carries **`stepId`, not `id`**.
 *
 * `UpdateActionStepDto` is the one update DTO in this area that does NOT inherit `UpdateDtoBase`;
 * it declares its own `StepId`. The controller **assigns** `dto.StepId = id` from the route rather
 * than comparing them, so unlike every sibling here it does not 400 on a mismatch — it silently
 * wins. (`[Required]` on a non-nullable `Guid` does not reject `Guid.Empty` either, so a missing
 * one would not be caught.) The field is sent anyway and named correctly, because relying on the
 * route to paper over a wrong body is how the next person gets caught by a DTO that does compare.
 */
export interface UpdateDisciplineActionStep {
  stepId: string;
  dueDate?: string | null;
  startedDate?: string | null;
  completedDate?: string | null;
  status: DisciplinaryActionStepStatus;
  /** The step's actor. Stamped by the service from the token on complete/skip. */
  actionedById?: string | null;
  notes?: string | null;
}

/**
 * ⚠ `employeeId` is NOT derived from the case — `CreateAsync` copies it straight from the body, so
 * the screen must pass the case's own subject or the plan attaches to the wrong person. It is the
 * person the plan is FOR, not the actor; the actor comes from the token.
 */
export interface CreateDisciplineCorrectiveAction {
  employeeId: string;
  supervisorId: string;
  objective: string;
  startDate: string;
  reviewDate: string;
  notes?: string | null;
}

/** ⚠ No `employeeId` — the plan's subject is fixed at creation. */
export interface UpdateDisciplineCorrectiveAction {
  id: string;
  supervisorId: string;
  objective: string;
  startDate: string;
  reviewDate: string;
  completedDate?: string | null;
  status: DisciplineCorrectiveActionStatus;
  notes?: string | null;
}

export interface CreateDisciplineCorrectiveActionItem {
  description: string;
  targetDate: string;
}

/**
 * ⚠ `completionNotes` is on the update payload but the natural way to set it is
 * `corrective-action-items/{id}/complete`, which takes the notes as a **bare JSON string body**
 * and stamps the completion date and actor. Setting `status: 'Completed'` through this payload
 * instead leaves those unset.
 */
export interface UpdateDisciplineCorrectiveActionItem {
  id: string;
  description: string;
  targetDate: string;
  completedDate?: string | null;
  status: DisciplineCorrectiveActionStatus;
  completionNotes?: string | null;
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

  // FR-HR-180. The FILING window is enforced — an appeal out of time is refused. The DECISION window
  // is advisory: an appeal decided late is still a decision, and refusing to record it would leave
  // the employee with no answer at all.
  decisionMade: boolean;
  appealFilingClosesAt?: string | null;
  appealFilingWindowOpen: boolean;
  appealFiled: boolean;
  appealFiledAt?: string | null;
  appealDecisionDueAt?: string | null;
  appealDecisionBreached: boolean;
  appealDecisionWorkingDaysLate?: number | null;

  advisories: string[];
}

/** Grounds of appeal. The appellant is the token's employee — never a field. */
export interface FileAppealRequest {
  caseId: string;
  reason: string;
}

export interface ScheduleAppealHearingRequest {
  caseId: string;
  hearingDate: string;
  hearingVenue?: string | null;
  appealOfficerId?: string | null;
}

/** `appealOutcomeById` is absent by design — who decided an appeal is testimony, taken from the token. */
export interface RecordAppealOutcomeRequest {
  caseId: string;
  appealOutcome: DisciplineAppealOutcomeType;
  appealOutcomeNotes?: string | null;
  appealOutcomeDate: string;
  hearingNotes?: string | null;
}

export const APPEAL_OUTCOME_OPTIONS: { value: DisciplineAppealOutcomeType; label: string; hint: string }[] = [
  {
    value: 'Upheld',
    label: 'Sanction upheld',
    hint: 'The appeal fails and the original sanction stands.',
  },
  {
    value: 'Overturned',
    label: 'Sanction overturned',
    hint: 'The appeal succeeds and the sanction falls away.',
  },
  {
    value: 'Reduced',
    label: 'Sanction reduced',
    hint: 'The appeal partly succeeds and a lesser sanction replaces it.',
  },
];

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
