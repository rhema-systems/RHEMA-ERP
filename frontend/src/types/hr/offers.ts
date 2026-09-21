/**
 * Recruitment — slice D: offers, hires and pre-employment checks.
 *
 * Four controllers: `api/job-offers` (28 endpoints), `api/job-hires` (9),
 * `api/pre-employment-checks` (19) and `api/pre-employment-check-templates` (8, setup).
 *
 * **All four are flat HR-only, reads included.** Between them they carry every new hire's salary,
 * bonus and benefits, plus criminal-record results, medical outcomes and referees' candid opinions
 * about a named person — the most sensitive data the module holds. There is no per-record exception
 * like the interview panelist rule: an employee who is not HR has no business on any of these
 * screens, so none of them are reachable from a non-HR route.
 *
 * ⚠ The candidate's own view of their offer (`api/offer-response`, the tokenised accept/decline
 * flow) is deliberately NOT here. It belongs to the candidate portal, which reuses the external
 * portal's auth rather than the ERP session.
 */

// ── enums (string unions matching the backend's JSON) ──────────────────────

/**
 * `JobOfferStatus`. The lifecycle runs Draft → PendingApproval → Approved → Sent, and then splits on
 * what the candidate says: Accepted, Negotiating (which a revision answers), Declined, or
 * ConditionallyAccepted → ChecksCleared when the offer is conditional.
 *
 * ⚠ `Rejected` means *an approver* turned the offer down and it is resubmittable — it is not the
 * candidate declining, which is `Declined`. Two different people saying no at two different points.
 *
 * ⚠ `Superseded` is what a version becomes when a revision replaces it (G-10.2, added 2026-09-15).
 * Distinct from `Withdrawn`, which is the organisation taking a live offer back — nobody revoked a
 * superseded version, better terms simply replaced it. `Expired` is now written too, by the nightly
 * recruitment sweep (G-2.4); before that job existed no offer ever reached it, so the analytics
 * screen's Expired bucket was structurally zero and the lapsed offers sat in Pending instead.
 */
export const JOB_OFFER_STATUSES = [
  'Draft',
  'PendingApproval',
  'Approved',
  'Sent',
  'Negotiating',
  'Accepted',
  'Declined',
  'Withdrawn',
  'Expired',
  'OnHold',
  'ConditionallyAccepted',
  'ChecksCleared',
  'Rejected',
  'Superseded',
] as const;
export type JobOfferStatus = (typeof JOB_OFFER_STATUSES)[number];

/**
 * The three responses `record-response` accepts. The server refuses anything else with a 422 that
 * names the alternatives — the endpoint takes a full `JobOfferStatus` and used to assign it
 * unchecked, so "record what the candidate said" could set an offer to Approved.
 */
export const OFFER_RESPONSES = ['Accepted', 'Negotiating', 'Declined'] as const;
export type OfferResponse = (typeof OFFER_RESPONSES)[number];

/** Statuses a revoke is refused on — there is nothing left to withdraw. */
export const UNREVOKABLE_OFFER_STATUSES: readonly JobOfferStatus[] = ['Withdrawn', 'Declined', 'Expired'];

export const EMPLOYMENT_TYPES = [
  'Permanent',
  'Contract',
  'FixedTerm',
  'Internship',
  'Casual',
  'PartTime',
  'Temporary',
  'Consultant',
  'Freelance',
] as const;
export type EmploymentType = (typeof EMPLOYMENT_TYPES)[number];

export const WORK_MODES = ['OnSite', 'Remote', 'Hybrid'] as const;
export type WorkMode = (typeof WORK_MODES)[number];

/**
 * `JobHireStatus`.
 *
 * ⚠ **`Active` is not settable.** It is what confirm-start writes *after* it has created the
 * employee record, and the status endpoint refuses it explicitly: setting it directly used to be a
 * one-way door that permanently prevented the employee from ever being created. The picker on the
 * status dialog is built from {@link HIRE_TRANSITIONS}, which excludes it.
 */
export const JOB_HIRE_STATUSES = [
  'PendingOnboarding',
  'OnboardingInProgress',
  'OnboardingCompleted',
  'Active',
  'Cancelled',
] as const;
export type JobHireStatus = (typeof JOB_HIRE_STATUSES)[number];

/**
 * The server's hire state machine, mirrored so the dialog offers only legal moves. The server owns
 * the real rules and refuses an illegal move with a 422 naming the current state — this is here to
 * keep the picker honest, not to replace that check.
 */
export const HIRE_TRANSITIONS: Record<JobHireStatus, readonly JobHireStatus[]> = {
  PendingOnboarding: ['OnboardingInProgress', 'Cancelled'],
  OnboardingInProgress: ['OnboardingCompleted', 'Cancelled'],
  OnboardingCompleted: ['Cancelled'],
  Active: [],
  Cancelled: [],
};

export const PRE_EMPLOYMENT_CHECK_STATUSES = [
  'Pending',
  'InProgress',
  'Completed',
  'CompletedWithCaution',
  'Failed',
  'Waived',
] as const;
export type PreEmploymentCheckStatus = (typeof PRE_EMPLOYMENT_CHECK_STATUSES)[number];

export const PRE_EMPLOYMENT_CHECK_TYPES = [
  'MedicalExamination',
  'PoliceClearance',
  'BackgroundCheck',
  'AcademicVerification',
  'ProfessionalLicenceVerification',
  'ReferenceCheck',
  'CreditCheck',
  'DrugTest',
  'Other',
] as const;
export type PreEmploymentCheckType = (typeof PRE_EMPLOYMENT_CHECK_TYPES)[number];

/**
 * `CheckItemStatus`.
 *
 * ⚠ The distinction that matters on this screen: **Pending and Requested are "not done yet", which
 * blocks completion; Waived and NotApplicable are "settled", which does not.** Waiving a check is a
 * decision to accept it, not a failure — the completion rule used to count both as failures, which
 * made the waiver facility guarantee the outcome it exists to avoid.
 */
export const CHECK_ITEM_STATUSES = [
  'Pending',
  'Requested',
  'Received',
  'Verified',
  'Failed',
  'Waived',
  'NotApplicable',
] as const;
export type CheckItemStatus = (typeof CHECK_ITEM_STATUSES)[number];

/** Item states that hold up `complete` when the item is mandatory or blocking. */
export const OUTSTANDING_CHECK_ITEM_STATUSES: readonly CheckItemStatus[] = ['Pending', 'Requested'];

/** Item states that count as settled without having passed. */
export const SETTLED_WITHOUT_PASS_STATUSES: readonly CheckItemStatus[] = ['Waived', 'NotApplicable'];

export const REFERENCE_RESPONSE_METHODS = ['Email', 'Phone', 'InPerson', 'Form', 'Letter'] as const;
export type ReferenceResponseMethod = (typeof REFERENCE_RESPONSE_METHODS)[number];

export const REFERENCE_RATINGS = ['Excellent', 'Good', 'Satisfactory', 'Poor', 'Unsatisfactory'] as const;
export type ReferenceRating = (typeof REFERENCE_RATINGS)[number];

// ── job offer ──────────────────────────────────────────────────────────────

export interface JobOfferBenefit {
  id: string;
  jobOfferId: string;
  benefitName: string;
  description?: string | null;
  monetaryValue?: number | null;
  currencyCode?: string | null;
  isMonetary: boolean;
  displayOrder: number;
}

export interface JobOfferNote {
  id: string;
  jobOfferId: string;
  body: string;
  authorName?: string | null;
  createdAt: string;
}

export interface JobOffer {
  id: string;
  tenantId: string;
  offerNumber: string;

  jobApplicationId: string;
  applicationNumber: string;
  candidateId: string;
  candidateName: string;

  offerStatus: JobOfferStatus;
  offerStatusName: string;

  // The role snapshot. Server-authoritative — taken from the vacancy's position at create, and not
  // editable afterwards. Sending any of these on create or update is refused, not ignored.
  positionId: string;
  positionTitle: string;
  reportsToTitle: string;
  gradeTitle: string;
  departmentName: string;
  workMode: WorkMode;
  workModeName: string;
  employmentType: EmploymentType;
  employmentTypeName: string;

  salaryGradeMin?: number | null;
  salaryGradeMax?: number | null;
  salaryLevelId?: string | null;
  salaryLevelName?: string | null;
  salaryNotchId?: string | null;
  salaryNotchNumber?: number | null;
  salaryNotchAmount?: number | null;

  probationPeriodMonths?: number | null;
  noticePeriodMonths?: number | null;
  annualLeaveDays?: number | null;
  weeklyHours?: number | null;
  ndaRequired: boolean;

  locationLevelId?: string | null;
  locationLevelName?: string | null;
  locationId?: string | null;
  locationName?: string | null;

  contractDurationMonths?: number | null;
  baseSalary?: number | null;
  currencyCode?: string | null;
  bonus?: number | null;
  bonusTerms?: string | null;
  commission?: number | null;
  commissionStructure?: string | null;
  benefits: JobOfferBenefit[];

  proposedStartDate?: string | null;
  additionalTerms?: string | null;

  preparedById?: string | null;
  preparedByName?: string | null;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvedDate?: string | null;

  offerDate?: string | null;
  expiryDate?: string | null;

  offerLetterPath?: string | null;
  signedOfferLetterPath?: string | null;

  acceptedDate?: string | null;
  counteredDate?: string | null;
  candidateResponseNotes?: string | null;
  declinedDate?: string | null;
  declineReason?: string | null;
  revokedDate?: string | null;
  revocationReason?: string | null;
  approvalRejectionReason?: string | null;

  version: number;
  previousOfferId?: string | null;

  isConditional: boolean;
  preEmploymentCheckId?: string | null;
  preEmploymentCheckStatus?: PreEmploymentCheckStatus | null;
  preEmploymentCheckStatusName?: string | null;

  createdAt: string;
  updatedAt?: string | null;
}

export interface JobOfferSummary {
  id: string;
  offerNumber: string;
  jobApplicationId: string;
  applicationNumber: string;
  candidateName: string;
  offerStatus: JobOfferStatus;
  offerStatusName: string;
  positionTitle: string;
  employmentType: EmploymentType;
  employmentTypeName: string;
  baseSalary?: number | null;
  currencyCode?: string | null;
  proposedStartDate?: string | null;
  offerDate?: string | null;
  expiryDate?: string | null;
  version: number;
  createdAt: string;
}

/**
 * ⚠ **Negotiated terms only.** `positionId`, `positionTitle`, `reportsToTitle`, `gradeTitle` and
 * `employmentType` are gone from this payload — they were all `[Required]` and then always
 * overwritten from the application's vacancy and position, so a caller was forced to invent values
 * guaranteed to be discarded. The DTO now sets `[JsonUnmappedMemberHandling(Disallow)]`, so sending
 * one is **refused with a 400**, not quietly dropped.
 */
export interface CreateJobOffer {
  jobApplicationId: string;
  locationLevelId?: string | null;
  locationId?: string | null;
  contractDurationMonths?: number | null;
  probationPeriodMonths?: number | null;
  noticePeriodMonths?: number | null;
  annualLeaveDays?: number | null;
  weeklyHours?: number | null;
  ndaRequired: boolean;
  isConditional: boolean;
  expiryDate?: string | null;
  baseSalary?: number | null;
  salaryLevelId?: string | null;
  salaryNotchId?: string | null;
  currencyCode?: string | null;
  bonus?: number | null;
  bonusTerms?: string | null;
  commission?: number | null;
  commissionStructure?: string | null;
  proposedStartDate?: string | null;
  additionalTerms?: string | null;
}

/**
 * ⚠ Same shape as create, and the same refusal on unmapped members. On the update path the removed
 * fields were worse than on create: update wrote them **straight onto the entity**, so an edit could
 * rewrite an offer's position title, reporting line, grade and employment type until the record no
 * longer described the role it was raised against.
 */
export type UpdateJobOffer = Omit<CreateJobOffer, 'jobApplicationId'>;

/** ⚠ `offerLetterPath` is gone — the letter is uploaded through the gate, not named by the caller. */
export interface IssueJobOffer {
  offerDate: string;
  expiryDate?: string | null;
}

export interface RecordOfferResponse {
  response: OfferResponse;
  candidateResponseNotes?: string | null;
}

export interface ReviseJobOffer {
  newBaseSalary?: number | null;
  newProposedStartDate?: string | null;
  newAdditionalTerms?: string | null;
  revisionReason?: string | null;
}

export interface CreateJobOfferBenefit {
  benefitName: string;
  description?: string | null;
  monetaryValue?: number | null;
  currencyCode?: string | null;
  isMonetary: boolean;
  displayOrder: number;
}

export type UpdateJobOfferBenefit = Partial<CreateJobOfferBenefit>;

/** A rendered letter for preview and print-to-PDF — not the stored file. See the service note. */
export interface OfferLetter {
  offerId: string;
  offerNumber: string;
  candidateName: string;
  positionTitle: string;
  subject: string;
  htmlBody: string;
}

// ── hire record ────────────────────────────────────────────────────────────

export interface JobHireRecord {
  id: string;
  tenantId: string;
  hireNumber: string;
  applicationId: string;
  applicationNumber: string;
  candidateName: string;
  offerId: string;
  offerNumber: string;
  status: JobHireStatus;
  statusName: string;
  expectedStartDate: string;
  actualStartDate?: string | null;
  employeeId?: string | null;
  employeeNumber?: string | null;
  employeeName?: string | null;
  confirmedById?: string | null;
  confirmedByName?: string | null;
  confirmedDate?: string | null;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface JobHireRecordSummary {
  id: string;
  hireNumber: string;
  candidateName: string;
  positionTitle: string;
  status: JobHireStatus;
  statusName: string;
  expectedStartDate: string;
  actualStartDate?: string | null;
  employeeNumber?: string | null;
}

export interface CreateJobHireRecord {
  applicationId: string;
  offerId: string;
  expectedStartDate: string;
  notes?: string | null;
}

export interface UpdateJobHireRecordStatus {
  newStatus: JobHireStatus;
  employeeId?: string | null;
  actualStartDate?: string | null;
  notes?: string | null;
}

export interface ConfirmHireStart {
  actualStartDate: string;
  /** Set for an internal hire or a manual link; omitted, the server creates a new Employee. */
  linkedEmployeeId?: string | null;
}

// ── pre-employment check ───────────────────────────────────────────────────

export interface PreEmploymentCheckItem {
  id: string;
  tenantId: string;
  preEmploymentCheckId: string;
  checkType: PreEmploymentCheckType;
  checkTypeName: string;
  name?: string | null;
  /** `name` when set, otherwise the check type — the server computes it. */
  displayName: string;
  serviceProviderName?: string | null;
  /** The supplier behind the provider name, when it is one (round 3, lane G; D-14). */
  serviceProviderSupplierId?: string | null;
  status: CheckItemStatus;
  statusName: string;
  requestedDate?: string | null;
  receivedDate?: string | null;
  expiryDate?: string | null;
  passed?: boolean | null;
  instructions?: string | null;
  remarks?: string | null;
  /** ⚠ There is no `documentPath`: the storage path is deliberately not exposed to the client. */
  hasDocument: boolean;
  documentFileName?: string | null;
  expectedDays?: number | null;
  isMandatory: boolean;
  isBlockingOnFail: boolean;
  reviewedById?: string | null;
  reviewedByName?: string | null;
  reviewedDate?: string | null;
  hasReferenceResponse: boolean;
  createdAt: string;
}

export interface PreEmploymentCheck {
  id: string;
  tenantId: string;
  jobOfferId: string;
  offerNumber: string;
  candidateName: string;
  offerStatus: JobOfferStatus;
  overallStatus: PreEmploymentCheckStatus;
  overallStatusName: string;
  coordinatedById?: string | null;
  coordinatedByName?: string | null;
  completedDate?: string | null;
  notes?: string | null;
  totalItems: number;
  completedItems: number;
  passedItems: number;
  failedItems: number;
  createdAt: string;
}

export interface PreEmploymentCheckDetail extends PreEmploymentCheck {
  items: PreEmploymentCheckItem[];
}

export interface CreatePreEmploymentCheckItem {
  checkType: PreEmploymentCheckType;
  name?: string | null;
  serviceProviderName?: string | null;
  /** A Procurement supplier (round 3, lane G); the server mirrors its name into `serviceProviderName`. */
  serviceProviderSupplierId?: string | null;
  instructions?: string | null;
  isMandatory: boolean;
  isBlockingOnFail: boolean;
  expectedDays?: number | null;
}

export interface CreatePreEmploymentCheck {
  jobOfferId: string;
  coordinatedById?: string | null;
  notes?: string | null;
  items: CreatePreEmploymentCheckItem[];
}

/** ⚠ `documentPath` is gone — evidence goes through the controlled-upload gate. Unmapped refused. */
export interface UpdatePreEmploymentCheckItem {
  name?: string | null;
  serviceProviderName?: string | null;
  serviceProviderSupplierId?: string | null;
  status: CheckItemStatus;
  requestedDate?: string | null;
  receivedDate?: string | null;
  expiryDate?: string | null;
  passed?: boolean | null;
  instructions?: string | null;
  remarks?: string | null;
  expectedDays?: number | null;
  reviewedById?: string | null;
  reviewedDate?: string | null;
}

// ── reference check responses ──────────────────────────────────────────────

export interface ReferenceCheckResponse {
  id: string;
  tenantId: string;
  checkItemId: string;
  refereeId?: string | null;
  refereeName: string;
  refereeOrganisation: string;
  refereePosition: string;
  refereeEmail: string;
  refereePhone?: string | null;
  responseDate?: string | null;
  responseMethod: ReferenceResponseMethod;
  responseMethodName: string;
  overallRating?: ReferenceRating | null;
  overallRatingName?: string | null;
  comments?: string | null;
  wouldRehire?: boolean | null;
  confirmedDatesOfEmployment?: boolean | null;
  confirmedPositionHeld?: boolean | null;
  confirmedReasonForLeaving?: boolean | null;
  hasDocument: boolean;
  documentFileName?: string | null;
  createdAt: string;
}

export interface CreateReferenceCheckResponse {
  refereeId?: string | null;
  refereeName: string;
  refereeOrganisation: string;
  refereePosition: string;
  refereeEmail: string;
  refereePhone?: string | null;
  responseMethod: ReferenceResponseMethod;
  overallRating?: ReferenceRating | null;
  comments?: string | null;
  wouldRehire?: boolean | null;
  confirmedDatesOfEmployment?: boolean | null;
  confirmedPositionHeld?: boolean | null;
  confirmedReasonForLeaving?: boolean | null;
}

/**
 * ⚠ Narrower than create by design: the referee's identity and how they were reached are facts about
 * who was contacted, and correcting them means the response came from someone else. Delete and
 * re-add for that.
 */
export type UpdateReferenceCheckResponse = Pick<
  CreateReferenceCheckResponse,
  | 'overallRating'
  | 'comments'
  | 'wouldRehire'
  | 'confirmedDatesOfEmployment'
  | 'confirmedPositionHeld'
  | 'confirmedReasonForLeaving'
>;

// ── check templates (setup) ────────────────────────────────────────────────

export interface PreEmploymentCheckTemplateItem {
  id: string;
  tenantId: string;
  templateId: string;
  checkType: PreEmploymentCheckType;
  checkTypeName: string;
  defaultServiceProvider?: string | null;
  /** The supplier behind the default provider, when it is one (round 3, lane G; D-14). */
  defaultServiceProviderSupplierId?: string | null;
  instructions?: string | null;
  isMandatory: boolean;
  isBlockingOnFail: boolean;
  expectedDays?: number | null;
}

/** A supplier that provides one kind of check (round 3, lane G; D-14) — the check-type → provider cascade reads these. */
export interface PreEmploymentCheckProviderService {
  id: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  supplierIsActive: boolean;
  checkType: PreEmploymentCheckType;
  checkTypeName: string;
  notes?: string | null;
  isActive: boolean;
}

export interface CreatePreEmploymentCheckProviderServices {
  supplierId: string;
  checkTypes: PreEmploymentCheckType[];
  notes?: string | null;
}

export interface PreEmploymentCheckTemplate {
  id: string;
  tenantId: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  itemCount: number;
  createdAt: string;
}

export interface PreEmploymentCheckTemplateDetail extends PreEmploymentCheckTemplate {
  items: PreEmploymentCheckTemplateItem[];
}

export interface CreatePreEmploymentCheckTemplateItem {
  checkType: PreEmploymentCheckType;
  defaultServiceProvider?: string | null;
  /** A Procurement supplier (round 3, lane G); the server mirrors its name into `defaultServiceProvider`. */
  defaultServiceProviderSupplierId?: string | null;
  instructions?: string | null;
  isMandatory: boolean;
  isBlockingOnFail: boolean;
  expectedDays?: number | null;
}

export interface CreatePreEmploymentCheckTemplate {
  name: string;
  description?: string | null;
  items: CreatePreEmploymentCheckTemplateItem[];
}

export interface UpdatePreEmploymentCheckTemplate {
  name: string;
  description?: string | null;
  isActive: boolean;
}

/** ⚠ `checkType` is excluded deliberately — delete and re-add the item to change it. */
export type UpdatePreEmploymentCheckTemplateItem = Omit<CreatePreEmploymentCheckTemplateItem, 'checkType'>;

export interface ApplyCheckTemplate {
  templateId: string;
  /** Replaces existing items of the same check type rather than skipping them. */
  overwriteExisting: boolean;
}
