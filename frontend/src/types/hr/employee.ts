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
  emailAddress: string;
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
}

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
  isFullTime: boolean;
  dateEmployed?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  digitalAddress?: string | null;
  countryId?: string | null;
  emailAddress: string;
  telephoneNumber?: string | null;
  mobileNumber?: string | null;
  employmentType: EmploymentType;
  probationPeriodDays: number;
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
  payTax: boolean;
  ssFund: boolean;
  grossUp: boolean;
  tier2Only: boolean;
  overtime: boolean;
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
  | 'NoPayBasis';

export const PAYROLL_ISSUE_LABELS: Record<PayrollReconciliationIssue, string> = {
  AwaitingPayrollSetup: 'On payroll in HR, not yet set up in Payroll',
  InactiveInPayroll: 'On payroll in HR, switched off in Payroll',
  StillActiveInPayroll: 'Off payroll in HR, still active in Payroll',
  NoPayBasis: 'On payroll with no salary and no graded notch',
};

export interface EmployeePayrollStatus {
  employeeId: string;
  employeeNumber: string;
  isOnPayroll: boolean;
  offPayrollReason?: OffPayrollReason | null;
  offPayrollNote?: string | null;
  hrMonthlyBasicPay?: number | null;
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
  hrMonthlyBasicPay?: number | null;
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
  rows: PayrollReconciliationRow[];
}
