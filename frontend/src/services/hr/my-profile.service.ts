import { apiService } from '../api.service';

/**
 * Area 25 slice 12a (decision D6) — the employee's own profile, and the approval path for the
 * parts of it that carry identity or payment consequences.
 *
 * Every shape here was measured against the live API (`probe-slice12a.mjs`), not inferred from
 * an endpoint name. Enums arrive as STRINGS (`"Male"`, `"Pending"`, `"LastName"`, `"Savings"`)
 * and are sent back the same way; `dateOfBirth` is a plain `yyyy-MM-dd` date string.
 *
 * The law of this surface: no method takes an employee id. The server derives the employee
 * from the JWT, and a request belonging to someone else does not resolve at all.
 */

// ── The field classification, mirrored from the backend enum ────────────────────────────────
// Only these can be the subject of a change request. Anything absent (position, manager,
// salary, employee number, employment dates) is HR's to change, not the employee's to ask about.
export type EmployeeProfileField =
  | 'FirstName'
  | 'MiddleName'
  | 'LastName'
  | 'Title'
  | 'DateOfBirth'
  | 'Gender'
  | 'MaritalStatus'
  | 'EmailAddress'
  | 'Address'
  | 'City'
  | 'State'
  | 'PostalCode'
  | 'DigitalAddress'
  | 'CountryId'
  | 'SocialSecurityNumber'
  | 'TINNumber'
  | 'TaxNumber'
  | 'BankName'
  | 'BankBranchName'
  | 'BankAccountNumber'
  | 'BankAccountName'
  | 'BankAccountType'
  | 'MobileMoneyNumber';

/** The bank fields target the request's `bankDetailId`; the rest live on the employee record. */
export const BANK_PROFILE_FIELDS: readonly EmployeeProfileField[] = [
  'BankName',
  'BankBranchName',
  'BankAccountNumber',
  'BankAccountName',
  'BankAccountType',
  'MobileMoneyNumber',
] as const;

export type ProfileChangeRequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';

export interface MyProfileEmergencyContact {
  id: string;
  employeeId: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  relationship: string;
  contactType: string;
  phoneNumber: string;
  alternatePhoneNumber?: string | null;
  emailAddress?: string | null;
  address?: string | null;
  city?: string | null;
  countryId?: string | null;
  digitalAddress?: string | null;
  isPrimary: boolean;
  isActive: boolean;
  notes?: string | null;
}

export interface MyProfileDependent {
  id: string;
  employeeId: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  relationship: string;
  dateOfBirth?: string | null;
  gender?: string | null;
  occupation?: string | null;
  isEligibleForBenefits: boolean;
  age?: number | null;
  isDeceased: boolean;
}

/** ⚠ `accountNumber` arrives MASKED to its last four digits — it is never the full number. */
export interface MyProfileBankDetail {
  id: string;
  employeeId: string;
  bankId?: string | null;
  bankCode?: string | null;
  branchId?: string | null;
  branchCode?: string | null;
  bankName: string;
  branchName: string;
  accountNumber: string;
  accountName: string;
  mobileMoneyNumber?: string | null;
  accountType: string;
  allocationPercentage: number;
  isPrimary: boolean;
  isActive: boolean;
  isVerified: boolean;
  verifiedDate?: string | null;
  verifiedById?: string | null;
}

export interface MyProfile {
  employeeId: string;
  employeeNumber: string;

  firstName: string;
  middleName?: string | null;
  lastName: string;
  fullName: string;
  title?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  maritalStatus?: string | null;
  religion?: string | null;
  bloodType?: string | null;
  picturePath?: string | null;

  emailAddress: string;
  mobileNumber?: string | null;
  telephoneNumber?: string | null;
  businessNumber?: string | null;
  extension?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  digitalAddress?: string | null;
  countryId?: string | null;
  countryName?: string | null;

  socialSecurityNumber?: string | null;
  tinNumber?: string | null;
  taxNumber?: string | null;

  // Read-only, always — HR and payroll own every one of these.
  positionTitle?: string | null;
  departmentName?: string | null;
  sectionName?: string | null;
  organizationUnitName?: string | null;
  locationName?: string | null;
  managerName?: string | null;
  employmentType: string;
  staffStatus: string;
  dateEmployed?: string | null;
  confirmationDate?: string | null;
  /** Null when no hire date is recorded — "unknown", not "zero years". */
  yearsOfService?: number | null;

  emergencyContacts: MyProfileEmergencyContact[];
  dependents: MyProfileDependent[];
  bankDetails: MyProfileBankDetail[];

  /** Fields already waiting on HR — the screen offers no second request for these. */
  fieldsWithPendingRequests: EmployeeProfileField[];
}

/** Null means "leave alone"; an empty string clears the field. The two differ deliberately. */
export interface UpdateMyContactDetailsRequest {
  mobileNumber?: string | null;
  telephoneNumber?: string | null;
  businessNumber?: string | null;
  extension?: string | null;
  religion?: string | null;
  maritalStatus?: string | null;
}

export interface ProfileChangeItem {
  id: string;
  field: EmployeeProfileField;
  fieldName: string;
  /** A human label supplied by the server, so no screen keeps its own map. */
  fieldLabel: string;
  oldValue?: string | null;
  newValue: string;
  /** What actually landed on approval. Stamped by the server, never sent. */
  appliedValue?: string | null;
}

export interface ProfileChangeRequest {
  id: string;
  requestNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  status: ProfileChangeRequestStatus;
  statusName: string;
  submittedAt: string;
  reason: string;
  bankDetailId?: string | null;
  /** The targeted account, masked — enough to tell two accounts apart in a queue. */
  bankAccountMasked?: string | null;
  reviewedById?: string | null;
  reviewedByName?: string | null;
  reviewedAt?: string | null;
  reviewComments?: string | null;
  appliedAt?: string | null;
  cancelledAt?: string | null;
  hasEvidence: boolean;
  evidenceFileName?: string | null;
  items: ProfileChangeItem[];
}

export interface CreateProfileChangeRequest {
  reason: string;
  /** Required when any bank field is included; must be one of the caller's own accounts. */
  bankDetailId?: string | null;
  items: { field: EmployeeProfileField; newValue: string }[];
}

class MyProfileService {
  private readonly baseUrl = '/employee-portal/profile';

  getProfile(): Promise<MyProfile> {
    return apiService.get<MyProfile>(this.baseUrl);
  }

  /** The low-risk fields, applied immediately — no approval. */
  updateContactDetails(payload: UpdateMyContactDetailsRequest): Promise<MyProfile> {
    return apiService.put<MyProfile>(`${this.baseUrl}/contact-details`, payload);
  }

  createChangeRequest(payload: CreateProfileChangeRequest): Promise<ProfileChangeRequest> {
    return apiService.post<ProfileChangeRequest>(`${this.baseUrl}/change-requests`, payload);
  }

  getMyChangeRequests(): Promise<ProfileChangeRequest[]> {
    return apiService.get<ProfileChangeRequest[]>(`${this.baseUrl}/change-requests`);
  }

  /** Somebody else's id is a 404 lookup miss — never a 403. */
  getChangeRequest(id: string): Promise<ProfileChangeRequest> {
    return apiService.get<ProfileChangeRequest>(`${this.baseUrl}/change-requests/${id}`);
  }

  cancelChangeRequest(id: string): Promise<ProfileChangeRequest> {
    return apiService.post<ProfileChangeRequest>(`${this.baseUrl}/change-requests/${id}/cancel`, {});
  }

  /** Multipart, through the shared controlled-upload gate (scanned, checksummed, catalogued). */
  uploadEvidenceEndpoint(id: string): string {
    return `${this.baseUrl}/change-requests/${id}/evidence`;
  }
}

/** The HR desk side. Gated on the same permissions that govern changing an employee record. */
class ProfileChangeQueueService {
  private readonly baseUrl = '/hr/profile-change-requests';

  getQueue(status?: ProfileChangeRequestStatus): Promise<ProfileChangeRequest[]> {
    return apiService.get<ProfileChangeRequest[]>(
      status ? `${this.baseUrl}?status=${status}` : this.baseUrl,
    );
  }

  getById(id: string): Promise<ProfileChangeRequest> {
    return apiService.get<ProfileChangeRequest>(`${this.baseUrl}/${id}`);
  }

  /** Approving APPLIES the values onto the employee record in the same transaction. */
  approve(id: string, comments?: string): Promise<ProfileChangeRequest> {
    return apiService.post<ProfileChangeRequest>(`${this.baseUrl}/${id}/approve`, { comments });
  }

  /** The comment is required — the employee reads it back. */
  reject(id: string, comments: string): Promise<ProfileChangeRequest> {
    return apiService.post<ProfileChangeRequest>(`${this.baseUrl}/${id}/reject`, { comments });
  }

  evidenceEndpoint(id: string): string {
    return `${this.baseUrl}/${id}/evidence`;
  }
}

export const myProfileService = new MyProfileService();
export const profileChangeQueueService = new ProfileChangeQueueService();
