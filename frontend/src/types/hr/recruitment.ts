/**
 * Recruitment — slice A: requisition → vacancy → posting.
 *
 * Routes differ per controller, as everywhere in HR: `api/StaffRequisitions`,
 * `api/job-vacancies`, `api/job-postings`, `api/position-vacancies`.
 */

// ── enums (string unions matching the backend's JSON) ──────────────────────

export const STAFF_REQUISITION_STATUSES = [
  'Draft',
  'Submitted',
  'UnderReview',
  'Approved',
  'Rejected',
  'OnHold',
  'Cancelled',
  'PartiallyFulfilled',
  'Fulfilled',
] as const;
export type StaffRequisitionStatus = (typeof STAFF_REQUISITION_STATUSES)[number];

export const STAFF_REQUISITION_TYPES = [
  'NewPosition',
  'Replacement',
  'Temporary',
  'Contract',
  'Internship',
  'Secondment',
] as const;
export type StaffRequisitionType = (typeof STAFF_REQUISITION_TYPES)[number];

export const STAFF_REQUISITION_PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'] as const;
export type StaffRequisitionPriority = (typeof STAFF_REQUISITION_PRIORITIES)[number];

export const JOB_VACANCY_STATUSES = [
  'Draft',
  'PendingApproval',
  'Approved',
  'Rejected',
  'Published',
  'ClosedForApplications',
  'Shortlisting',
  'Interviewing',
  'OfferStage',
  'Filled',
  'Cancelled',
] as const;
export type JobVacancyStatus = (typeof JOB_VACANCY_STATUSES)[number];

export const JOB_POSTING_STATUSES = ['Draft', 'Published', 'Expired', 'Withdrawn'] as const;
export type JobPostingStatus = (typeof JOB_POSTING_STATUSES)[number];

export const JOB_POSTING_CHANNELS = [
  'InternalPortal',
  'CompanyWebsite',
  'LinkedIn',
  'JobBoard',
  'Newspaper',
  'Radio',
  'RecruitmentAgency',
  'Referral',
  'Other',
] as const;
export type JobPostingChannel = (typeof JOB_POSTING_CHANNELS)[number];

export const EMPLOYMENT_TYPES = [
  'Permanent',
  'Contract',
  'Temporary',
  'Internship',
  'PartTime',
  'Casual',
] as const;
export type EmploymentType = (typeof EMPLOYMENT_TYPES)[number];

export const WORK_MODES = ['OnSite', 'Remote', 'Hybrid'] as const;
export type WorkMode = (typeof WORK_MODES)[number];

/**
 * ⚠ Not `PositionCancelled` — the member is `PositionEliminated`, and the API answers a bare 400
 * for an unrecognised enum name rather than anything readable.
 */
export const VACANCY_CLOSURE_REASONS = [
  'PositionFilled',
  'BudgetConstraints',
  'PositionEliminated',
  'HiringFreeze',
  'NoSuitableCandidates',
  'ApplicationDeadlinePassed',
  'SufficientApplicationsReceived',
  'Other',
] as const;
export type VacancyClosureReason = (typeof VACANCY_CLOSURE_REASONS)[number];

/**
 * ⚠ Mirrors `StaffRequisitionCostCategory` on the server — ALL TEN members, in the server's
 * spelling. The previous list here had seven names, three of which (`Advertising`, `AgencyFees`,
 * `Travel`) were not members at all, so the form's default and three of its options could never
 * be saved (round 2b, R7).
 */
export const REQUISITION_COST_CATEGORIES = [
  'RecruitmentAgencyFee',
  'JobAdvertising',
  'BackgroundCheck',
  'Assessment',
  'Relocation',
  'OnboardingMaterials',
  'TrainingAndInduction',
  'MedicalExamination',
  'TravelAndInterview',
  'Other',
] as const;
export type RequisitionCostCategory = (typeof REQUISITION_COST_CATEGORIES)[number];

export type RequisitionCostStatus = 'Recorded' | 'Approved' | 'Rejected';

// ── staff requisition ──────────────────────────────────────────────────────

export interface StaffRequisition {
  id: string;
  requisitionNumber: string;
  requisitionTitle: string;
  description?: string | null;

  locationLevelId?: string | null;
  locationLevelName?: string | null;
  locationId?: string | null;
  locationName?: string | null;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;

  positionId: string;
  positionTitle: string;
  jobDescriptionId?: string | null;
  jobDescriptionTitle?: string | null;

  type: StaffRequisitionType;
  priority: StaffRequisitionPriority;
  status: StaffRequisitionStatus;

  numberOfPositions: number;
  positionsFilled: number;

  replacementForEmployeeId?: string | null;
  replacementForEmployeeName?: string | null;
  replacementReason?: string | null;
  employeeDepartureDate?: string | null;

  requestDate: string;
  desiredStartDate: string;
  latestAcceptableStartDate?: string | null;
  targetFillDate?: string | null;
  expectedOfferDate?: string | null;
  daysToFill?: number | null;
  targetStartDateReason?: string | null;

  businessJustification: string;
  impactIfNotFilled?: string | null;

  /** Derived from `manpowerBudgetLineId` since round 2b, R5; `budgetCode` is the budget's number. */
  isBudgeted: boolean;
  budgetCode?: string | null;
  manpowerBudgetLineId?: string | null;
  exceptionJustification?: string | null;
  /** The establishment as it stood at submit (D-2). */
  establishmentSnapshotOn?: string | null;
  establishmentSnapshotIsEstablished?: boolean | null;
  establishmentSnapshotExpected?: number | null;
  establishmentSnapshotFilled?: number | null;
  establishmentSnapshotSourceBudgetNumber?: string | null;

  allowInternalCandidates: boolean;
  allowExternalCandidates: boolean;

  requestedById: string;
  requestedByName: string;

  isFulfilled: boolean;
  fulfilledDate?: string | null;

  cancelledById?: string | null;
  cancelledByName?: string | null;
  cancelledDate?: string | null;
  cancellationReason?: string | null;

  workflowInstanceId?: string | null;
  jobVacancyId?: string | null;
  jobVacancyNumber?: string | null;

  notes?: string | null;
}

export interface StaffRequisitionSummary {
  id: string;
  requisitionNumber: string;
  requisitionTitle: string;
  positionTitle: string;
  organizationUnitName?: string | null;
  locationName?: string | null;
  type: StaffRequisitionType;
  priority: StaffRequisitionPriority;
  status: StaffRequisitionStatus;
  numberOfPositions: number;
  positionsFilled: number;
  requestDate: string;
  desiredStartDate: string;
  requestedByName: string;
}

export interface StaffRequisitionStatusSummary {
  totalRequisitions: number;
  draft: number;
  submitted: number;
  underReview: number;
  approved: number;
  rejected: number;
  onHold: number;
  cancelled: number;
  partiallyFulfilled: number;
  fulfilled: number;
}

export interface CreateStaffRequisition {
  positionId: string;
  jobDescriptionId?: string | null;
  locationLevelId?: string | null;
  locationId?: string | null;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  type: StaffRequisitionType;
  priority: StaffRequisitionPriority;
  requisitionTitle: string;
  description?: string | null;
  numberOfPositions: number;
  replacementForEmployeeId?: string | null;
  replacementReason?: string | null;
  employeeDepartureDate?: string | null;
  desiredStartDate: string;
  latestAcceptableStartDate?: string | null;
  targetFillDate?: string | null;
  expectedOfferDate?: string | null;
  targetStartDateReason?: string | null;
  businessJustification: string;
  impactIfNotFilled?: string | null;
  /** ⚠ Ignored by the server since R5 — both are derived from `manpowerBudgetLineId`. */
  isBudgeted?: boolean;
  budgetCode?: string | null;
  /** The approved budget line to draw down from, or null (R5). */
  manpowerBudgetLineId?: string | null;
  /** Required at submit when the check says `exceptionRequired` and enforcement is not Block (D-4). */
  exceptionJustification?: string | null;
  allowInternalCandidates: boolean;
  allowExternalCandidates: boolean;
  notes?: string | null;
}

export type UpdateStaffRequisition = CreateStaffRequisition & { id: string };

/** The establishment half of the check (R5): what used to be thrown at submit and never shown. */
export interface RequisitionEstablishmentCheck {
  mode: 'Off' | 'Warn' | 'Block';
  isEstablished: boolean;
  expectedHeadcount?: number | null;
  filled: number;
  gap?: number | null;
  sourceBudgetNumber?: string | null;
  wouldExceed: boolean;
  wouldBlock: boolean;
  message: string;
}

/** The form's live preview input (R5): the same check for a requisition not yet saved. */
export interface RequisitionBudgetCheckPreview {
  positionId: string;
  numberOfPositions: number;
  desiredStartDate?: string | null;
  manpowerBudgetLineId?: string | null;
  excludeRequisitionId?: string | null;
}

/** An approved budget line a requisition may draw down from — the picker's rows (R5). */
export interface BudgetLineForRequisition {
  lineId: string;
  budgetId: string;
  budgetNumber: string;
  fiscalYear: number;
  budgetStatus: string;
  organizationUnitName?: string | null;
  plannedCount: number;
  plannedNewPositions: number;
  requisitionedCount: number;
  remaining: number;
  plannedAverageSalary: number;
}

/**
 * The result of checking a requisition against its position's approved manpower budget.
 *
 * `wouldBlock` is the one that matters: in Block mode an over-budget requisition cannot be
 * submitted or approved at all, and the API refuses with `message` as the explanation.
 */
export interface RequisitionBudgetCheck {
  mode: 'Off' | 'Warn' | 'Block';
  fiscalYear: number;
  hasBudgetLine: boolean;
  plannedCount?: number | null;
  currentFilled?: number | null;
  requestedPositions: number;
  projectedHeadcount: number;
  isOverBudget: boolean;
  wouldBlock: boolean;
  message: string;
  // ── round 2b, R5 ──
  isLinked: boolean;
  linkedLineId?: string | null;
  linkedBudgetId?: string | null;
  linkedBudgetNumber?: string | null;
  linkedBudgetStatus?: string | null;
  budgetedNewPosts?: number | null;
  /** Posts on OTHER live requisitions drawing down from the same line (D-8). */
  drawdown: number;
  remaining?: number | null;
  establishment?: RequisitionEstablishmentCheck | null;
  exceptionRequired: boolean;
  exceptionReason?: string | null;
}

export interface RequisitionCost {
  id: string;
  requisitionId: string;
  requisitionNumber: string;
  category: RequisitionCostCategory;
  purpose: string;
  amount: number;
  currency: string;
  /** Finance's rate for the cost date, read by the server (R7) — never typed. */
  exchangeRate: number;
  description?: string | null;
  /** Still a typed record until the Finance AP hand-off (R8). */
  paymentVoucherNumber?: string | null;
  recordedById: string;
  recordedByName: string;
  recordedDate: string;
  // ── round 2b, R7 ──
  costDate: string;
  amountBaseCurrency: number;
  supplierId?: string | null;
  supplierName?: string | null;
  payeeName?: string | null;
  status: RequisitionCostStatus;
  statusName?: string;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvedOn?: string | null;
  approvalNote?: string | null;
}

export interface RequisitionCostForm {
  category: RequisitionCostCategory;
  purpose: string;
  amount: number;
  currency: string;
  /** `YYYY-MM-DD`; the day the money moved. The rate is Finance's for this date. */
  costDate?: string | null;
  supplierId?: string | null;
  /** Required when there is no supplier. */
  payeeName?: string | null;
  description?: string | null;
  paymentVoucherNumber?: string | null;
}

export interface RequisitionComment {
  id: string;
  requisitionId: string;
  parentCommentId?: string | null;
  body: string;
  authorId: string;
  authorName: string;
  postedDate: string;
  replies: RequisitionComment[];
}

export interface RequisitionHistoryEntry {
  id: string;
  requisitionId: string;
  requisitionNumber: string;
  fromStatus: StaffRequisitionStatus;
  toStatus: StaffRequisitionStatus;
  changedById: string;
  changedByName: string;
  comments?: string | null;
  actionDate: string;
}

export interface RequisitionAttachment {
  id: string;
  requisitionId: string;
  requisitionNumber: string;
  fileName: string;
  filePath: string;
  description?: string | null;
  uploadDate: string;
  uploadedById: string;
  uploadedByName: string;
}

// ── job vacancy ────────────────────────────────────────────────────────────

export interface JobVacancy {
  id: string;
  vacancyNumber: string;
  jobTitle: string;
  customAdvertTitle?: string | null;
  staffRequisitionId: string;
  requisitionNumber: string;
  orgUnitName?: string | null;
  positionId?: string | null;
  positionTitle: string;
  vacancyStatus: JobVacancyStatus;
  numberOfPositions: number;
  hiringManagerId?: string | null;
  hiringManagerName?: string | null;
  recruiterId?: string | null;
  recruiterName?: string | null;
  publishDate?: string | null;
  actualPublishDate?: string | null;
  applicationDeadline?: string | null;
  shortlistingDeadline?: string | null;
  numberOfInterviewRounds?: number | null;
  targetStartDate?: string | null;
  employmentType: EmploymentType;
  workMode: WorkMode;
  isSalaryVisible: boolean;
  salaryRangeMin?: number | null;
  salaryRangeMax?: number | null;
  salaryCurrencyCode?: string | null;
  requiredMinExperienceYears?: number | null;
  keyBenefitsSummary?: string | null;
  requiresWrittenTest: boolean;
  requiresPracticalTest: boolean;
  recruitmentPipelineId?: string | null;
  pipelineName?: string | null;
  allowInternalCandidates: boolean;
  allowExternalCandidates: boolean;
  applicationCount: number;
  shortlistedCount: number;
  interviewCount: number;
  offerCount: number;
  hireCount: number;
  closureReason?: string | null;
  closureNotes?: string | null;
  closedDate?: string | null;
  shortlistApprovalStatus?: string | null;
  shortlistSubmittedByName?: string | null;
  shortlistApprovedByName?: string | null;
}

export interface JobVacancySummary {
  id: string;
  vacancyNumber: string;
  jobTitle: string;
  positionTitle: string;
  staffRequisitionId: string;
  requisitionNumber?: string | null;
  orgUnitName?: string | null;
  vacancyStatus: JobVacancyStatus;
  employmentType: EmploymentType;
  publishDate?: string | null;
  applicationDeadline?: string | null;
  shortlistingDeadline?: string | null;
  numberOfPositions: number;
  hiringManagerName?: string | null;
  recruiterName?: string | null;
  allowInternalCandidates: boolean;
  allowExternalCandidates: boolean;
  applicationCount: number;
  shortlistedCount: number;
  offerCount: number;
  createdAt: string;
}

export interface CreateJobVacancy {
  staffRequisitionId: string;
  customAdvertTitle?: string | null;
  numberOfPositions: number;
  hiringManagerId?: string | null;
  recruiterId?: string | null;
  applicationDeadline?: string | null;
  shortlistingDeadline?: string | null;
  numberOfInterviewRounds?: number | null;
  targetStartDate?: string | null;
  isSalaryVisible: boolean;
  employmentType: EmploymentType;
  workMode: WorkMode;
  salaryRangeMin?: number | null;
  salaryRangeMax?: number | null;
  salaryCurrencyCode?: string | null;
  requiredMinExperienceYears?: number | null;
  keyBenefitsSummary?: string | null;
  requiresWrittenTest: boolean;
  requiresPracticalTest: boolean;
  recruitmentPipelineId?: string | null;

  /**
   * ⚠ These three were missing from this type while the backend DTO carried them, and the mapper
   * assigns all of them unconditionally. Two consequences: blind screening could never be switched
   * on (so `GET .../blind-applications` answered 422 forever), and any future edit form that omitted
   * them would silently clear them — the same shape as the cycle-status revert.
   */
  isBlindScreeningEnabled: boolean;
  autoShortlistMinScore?: number | null;
  autoShortlistRequireAllMandatory: boolean;
}

export type UpdateJobVacancy = Omit<CreateJobVacancy, 'staffRequisitionId'> & { id: string };

export interface JobVacancyStatusHistoryEntry {
  id: string;
  jobVacancyId: string;
  fromStatus: JobVacancyStatus;
  toStatus: JobVacancyStatus;
  changedDate: string;
  changedById: string;
  changedByName: string;
  reason?: string | null;
  comments?: string | null;
}

// ── shortlisting criteria ───────────────────────────────────────────────────
// ⚠ Every shape below was read off a live response (dev-harness/hr-recruitment/
// probe-lane5-criteria.mjs), not inferred from the endpoint name. The interface that stood here
// until 2026-09-01 declared `minimumScore` and `displayOrder`, which exist on neither the entity
// nor the DTO — the form sent them and the server discarded them silently.

/** HREnums.cs `JobShortlistingCriteriaType` — 1..10. There is no member at 0. */
export const SHORTLISTING_CRITERIA_TYPES = [
  'Qualification',
  'YearsOfExperience',
  'Skill',
  'Certification',
  'Language',
  'Gender',
  'Age',
  'Location',
  'EducationLevel',
  'Other',
] as const;
export type ShortlistingCriteriaType = (typeof SHORTLISTING_CRITERIA_TYPES)[number];

/** HREnums.cs `MandatoryMatchMode` — how a multi-valued required list is matched. */
export const MANDATORY_MATCH_MODES = ['AnyMatched', 'AllRequired'] as const;
export type MandatoryMatchMode = (typeof MANDATORY_MATCH_MODES)[number];

/** HREnums.cs `ValueMatchStrategy` — how one required value is compared to a candidate string. */
export const VALUE_MATCH_STRATEGIES = ['Exact', 'Contains', 'Fuzzy'] as const;
export type ValueMatchStrategy = (typeof VALUE_MATCH_STRATEGIES)[number];

/** HREnums.cs `ShortlistingComparisonOperator` — 1..9. */
export const SHORTLISTING_COMPARISON_OPERATORS = [
  'Equals',
  'NotEquals',
  'Contains',
  'GreaterThan',
  'GreaterThanOrEqual',
  'LessThan',
  'LessThanOrEqual',
  'Between',
  'In',
] as const;
export type ShortlistingComparisonOperator = (typeof SHORTLISTING_COMPARISON_OPERATORS)[number];

export interface ShortlistingCriteria {
  id: string;
  jobVacancyId: string;
  criteriaName: string;
  description?: string | null;
  /**
   * What the criterion measures — this is what selects the arm of the scoring engine.
   *
   * ⚠ `0` is not a member. Rows created before 2026-09-01 carry it, because the old form never
   * sent a type and a non-nullable enum binds to `0`; those rows reach the engine's `default:`
   * arm and **pass every candidate**. `typeName` comes back as the string `"0"` for them. The
   * panel flags them; the service now refuses to create any more.
   */
  type: ShortlistingCriteriaType | 0;
  typeName: string;
  requiredValue?: string | null;
  minValue?: number | null;
  maxValue?: number | null;
  isMandatory: boolean;
  matchMode: MandatoryMatchMode;
  matchStrategy: ValueMatchStrategy;
  requiredSkillId?: string | null;
  requiredQualificationId?: string | null;
  weight: number;
  comparisonOperator?: ShortlistingComparisonOperator | null;
  comparisonOperatorName?: string | null;
}

export interface VacancyAttachment {
  id: string;
  jobVacancyId: string;
  fileName: string;
  filePath: string;
  description?: string | null;
  uploadDate: string;
  uploadedById: string;
  uploadedByName: string;
}

export interface JobPostingAttachment {
  id: string;
  jobPostingId: string;
  fileName: string;
  filePath: string;
  description?: string | null;
  uploadDate: string;
  uploadedById: string;
  uploadedByName: string;
}

/**
 * The create/update payload.
 *
 * ⚠ `weight` is a **non-nullable `int`** on both DTOs. Sending `null` is not "leave it out" — the
 * request is rejected 400 by the JSON reader before any handler sees it, which is what the old
 * form did on every add that left the weight blank. Keep it a number.
 */
export interface ShortlistingCriteriaForm {
  criteriaName: string;
  description?: string | null;
  type: ShortlistingCriteriaType;
  requiredValue?: string | null;
  minValue?: number | null;
  maxValue?: number | null;
  isMandatory: boolean;
  matchMode: MandatoryMatchMode;
  matchStrategy: ValueMatchStrategy;
  requiredSkillId?: string | null;
  requiredQualificationId?: string | null;
  weight: number;
  comparisonOperator?: ShortlistingComparisonOperator | null;
}

// ── pipeline stage assignments (stage owners) ──────────────────────────────
// Who owns each pipeline stage of THIS vacancy — not the application board, which moves
// applications between stages. VacancyStageAssignmentStatus — HREnums.cs (NotStarted=1 … Skipped=6).

export const STAGE_ASSIGNMENT_STATUSES = [
  'NotStarted',
  'InProgress',
  'Completed',
  'Overdue',
  'Escalated',
  'Skipped',
] as const;
export type StageAssignmentStatus = (typeof STAGE_ASSIGNMENT_STATUSES)[number];

export interface VacancyStageAssignment {
  id: string;
  jobVacancyId: string;
  pipelineStageId: string;
  stageName: string;
  stageOrder: number;
  stageType: string;
  stageTypeName: string;
  assignedToId: string;
  assignedToName: string;
  assignedById: string;
  assignedByName: string;
  assignedAt: string;
  dueDate?: string | null;
  status: StageAssignmentStatus;
  statusName: string;
  completedAt?: string | null;
  completedById?: string | null;
  completedByName?: string | null;
  completionNotes?: string | null;
  escalationEnabled: boolean;
  escalationDaysAfterDue?: number | null;
  escalateToId?: string | null;
  escalateToName?: string | null;
  escalatedAt?: string | null;
  escalationNotes?: string | null;
}

export interface VacancyStageAssignmentForm {
  assignedToId: string;
  dueDate?: string | null;
  escalationEnabled: boolean;
  escalationDaysAfterDue?: number | null;
  escalateToId?: string | null;
}

// ── job posting ────────────────────────────────────────────────────────────

export interface JobPosting {
  id: string;
  jobVacancyId: string;
  vacancyNumber: string;
  channel: JobPostingChannel;
  title: string;
  description: string;
  postingUrl?: string | null;
  externalPostingId?: string | null;
  publishDate?: string | null;
  actualPublishDate?: string | null;
  expiryDate?: string | null;
  status: JobPostingStatus;
  isActive: boolean;
  applicationCount: number;
  postedById?: string | null;
  postedByName?: string | null;
  advertHeadline?: string | null;
  advertBody?: string | null;
  howToApply?: string | null;
  closingDateText?: string | null;
  showSalaryInAdvert: boolean;
  contactDetails?: string | null;
}

export interface JobPostingSummary {
  id: string;
  jobVacancyId: string;
  vacancyNumber?: string | null;
  channel: JobPostingChannel;
  title: string;
  status: JobPostingStatus;
  isActive: boolean;
  publishDate?: string | null;
  actualPublishDate?: string | null;
  expiryDate?: string | null;
  applicationCount: number;
  postingUrl?: string | null;
}

export interface JobPostingForm {
  channel: JobPostingChannel;
  title: string;
  description: string;
  postingUrl?: string | null;
  externalPostingId?: string | null;
  publishDate?: string | null;
  expiryDate?: string | null;
  advertHeadline?: string | null;
  advertBody?: string | null;
  howToApply?: string | null;
  closingDateText?: string | null;
  showSalaryInAdvert?: boolean;
  contactDetails?: string | null;
}

// ── position vacancy (establishment) ───────────────────────────────────────

export const POSITION_VACANCY_STATUSES = [
  'Anticipated',
  'Open',
  'UnderReview',
  'RequisitionRaised',
  'Filled',
  'Closed',
] as const;
export type PositionVacancyStatus = (typeof POSITION_VACANCY_STATUSES)[number];

export interface PositionEstablishment {
  positionId: string;
  positionTitle: string;
  positionCode?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  /** ⚠ Meaningless unless `isEstablished` — the column default is 1 (round 2b, R4a). */
  expectedHeadcount: number;
  filledCount: number;
  /** 0 for an unestablished post: no gap can be stated. Read `gapKnown` first. */
  vacantCount: number;
  isEstablished: boolean;
  gapKnown: boolean;
  isOverEstablishment: boolean;
  establishmentApprovedOn?: string | null;
  establishmentSourceBudgetId?: string | null;
  establishmentSourceBudgetNumber?: string | null;
  openVacancyId?: string | null;
}

/**
 * The vacancy in full.
 *
 * ⚠ The LIST returns `PositionVacancySummaryDto`, which has no `notes` — so an annotation
 * dialog seeded from a row would open empty and save a blank over whatever was there. The notes
 * come from this by-id read. The same summary-shaped-read trap this codebase has met four times.
 */
export interface PositionVacancyDetail extends PositionVacancySummary {
  positionCode?: string | null;
  organizationUnitId?: string | null;
  vacatedByEmployeeId?: string | null;
  notes?: string | null;
}

export interface PositionVacancySummary {
  id: string;
  positionId: string;
  positionTitle: string;
  organizationUnitName?: string | null;
  reason: string;
  status: PositionVacancyStatus;
  classification: string;
  vacatedByEmployeeName?: string | null;
  vacatedDate?: string | null;
  isAnticipated: boolean;
  staffRequisitionId?: string | null;
}

export interface PositionVacancyStats {
  totalOpen: number;
  anticipated: number;
  underReview: number;
  requisitionRaised: number;
  withinEstablishment: number;
  noShortfallOrOver: number;
  totalPositions: number;
  positionsWithVacancy: number;
}

export interface RaiseRequisitionResult {
  requisitionId: string;
  requisitionNumber: string;
  vacancyId: string;
}

// ── paged envelope ─────────────────────────────────────────────────────────
// ⚠ HR's PagedResult differs from finance/AR's — `page`/`hasPrevious`, not `pageNumber`.
export interface HrPagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

/**
 * A published vacancy as an APPLICANT sees it — the internal job board and the public career
 * portal share this projection.
 *
 * Transcribed from a live `GET api/job-vacancies/published` (`probe-slice13b.mjs`), 23 keys.
 *
 * ⚠ This is deliberately not {@link JobVacancy}. That type is the recruitment record: it carries
 * the auto-shortlist threshold, the test-score weight, the internal-candidate boost points, the
 * blind-screening flag, the shortlist approval notes and approver, the workflow instance id, the
 * pipeline counts and the hiring manager and recruiter by name — the terms an applicant is about
 * to be judged on. Until area 25 slice 13b the internal board served all of it, and served
 * `salaryRangeMin`/`Max` even when `isSalaryVisible` was false, which the public projection has
 * always withheld. Do not "upgrade" this back to `JobVacancy` to reach a field: if a board needs
 * something more, add it to the server-side projection deliberately.
 *
 * ⚠ `salaryRangeMin`/`salaryRangeMax`/`salaryCurrencyCode` are **null unless `isSalaryVisible`**.
 * The server nulls them; the screen must not assume a range exists.
 */
export interface PublicVacancy {
  id: string;
  vacancyNumber: string;
  jobTitle: string;
  positionTitle: string;
  /** The requisition's organisation unit. Named "department" by the public contract. */
  departmentName: string;
  locationName: string;
  employmentType: EmploymentType;
  employmentTypeName: string;
  workMode: WorkMode;
  workModeName: string;
  numberOfPositions: number;
  requiredMinExperienceYears?: number | null;
  keyBenefitsSummary?: string | null;
  targetStartDate?: string | null;
  isSalaryVisible: boolean;
  salaryRangeMin?: number | null;
  salaryRangeMax?: number | null;
  salaryCurrencyCode?: string | null;
  publishDate?: string | null;
  applicationDeadline?: string | null;
  requiresWrittenTest: boolean;
  requiresPracticalTest: boolean;
  /** The advert body, from the requisition's job description. The board's whole point. */
  jobDescription?: string | null;
}
