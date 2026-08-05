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
}

/** The backend uses the same DTO for create and update. */
export interface QualificationRequest {
  name: string;
  shortCode?: string | null;
  description?: string | null;
  type: QualificationType;
  issuingAuthority?: string | null;
  isActive: boolean;
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
  isActive: boolean;
}

export interface CreateIdentificationTypeRequest {
  name: string;
  code?: string | null;
  description?: string | null;
  issuingAuthorityName: string;
  issuingCountryId?: string | null;
  hasExpiryDate: boolean;
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
