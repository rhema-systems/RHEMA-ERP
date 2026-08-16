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
  hasDemotion: boolean;

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

// ── Sub-entities (read shapes; their write screens land in slices 3-6) ────────

export interface DisciplineInvestigation {
  id: string;
  disciplinaryActionId: string;
  investigatorId?: string | null;
  investigatorName?: string | null;
  investigationStartDate?: string | null;
  investigationEndDate?: string | null;
  findings?: string | null;
  recommendations?: string | null;
}

export interface DisciplineHearing {
  id: string;
  disciplinaryActionId: string;
  hearingDate?: string | null;
  hearingVenue?: string | null;
  hearingOfficerId?: string | null;
  hearingOfficerName?: string | null;
  representativeEmployeeId?: string | null;
  representativeEmployeeName?: string | null;
  representativeType?: DisciplinaryRepresentativeType | null;
  hearingNotes?: string | null;
}

export interface DisciplineWarning {
  id: string;
  disciplinaryActionId: string;
  warningType: DisciplinaryWarningType;
  warningTypeName: string;
  warningExpiryDate?: string | null;
  warningLetterReference?: string | null;
}

export interface DisciplineSuspension {
  id: string;
  disciplinaryActionId: string;
  suspensionStartDate?: string | null;
  suspensionEndDate?: string | null;
  isPaid: boolean;
  suspensionReason?: string | null;
}

export interface DisciplineFine {
  id: string;
  disciplinaryActionId: string;
  fineAmount?: number | null;
  finePaidAmount?: number | null;
  fineDueDate?: string | null;
  finePaymentStatus: DisciplinaryFinePaymentStatus;
  finePaymentStatusName: string;
}

export interface DisciplineTermination {
  id: string;
  disciplinaryActionId: string;
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
  exitInterviewCompleted: boolean;
  exitInterviewDate?: string | null;
  exitInterviewerId?: string | null;
  exitInterviewerName?: string | null;
  equipmentReturned: boolean;
  accessRevoked: boolean;
  finalPayrollProcessed: boolean;
  benefitsTerminated: boolean;
  exitChecklistCompleted: boolean;
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
  targetDate?: string | null;
  status: DisciplineCorrectiveActionStatus;
  completedDate?: string | null;
}

export interface DisciplineCorrectiveAction {
  id: string;
  disciplinaryActionId: string;
  employeeId: string;
  employeeName?: string | null;
  supervisorId: string;
  supervisorName?: string | null;
  startDate?: string | null;
  reviewDate?: string | null;
  status: DisciplineCorrectiveActionStatus;
  statusName: string;
  items: DisciplineCorrectiveActionItem[];
}

export interface DisciplineActionStep {
  id: string;
  disciplinaryActionId: string;
  offenseProcedureId: string;
  stepName?: string | null;
  sequence?: number | null;
  dueDate?: string | null;
  startedDate?: string | null;
  completedDate?: string | null;
  status: DisciplinaryActionStepStatus;
  statusName: string;
  actionedById?: string | null;
  actionedByName?: string | null;
  notes?: string | null;
}

export interface DisciplineWitnessSummary {
  id: string;
  disciplinaryActionId: string;
  name: string;
  isEmployee: boolean;
  employeeId?: string | null;
  employeeName?: string | null;
  statementDate?: string | null;
  hasStatement: boolean;
}

export interface DisciplineDocumentSummary {
  id: string;
  disciplinaryActionId: string;
  documentName: string;
  category: DisciplinaryDocumentCategory;
  categoryName: string;
  scope: DisciplinaryDocumentScope;
  uploadDate: string;
  uploadedById: string;
  uploadedByName?: string | null;
}

export interface DisciplineNoteSummary {
  id: string;
  disciplinaryActionId: string;
  noteDate: string;
  isConfidential: boolean;
  createdByEmployeeId: string;
  createdByName?: string | null;
  content: string;
}

export interface DisciplineNotificationSummary {
  id: string;
  disciplinaryActionId: string;
  notificationType: DisciplinaryNotificationType;
  notificationTypeName: string;
  sentDate: string;
  sentById: string;
  sentByName?: string | null;
  acknowledgedDate?: string | null;
  isAcknowledged: boolean;
  isFollowupSent: boolean;
}

export interface DisciplineLegalReviewSummary {
  id: string;
  disciplinaryActionId: string;
  referredToLegalDate?: string | null;
  legalReviewCompleteDate?: string | null;
  legalRiskLevel: DisciplineLegalRiskLevel;
  legalRiskLevelName: string;
  requiresExternalCounsel: boolean;
  referredByName?: string | null;
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
