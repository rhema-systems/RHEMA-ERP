import type { PagedResult } from './common';

// --- Enums (serialize as strings via JsonStringEnumConverter) ---
export type Gender = 'Male' | 'Female' | 'Other' | 'PreferNotToSay';
export type MaritalStatus = 'Single' | 'Married' | 'Divorced' | 'Widowed' | 'Separated' | 'Other';
export type StaffStatus =
  | 'Active'
  | 'Inactive'
  | 'Probation'
  | 'Suspended'
  | 'Terminated'
  | 'Retired'
  | 'OnLeave';
export type EmploymentType =
  | 'Permanent'
  | 'Contract'
  | 'FixedTerm'
  | 'Internship'
  | 'Casual'
  | 'PartTime'
  | 'Temporary'
  | 'Consultant'
  | 'Freelance';
export type BloodType =
  | 'APositive'
  | 'ANegative'
  | 'BPositive'
  | 'BNegative'
  | 'ABPositive'
  | 'ABNegative'
  | 'OPositive'
  | 'ONegative'
  | 'Unknown';

export const GENDER_OPTIONS: { value: Gender; label: string }[] = [
  { value: 'Male', label: 'Male' },
  { value: 'Female', label: 'Female' },
  { value: 'Other', label: 'Other' },
  { value: 'PreferNotToSay', label: 'Prefer not to say' },
];

export const MARITAL_STATUS_OPTIONS: { value: MaritalStatus; label: string }[] = [
  { value: 'Single', label: 'Single' },
  { value: 'Married', label: 'Married' },
  { value: 'Divorced', label: 'Divorced' },
  { value: 'Widowed', label: 'Widowed' },
  { value: 'Separated', label: 'Separated' },
  { value: 'Other', label: 'Other' },
];

export const STAFF_STATUS_OPTIONS: { value: StaffStatus; label: string }[] = [
  { value: 'Active', label: 'Active' },
  { value: 'Inactive', label: 'Inactive' },
  { value: 'Probation', label: 'Probation' },
  { value: 'Suspended', label: 'Suspended' },
  { value: 'Terminated', label: 'Terminated' },
  { value: 'Retired', label: 'Retired' },
  { value: 'OnLeave', label: 'On leave' },
];

export const EMPLOYMENT_TYPE_OPTIONS: { value: EmploymentType; label: string }[] = [
  { value: 'Permanent', label: 'Permanent' },
  { value: 'Contract', label: 'Contract' },
  { value: 'FixedTerm', label: 'Fixed term' },
  { value: 'Internship', label: 'Internship' },
  { value: 'Casual', label: 'Casual' },
  { value: 'PartTime', label: 'Part-time' },
  { value: 'Temporary', label: 'Temporary' },
  { value: 'Consultant', label: 'Consultant' },
  { value: 'Freelance', label: 'Freelance' },
];

/**
 * Why an employee is not paid through the payroll run. Mirrors `OffPayrollReason` in HREnums.cs;
 * the API serialises enums as their names.
 */
export type OffPayrollReason =
  | 'PaidByInvoice'
  | 'Allowance'
  | 'PaidByParentOrganisation'
  | 'Unpaid'
  | 'BoardOrCommittee'
  | 'Other';

export const OFF_PAYROLL_REASON_OPTIONS: { value: OffPayrollReason; label: string }[] = [
  { value: 'PaidByInvoice', label: 'Paid by invoice (consultant, contractor)' },
  { value: 'Allowance', label: 'Allowance or stipend (intern, national service)' },
  { value: 'PaidByParentOrganisation', label: 'Paid by parent organisation (secondee)' },
  { value: 'Unpaid', label: 'Unpaid (volunteer, honorary)' },
  { value: 'BoardOrCommittee', label: 'Board or committee member (sitting allowance)' },
  { value: 'Other', label: 'Other (say what in the note)' },
];

export const offPayrollReasonLabel = (value?: OffPayrollReason | null) =>
  OFF_PAYROLL_REASON_OPTIONS.find((o) => o.value === value)?.label ?? value ?? '—';

export const BLOOD_TYPE_OPTIONS: { value: BloodType; label: string }[] = [
  { value: 'APositive', label: 'A+' },
  { value: 'ANegative', label: 'A-' },
  { value: 'BPositive', label: 'B+' },
  { value: 'BNegative', label: 'B-' },
  { value: 'ABPositive', label: 'AB+' },
  { value: 'ABNegative', label: 'AB-' },
  { value: 'OPositive', label: 'O+' },
  { value: 'ONegative', label: 'O-' },
  { value: 'Unknown', label: 'Unknown' },
];

// --- Lightweight lookup (pickers) — subset of EmployeeDto ---
export interface EmployeeLookup {
  id: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  fullName: string;
  displayName: string;
  positionTitle?: string;
  departmentName?: string;
  isActive: boolean;
}

// Mirrors EmployeeSearchDto (POST /hr/Employees/paged body). All optional.
export interface EmployeeSearchRequest {
  searchTerm?: string;
  positionId?: string;
  staffStatus?: StaffStatus;
  employmentType?: EmploymentType;
  isActive?: boolean;
  /** true = on payroll only; false = off-payroll staff only. */
  isOnPayroll?: boolean;
}

export type EmployeePagedResult = PagedResult<EmployeeLookup>;

// --- Summary (list) — mirrors EmployeeDto ---
export interface Employee {
  id: string;
  employeeNumber: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  fullName: string;
  displayName: string;
  title?: string | null;
  gender?: Gender | null;
  emailAddress?: string | null;
  mobileNumber?: string | null;
  departmentName: string;
  sectionName?: string | null;
  positionTitle: string;
  staffLevelName?: string | null;
  organizationLevelName?: string | null;
  organizationUnitName?: string | null;
  locationLevelName?: string | null;
  locationId?: string | null;
  locationName?: string | null;
  staffStatus: StaffStatus;
  employmentType: EmploymentType;
  isActive: boolean;
  isFullTime: boolean;
  isExpatriate: boolean;
  /** Paid through the payroll run. False for invoice, allowance and secondee staff. */
  isOnPayroll: boolean;
  dateEmployed?: string | null;
  yearsOfService?: number | null;
  picturePath?: string | null;
}

// --- Detail — mirrors EmployeeDetailDto (extends EmployeeDto) ---
export interface EmployeeDetail extends Employee {
  // ⚠ Read-only. The photograph arrives through POST employee-documents/employee/{id}/photo and
  // the gate fills these in; `picturePath` above is the LEGACY caller-supplied location, kept only
  // so ported images still resolve. Prefer `hasPhoto`.
  hasPhoto?: boolean;
  photoFileName?: string | null;
  photoMimeType?: string | null;
  photoFileSizeBytes?: number | null;

  departmentId?: string | null;
  sectionId?: string | null;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  positionId: string;
  managerId?: string | null;
  locationLevelId?: string | null;
  countryId?: string | null;
  /** The deepest administrative area on record — one id whatever the scheme's depth. */
  geoAreaId?: string | null;
  shiftId?: string | null;
  dateOfBirth?: string | null;
  maritalStatus?: MaritalStatus | null;
  religion?: string | null;

  // ── Employee Master feedback, lane 3a ──────────────────────────────────────
  /** How the employee describes their gender, where `gender` is Other. */
  genderDescription?: string | null;
  /** Home town or place of origin. */
  hometown?: string | null;
  /**
   * ⚠ The EMPLOYEE's own disability — added alongside, never replacing, the one on a DEPENDANT.
   * A dependant's disability and an employee's are different facts about different people.
   */
  hasDisability?: boolean;
  disabilityDescription?: string | null;
  /** Round 3, lane P2: the catalogue row and its name. The description stays as notes. */
  disabilityTypeId?: string | null;
  disabilityTypeName?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  digitalAddress?: string | null;
  countryName?: string | null;
  telephoneNumber?: string | null;
  businessNumber?: string | null;
  extension?: string | null;
  probationPeriodDays: number;
  /** Where the term came from. Null on records created before 2026-09-09. */
  probationSource?: ProbationSource | null;
  /**
   * When probation is DUE to end — hire date plus the term, computed by the server on every read.
   * Not the same thing as `confirmationDate`, which is when it was actually passed.
   */
  expectedConfirmationDate?: string | null;
  /**
   * The date probation was passed.
   *
   * ⚠ Read-only everywhere but the import form. The server refuses a change to it from the
   * ordinary employee update — it is written by confirming the probation record, which is what
   * issues the letter.
   */
  confirmationDate?: string | null;
  retirementDate?: string | null;
  taxNumber?: string | null;
  socialSecurityNumber?: string | null;
  tinNumber?: string | null;
  bloodType?: BloodType | null;
  shiftName?: string | null;
  salary?: number | null;
  payTax: boolean;
  ssFund: boolean;
  grossUp: boolean;
  tier2Only: boolean;
  overtime: boolean;
  /** Set when `isOnPayroll` is false. */
  offPayrollReason?: OffPayrollReason | null;
  offPayrollNote?: string | null;
  badgeNumber?: string | null;
  notes?: string | null;
  terminationDate?: string | null;
  terminationReason?: string | null;
  terminationNotes?: string | null;
  isOnProbation: boolean;
  /**
   * How basic pay is arrived at — the scale, or an amount agreed for this person. Written only
   * through `PUT {id}/pay-basis` (the Salary tab), never through the create or the ordinary edit.
   */
  payBasis: PayBasis;
  payBasisNote?: string | null;
}

/**
 * Scale or negotiated. HR's fact: the scale is HR's concept (a placement on a notch whose amount
 * is the pay); payroll is amount-based and never reads the placement. Independent of
 * employmentType and of isOnPayroll — a permanent employee can be negotiated, a contractor can be
 * on the scale.
 */
export type PayBasis = 'SalaryScale' | 'Negotiated';

export const PAY_BASIS_OPTIONS: { value: PayBasis; label: string; description: string }[] = [
  {
    value: 'SalaryScale',
    label: 'Salary scale',
    description: 'Basic pay is the amount of the notch the person is placed on.',
  },
  {
    value: 'Negotiated',
    label: 'Negotiated',
    description: 'Basic pay is an amount agreed for this person. Placement on the scale is refused while this stands.',
  },
];

export interface SetPayBasisRequest {
  payBasis: PayBasis;
  /** Required when negotiated: who agreed what, and when. */
  note?: string | null;
}

/**
 * Where an employee's probation term came from.
 *
 * The position is the source and the company policy default is the fallback — the same resolution
 * the probation policy read has always used. `Override` only happens where the position is silent.
 */
export type ProbationSource = 'Position' | 'PolicyDefault' | 'Override';

export const PROBATION_SOURCE_LABEL: Record<ProbationSource, string> = {
  Position: 'from the position',
  PolicyDefault: 'the company default',
  Override: 'set for this employee',
};

// --- Create / Update requests (Department & Section intentionally omitted) ---
export interface CreateEmployeeRequest {
  employeeNumber?: string; // blank => auto-generated server-side
  firstName: string;
  middleName?: string | null;
  lastName: string;
  title?: string | null;
  gender?: Gender | null;
  dateOfBirth?: string | null;
  maritalStatus?: MaritalStatus | null;
  religion?: string | null;

  // ── Employee Master feedback, lane 3a ──────────────────────────────────────
  /** How the employee describes their gender, where `gender` is Other. */
  genderDescription?: string | null;
  /** Home town or place of origin. */
  hometown?: string | null;
  /**
   * ⚠ The EMPLOYEE's own disability — added alongside, never replacing, the one on a DEPENDANT.
   * A dependant's disability and an employee's are different facts about different people.
   */
  hasDisability?: boolean;
  disabilityDescription?: string | null;
  /** Round 3, lane P2: one of the tenant's live disability types; only with hasDisability. */
  disabilityTypeId?: string | null;
  isFullTime: boolean;
  dateEmployed?: string | null;
  address?: string | null;
  /** ⚠ Overwritten by the resolved town/district when `geoAreaId` is sent. */
  city?: string | null;
  /** ⚠ Overwritten by the resolved region when `geoAreaId` is sent. */
  state?: string | null;
  postalCode?: string | null;
  digitalAddress?: string | null;
  countryId?: string | null;
  /** The deepest administrative area chosen. Sending it also rewrites `city` and `state`. */
  geoAreaId?: string | null;
  /**
   * Removes the area on an update. Needed because a null `geoAreaId` reads as "not supplied" —
   * without this, clearing the picker would save and change nothing. Ignored on create.
   */
  clearGeoArea?: boolean;
  emailAddress?: string | null;
  telephoneNumber?: string | null;
  mobileNumber?: string | null;
  employmentType: EmploymentType;
  /**
   * Which kind of engagement the first contract is, from the tenant's contract-type list.
   * Optional; its duration gives a fixed-term contract its end date.
   */
  contractTypeId?: string | null;
  /**
   * The probation term in days.
   *
   * ⚠ Send `null` and the server derives it from the position, falling back to the company policy
   * default. A value sent against a position that states its own term is REFUSED — the form only
   * sends one where the position is silent.
   */
  probationPeriodDays?: number | null;
  /** Accepted on the IMPORT path only: someone confirmed before this system existed. */
  confirmationDate?: string | null;
  positionId: string;
  organizationUnitId: string; // derived from the selected position
  locationId: string; // required by the service
  managerId?: string | null;
  staffStatus: StaffStatus;
  taxNumber?: string | null;
  socialSecurityNumber?: string | null;
  tinNumber?: string | null;
  bloodType?: BloodType | null;
  salary?: number | null;
  /** Optional since lane E1: the form no longer sends them; absent means untouched on an update. */
  payTax?: boolean;
  ssFund?: boolean;
  grossUp?: boolean;
  tier2Only?: boolean;
  overtime?: boolean;
  /**
   * Payroll membership. Off requires a reason and must not carry a salary or any switch above —
   * the service refuses rather than dropping them (EmployeeService.ValidatePayrollMembership).
   */
  isOnPayroll: boolean;
  offPayrollReason?: OffPayrollReason | null;
  offPayrollNote?: string | null;
  badgeNumber?: string | null;
  notes?: string | null;
  isExpatriate: boolean;
}

export type UpdateEmployeeRequest = Partial<CreateEmployeeRequest> & {
  isActive?: boolean;
};

// Employee lifecycle transition payloads.
export interface TerminateEmployeeRequest {
  terminationDate: string;
  terminationReason: string;
  terminationNotes?: string | null;
}

// --- Payroll membership: HR's flag beside payroll's own profile ---
// Mirrors EmployeePayrollStatusDto / PayrollReconciliationDto (HRDTOs.cs). Both sides are read
// from GET api/hr/Employees/{id}/payroll-status and GET api/hr/Employees/payroll-reconciliation.

export type PayrollReconciliationIssue =
  | 'AwaitingPayrollSetup'
  | 'InactiveInPayroll'
  | 'StillActiveInPayroll'
  | 'NoPayBasis'
  | 'BasicPayMismatch';

export const PAYROLL_ISSUE_LABELS: Record<PayrollReconciliationIssue, string> = {
  AwaitingPayrollSetup: 'On payroll in HR, not yet set up in Payroll',
  InactiveInPayroll: 'On payroll in HR, switched off in Payroll',
  StillActiveInPayroll: 'Off payroll in HR, still active in Payroll',
  NoPayBasis: 'On payroll with no salary and no graded notch',
  // Round-2 lane E1. Scale only, and only from a placed NOTCH: the run pays payroll's figure while
  // HR's placement says another, every month until somebody sees it.
  BasicPayMismatch: 'Placed on a notch whose amount differs from the payroll basis',
};

export interface EmployeePayrollStatus {
  employeeId: string;
  employeeNumber: string;
  isOnPayroll: boolean;
  offPayrollReason?: OffPayrollReason | null;
  offPayrollNote?: string | null;
  /** Scale or negotiated — decides which figure `hrMonthlyBasicPay` is. */
  payBasis: PayBasis;
  payBasisNote?: string | null;
  hrMonthlyBasicPay?: number | null;
  /** Where the figure came from, in words the tab prints beside it. */
  hrBasicPaySource?: string | null;
  hasActiveSalaryAssignment: boolean;
  hasPayrollProfile: boolean;
  payrollActive?: boolean | null;
  payrollMonthlyBasicSalary?: number | null;
  payrollCurrencyCode?: string | null;
  issue?: PayrollReconciliationIssue | null;
}

export interface PayrollReconciliationRow {
  employeeId: string;
  employeeNumber: string;
  fullName: string;
  positionTitle?: string | null;
  organizationUnitName?: string | null;
  employmentType: EmploymentType;
  staffStatus: StaffStatus;
  isOnPayroll: boolean;
  offPayrollReason?: OffPayrollReason | null;
  hasPayrollProfile: boolean;
  payrollActive?: boolean | null;
  payBasis: PayBasis;
  hrMonthlyBasicPay?: number | null;
  /** Payroll's active basis, so a mismatch row shows both figures. */
  payrollMonthlyBasicSalary?: number | null;
  issue: PayrollReconciliationIssue;
}

export interface PayrollReconciliation {
  generatedAt: string;
  onPayrollCount: number;
  offPayrollCount: number;
  awaitingPayrollSetup: number;
  inactiveInPayroll: number;
  stillActiveInPayroll: number;
  noPayBasis: number;
  basicPayMismatch: number;
  rows: PayrollReconciliationRow[];
}
