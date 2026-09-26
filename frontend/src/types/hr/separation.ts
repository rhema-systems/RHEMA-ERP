/**
 * Area 9b — Separation, Clearance & Exit. Backend route: `api/hr/separations`.
 *
 * ⚠ Every type here was written from the C# DTO, not from an endpoint name. The area-12 lesson —
 * "a TypeScript type written from an endpoint name is fiction that type-checks" — cost a slice
 * there, and this area has already paid it twice on the harness side (a payload with `hireDate`
 * that the API silently ignored, and a `CurrencyDto.Code` that is actually `CurrencyCode`).
 */

/** FR-HR-182's routes out, plus contract expiry. Two were appended by this area. */
export type SeparationType =
  | 'InvoluntaryForCause'
  | 'InvoluntaryPerformance'
  | 'InvoluntaryRedundancy'
  | 'VoluntaryResignation'
  | 'VoluntaryRetirement'
  | 'MutualAgreement'
  | 'ContractExpiry'
  | 'Death'
  | 'SummaryDismissal'
  | 'CompulsoryRetirement'
  | 'MedicalRetirement'
  | 'Other';

/**
 * Ordered to the FRD's own sequence: clearance gates the settlement, and Internal Audit's review
 * gates payment.
 */
export type SeparationStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'ClearanceInProgress'
  | 'ClearanceCompleted'
  | 'SettlementPending'
  | 'SettlementUnderReview'
  | 'SettlementApproved'
  | 'Completed'
  | 'Cancelled'
  | 'Rejected';

export type TerminationReason =
  | 'Resignation'
  | 'Redundancy'
  | 'Dismissal'
  | 'ContractExpiry'
  | 'Retirement'
  | 'Death'
  | 'MutualAgreement'
  | 'EndOfInternship'
  | 'Other';

export interface SeparationListItem {
  id: string;
  separationNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  positionTitle?: string | null;
  organizationUnitName?: string | null;
  separationType: SeparationType;
  separationTypeName: string;
  status: SeparationStatus;
  statusName: string;
  initiatedOn: string;
  lastWorkingDay?: string | null;
  effectiveDate?: string | null;
  isProcedural: boolean;
  isSystemInitiated: boolean;
  isDisciplinary: boolean;
  /** False on a completed separation is the defect this area was opened on. */
  employeeRecordUpdated: boolean;
}

export interface SeparationDetail extends SeparationListItem {
  reasonCategory?: TerminationReason | null;
  reasonCategoryName?: string | null;
  reasonNotes?: string | null;

  /**
   * The medical board a medical retirement rests on (round 5, lane K-II-a). A bare id: the board is
   * a Medical record, read from its own service under the Medical permissions.
   */
  medicalBoardId?: string | null;

  noticeGivenOn?: string | null;
  noticeDays?: number | null;
  noticeRequiredDays: number;
  /** Null while either notice date is unknown — not zero. */
  noticeServedDays?: number | null;
  /** What FR-HR-184's notice pay is computed from. Derived, never stored. */
  noticeShortfallDays?: number | null;

  submittedOn?: string | null;
  submittedById?: string | null;
  submittedByName?: string | null;

  initiatedById?: string | null;
  initiatedByName?: string | null;

  approvedById?: string | null;
  approvedByName?: string | null;
  approvedOn?: string | null;
  approvalNotes?: string | null;
  workflowInstanceId?: string | null;

  rejectedById?: string | null;
  rejectedByName?: string | null;
  rejectedOn?: string | null;
  rejectionReason?: string | null;

  absenceDays?: number | null;
  /** FR-HR-092: everything except a procedural separation needs the MD. */
  requiresManagingDirectorSignature: boolean;

  isNoticeWaived: boolean;
  noticeWaiverReason?: string | null;
  isNoticePaidInLieu: boolean;
  noticeDecisionOn?: string | null;
  noticeDecidedById?: string | null;
  noticeDecidedByName?: string | null;
  /** Notice was left unserved and nobody has decided what happens to it — blocks the settlement. */
  requiresNoticeDecision: boolean;

  isEligibleForRehire: boolean;
  eligibleForRehireDate?: string | null;
  rehireRestrictions?: string | null;

  disciplinaryActionId?: string | null;
  employeeRecordUpdatedOn?: string | null;

  cancelledOn?: string | null;
  cancelledById?: string | null;
  cancelledByName?: string | null;
  cancellationReason?: string | null;

  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateSeparation {
  employeeId: string;
  separationType: SeparationType;
  reasonCategory?: TerminationReason | null;
  reasonNotes?: string | null;
  initiatedOn?: string | null;
  noticeGivenOn?: string | null;
  /** Omit to take the tenant default from policy. */
  noticeDays?: number | null;
  lastWorkingDay?: string | null;
  /**
   * ⚠ Leave null for a compulsory retirement — the service computes the birthday and REFUSES a
   * date that disagrees (FR-HR-093). Same for a contract expiry where a contract end date exists.
   */
  effectiveDate?: string | null;
  isEligibleForRehire?: boolean;
  eligibleForRehireDate?: string | null;
  rehireRestrictions?: string | null;
  /** At or above the tenant's threshold this makes the separation procedural (FR-HR-092). */
  absenceDays?: number | null;
  disciplinaryActionId?: string | null;
}

export interface UpdateSeparation {
  separationType?: SeparationType;
  reasonCategory?: TerminationReason;
  reasonNotes?: string | null;
  noticeGivenOn?: string | null;
  noticeDays?: number | null;
  lastWorkingDay?: string | null;
  effectiveDate?: string | null;
  isEligibleForRehire?: boolean;
  eligibleForRehireDate?: string | null;
  absenceDays?: number | null;
  rehireRestrictions?: string | null;
}

export interface SeparationQuery {
  pageNumber?: number;
  pageSize?: number;
  employeeId?: string;
  separationType?: SeparationType;
  status?: SeparationStatus;
  effectiveFrom?: string;
  effectiveTo?: string;
  search?: string;
  /** Completed separations never applied to an employee record. */
  onlyUnappliedToEmployee?: boolean;
}

export interface SubmitSeparation {
  notes?: string | null;
}

/**
 * FR-HR-092. Who may send this depends on the record, not on a permission.
 *
 * ⚠ The notice settlement is NOT here any more — see {@link RecordNoticeDecision}. Approval is a
 * yes/no plus a comment, which is all the generic workflow engine's approve action carries.
 */
export interface ApproveSeparation {
  notes?: string | null;
}

/**
 * What happens to notice that was not served: waive the balance, pay it instead of working it, or
 * neither. Taken by whoever may sign the separation, and always before the settlement is prepared.
 *
 * ⚠ Sending an empty payload records "neither applies", which is a real decision and clears the
 * settlement block. That is not the same as never calling this at all.
 */
export interface RecordNoticeDecision {
  waiveNotice?: boolean;
  reason?: string | null;
  payNoticeInLieu?: boolean;
}

export interface RejectSeparation {
  reason: string;
}

export interface CancelSeparation {
  reason: string;
}

// ── Documents ────────────────────────────────────────────────────────────────

export type SeparationDocumentCategory =
  | 'ResignationLetter'
  | 'AcceptanceOrNoticeLetter'
  | 'ClearanceForm'
  | 'SettlementStatement'
  | 'ExitInterviewRecord'
  | 'MedicalReport'
  | 'DeathCertificate'
  | 'Other';

export interface SeparationDocument {
  id: string;
  separationId: string;
  category: SeparationDocumentCategory;
  categoryName: string;
  fileName: string;
  description?: string | null;
  uploadedOn: string;
  uploadedById?: string | null;
  uploadedByName?: string | null;
  isRegisteredInDms: boolean;
  /** ⚠ There is deliberately no filePath — the download endpoint serves the file. */
}

// ── Clearance (FR-HR-091 / FR-HR-183) ────────────────────────────────────────

export type ClearanceItemKind =
  | 'OutstandingLoan'
  | 'SalaryAdvance'
  | 'CompanyProperty'
  | 'OfficeEquipment'
  | 'DutyPostKeys'
  | 'DocumentsAndRecords'
  | 'PayrollRecovery'
  | 'Other';

export type ClearanceItemStatus =
  | 'Pending'
  | 'Cleared'
  | 'Blocked'
  | 'Waived'
  | 'NotApplicable';

export interface ClearanceTemplate {
  id: string;
  name: string;
  kind: ClearanceItemKind;
  kindName: string;
  description?: string | null;
  owningOrganizationUnitId?: string | null;
  owningOrganizationUnitName?: string | null;
  isMandatory: boolean;
  isActive: boolean;
  sortOrder: number;
  /** Only loans, advances and recoveries can carry money. */
  carriesAmount: boolean;
  /**
   * Whether this line is fed automatically from the HR Assets register.
   *
   * ⚠ **At most one line may be**, enforced server-side by `RequireSoleAssetSourceAsync`: two
   * would list every unreturned asset twice and deduct every surcharge twice from the final
   * settlement. The seeded catalogue puts it on "Company property".
   */
  sourcesFromAssetRegister: boolean;
}

export interface CreateClearanceTemplate {
  name: string;
  kind: ClearanceItemKind;
  description?: string | null;
  owningOrganizationUnitId?: string | null;
  isMandatory?: boolean;
  isActive?: boolean;
  sortOrder?: number;
  sourcesFromAssetRegister?: boolean;
}

export interface UpdateClearanceTemplate {
  name?: string;
  kind?: ClearanceItemKind;
  description?: string | null;
  owningOrganizationUnitId?: string | null;
  isMandatory?: boolean;
  isActive?: boolean;
  sortOrder?: number;
  sourcesFromAssetRegister?: boolean;
}

export interface ClearanceItem {
  id: string;
  separationId: string;
  templateId?: string | null;
  name: string;
  kind: ClearanceItemKind;
  kindName: string;
  owningOrganizationUnitId?: string | null;
  owningOrganizationUnitName?: string | null;
  isMandatory: boolean;
  sortOrder: number;
  status: ClearanceItemStatus;
  statusName: string;
  outstandingAmount?: number | null;
  carriesAmount: boolean;
  notes?: string | null;
  signedOffBy?: string | null;
  recordedById?: string | null;
  recordedByName?: string | null;
  recordedOn?: string | null;
}

export interface RecordClearanceItem {
  status: ClearanceItemStatus;
  signedOffBy?: string | null;
  outstandingAmount?: number | null;
  notes?: string | null;
}

export interface SeparationClearance {
  separationId: string;
  separationNumber: string;
  employeeName: string;
  separationStatus: SeparationStatus;
  separationStatusName: string;
  items: ClearanceItem[];
  totalItems: number;
  pendingItems: number;
  clearedItems: number;
  blockedItems: number;
  waivedItems: number;
  notApplicableItems: number;
  /** Mandatory lines still without a terminal answer, or answered Blocked. */
  mandatoryOutstanding: number;
  totalOutstandingAmount: number;
  /** FR-HR-091's gate, stated as an answer rather than left to be inferred. */
  canComplete: boolean;
  blockedReason?: string | null;
}

// ── Settlement (FR-HR-184) and its review (FR-HR-185) ────────────────────────

export type SettlementLineCategory =
  | 'UnpaidSalary'
  | 'NoticePay'
  | 'LeaveEncashment'
  | 'GratuityOrEndOfService'
  | 'BenefitPayment'
  | 'PensionRelated'
  | 'OtherEarning'
  | 'LoanRepayment'
  | 'SalaryAdvanceRecovery'
  | 'TravelAdvanceRecovery'
  | 'PropertyRecovery'
  | 'TaxDeduction'
  | 'OtherDeduction';

/**
 * ⚠ `CannotCompute` is not a failure state — it is the honest one. 202 of 3,883 employees have a
 * salary on file and there are no leave balances, so most computed lines land here on live data. A
 * screen must show "not computed" rather than a zero, because zero is a claim somebody will sign.
 */
export type SettlementLineComputation = 'Computed' | 'ManuallyEntered' | 'CannotCompute';

export type SettlementReviewOutcome = 'NotReviewed' | 'Approved' | 'Returned';

export interface SettlementLine {
  id: string;
  settlementId: string;
  category: SettlementLineCategory;
  categoryName: string;
  isDeduction: boolean;
  description: string;
  /** Null where the line could not be valued. Null is not zero. */
  amount?: number | null;
  computation: SettlementLineComputation;
  computationName: string;
  basis?: string | null;
  sourceReference?: string | null;
  sourceClearanceItemId?: string | null;
  sourceTravelAdvanceId?: string | null;
  isSystemGenerated: boolean;
  sortOrder: number;
}

export interface SeparationSettlement {
  id: string;
  separationId: string;
  separationNumber: string;
  employeeName: string;
  separationStatus: SeparationStatus;
  separationStatusName: string;
  currencyCode: string;
  dailyRate?: number | null;
  dailyRateBasis?: string | null;
  lines: SettlementLine[];
  grossEarnings: number;
  totalDeductions: number;
  netPayable: number;
  /** While above zero the net figure is incomplete and the statement cannot be finalised. */
  uncomputedLines: number;
  isFinalised: boolean;
  finalisedOn?: string | null;
  finalisedByName?: string | null;
  preparedById?: string | null;
  preparedByName?: string | null;
  preparedOn?: string | null;
  notes?: string | null;
  canFinalise: boolean;
  blockedReason?: string | null;
  reviewOutcome: SettlementReviewOutcome;
  reviewOutcomeName: string;
  reviewedById?: string | null;
  reviewedByName?: string | null;
  reviewedOn?: string | null;
  reviewNotes?: string | null;
  returnCount: number;
  isClearedForPayment: boolean;
}

export interface AddSettlementLine {
  category: SettlementLineCategory;
  isDeduction?: boolean;
  description: string;
  /** Omit to record the line as still uncomputed. */
  amount?: number | null;
  /** Required whenever an amount is supplied. */
  sourceReference?: string | null;
}

export interface UpdateSettlementLine {
  description?: string;
  amount?: number | null;
  sourceReference?: string | null;
  isDeduction?: boolean;
}

export interface FinaliseSettlement {
  notes?: string | null;
}

export interface ReviewSettlement {
  notes?: string | null;
}

// ── Retirement, contract expiry, reminders ───────────────────────────────────

export interface UpcomingRetirement {
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  positionTitle?: string | null;
  organizationUnitName?: string | null;
  dateOfBirth?: string | null;
  currentAge?: number | null;
  retirementAge: number;
  retirementDate: string;
  daysUntilRetirement: number;
  isOverdue: boolean;
  isExplicitDate: boolean;
  existingSeparationId?: string | null;
  existingSeparationNumber?: string | null;
  existingSeparationStatus?: string | null;
}

export interface UpcomingContractExpiry {
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  positionTitle?: string | null;
  organizationUnitName?: string | null;
  contractId: string;
  contractNumber?: string | null;
  contractStartDate?: string | null;
  contractEndDate: string;
  daysUntilExpiry: number;
  isOverdue: boolean;
  existingSeparationId?: string | null;
  existingSeparationNumber?: string | null;
  existingSeparationStatus?: string | null;
}

export interface SweepResult {
  horizonDays: number;
  dueCount: number;
  raisedCount: number;
  skippedExistingCount: number;
  failures: string[];
  raised: SeparationListItem[];
}

export type SeparationReminderKind =
  | 'RetirementApproaching'
  | 'ContractExpiring'
  | 'ClearanceOutstanding'
  | 'SettlementAwaitingReview'
  | 'SettlementApprovedNotCompleted';

export interface SeparationReminderItem {
  kind: SeparationReminderKind;
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  separationId?: string | null;
  reference?: string | null;
  dueDate?: string | null;
  daysRemaining: number;
  escalationTier: number;
  message: string;
  alreadyRaised: boolean;
}

export interface SeparationReminderRunResult {
  runId: string;
  startedAt: string;
  completedAt?: string | null;
  trigger: string;
  candidatesFound: number;
  remindersQueued: number;
  suppressedAsDuplicate: number;
  byKind: Record<string, number>;
  raised: SeparationReminderItem[];
}

export interface DisciplinaryOrphanRepair {
  dryRun: boolean;
  foundCount: number;
  raisedCount: number;
  alreadyTerminatedCount: number;
  wouldRaise: string[];
  failures: string[];
  raised: SeparationListItem[];
}

// ── Exit interview ───────────────────────────────────────────────────────────
//
// ⚠ Deliberately NOT the same list as the separation's own reason. The organisation records
// "resignation"; the employee says whether it was the pay or the manager. Same exit, two different
// facts, and only the second one tells anybody what to fix.
export type ExitInterviewReason =
  | 'PayAndBenefits'
  | 'CareerProgression'
  | 'ManagementOrSupervision'
  | 'WorkloadOrStress'
  | 'WorkLifeBalance'
  | 'WorkingConditions'
  | 'RelationshipWithColleagues'
  | 'JobSecurity'
  | 'Relocation'
  | 'HealthOrPersonal'
  | 'EndOfService'
  | 'Other';

export const EXIT_INTERVIEW_REASONS: { value: ExitInterviewReason; label: string }[] = [
  { value: 'PayAndBenefits', label: 'Pay and benefits' },
  { value: 'CareerProgression', label: 'Career progression' },
  { value: 'ManagementOrSupervision', label: 'Management or supervision' },
  { value: 'WorkloadOrStress', label: 'Workload or stress' },
  { value: 'WorkLifeBalance', label: 'Work-life balance' },
  { value: 'WorkingConditions', label: 'Working conditions' },
  { value: 'RelationshipWithColleagues', label: 'Relationship with colleagues' },
  { value: 'JobSecurity', label: 'Job security' },
  { value: 'Relocation', label: 'Relocation' },
  { value: 'HealthOrPersonal', label: 'Health or personal' },
  { value: 'EndOfService', label: 'Not applicable — end of service' },
  { value: 'Other', label: 'Other' },
];

/**
 * ⚠ Every rating is nullable and runs 1–5. `null` means the question was not asked; zero would
 * mean the worst possible answer. A screen must render the difference, and must never send 0.
 */
export interface SeparationExitInterview {
  id: string;
  separationId: string;
  separationNumber: string;
  employeeName: string;
  wasDeclined: boolean;
  declinedReason?: string | null;
  conductedOn?: string | null;
  conductedById?: string | null;
  conductedByName?: string | null;
  primaryReason?: ExitInterviewReason | null;
  primaryReasonName?: string | null;
  primaryReasonDetail?: string | null;
  overallExperienceRating?: number | null;
  managementRating?: number | null;
  payAndBenefitsRating?: number | null;
  careerDevelopmentRating?: number | null;
  wouldRecommendEmployer?: boolean | null;
  wouldConsiderReturning?: boolean | null;
  whatWorkedWell?: string | null;
  whatShouldChange?: string | null;
  additionalComments?: string | null;
  recordedById?: string | null;
  recordedByName?: string | null;
  recordedOn: string;
}

export interface RecordExitInterviewPayload {
  wasDeclined: boolean;
  declinedReason?: string | null;
  conductedOn?: string | null;
  conductedById?: string | null;
  conductedByName?: string | null;
  primaryReason?: ExitInterviewReason | null;
  primaryReasonDetail?: string | null;
  overallExperienceRating?: number | null;
  managementRating?: number | null;
  payAndBenefitsRating?: number | null;
  careerDevelopmentRating?: number | null;
  wouldRecommendEmployer?: boolean | null;
  wouldConsiderReturning?: boolean | null;
  whatWorkedWell?: string | null;
  whatShouldChange?: string | null;
  additionalComments?: string | null;
}

// ── Analytics ────────────────────────────────────────────────────────────────

export interface SeparationBreakdownRow {
  key: string;
  label: string;
  count: number;
  percentage: number;
}

export interface SeparationPipelineStage {
  status: string;
  label: string;
  count: number;
  /** Null when the stage is empty — not zero, which would read as "something arrived today". */
  oldestDays?: number | null;
}

export interface SeparationAnalytics {
  fromDate: string;
  toDate: string;
  completedInPeriod: number;
  raisedInPeriod: number;
  inFlight: number;
  activeHeadcount: number;
  exitRatePercent: number;
  byRoute: SeparationBreakdownRow[];
  byReason: SeparationBreakdownRow[];
  pipeline: SeparationPipelineStage[];
  settledEarnings: number;
  settledRecoveries: number;
  settledNetPayable: number;
  currencyCode: string;
  /** Settlements finalised with lines nobody could value. */
  settlementsWithUnvaluedLines: number;
  /** ⚠ This area's founding defect as a number: exits completed, employee record never updated. */
  completedButNotApplied: number;
}

/**
 * ⚠ Coverage and decline rate come FIRST for a reason: an average over four interviews out of
 * ninety exits describes those four people, not the organisation. The averages are nullable
 * because they run over answers actually given, and there may be none.
 */
export interface ExitInterviewThemes {
  separationsInPeriod: number;
  interviewsRecorded: number;
  interviewsConducted: number;
  interviewsDeclined: number;
  coveragePercent: number;
  declineRatePercent: number;
  byPrimaryReason: SeparationBreakdownRow[];
  averageOverallExperience?: number | null;
  averageManagement?: number | null;
  averagePayAndBenefits?: number | null;
  averageCareerDevelopment?: number | null;
  wouldRecommendPercent?: number | null;
  wouldReturnPercent?: number | null;
}
