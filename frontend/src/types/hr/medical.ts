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

// ── Medical expense claims (slice 6) ─────────────────────────────────────────

export type ClaimStatus =
  | 'Pending'
  | 'Submitted'
  | 'SupervisorReview'
  | 'HrReview'
  | 'FinanceReview'
  | 'Approved'
  | 'Rejected'
  | 'Paid'
  | 'Cancelled';

export type MedicalExpenseType =
  | 'Consultation'
  | 'Medication'
  | 'LaboratoryTests'
  | 'Imaging'
  | 'Surgery'
  | 'Hospitalization'
  | 'DentalCare'
  | 'OpticalCare'
  | 'Physiotherapy'
  | 'EmergencyCare'
  | 'MaternityCare'
  | 'MentalHealth'
  | 'Vaccination'
  | 'HealthScreening'
  | 'MedicalEquipment'
  | 'AmbulanceService'
  | 'Other';

export const MEDICAL_EXPENSE_TYPE_OPTIONS: { value: MedicalExpenseType; label: string }[] = [
  { value: 'Consultation', label: 'Consultation' },
  { value: 'Medication', label: 'Medication' },
  { value: 'LaboratoryTests', label: 'Laboratory tests' },
  { value: 'Imaging', label: 'X-ray / imaging' },
  { value: 'Surgery', label: 'Surgery' },
  { value: 'Hospitalization', label: 'Hospitalisation' },
  { value: 'DentalCare', label: 'Dental care' },
  { value: 'OpticalCare', label: 'Optical care' },
  { value: 'Physiotherapy', label: 'Physiotherapy' },
  { value: 'EmergencyCare', label: 'Emergency care' },
  { value: 'MaternityCare', label: 'Maternity care' },
  { value: 'MentalHealth', label: 'Mental health' },
  { value: 'Vaccination', label: 'Vaccination' },
  { value: 'HealthScreening', label: 'Health screening' },
  { value: 'MedicalEquipment', label: 'Medical equipment' },
  { value: 'AmbulanceService', label: 'Ambulance' },
  { value: 'Other', label: 'Other' },
];

export type MedicalItemType =
  | 'ConsultationFee'
  | 'LaboratoryTest'
  | 'Imaging'
  | 'Medication'
  | 'Procedure'
  | 'Surgery'
  | 'HospitalBed'
  | 'MedicalSupply'
  | 'MedicalEquipment'
  | 'TherapySession'
  | 'ProfessionalFee'
  | 'FacilityFee'
  | 'Other';

export const MEDICAL_ITEM_TYPE_OPTIONS: { value: MedicalItemType; label: string }[] = [
  { value: 'ConsultationFee', label: 'Consultation fee' },
  { value: 'LaboratoryTest', label: 'Laboratory test' },
  { value: 'Imaging', label: 'Imaging' },
  { value: 'Medication', label: 'Medication' },
  { value: 'Procedure', label: 'Procedure' },
  { value: 'Surgery', label: 'Surgery' },
  { value: 'HospitalBed', label: 'Hospital bed' },
  { value: 'MedicalSupply', label: 'Medical supply' },
  { value: 'MedicalEquipment', label: 'Medical equipment' },
  { value: 'TherapySession', label: 'Therapy session' },
  { value: 'ProfessionalFee', label: 'Professional fee' },
  { value: 'FacilityFee', label: 'Facility fee' },
  { value: 'Other', label: 'Other' },
];

export type MedicalDocumentType =
  | 'Receipt'
  | 'Invoice'
  | 'Prescription'
  | 'MedicalReport'
  | 'LabResults'
  | 'ImagingResults'
  | 'DischargeSummary'
  | 'ReferralLetter'
  | 'InsuranceClaimForm'
  | 'Other';

export const MEDICAL_DOCUMENT_TYPE_OPTIONS: { value: MedicalDocumentType; label: string }[] = [
  { value: 'Receipt', label: 'Receipt' },
  { value: 'Invoice', label: 'Invoice' },
  { value: 'Prescription', label: 'Prescription' },
  { value: 'MedicalReport', label: 'Medical report' },
  { value: 'LabResults', label: 'Lab results' },
  { value: 'ImagingResults', label: 'Imaging results' },
  { value: 'DischargeSummary', label: 'Discharge summary' },
  { value: 'ReferralLetter', label: 'Referral letter' },
  { value: 'InsuranceClaimForm', label: 'Insurance claim form' },
  { value: 'Other', label: 'Other' },
];

export type PaymentMethod =
  | 'BankTransfer'
  | 'Cash'
  | 'Cheque'
  | 'MobileMoney'
  | 'DirectDeposit'
  | 'SalaryDeduction';

export const PAYMENT_METHOD_OPTIONS: { value: PaymentMethod; label: string }[] = [
  { value: 'BankTransfer', label: 'Bank transfer' },
  { value: 'MobileMoney', label: 'Mobile money' },
  { value: 'Cheque', label: 'Cheque' },
  { value: 'Cash', label: 'Cash' },
  { value: 'DirectDeposit', label: 'Direct deposit' },
  { value: 'SalaryDeduction', label: 'Salary deduction' },
];

export interface MedicalExpenseClaimSummary {
  id: string;
  claimNumber: string;
  employeeName: string;
  isForDependent: boolean;
  dependentName?: string | null;
  claimDate: string;
  serviceDate: string;
  expenseType: MedicalExpenseType;
  facilityName: string;
  amountRequested: number;
  amountApproved?: number | null;
  status: ClaimStatus;
  isFlaggedForReview: boolean;
}

export interface MedicalExpenseClaim {
  id: string;
  claimNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  isForDependent: boolean;
  dependentName?: string | null;
  claimDate: string;
  serviceDate: string;
  serviceEndDate?: string | null;
  expenseType: MedicalExpenseType;
  description: string;
  facilityId: string;
  facilityName: string;
  physicianId?: string | null;
  physicianName?: string | null;
  diagnosis?: string | null;
  icdCode?: string | null;
  treatmentReceived?: string | null;
  isEmergency: boolean;
  requiredHospitalization: boolean;
  totalAmount: number;
  amountRequested: number;
  amountApproved?: number | null;
  insurancePolicyId?: string | null;
  insurancePolicyNumber?: string | null;
  status: ClaimStatus;
  isFlaggedForReview: boolean;
  additionalNotes?: string | null;
}

export interface MedicalExpenseClaimCreateRequest {
  /** Required on the HR endpoint; ignored by self-service, which uses the token's employee. */
  employeeId?: string | null;
  serviceDate: string;
  serviceEndDate?: string | null;
  expenseType: MedicalExpenseType;
  description: string;
  facilityId: string;
  physicianId?: string | null;
  diagnosis?: string | null;
  isEmergency: boolean;
  requiredHospitalization: boolean;
  totalAmount: number;
  amountRequested: number;
  insurancePolicyId?: string | null;
  additionalNotes?: string | null;
  /** Lines submitted with the claim. Honoured since slice 2 — before that they were dropped. */
  items?: MedicalExpenseItemCreateRequest[];
}

export interface MedicalExpenseItem {
  id: string;
  claimId: string;
  description: string;
  itemType: MedicalItemType;
  quantity: number;
  unitCost: number;
  remarks?: string | null;
}

export interface MedicalExpenseItemCreateRequest {
  claimId?: string;
  description: string;
  itemType: MedicalItemType;
  quantity: number;
  unitCost: number;
  remarks?: string | null;
}

export interface MedicalExpenseDocument {
  id: string;
  claimId: string;
  fileName: string;
  description?: string | null;
  type: MedicalDocumentType;
  uploadDate: string;
}

export type MedicalExpenseClaimNoteType =
  | 'General'
  | 'InternalHR'
  | 'FinanceNote'
  | 'InsuranceCorrespondence'
  | 'EmployeeComment';

export const CLAIM_NOTE_TYPE_OPTIONS: { value: MedicalExpenseClaimNoteType; label: string }[] = [
  { value: 'General', label: 'General' },
  { value: 'InternalHR', label: 'Internal HR' },
  { value: 'FinanceNote', label: 'Finance' },
  { value: 'InsuranceCorrespondence', label: 'Insurer correspondence' },
  { value: 'EmployeeComment', label: 'Employee comment' },
];

export interface MedicalExpenseClaimNote {
  id: string;
  claimId: string;
  authorId: string;
  authorName: string;
  noteType: MedicalExpenseClaimNoteType;
  content: string;
  isInternal: boolean;
  noteDate: string;
}

export interface ProcessClaimRequest {
  claimId: string;
  status: 'Approved' | 'Rejected';
  amountApproved?: number | null;
  comments?: string | null;
}

export interface ProcessClaimPaymentRequest {
  claimId: string;
  paymentMethod: PaymentMethod;
  paymentReference: string;
  paymentDate: string;
}

/** What a claimant sees of their own claim — the HR contract minus adjudication internals. */
export interface OwnMedicalClaim {
  id: string;
  claimNumber: string;
  claimDate: string;
  serviceDate: string;
  serviceEndDate?: string | null;
  expenseType: MedicalExpenseType;
  description: string;
  facilityId: string;
  facilityName: string;
  physicianName?: string | null;
  isEmergency: boolean;
  requiredHospitalization: boolean;
  totalAmount: number;
  amountRequested: number;
  amountApproved?: number | null;
  status: ClaimStatus;
}

export interface OwnMedicalClaimSummary {
  id: string;
  claimNumber: string;
  claimDate: string;
  serviceDate: string;
  expenseType: MedicalExpenseType;
  facilityName: string;
  status: ClaimStatus;
  amountRequested: number;
  amountApproved?: number | null;
}

// ── NHIS claims ──────────────────────────────────────────────────────────────

export type NHISClaimStatus =
  | 'Draft'
  | 'Submitted'
  | 'UnderReview'
  | 'Approved'
  | 'PartiallyApproved'
  | 'Rejected'
  | 'Paid'
  | 'Appealed';

export const NHIS_CLAIM_STATUS_OPTIONS: { value: NHISClaimStatus; label: string }[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Submitted', label: 'Submitted' },
  { value: 'UnderReview', label: 'Under review' },
  { value: 'Approved', label: 'Approved' },
  { value: 'PartiallyApproved', label: 'Partially approved' },
  { value: 'Rejected', label: 'Rejected' },
  { value: 'Paid', label: 'Paid' },
  { value: 'Appealed', label: 'Appealed' },
];

export type MedicalServiceType =
  | 'Consultation'
  | 'EmergencyCare'
  | 'InpatientCare'
  | 'OutpatientCare'
  | 'Surgery'
  | 'LaboratoryServices'
  | 'ImagingRadiology'
  | 'Pharmacy'
  | 'Physiotherapy'
  | 'DentalCare'
  | 'OpticalCare'
  | 'MaternityCare'
  | 'Vaccination'
  | 'HealthScreening'
  | 'MentalHealth'
  | 'AmbulanceService'
  | 'Other';

export const MEDICAL_SERVICE_TYPE_OPTIONS: { value: MedicalServiceType; label: string }[] = [
  { value: 'Consultation', label: 'Consultation' },
  { value: 'OutpatientCare', label: 'Outpatient care' },
  { value: 'InpatientCare', label: 'Inpatient care' },
  { value: 'EmergencyCare', label: 'Emergency care' },
  { value: 'Surgery', label: 'Surgery' },
  { value: 'LaboratoryServices', label: 'Laboratory' },
  { value: 'ImagingRadiology', label: 'Imaging / radiology' },
  { value: 'Pharmacy', label: 'Pharmacy' },
  { value: 'Physiotherapy', label: 'Physiotherapy' },
  { value: 'DentalCare', label: 'Dental care' },
  { value: 'OpticalCare', label: 'Optical care' },
  { value: 'MaternityCare', label: 'Maternity care' },
  { value: 'Vaccination', label: 'Vaccination' },
  { value: 'HealthScreening', label: 'Health screening' },
  { value: 'MentalHealth', label: 'Mental health' },
  { value: 'AmbulanceService', label: 'Ambulance' },
  { value: 'Other', label: 'Other' },
];

export interface NHISClaimSummary {
  id: string;
  claimNumber: string;
  employeeName: string;
  serviceDate: string;
  totalCost: number;
  status: NHISClaimStatus;
}

export interface NHISClaim extends NHISClaimSummary {
  employeeId: string;
  nhisMembershipNumber: string;
  facilityId: string;
  facilityName: string;
  physicianId?: string | null;
  physicianName?: string | null;
  serviceType: MedicalServiceType;
  serviceDescription: string;
  diagnosis?: string | null;
  icdCode?: string | null;
  nhisCoveredAmount?: number | null;
  coPayAmount?: number | null;
  batchNumber?: string | null;
  submissionDate?: string | null;
  approvalDate?: string | null;
  approvedAmount?: number | null;
  rejectionDate?: string | null;
  rejectionReason?: string | null;
  paymentDate?: string | null;
  paymentReference?: string | null;
  linkedMedicalClaimNumber?: string | null;
}

export interface NHISClaimCreateRequest {
  employeeId: string;
  nhisMembershipNumber: string;
  facilityId: string;
  physicianId?: string | null;
  serviceDate: string;
  serviceType: MedicalServiceType;
  serviceDescription: string;
  diagnosis?: string | null;
  totalCost: number;
  nhisCoveredAmount?: number | null;
  coPayAmount?: number | null;
}

export interface NHISClaimUpdateRequest extends NHISClaimCreateRequest {
  id: string;
}

// ── Dashboard ────────────────────────────────────────────────────────────────

export interface MedicalClaimSpotlight {
  id: string;
  claimNumber: string;
  employeeName: string;
  expenseType: MedicalExpenseType;
  status: ClaimStatus;
  amountRequested: number;
  claimDate: string;
}

export interface MedicalAppointmentSpotlight {
  id: string;
  appointmentNumber: string;
  employeeName: string;
  facilityName: string;
  appointmentDateTime: string;
  status: string;
}

export interface MedicalDashboard {
  totalClaims: number;
  pendingClaims: number;
  flaggedClaims: number;
  approvedClaims: number;
  paidClaims: number;
  totalReimbursedAmount: number;
  pendingClaimsAmount: number;
  activePolicies: number;
  expiringPolicies: number;
  overduePremiums: number;
  overduePremiumAmount: number;
  pendingPreAuthorizations: number;
  pendingReferrals: number;
  upcomingAppointments: number;
  examsDue: number;
  claimsByStatus: { status: ClaimStatus; count: number }[];
  monthlyClaimTrend: { year: number; month: number; label: string; count: number }[];
  recentClaims: MedicalClaimSpotlight[];
  pendingApprovalClaims: MedicalClaimSpotlight[];
  upcomingAppointmentList: MedicalAppointmentSpotlight[];
}
