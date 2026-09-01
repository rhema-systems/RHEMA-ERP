/**
 * HR reference lookups that feed the employee profile tabs and other HR forms.
 * Routes vary per controller — see each service for the exact one.
 */

// ── Qualifications — api/hr/qualifications (QualificationCatalogueDto) ──────────

export type QualificationType =
  | 'Education'
  | 'Experience'
  | 'Certification'
  | 'License'
  | 'TechnicalSkills'
  | 'Language'
  | 'Membership'
  | 'Other';

export const QUALIFICATION_TYPE_OPTIONS: { value: QualificationType; label: string }[] = [
  { value: 'Education', label: 'Education' },
  { value: 'Experience', label: 'Work Experience' },
  { value: 'Certification', label: 'Certification' },
  { value: 'License', label: 'License' },
  { value: 'TechnicalSkills', label: 'Technical Skills' },
  { value: 'Language', label: 'Language' },
  { value: 'Membership', label: 'Membership' },
  { value: 'Other', label: 'Other' },
];

export interface Qualification {
  id: string;
  name: string;
  shortCode?: string | null;
  description?: string | null;
  type: QualificationType;
  issuingAuthority?: string | null;
  isActive: boolean;
  /**
   * Which rung of the ladder this sits on. Null means unranked.
   *
   * ⚠ Not a substitute for `type`, which is a CATEGORY and cannot rank anything.
   */
  qualificationLevelId?: string | null;
  /** Resolved by the server — the read Includes the level so this is never a silent null. */
  qualificationLevelName?: string | null;
  qualificationLevelRank?: number | null;
}

/** The backend uses the same DTO for create and update. */
export interface QualificationRequest {
  name: string;
  shortCode?: string | null;
  description?: string | null;
  type: QualificationType;
  issuingAuthority?: string | null;
  isActive: boolean;
  qualificationLevelId?: string | null;
}

// ── Identification types — api/hr/IdentificationTypes ───────────────────────────

export interface IdentificationType {
  id: string;
  name: string;
  code?: string | null;
  description?: string | null;
  issuingAuthorityName: string;
  issuingCountryId?: string | null;
  issuingCountryName?: string | null;
  hasExpiryDate: boolean;
  /**
   * How many days ahead the expiry sweep warns about a card of this type.
   *
   * ⚠ `null` means it warns about NOTHING — the correct reading for an ID that does not expire,
   * and a silent one for an ID that does. The sweep joins on this column, so an unset value is the
   * usual reason a card register full of expiries produces an empty sweep.
   */
  expiryNotificationLeadDays?: number | null;
  isActive: boolean;
}

export interface CreateIdentificationTypeRequest {
  name: string;
  code?: string | null;
  description?: string | null;
  issuingAuthorityName: string;
  issuingCountryId?: string | null;
  hasExpiryDate: boolean;
  /**
   * How many days ahead the expiry sweep warns about a card of this type.
   *
   * ⚠ `null` means it warns about NOTHING — the correct reading for an ID that does not expire,
   * and a silent one for an ID that does. The sweep joins on this column, so an unset value is the
   * usual reason a card register full of expiries produces an empty sweep.
   */
  expiryNotificationLeadDays?: number | null;
  isActive: boolean;
}

export interface UpdateIdentificationTypeRequest extends CreateIdentificationTypeRequest {
  id: string;
}

// ── Reason codes — api/reason-codes ─────────────────────────────────────────────

export type ReasonCodeCategory =
  | 'General'
  | 'LeaveAdjustment'
  | 'LeaveEncashment'
  | 'LeaveCancellation'
  | 'LeaveRejection';

export const REASON_CODE_CATEGORY_OPTIONS: { value: ReasonCodeCategory; label: string }[] = [
  { value: 'General', label: 'General' },
  { value: 'LeaveAdjustment', label: 'Leave Adjustment' },
  { value: 'LeaveEncashment', label: 'Leave Encashment' },
  { value: 'LeaveCancellation', label: 'Leave Cancellation' },
  { value: 'LeaveRejection', label: 'Leave Rejection' },
];

export interface ReasonCode {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  category: ReasonCodeCategory;
  isActive: boolean;
}

/** The backend create/update DTOs are identical and omit the id (it comes from the route). */
export interface ReasonCodeRequest {
  code: string;
  name: string;
  description?: string | null;
  category: ReasonCodeCategory;
  isActive: boolean;
}

// ── Departments — api/departments (READ ONLY: the controller exposes GET only) ──

export type DepartmentType = string;

export interface Department {
  id: string;
  name: string;
  code: string;
  description?: string | null;
  departmentType: DepartmentType;
  parentDepartmentId?: string | null;
  parentDepartmentName?: string | null;
  departmentHeadId?: string | null;
  departmentHeadName?: string | null;
  budget?: number | null;
  isActive: boolean;
  employeeCount: number;
  sectionCount: number;
}

// ── Lane 3b: reference dimensions ───────────────────────────────────────────
// Every shape below was read off a live response, not inferred from an endpoint name.

/** A rung on the academic / professional ladder. */
export interface QualificationLevel {
  id: string;
  name: string;
  code?: string | null;
  description?: string | null;
  /** Ascending. Higher is more advanced; ties mean equivalents. */
  rank: number;
  isActive: boolean;
  /** How many qualifications sit here — so the screen can warn before retiring it. */
  qualificationCount: number;
}

export interface QualificationLevelRequest {
  name: string;
  code?: string | null;
  description?: string | null;
  rank: number;
  isActive: boolean;
}

/** An organisation that certifies a skill. */
export interface CertifyingBody {
  id: string;
  name: string;
  abbreviation?: string | null;
  description?: string | null;
  countryId?: string | null;
  countryName?: string | null;
  website?: string | null;
  isActive: boolean;
}

export interface CertifyingBodyRequest {
  name: string;
  abbreviation?: string | null;
  description?: string | null;
  countryId?: string | null;
  website?: string | null;
  isActive: boolean;
}

/** HREnums.cs `EmploymentType` — 1..9. `null` on a rule means the tenant default. */
export const EMPLOYMENT_REGISTERS = [
  'Permanent', 'Contract', 'FixedTerm', 'Internship', 'Casual',
  'PartTime', 'Temporary', 'Consultant', 'Freelance',
] as const;
export type EmploymentRegister = (typeof EMPLOYMENT_REGISTERS)[number];

/** How one register of employees is numbered. */
export interface StaffNumberFormat {
  id: string;
  name: string;
  /** null is the tenant's DEFAULT rule, which catches every register without one of its own. */
  appliesToEmploymentType?: EmploymentRegister | null;
  /** Resolved label, "All other staff" for the default rule. */
  appliesToName: string;
  prefix: string;
  separator: string;
  includeYear: boolean;
  yearDigits: number;
  sequenceDigits: number;
  suffix: string;
  /** Whether the system issues numbers for this register, or HR types them. */
  autoGenerate: boolean;
  sequenceKey: string;
  isActive: boolean;
  /**
   * What this rule produces for counter 1.
   *
   * ⚠ Composed by the SERVER, by the same code that issues the number. Recomposing it in
   * TypeScript would be a second implementation that can drift from the one that matters.
   */
  example: string;
}

export interface StaffNumberFormatRequest {
  name: string;
  appliesToEmploymentType?: EmploymentRegister | null;
  prefix: string;
  separator: string;
  includeYear: boolean;
  yearDigits: number;
  sequenceDigits: number;
  suffix: string;
  autoGenerate: boolean;
  sequenceKey: string;
  isActive: boolean;
}

/**
 * Where one rule's counter stands against the numbers already in the register.
 *
 * ⚠ The read that makes the import hazard visible. A counter only knows about numbers it issued
 * itself, so a register loaded by SQL, a seeder or a restore leaves it at zero with thousands of
 * numbers already in use — and nothing says so until a hire fails on the unique index.
 */
export interface StaffNumberCounterState {
  formatId: string;
  formatName: string;
  sequenceKey: string;
  autoGenerate: boolean;
  isActive: boolean;
  /** The counter's year bucket, or null when the rule does not print the year. */
  yearBucket?: number | null;
  /** The value last issued. 0 when the counter has never been used. */
  counterStandsAt: number;
  /** What the next auto-issued number would be, composed by the rule itself. */
  nextNumber: string;
  /** ⚠ True when that next number is already somebody's. */
  nextNumberIsInUse: boolean;
  numbersInRegister: number;
  /** Did not fit the rule at all — another register's numbering, or another year. */
  numbersNotMatchingFormat: number;
  highestInRegister: number;
  highestNumberInRegister?: string | null;
  /** True when the register is ahead of the counter, so reconciliation is owed. */
  counterIsBehind: boolean;
}

/** One card the sweep would remind about, or has. */
export interface IdentificationExpiryItem {
  /** `IdentificationExpiring` or `IdentificationExpired`. */
  kind: string;
  employeeId: string;
  employeeName?: string | null;
  employeeNumber?: string | null;
  employeeIdentificationCardId: string;
  identificationTypeId: string;
  identificationTypeName: string;
  documentNumber?: string | null;
  reference?: string | null;
  /** DateOnly — 'YYYY-MM-DD'. */
  dueDate?: string | null;
  /** Negative once the date has passed. */
  daysRemaining: number;
  /** The type's own lead time, so a reader can see why this surfaced when it did. */
  leadDays: number;
  /** 1 while valid and inside the window; 2 once expired. */
  escalationTier: number;
  /**
   * Whose responsibility it is now — the holder at tier 1, HR (null) at tier 2.
   *
   * ⚠ An ownership stamp, NOT a visibility switch. HR sees every row at both tiers.
   */
  routedToEmployeeId?: string | null;
  /** Whether a sweep has already raised this exact reminder. Meaningful on a preview only. */
  alreadyRaised: boolean;
}

export interface IdentificationExpiryRunResult {
  runId: string;
  startedAt: string;
  completedAt?: string | null;
  trigger: string;
  cardsConsidered: number;
  remindersQueued: number;
  /** Considered but already raised at this tier — the dedupe working, not a failure. */
  alreadyRaised: number;
}

/** One pass of the sweep. */
export interface IdentificationExpiryRun {
  id: string;
  startedAt: string;
  completedAt?: string | null;
  /** 'Scheduled' for the nightly host, 'Manual' for a run from the screen. */
  trigger: string;
  triggeredByUserId?: string | null;
  remindersQueued: number;
}

/**
 * A reminder that was actually raised.
 *
 * ⚠ The RAISED record, not a live view of the card — its dates are as they stood when the
 * reminder went out. Renewing the document raises a fresh reminder rather than rewriting this one.
 */
export interface IdentificationExpiryLogEntry {
  id: string;
  runId: string;
  kind: string;
  employeeId: string;
  employeeName?: string | null;
  employeeNumber?: string | null;
  employeeIdentificationCardId: string;
  identificationTypeId: string;
  identificationTypeName?: string | null;
  reference?: string | null;
  dueDate?: string | null;
  daysRemaining: number;
  escalationTier: number;
  routedToEmployeeId?: string | null;
  raisedAt: string;
}
