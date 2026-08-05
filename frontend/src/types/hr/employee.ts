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
  dateEmployed?: string | null;
  yearsOfService?: number | null;
  picturePath?: string | null;
}

// --- Detail — mirrors EmployeeDetailDto (extends EmployeeDto) ---
export interface EmployeeDetail extends Employee {
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
