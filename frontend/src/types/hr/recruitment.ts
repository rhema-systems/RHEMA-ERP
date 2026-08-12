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

export const REQUISITION_COST_CATEGORIES = [
  'Advertising',
  'AgencyFees',
  'Assessment',
  'Travel',
  'Relocation',
  'BackgroundCheck',
  'Other',
] as const;
export type RequisitionCostCategory = (typeof REQUISITION_COST_CATEGORIES)[number];

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

  isBudgeted: boolean;
  budgetCode?: string | null;

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
  isBudgeted: boolean;
  budgetCode?: string | null;
  allowInternalCandidates: boolean;
  allowExternalCandidates: boolean;
  notes?: string | null;
}

export type UpdateStaffRequisition = CreateStaffRequisition & { id: string };

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
}

export interface RequisitionCost {
  id: string;
  requisitionId: string;
  requisitionNumber: string;
  category: RequisitionCostCategory;
  purpose: string;
  amount: number;
  currency: string;
  exchangeRate: number;
  description?: string | null;
  paymentVoucherNumber?: string | null;
  recordedById: string;
  recordedByName: string;
  recordedDate: string;
}

export interface RequisitionCostForm {
  category: RequisitionCostCategory;
  purpose: string;
  amount: number;
  currency: string;
  exchangeRate: number;
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

export interface ShortlistingCriteria {
  id: string;
  jobVacancyId: string;
  criteriaName: string;
  description?: string | null;
  isMandatory: boolean;
  weight?: number | null;
  minimumScore?: number | null;
  displayOrder?: number | null;
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

export interface ShortlistingCriteriaForm {
  criteriaName: string;
  description?: string | null;
  isMandatory: boolean;
  weight?: number | null;
  minimumScore?: number | null;
  displayOrder?: number | null;
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
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  expectedHeadcount: number;
  filledCount: number;
  vacantCount: number;
  openVacancyId?: string | null;
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
