/**
 * Employee profile sub-resources. Every one of these hangs off
 * `api/hr/Employees/{employeeId}/...` (see EmployeesController).
 *
 * Enums serialize as strings (JsonStringEnumConverter is registered globally).
 * `DateOnly` fields serialize as 'YYYY-MM-DD'; `DateTime` fields as full ISO strings —
 * the distinction matters when posting back, so it is noted per field group below.
 */
import type { Gender, EmploymentType } from './employee';
import type { SkillLevel } from './position';

// ── Shared enums ────────────────────────────────────────────────────────────────

export type EmployeeContactType = 'Home' | 'Postal' | 'Temporary' | 'Other';
export type EmergencyContactType = 'EmergencyContact' | 'NextOfKin' | 'Both';
export type RefereeType = 'Professional' | 'Academic' | 'Personal';
export type EmployeeBankAccountType = 'Current' | 'Savings' | 'MobileMoney';
export type ContractStatus = 'Active' | 'Expired' | 'Terminated';
export type PayFrequency = 'Weekly' | 'BiWeekly' | 'Monthly' | 'Quarterly' | 'Annually' | 'OneTime';
export type TaxTreatmentType = 'None' | 'PAYE' | 'WithholdingTax';
export type PositionChangeReason =
  | 'InitialAssignment'
  | 'Promotion'
  | 'Demotion'
  | 'Transfer'
  | 'Restructure'
  | 'Termination'
  | 'Other'
  // Added with area 8, so the timeline can say which kind of move it was.
  | 'Secondment'
  | 'ActingAppointment'
  | 'Redesignation';
export type DependentRelationship =
  | 'Spouse'
  | 'Son'
  | 'Daughter'
  | 'Mother'
  | 'Father'
  | 'Brother'
  | 'Sister'
  | 'Uncle'
  | 'Aunt'
  | 'Nephew'
  | 'Niece'
  | 'Grandfather'
  | 'Grandmother'
  | 'Other';

const asOptions = <T extends string>(values: readonly T[]) =>
  values.map((value) => ({ value, label: value.replace(/([a-z])([A-Z])/g, '$1 $2') }));

export const EMPLOYEE_CONTACT_TYPE_OPTIONS = asOptions([
  'Home',
  'Postal',
  'Temporary',
  'Other',
] as const);
export const EMERGENCY_CONTACT_TYPE_OPTIONS = asOptions([
  'EmergencyContact',
  'NextOfKin',
  'Both',
] as const);
export const REFEREE_TYPE_OPTIONS = asOptions(['Professional', 'Academic', 'Personal'] as const);
export const BANK_ACCOUNT_TYPE_OPTIONS = asOptions(['Current', 'Savings', 'MobileMoney'] as const);
export const CONTRACT_STATUS_OPTIONS = asOptions(['Active', 'Expired', 'Terminated'] as const);
export const PAY_FREQUENCY_OPTIONS = asOptions([
  'Weekly',
  'BiWeekly',
  'Monthly',
  'Quarterly',
  'Annually',
  'OneTime',
] as const);
export const TAX_TREATMENT_OPTIONS = asOptions(['None', 'PAYE', 'WithholdingTax'] as const);
export const POSITION_CHANGE_REASON_OPTIONS = asOptions([
  'InitialAssignment',
  'Promotion',
  'Demotion',
  'Transfer',
  'Secondment',
  'ActingAppointment',
  'Redesignation',
  'Restructure',
  'Termination',
  'Other',
] as const);
export const DEPENDENT_RELATIONSHIP_OPTIONS = asOptions([
  'Spouse',
  'Son',
  'Daughter',
  'Mother',
  'Father',
  'Brother',
  'Sister',
  'Uncle',
  'Aunt',
  'Nephew',
  'Niece',
  'Grandfather',
  'Grandmother',
  'Other',
] as const);

// ── Contacts (addresses) — EmployeeContactDto ───────────────────────────────────

export interface EmployeeContact {
  id: string;
  employeeId: string;
  contactType: EmployeeContactType;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  region?: string | null;
  digitalAddress?: string | null;
  countryId?: string | null;
  isPrimary: boolean;
}

export interface CreateEmployeeContactRequest {
  employeeId: string;
  contactType: EmployeeContactType;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  region?: string | null;
  digitalAddress?: string | null;
  countryId?: string | null;
  isPrimary: boolean;
}

// Update DTOs across these sub-resources are patch-style: every field is optional and
// `id` is required in the body as well as the route.
export interface UpdateEmployeeContactRequest extends Partial<CreateEmployeeContactRequest> {
  id: string;
}

// ── Emergency contacts — EmployeeEmergencyContactDto ────────────────────────────

export interface EmployeeEmergencyContact {
  id: string;
  employeeId: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  relationship: string;
  contactType: EmergencyContactType;
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

export interface CreateEmployeeEmergencyContactRequest {
  employeeId: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  relationship: string;
  contactType: EmergencyContactType;
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

export interface UpdateEmployeeEmergencyContactRequest
  extends Partial<CreateEmployeeEmergencyContactRequest> {
  id: string;
}

// ── Dependents — EmployeeDependentReadDto / Create / Update ─────────────────────

export interface EmployeeDependent {
  id: string;
  employeeId: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  relationship: DependentRelationship;
  relationshipDescription?: string | null;
  /** DateOnly */
  dateOfBirth?: string | null;
  gender?: Gender | null;
  hasDisability: boolean;
  disabilityDescription?: string | null;
  ghanaCardNumber?: string | null;
  phone?: string | null;
  digitalAddress?: string | null;
  occupation?: string | null;
  isEligibleForBenefits: boolean;
  isDeceased: boolean;
  /** LEGACY caller-supplied location, kept so ported images still resolve. Prefer `hasPhoto`. */
  picturePath?: string | null;
  // ⚠ Read-only: the photograph arrives through POST employee-documents/dependants/{id}/photo.
  hasPhoto?: boolean;
  photoFileName?: string | null;
  photoMimeType?: string | null;
  photoFileSizeBytes?: number | null;
  notes?: string | null;
}

export interface CreateEmployeeDependentRequest {
  employeeId: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  relationship: DependentRelationship;
  relationshipDescription?: string | null;
  dateOfBirth?: string | null;
  gender?: Gender | null;
  hasDisability: boolean;
  disabilityDescription?: string | null;
  ghanaCardNumber?: string | null;
  phone?: string | null;
  digitalAddress?: string | null;
  occupation?: string | null;
  isEligibleForBenefits: boolean;
  isDeceased: boolean;
  notes?: string | null;
}

export interface UpdateEmployeeDependentRequest extends Partial<CreateEmployeeDependentRequest> {
  id: string;
}

// ── Dependent benefits — nested under a dependent ───────────────────────────────

export interface EmployeeDependentBenefit {
  id: string;
  employeeDependentId: string;
  policyId: string;
  policyName?: string | null;
  /** DateOnly */
  enrolledDate: string;
  coverageStartDate?: string | null;
  coverageEndDate?: string | null;
  benefitAmountUsed: number;
  isActive: boolean;
}

export interface CreateEmployeeDependentBenefitRequest {
  employeeDependentId: string;
  policyId: string;
  enrolledDate: string;
  coverageStartDate?: string | null;
  coverageEndDate?: string | null;
  benefitAmountUsed: number;
  isActive: boolean;
}

export interface UpdateEmployeeDependentBenefitRequest {
  id: string;
  coverageStartDate?: string | null;
  coverageEndDate?: string | null;
  benefitAmountUsed?: number;
  isActive?: boolean;
}

// ── Qualifications — EmployeeQualificationDto ───────────────────────────────────

export interface EmployeeQualification {
  id: string;
  employeeId: string;
  qualificationId?: string | null;
  qualificationName: string;
  customQualificationName?: string | null;
  institution: string;
  fieldOfStudy?: string | null;
  /** DateOnly */
  startDate?: string | null;
  completionDate?: string | null;
  grade?: string | null;
  countryId?: string | null;
  countryName?: string | null;
  description?: string | null;
  isVerified: boolean;
  notes?: string | null;
}

export interface CreateEmployeeQualificationRequest {
  employeeId: string;
  /** Pick from the Qualifications lookup, or leave null and supply customQualificationName. */
  qualificationId?: string | null;
  customQualificationName?: string | null;
  institution: string;
  fieldOfStudy?: string | null;
  startDate?: string | null;
  completionDate?: string | null;
  grade?: string | null;
  countryId?: string | null;
  description?: string | null;
  notes?: string | null;
}

export interface UpdateEmployeeQualificationRequest
  extends Partial<CreateEmployeeQualificationRequest> {
  id: string;
  isVerified?: boolean;
}

// ── Skills — EmployeeSkillDto ───────────────────────────────────────────────────

export interface EmployeeSkill {
  id: string;
  employeeId: string;
  skillId: string;
  skillName: string;
  skillCategory?: string | null;
  skillLevel: SkillLevel;
  /** DateOnly */
  acquiredDate?: string | null;
  certificationDate?: string | null;
  certificationExpiryDate?: string | null;
  certificationNumber?: string | null;
  /** Free text, kept for certifiers that are not in the catalogue. */
  certifyingBody?: string | null;
  /** The catalogued certifier, where there is one. */
  certifyingBodyId?: string | null;
  /** Resolved by the server — the reads Include the body, so this is not a silent null. */
  certifyingBodyName?: string | null;
  isVerified: boolean;
  isCertificationExpired: boolean;
  notes?: string | null;
  /** The credential on the certification tab that evidences this skill (round 2, lane C2). */
  employeeCertificationId?: string | null;
  employeeCertificationName?: string | null;
  requiresCertification: boolean;
  /** False when the skill requires certification and nothing valid evidences it. Flagged, not refused. */
  isCompliant: boolean;
}

export interface CreateEmployeeSkillRequest {
  employeeId: string;
  skillId: string;
  skillLevel: SkillLevel;
  acquiredDate?: string | null;
  certificationDate?: string | null;
  certificationExpiryDate?: string | null;
  certificationNumber?: string | null;
  certifyingBody?: string | null;
  certifyingBodyId?: string | null;
  /** A credential the employee holds that evidences this skill. */
  employeeCertificationId?: string | null;
  notes?: string | null;
}

export interface UpdateEmployeeSkillRequest {
  id: string;
  skillLevel?: SkillLevel;
  acquiredDate?: string | null;
  isCertified?: boolean;
  certificationDate?: string | null;
  certificationExpiryDate?: string | null;
  certificationNumber?: string | null;
  certifyingBody?: string | null;
  /**
   * ⚠ Applied UNCONDITIONALLY by the service, unlike every other field here. Those treat null as
   * "unchanged", which means they can never be cleared; for a picker that would be a trap, so this
   * one is always sent and null genuinely means "no catalogued body".
   */
  certifyingBodyId?: string | null;
  /** Same rule as certifyingBodyId: always sent, applied unconditionally, null clears it. */
  employeeCertificationId?: string | null;
  notes?: string | null;
  isVerified?: boolean;
}

// ── Identification cards ────────────────────────────────────────────────────────

export interface EmployeeIdentificationCard {
  id: string;
  employeeId: string;
  identificationTypeId: string;
  identificationTypeName: string;
  cardTypeName: string;
  cardNumber: string;
  /** The list projection masks the number; the detail projection carries documentNumber. */
  documentNumberMasked?: string | null;
  documentNumber?: string;
  /** DateOnly */
  issueDate?: string | null;
  expiryDate?: string | null;
  issuingAuthority?: string | null;
  countryId?: string | null;
  countryName?: string | null;
  isVerified: boolean;
  /** DateTime */
  verifiedDate?: string | null;
  documentPath?: string | null;
  notes?: string | null;
}

export interface CreateEmployeeIdentificationCardRequest {
  employeeId: string;
  identificationTypeId: string;
  documentNumber: string;
  issueDate?: string | null;
  expiryDate?: string | null;
  documentPath?: string | null;
  isVerified: boolean;
  verifiedDate?: string | null;
  notes?: string | null;
}

export interface UpdateEmployeeIdentificationCardRequest
  extends Partial<CreateEmployeeIdentificationCardRequest> {
  id: string;
}

/** Body for POST .../identification-cards/{id}/verify. DateTime. */
export interface VerifyIdentificationCardRequest {
  verifiedDate: string;
}

// ── Work histories ──────────────────────────────────────────────────────────────

export interface EmployeeWorkHistory {
  id: string;
  employeeId: string;
  companyName: string;
  jobTitle: string;
  /** DateOnly */
  startDate: string;
  endDate?: string | null;
  companyAddress?: string | null;
  jobDescription?: string | null;
  salary?: number | null;
  reasonForLeaving?: string | null;
  supervisorName?: string | null;
  supervisorPhone?: string | null;
  canContact?: boolean;
}

export interface CreateEmployeeWorkHistoryRequest {
  employeeId: string;
  companyName: string;
  companyAddress?: string | null;
  jobTitle: string;
  jobDescription?: string | null;
  startDate: string;
  endDate?: string | null;
  salary?: number | null;
  reasonForLeaving?: string | null;
  supervisorName?: string | null;
  supervisorPhone?: string | null;
  canContact: boolean;
}

export interface UpdateEmployeeWorkHistoryRequest
  extends Partial<CreateEmployeeWorkHistoryRequest> {
  id: string;
}

// ── Contracts ───────────────────────────────────────────────────────────────────

export interface EmployeeContract {
  id: string;
  employeeId: string;
  contractNumber: string;
  employmentType: EmploymentType;
  /** The tenant's own name for this kind of engagement. Not the same axis as employmentType. */
  contractTypeId?: string | null;
  contractTypeName?: string | null;
  /** DateOnly */
  startDate: string;
  /**
   * The day these terms took effect — what the list is ordered on and what supersession runs on.
   *
   * ⚠ Reads `0001-01-01` on rows added through this tab before 2026-09-09: the writer never set it.
   */
  effectiveDate: string;
  /** When the engagement ACTUALLY ended. Null while it is running. */
  endDate?: string | null;
  /** When the engagement is SCHEDULED to end. Null for permanent employment. */
  contractEndDate?: string | null;
  /** Whether these are the terms in force today. At most one contract per employee carries it. */
  isCurrent: boolean;
  salary: number;
  payFrequency: string;
  payFrequencyType?: PayFrequency | null;
  taxTreatmentType?: TaxTreatmentType | null;
  withholdingTaxRate?: number | null;
  isPensionApplicable?: boolean | null;
  isTaxExempt?: boolean | null;
  contractStatus?: ContractStatus | null;
  workingHoursPerWeek: number;
  /** The annual leave the contract grants. Reachable from nowhere at all before 2026-09-09. */
  annualLeaveEntitlementDays: number;
  vacationDaysPerYear: number;
  sickDaysPerYear: number;
  /**
   * The probation term and the date it was passed.
   *
   * ⚠ Both were settable on create and update and readable NOWHERE until 2026-09-01, so the edit
   * form hardcoded them blank. Instrument 03 cannot see this class of gap: it scans Create/Update
   * DTOs, not read ones.
   */
  probationPeriodDays?: number | null;
  confirmationDate?: string | null;
  /** The currency the salary is in. A salary was previously shown as a bare number. */
  currencyCode?: string | null;
  /** Full time, part time, shift, flexi, remote, hybrid — not the same axis as employmentType. */
  workSchedule?: WorkArrangementType | null;
  terms?: string | null;
  specialConditions?: string | null;
  notes?: string | null;
  isActive: boolean;
  contractPath?: string | null;
  terminationDate?: string | null;
  terminationReason?: string | null;
}

/** HREnums.cs `WorkArrangementType` — 1..6. */
export type WorkArrangementType =
  | 'FullTime'
  | 'PartTime'
  | 'Shift'
  | 'Flexi'
  | 'Remote'
  | 'Hybrid';

export interface CreateEmployeeContractRequest {
  employeeId: string;
  contractNumber: string;
  employmentType: EmploymentType;
  contractTypeId?: string | null;
  startDate: string;
  /** Defaults to `startDate` when omitted. */
  effectiveDate?: string | null;
  endDate?: string | null;
  /** Defaults from the contract type's duration when a kind is named and no date is given. */
  contractEndDate?: string | null;
  /**
   * ⚠ Optional since lane E1 (§ 6.5.4): basic pay and its tax treatment are payroll's, hosted on
   * the Salary tab, and the contract dialog no longer captures them. The columns stay on the
   * entity for the rows that hold them; an omitted value leaves a row's figure untouched.
   */
  salary?: number;
  payFrequency?: PayFrequency;
  taxTreatmentType?: TaxTreatmentType;
  withholdingTaxRate?: number | null;
  isPensionApplicable?: boolean;
  isTaxExempt?: boolean;
  workingHoursPerWeek: number;
  annualLeaveEntitlementDays?: number | null;
  vacationDaysPerYear: number;
  sickDaysPerYear: number;
  probationPeriodDays?: number | null;
  confirmationDate?: string | null;
  terms?: string | null;
  isActive: boolean;
  contractPath?: string | null;
  contractStatus: ContractStatus;
  terminationDate?: string | null;
  terminationReason?: string | null;
}

export interface UpdateEmployeeContractRequest extends Partial<CreateEmployeeContractRequest> {
  id: string;
}

/** Body for POST .../contracts/{id}/terminate. terminationDate is DateOnly. */
export interface TerminateContractRequest {
  terminationDate: string;
  reason: string;
}

// ── Expatriate assignments ──────────────────────────────────────────────────────

/** A family member accompanying an expatriate assignee. */
export interface ExpatriateFamilyMember {
  id: string;
  expatriateAssignmentId: string;
  fullName: string;
  /** ⚠ The SAME enum a dependant uses, with the same description-for-Other companion. */
  relationship: string;
  relationshipDescription?: string | null;
  gender?: string | null;
  genderDescription?: string | null;
  dateOfBirth?: string | null;
  passportNumber?: string | null;
  passportExpiryDate?: string | null;
  /** Their own residence permit — separate from the assignee's. */
  residentPermitNumber?: string | null;
  residentPermitIssueDate?: string | null;
  residentPermitExpiryDate?: string | null;
  arrivalDate?: string | null;
  departureDate?: string | null;
  notes?: string | null;
}

export interface ExpatriateAssignment {
  /**
   * ⚠ Populated by the DETAIL read only — the list read is a summary and returns none. A panel
   * that binds to a list row would render "nobody accompanied them" for a posting with three.
   */
  familyMembers?: ExpatriateFamilyMember[];
  id: string;
  employeeId: string;
  homeCountryId: string;
  homeCountryName?: string | null;
  /** DateOnly */
  startDate: string;
  endDate?: string | null;
  familyAccompanying: boolean;
  relocationAllowance?: number | null;
  relocationDate?: string | null;
  assignmentObjective?: string | null;
  visaType?: string | null;
  /** ⚠ When the visa was ISSUED — every permit carried an expiry and no issue date. */
  visaIssueDate?: string | null;
  visaExpiryDate?: string | null;
  workPermitNumber?: string | null;
  workPermitIssueDate?: string | null;
  workPermitExpiryDate?: string | null;
  /** ⚠ A different instrument from the work permit, on a different authority's clock. */
  residentPermitNumber?: string | null;
  residentPermitIssueDate?: string | null;
  residentPermitExpiryDate?: string | null;
}

export interface CreateExpatriateAssignmentRequest {
  employeeId: string;
  homeCountryId: string;
  startDate: string;
  endDate?: string | null;
  relocationAllowance?: number | null;
  relocationDate?: string | null;
  familyAccompanying: boolean;
  assignmentObjective?: string | null;
  visaType?: string | null;
  /** ⚠ When the visa was ISSUED — every permit carried an expiry and no issue date. */
  visaIssueDate?: string | null;
  visaExpiryDate?: string | null;
  workPermitNumber?: string | null;
  workPermitIssueDate?: string | null;
  workPermitExpiryDate?: string | null;
  /** ⚠ A different instrument from the work permit, on a different authority's clock. */
  residentPermitNumber?: string | null;
  residentPermitIssueDate?: string | null;
  residentPermitExpiryDate?: string | null;
}

export interface UpdateExpatriateAssignmentRequest
  extends Partial<CreateExpatriateAssignmentRequest> {
  id: string;
}

// ── Position histories ──────────────────────────────────────────────────────────

export interface EmployeePositionHistory {
  id: string;
  employeeId: string;
  positionId: string;
  positionTitle?: string | null;
  /** DateTime (not DateOnly) on this resource. */
  startDate: string;
  endDate?: string | null;
  changeReason: PositionChangeReason;
  isCurrent: boolean;
  locationLevelId?: string | null;
  locationId?: string | null;
  organizationLevelId?: string;
  organizationUnitId?: string | null;
  notes?: string | null;
}

export interface CreateEmployeePositionHistoryRequest {
  employeeId: string;
  locationLevelId?: string | null;
  locationId?: string | null;
  organizationLevelId: string;
  organizationUnitId?: string | null;
  positionId: string;
  startDate: string;
  endDate?: string | null;
  changeReason: PositionChangeReason;
  notes?: string | null;
}

export interface UpdateEmployeePositionHistoryRequest
  extends Partial<CreateEmployeePositionHistoryRequest> {
  id: string;
}

// ── Salary assignments ──────────────────────────────────────────────────────────
// Grade/level/notch come from the payroll-defined structure mirrored into HR.

export interface EmployeeSalaryAssignment {
  id: string;
  employeeId: string;
  gradeId: string;
  gradeCode?: string | null;
  gradeName?: string | null;
  levelId?: string | null;
  levelCode?: string | null;
  notchId?: string | null;
  notchNumber?: string | null;
  /** DateTime */
  effectiveDate: string;
  effectiveTo?: string | null;
  reason?: string | null;
  /** In force TODAY: taken effect, not ended, not withdrawn. */
  isActive: boolean;
  /** Dated forward and not withdrawn — it has not taken effect yet. */
  isScheduled: boolean;
  /**
   * Withdrawn rather than superseded or run to its end.
   *
   * ⚠ A placement withdrawn before its start date has NO end date — it was never in force, so it
   * has no window. This is the only thing that marks it, and every as-of read excludes it.
   */
  withdrawnAt?: string | null;
  withdrawnReason?: string | null;
  amount?: number | null;
  assignmentReason?: string;
}

export interface CreateEmployeeSalaryAssignmentRequest {
  employeeId: string;
  gradeId: string;
  levelId?: string | null;
  notchId?: string | null;
  effectiveDate: string;
  effectiveTo?: string | null;
  assignmentReason: string;
}

export interface UpdateEmployeeSalaryAssignmentRequest
  extends Partial<CreateEmployeeSalaryAssignmentRequest> {
  id: string;
}

// ── Referees ────────────────────────────────────────────────────────────────────

export interface EmployeeReferee {
  id: string;
  employeeId: string;
  refereeType: RefereeType;
  fullName: string;
  organization?: string | null;
  positionOrTitle?: string | null;
  relationship: string;
  phoneNumber: string;
  emailAddress?: string | null;
  isPrimary: boolean;
  isActive: boolean;
  isContacted?: boolean;
  /** DateTime */
  contactedDate?: string | null;
  referenceNotes?: string | null;
  // ⚠ Read-only: the written reference arrives through POST employee-documents/referees/{id}/letter
  // and the gate fills these in. On the LIST projection since round 2 so the tab can show it.
  hasLetter?: boolean;
  letterFileName?: string | null;
  letterMimeType?: string | null;
  letterFileSizeBytes?: number | null;
}

export interface CreateEmployeeRefereeRequest {
  employeeId: string;
  refereeType: RefereeType;
  fullName: string;
  organization?: string | null;
  positionOrTitle?: string | null;
  relationship: string;
  phoneNumber: string;
  emailAddress?: string | null;
  isPrimary: boolean;
  isActive: boolean;
}

export interface UpdateEmployeeRefereeRequest extends Partial<CreateEmployeeRefereeRequest> {
  id: string;
  isContacted?: boolean;
  contactedDate?: string | null;
  referenceNotes?: string | null;
}

// ── Guarantors ──────────────────────────────────────────────────────────────────

export interface EmployeeGuarantor {
  // ⚠ Read-only: the photograph arrives through POST employee-documents/guarantors/{id}/photo.
  hasPhoto?: boolean;
  photoFileName?: string | null;
  photoMimeType?: string | null;
  photoFileSizeBytes?: number | null;
  id: string;
  employeeId: string;
  isPrimary: boolean;
  relationship: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  title?: string | null;
  gender?: Gender | null;
  /** DateOnly */
  dateOfBirth?: string | null;
  address?: string;
  city?: string | null;
  digitalAddress?: string | null;
  countryId?: string | null;
  phoneNumber?: string | null;
  emailAddress?: string | null;
  jobTitle?: string | null;
  employerName?: string | null;
  employerAddress?: string | null;
  employerPhone?: string | null;
  monthlyIncome?: number | null;
  /** ⚠ What they stand surety FOR — distinct from monthlyIncome, which is what they earn. */
  amountGuaranteed?: number | null;
  /** Validated against Finance's currency master. Null means HR's configured default. */
  amountGuaranteedCurrencyCode?: string | null;
  /** How the guarantor describes their gender, where gender is Other. */
  genderDescription?: string | null;
  /** Free-text kind, for rows recorded before the catalogue link. Prefer `nationalIdTypeId`. */
  nationalIdType?: string | null;
  /** The kind from the identification-type catalogue (round 2, E-13). On the list projection. */
  nationalIdTypeId?: string | null;
  nationalIdTypeName?: string | null;
  /** How many documents pertain to this guarantor. On the list projection. */
  documentCount?: number;
  /** The detail projection masks the ID number; writes use nationalIdNumber. */
  nationalIdNumberMasked?: string | null;
  nationalIdExpiryDate?: string | null;
  hasSignedGuarantorForm?: boolean;
  dateFormSigned?: string | null;
  /** LEGACY, read-only. The signed form is now a guarantor document through the gate. */
  guarantorFormPath?: string | null;
  isVerified: boolean;
  /** DateTime */
  verificationDate?: string | null;
  verifiedByEmployeeId?: string | null;
  isActive: boolean;
  notes?: string | null;
  lastContactDate?: string | null;
}

export interface CreateEmployeeGuarantorRequest {
  employeeId: string;
  isPrimary: boolean;
  relationship: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  title?: string | null;
  gender?: Gender | null;
  dateOfBirth?: string | null;
  address: string;
  city?: string | null;
  digitalAddress?: string | null;
  countryId?: string | null;
  phoneNumber?: string | null;
  emailAddress?: string | null;
  jobTitle?: string | null;
  employerName?: string | null;
  employerAddress?: string | null;
  employerPhone?: string | null;
  monthlyIncome?: number | null;
  /** ⚠ What they stand surety FOR — distinct from monthlyIncome, which is what they earn. */
  amountGuaranteed?: number | null;
  /** Validated against Finance's currency master. Null means HR's configured default. */
  amountGuaranteedCurrencyCode?: string | null;
  /** How the guarantor describes their gender, where gender is Other. */
  genderDescription?: string | null;
  nationalIdType?: string | null;
  /** The catalogue kind. Refused if unknown or inactive. */
  nationalIdTypeId?: string | null;
  nationalIdNumber?: string | null;
  nationalIdExpiryDate?: string | null;
  hasSignedGuarantorForm: boolean;
  dateFormSigned?: string | null;
  // ⚠ No guarantorFormPath: removed in round 2. The signed form is uploaded as a document.
  notes?: string | null;
  isActive: boolean;
}

export interface UpdateEmployeeGuarantorRequest extends Partial<CreateEmployeeGuarantorRequest> {
  id: string;
  /** A null id means "not supplied" on the update DTO, so unlinking has to say so explicitly. */
  clearNationalIdType?: boolean;
}

/** Body for POST .../guarantors/{id}/verify. verifiedDate is a DateTime. */
export interface VerifyGuarantorRequest {
  verifiedByEmployeeId: string;
  verifiedDate: string;
}

// ── Bank details ────────────────────────────────────────────────────────────────

export interface EmployeeBankDetail {
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
  accountType: EmployeeBankAccountType;
  allocationPercentage: number;
  isPrimary: boolean;
  isActive: boolean;
  isVerified: boolean;
  /** DateTime */
  verifiedDate?: string | null;
  verifiedById?: string | null;
}

export interface CreateEmployeeBankDetailRequest {
  employeeId: string;
  bankId?: string | null;
  branchId?: string | null;
  bankName: string;
  branchName: string;
  accountNumber: string;
  accountName: string;
  mobileMoneyNumber?: string | null;
  accountType: EmployeeBankAccountType;
  allocationPercentage: number;
  isPrimary: boolean;
}

export interface UpdateEmployeeBankDetailRequest extends Partial<CreateEmployeeBankDetailRequest> {
  id: string;
  isActive?: boolean;
  /** Unlinks the catalogue bank and branch so typed names stand alone (null id = "not supplied"). */
  clearBankLink?: boolean;
}
