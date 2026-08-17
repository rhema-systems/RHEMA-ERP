// HR area 11 — Medical & Health. Mirrors Core/DTOs/HR/MedicalDTOs.cs.
//
// This file covers the REFERENCE & CONFIG surface (slice 4): the facility and physician
// registers, insurance providers and their plans, and benefit schemes and their tiers.
// Claims, health records and clinical types arrive with slices 5–7.
//
// ⚠ Access: the medical controllers are gated on the HR.Medical.* permission policies, not a
// role. A 403 means the user lacks medical permissions specifically — HR staff hold Read and
// Write but NOT Admin, so deletes are refused for them by design.
//
// ⚠ Facilities, physicians and facility services are the exception: their READS are open to any
// authenticated user, because an employee filing their own claim has to name a facility. Writes
// and deletes still require medical permissions.
//
// Enums are serialised by the API as strings, not numbers.

// ── Healthcare facilities ────────────────────────────────────────────────────

export type HealthFacilityType =
  | 'GeneralHospital'
  | 'SpecializedHospital'
  | 'TeachingHospital'
  | 'Clinic'
  | 'Polyclinic'
  | 'MedicalCenter'
  | 'Pharmacy'
  | 'DiagnosticCenter'
  | 'Laboratory'
  | 'ImagingCenter'
  | 'UrgentCare'
  | 'DaySurgeryCenter'
  | 'RehabilitationCenter'
  | 'MaternityHome'
  | 'DentalClinic'
  | 'OpticalCenter';

export const HEALTH_FACILITY_TYPE_OPTIONS: { value: HealthFacilityType; label: string }[] = [
  { value: 'GeneralHospital', label: 'General hospital' },
  { value: 'SpecializedHospital', label: 'Specialized hospital' },
  { value: 'TeachingHospital', label: 'Teaching hospital' },
  { value: 'Clinic', label: 'Clinic' },
  { value: 'Polyclinic', label: 'Polyclinic' },
  { value: 'MedicalCenter', label: 'Medical centre' },
  { value: 'Pharmacy', label: 'Pharmacy' },
  { value: 'DiagnosticCenter', label: 'Diagnostic centre' },
  { value: 'Laboratory', label: 'Laboratory' },
  { value: 'ImagingCenter', label: 'Imaging centre' },
  { value: 'UrgentCare', label: 'Urgent care' },
  { value: 'DaySurgeryCenter', label: 'Day surgery centre' },
  { value: 'RehabilitationCenter', label: 'Rehabilitation centre' },
  { value: 'MaternityHome', label: 'Maternity home' },
  { value: 'DentalClinic', label: 'Dental clinic' },
  { value: 'OpticalCenter', label: 'Optical centre' },
];

export interface HealthcareFacilitySummary {
  id: string;
  facilityName: string;
  facilityCode: string;
  facilityType: HealthFacilityType;
  city?: string | null;
  primaryPhone?: string | null;
  hasEmergencyServices: boolean;
  acceptsNHIS: boolean;
  isActive: boolean;
}

export interface HealthcareFacility extends HealthcareFacilitySummary {
  shortName?: string | null;
  licenseNumber?: string | null;
  licenseExpiryDate?: string | null;
  physicalAddress: string;
  digitalAddress?: string | null;
  postalCode?: string | null;
  emergencyPhone?: string | null;
  email?: string | null;
  website?: string | null;
  description?: string | null;
  has24HourService: boolean;
  hasAmbulanceService: boolean;
  hasLaboratory: boolean;
  hasPharmacy: boolean;
  nhisAccreditationNumber?: string | null;
  contactPersonName?: string | null;
  contactPersonPhone?: string | null;
  contactPersonEmail?: string | null;
  operatingHours?: string | null;
  notes?: string | null;
}

export interface HealthcareFacilityCreateRequest {
  facilityName: string;
  shortName?: string | null;
  facilityCode: string;
  facilityType: HealthFacilityType;
  licenseNumber?: string | null;
  physicalAddress: string;
  digitalAddress?: string | null;
  city?: string | null;
  primaryPhone?: string | null;
  emergencyPhone?: string | null;
  email?: string | null;
  hasEmergencyServices: boolean;
  has24HourService: boolean;
  hasAmbulanceService: boolean;
  hasLaboratory: boolean;
  hasPharmacy: boolean;
  acceptsNHIS: boolean;
  nhisAccreditationNumber?: string | null;
  contactPersonName?: string | null;
  contactPersonPhone?: string | null;
  operatingHours?: string | null;
  isActive: boolean;
  notes?: string | null;
}

export interface HealthcareFacilityUpdateRequest extends HealthcareFacilityCreateRequest {
  id: string;
}

// ── Physicians ───────────────────────────────────────────────────────────────

export interface PhysicianSummary {
  id: string;
  fullName: string;
  title?: string | null;
  specialization?: string | null;
  phoneNumber?: string | null;
  facilityName?: string | null;
  isActive: boolean;
  /** Set by the verify action once the licence has been checked. */
  isVerified: boolean;
}

export interface Physician extends PhysicianSummary {
  firstName: string;
  lastName: string;
  middleName?: string | null;
  medicalLicenseNumber?: string | null;
  licenseExpiryDate?: string | null;
  email?: string | null;
  facilityId?: string | null;
  notes?: string | null;
}

export interface PhysicianCreateRequest {
  firstName: string;
  lastName: string;
  middleName?: string | null;
  title?: string | null;
  specialization?: string | null;
  medicalLicenseNumber?: string | null;
  licenseExpiryDate?: string | null;
  phoneNumber?: string | null;
  email?: string | null;
  facilityId?: string | null;
  isActive: boolean;
  notes?: string | null;
}

export interface PhysicianUpdateRequest extends PhysicianCreateRequest {
  id: string;
}

// ── Insurance providers and plans ────────────────────────────────────────────

export type MedicalInsuranceProviderType =
  | 'HealthInsurance'
  | 'LifeInsurance'
  | 'HMO'
  | 'PPO'
  | 'NationalHealthInsurance'
  | 'PrivateHealthInsurance'
  | 'GroupInsurance'
  | 'TravelInsurance'
  | 'DentalInsurance'
  | 'VisionInsurance';

export const MEDICAL_PROVIDER_TYPE_OPTIONS: {
  value: MedicalInsuranceProviderType;
  label: string;
}[] = [
  { value: 'HealthInsurance', label: 'Health insurance' },
  { value: 'LifeInsurance', label: 'Life insurance' },
  { value: 'HMO', label: 'HMO' },
  { value: 'PPO', label: 'PPO' },
  { value: 'NationalHealthInsurance', label: 'National health insurance' },
  { value: 'PrivateHealthInsurance', label: 'Private health insurance' },
  { value: 'GroupInsurance', label: 'Group insurance' },
  { value: 'TravelInsurance', label: 'Travel insurance' },
  { value: 'DentalInsurance', label: 'Dental insurance' },
  { value: 'VisionInsurance', label: 'Vision insurance' },
];

export type MedicalInsurancePlanType =
  | 'BasicPlan'
  | 'StandardPlan'
  | 'PremiumPlan'
  | 'ExecutivePlan'
  | 'FamilyPlan'
  | 'IndividualPlan'
  | 'CorporatePlan'
  | 'StudentPlan'
  | 'SeniorPlan'
  | 'MaternityPlan'
  | 'CatastrophicPlan'
  | 'CustomPlan';

export const MEDICAL_PLAN_TYPE_OPTIONS: { value: MedicalInsurancePlanType; label: string }[] = [
  { value: 'BasicPlan', label: 'Basic' },
  { value: 'StandardPlan', label: 'Standard' },
  { value: 'PremiumPlan', label: 'Premium' },
  { value: 'ExecutivePlan', label: 'Executive' },
  { value: 'FamilyPlan', label: 'Family' },
  { value: 'IndividualPlan', label: 'Individual' },
  { value: 'CorporatePlan', label: 'Corporate' },
  { value: 'StudentPlan', label: 'Student' },
  { value: 'SeniorPlan', label: 'Senior' },
  { value: 'MaternityPlan', label: 'Maternity' },
  { value: 'CatastrophicPlan', label: 'Catastrophic' },
  { value: 'CustomPlan', label: 'Custom' },
];

export interface MedicalInsuranceProviderSummary {
  id: string;
  name: string;
  code: string;
  providerType: MedicalInsuranceProviderType;
  primaryPhone: string;
  isActive: boolean;
  planCount: number;
}

export interface MedicalInsuranceProvider extends MedicalInsuranceProviderSummary {
  shortName?: string | null;
  licenseNumber: string;
  licenseExpiryDate?: string | null;
  address: string;
  city?: string | null;
  claimsHotline?: string | null;
  email: string;
  claimsEmail?: string | null;
  website?: string | null;
  standardProcessingDays: number;
  emergencyProcessingDays?: number | null;
  claimSubmissionDeadlineDays: number;
  claimSubmissionProcess?: string | null;
  notes?: string | null;
}

export interface MedicalInsuranceProviderCreateRequest {
  name: string;
  shortName?: string | null;
  code: string;
  providerType: MedicalInsuranceProviderType;
  licenseNumber: string;
  address: string;
  city?: string | null;
  primaryPhone: string;
  claimsHotline?: string | null;
  email: string;
  claimsEmail?: string | null;
  website?: string | null;
  standardProcessingDays: number;
  claimSubmissionDeadlineDays: number;
  isActive: boolean;
  notes?: string | null;
}

export interface MedicalInsuranceProviderUpdateRequest
  extends MedicalInsuranceProviderCreateRequest {
  id: string;
}

export interface MedicalInsurancePlan {
  id: string;
  medicalInsuranceProviderId: string;
  name: string;
  code: string;
  planType: MedicalInsurancePlanType;
  description?: string | null;
  annualLimit: number;
  outpatientLimit?: number | null;
  inpatientLimit?: number | null;
  dentalLimit?: number | null;
  opticalLimit?: number | null;
  maternityLimit?: number | null;
  coversDependents: boolean;
  maxDependents?: number | null;
  monthlyPremium?: number | null;
  annualPremium?: number | null;
  effectiveDate: string;
  expiryDate?: string | null;
  isActive: boolean;
  notes?: string | null;
}

export interface MedicalInsurancePlanCreateRequest {
  medicalInsuranceProviderId: string;
  name: string;
  code: string;
  planType: MedicalInsurancePlanType;
  description?: string | null;
  annualLimit: number;
  outpatientLimit?: number | null;
  inpatientLimit?: number | null;
  dentalLimit?: number | null;
  opticalLimit?: number | null;
  maternityLimit?: number | null;
  coversDependents: boolean;
  maxDependents?: number | null;
  monthlyPremium?: number | null;
  annualPremium?: number | null;
  effectiveDate: string;
  expiryDate?: string | null;
  isActive: boolean;
  notes?: string | null;
}

export interface MedicalInsurancePlanUpdateRequest extends MedicalInsurancePlanCreateRequest {
  id: string;
}

// ── Benefit schemes and tiers ────────────────────────────────────────────────

export interface MedicalBenefitSchemeSummary {
  id: string;
  name: string;
  code: string;
  effectiveDate: string;
  isActive: boolean;
  tierCount: number;
}

export interface MedicalBenefitScheme extends MedicalBenefitSchemeSummary {
  description?: string | null;
  expiryDate?: string | null;
  notes?: string | null;
}

export interface MedicalBenefitSchemeCreateRequest {
  name: string;
  code: string;
  description?: string | null;
  effectiveDate: string;
  expiryDate?: string | null;
  isActive: boolean;
  notes?: string | null;
}

export interface MedicalBenefitSchemeUpdateRequest extends MedicalBenefitSchemeCreateRequest {
  id: string;
}

/**
 * A tier is what an employee at a given staff level is entitled to under a scheme. The
 * sub-limits are optional caps within the annual limit, not additions to it.
 */
export interface MedicalBenefitTier {
  id: string;
  schemeId: string;
  schemeName: string;
  tierName: string;
  staffLevelId?: string | null;
  staffLevelName?: string | null;
  tierDescription?: string | null;
  annualLimit: number;
  inpatientLimit?: number | null;
  outpatientLimit?: number | null;
  dentalLimit?: number | null;
  opticalLimit?: number | null;
  maternityLimit?: number | null;
  mentalHealthLimit?: number | null;
  prescriptionLimit?: number | null;
  coversDependents: boolean;
  maxDependents?: number | null;
  dependentAnnualLimit?: number | null;
  isActive: boolean;
  notes?: string | null;
}

export interface MedicalBenefitTierCreateRequest {
  schemeId: string;
  tierName: string;
  staffLevelId?: string | null;
  tierDescription?: string | null;
  annualLimit: number;
  inpatientLimit?: number | null;
  outpatientLimit?: number | null;
  dentalLimit?: number | null;
  opticalLimit?: number | null;
  maternityLimit?: number | null;
  mentalHealthLimit?: number | null;
  prescriptionLimit?: number | null;
  coversDependents: boolean;
  maxDependents?: number | null;
  dependentAnnualLimit?: number | null;
  isActive: boolean;
  notes?: string | null;
}

export interface MedicalBenefitTierUpdateRequest extends MedicalBenefitTierCreateRequest {
  id: string;
}

// ── Employee health records (slice 5) ────────────────────────────────────────
//
// Special-category personal data. Unlike the registers above, NOTHING here is readable without
// HR.Medical.Read — an employee sees their own file through the self-service surface instead.

export type BloodGroup =
  | 'APositive'
  | 'ANegative'
  | 'BPositive'
  | 'BNegative'
  | 'ABPositive'
  | 'ABNegative'
  | 'OPositive'
  | 'ONegative'
  | 'Unknown';

export const BLOOD_GROUP_OPTIONS: { value: BloodGroup; label: string }[] = [
  { value: 'APositive', label: 'A+' },
  { value: 'ANegative', label: 'A−' },
  { value: 'BPositive', label: 'B+' },
  { value: 'BNegative', label: 'B−' },
  { value: 'ABPositive', label: 'AB+' },
  { value: 'ABNegative', label: 'AB−' },
  { value: 'OPositive', label: 'O+' },
  { value: 'ONegative', label: 'O−' },
  { value: 'Unknown', label: 'Not known' },
];

export type DisabilityStatus = 'None' | 'Mild' | 'Moderate' | 'Severe';

export const DISABILITY_STATUS_OPTIONS: { value: DisabilityStatus; label: string }[] = [
  { value: 'None', label: 'None' },
  { value: 'Mild', label: 'Mild' },
  { value: 'Moderate', label: 'Moderate' },
  { value: 'Severe', label: 'Severe' },
];

export interface EmployeeHealthProfile {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  bloodGroup: BloodGroup;
  heightCm?: number | null;
  weightKg?: number | null;
  disabilityStatus: DisabilityStatus;
  disabilityDescription?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  emergencyContactRelationship?: string | null;
  preferredFacilityId?: string | null;
  preferredFacilityName?: string | null;
  preferredPhysicianId?: string | null;
  preferredPhysicianName?: string | null;
  lastUpdated?: string | null;
  notes?: string | null;
}

export interface EmployeeHealthProfileCreateRequest {
  employeeId: string;
  bloodGroup: BloodGroup;
  heightCm?: number | null;
  weightKg?: number | null;
  disabilityStatus: DisabilityStatus;
  disabilityDescription?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  emergencyContactRelationship?: string | null;
  preferredFacilityId?: string | null;
  preferredPhysicianId?: string | null;
  notes?: string | null;
}

/** The update DTO drops employeeId — a profile cannot be moved to another employee. */
export interface EmployeeHealthProfileUpdateRequest
  extends Omit<EmployeeHealthProfileCreateRequest, 'employeeId'> {
  id: string;
}

// ── Conditions ───────────────────────────────────────────────────────────────

export type HealthConditionSeverity = 'Mild' | 'Moderate' | 'Severe' | 'Critical';
export type HealthConditionStatus = 'Active' | 'Managed' | 'Resolved' | 'InRemission';

export const HEALTH_CONDITION_SEVERITY_OPTIONS: {
  value: HealthConditionSeverity;
  label: string;
}[] = [
  { value: 'Mild', label: 'Mild' },
  { value: 'Moderate', label: 'Moderate' },
  { value: 'Severe', label: 'Severe' },
  { value: 'Critical', label: 'Critical' },
];

export const HEALTH_CONDITION_STATUS_OPTIONS: { value: HealthConditionStatus; label: string }[] = [
  { value: 'Active', label: 'Active' },
  { value: 'Managed', label: 'Managed' },
  { value: 'InRemission', label: 'In remission' },
  { value: 'Resolved', label: 'Resolved' },
];

export interface EmployeeHealthCondition {
  id: string;
  healthProfileId: string;
  conditionName: string;
  icdCode?: string | null;
  severity: HealthConditionSeverity;
  status: HealthConditionStatus;
  diagnosedDate?: string | null;
  resolvedDate?: string | null;
  treatmentSummary?: string | null;
  notes?: string | null;
}

export interface EmployeeHealthConditionCreateRequest {
  healthProfileId: string;
  conditionName: string;
  icdCode?: string | null;
  severity: HealthConditionSeverity;
  status: HealthConditionStatus;
  diagnosedDate?: string | null;
  resolvedDate?: string | null;
  treatmentSummary?: string | null;
  notes?: string | null;
}

export interface EmployeeHealthConditionUpdateRequest
  extends EmployeeHealthConditionCreateRequest {
  id: string;
}

// ── Allergies ────────────────────────────────────────────────────────────────

export type AllergyType = 'Drug' | 'Food' | 'Environmental' | 'Latex' | 'Insect' | 'Other';
export type AllergySeverity = 'Mild' | 'Moderate' | 'Severe' | 'Anaphylactic';

export const ALLERGY_TYPE_OPTIONS: { value: AllergyType; label: string }[] = [
  { value: 'Drug', label: 'Drug' },
  { value: 'Food', label: 'Food' },
  { value: 'Environmental', label: 'Environmental' },
  { value: 'Latex', label: 'Latex' },
  { value: 'Insect', label: 'Insect' },
  { value: 'Other', label: 'Other' },
];

export const ALLERGY_SEVERITY_OPTIONS: { value: AllergySeverity; label: string }[] = [
  { value: 'Mild', label: 'Mild' },
  { value: 'Moderate', label: 'Moderate' },
  { value: 'Severe', label: 'Severe' },
  { value: 'Anaphylactic', label: 'Anaphylactic' },
];

export interface EmployeeAllergy {
  id: string;
  healthProfileId: string;
  allergen: string;
  allergyType: AllergyType;
  severity: AllergySeverity;
  reactionDescription?: string | null;
  managementPlan?: string | null;
  isActive: boolean;
  notes?: string | null;
}

export interface EmployeeAllergyCreateRequest {
  healthProfileId: string;
  allergen: string;
  allergyType: AllergyType;
  severity: AllergySeverity;
  reactionDescription?: string | null;
  managementPlan?: string | null;
  isActive: boolean;
  notes?: string | null;
}

export interface EmployeeAllergyUpdateRequest extends EmployeeAllergyCreateRequest {
  id: string;
}

// ── Medical examinations ─────────────────────────────────────────────────────

export type MedicalExamResult =
  | 'Fit'
  | 'FitWithRestrictions'
  | 'TemporarilyUnfit'
  | 'Unfit'
  | 'RequiresFurtherInvestigation';

export const MEDICAL_EXAM_RESULT_OPTIONS: { value: MedicalExamResult; label: string }[] = [
  { value: 'Fit', label: 'Fit' },
  { value: 'FitWithRestrictions', label: 'Fit with restrictions' },
  { value: 'TemporarilyUnfit', label: 'Temporarily unfit' },
  { value: 'Unfit', label: 'Unfit' },
  { value: 'RequiresFurtherInvestigation', label: 'Needs further investigation' },
];

export interface EmployeeMedicalExamSummary {
  id: string;
  examDate: string;
  facilityName?: string | null;
  result: MedicalExamResult;
  nextExamDueDate?: string | null;
}

export interface EmployeeMedicalExam extends EmployeeMedicalExamSummary {
  healthProfileId: string;
  facilityId?: string | null;
  physicianId?: string | null;
  heightCm?: number | null;
  weightKg?: number | null;
  bloodPressure?: string | null;
  visionResult?: string | null;
  hearingResult?: string | null;
  findings?: string | null;
  recommendations?: string | null;
  restrictions?: string | null;
  notes?: string | null;
}

export interface EmployeeMedicalExamCreateRequest {
  healthProfileId: string;
  examDate: string;
  facilityId?: string | null;
  physicianId?: string | null;
  heightCm?: number | null;
  weightKg?: number | null;
  bloodPressure?: string | null;
  visionResult?: string | null;
  hearingResult?: string | null;
  result: MedicalExamResult;
  findings?: string | null;
  recommendations?: string | null;
  restrictions?: string | null;
  nextExamDueDate?: string | null;
  notes?: string | null;
}

export interface EmployeeMedicalExamUpdateRequest extends EmployeeMedicalExamCreateRequest {
  id: string;
}

export interface EmployeeMedicalExamDocument {
  id: string;
  examId: string;
  fileName: string;
  filePath: string;
  description?: string | null;
  uploadDate: string;
}
