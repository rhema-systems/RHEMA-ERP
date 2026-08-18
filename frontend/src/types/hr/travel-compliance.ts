/**
 * Staff travel — compliance and policy. Travel documents, visas, risk assessments, destination
 * alerts, insurance, health requirements, and the spend policy itself.
 *
 * Backend routes: `api/staff-travel/compliance` and `api/staff-travel/policies`.
 *
 * ⚠ **A travel policy is a DRAFT until it is approved, and a draft enforces nothing.** Approving is
 * `HR.Travel.Admin` — the same authority as authorising a booking above a cap — and it also puts
 * the policy in force, superseding whichever policy covered the same scope. An approved policy
 * cannot then be edited; raise a new version. Before this, `approvedById` had no writer anywhere,
 * so every policy was unapproved and the field was decoration — which stopped being harmless when
 * the caps began refusing bookings.
 *
 * ⚠ **Acknowledgements belong to the person acknowledging.** A risk assessment can only be
 * acknowledged by the traveller, and an alert only by the employee it was sent to. Both used to be
 * settable by anyone holding Write, which meant the record asserted that someone had read a
 * security briefing about their destination when they had not.
 */

import type { AuditFields } from './common';

// ── Travel documents ─────────────────────────────────────────────────────────

export type TravelDocumentType =
  | 'Passport'
  | 'NationalId'
  | 'Visa'
  | 'ResidentPermit'
  | 'WorkPermit'
  | 'FrequentFlyerCard'
  | 'HotelLoyaltyCard'
  | 'DrivingLicence'
  | 'VaccineCertificate';

export interface StaffTravelDocument extends AuditFields {
  employeeId: string;
  employeeName: string;
  documentType: TravelDocumentType;
  documentTypeName: string;
  documentNumber: string;
  issuingCountryId: string;
  issuingCountryName?: string | null;
  issueDate?: string | null;
  expiryDate?: string | null;
  isPrimary: boolean;
  isVerified: boolean;
  verifiedById?: string | null;
  verifiedByName?: string | null;
  /** Server-stamped when the document is verified. */
  verifiedAt?: string | null;
}

export interface CreateStaffTravelDocument {
  employeeId: string;
  documentType: TravelDocumentType;
  documentNumber: string;
  issuingCountryId: string;
  issueDate?: string | null;
  expiryDate?: string | null;
  isPrimary: boolean;
}

export type UpdateStaffTravelDocument = CreateStaffTravelDocument & { id: string };

// ── Visas ────────────────────────────────────────────────────────────────────

export type VisaApplicationStatus =
  | 'NotStarted'
  | 'InPreparation'
  | 'Submitted'
  | 'Approved'
  | 'Rejected'
  | 'Expired'
  | 'NotRequired';

export interface StaffTravelVisaApplication extends AuditFields {
  staffTravelRequestId: string;
  requestNumber?: string | null;
  employeeId: string;
  employeeName: string;
  destinationCountryId: string;
  destinationCountryName?: string | null;
  /** Free text, not an enum — "Business (single entry)" and the like. */
  visaType?: string | null;
  status: VisaApplicationStatus;
  statusName: string;
  submittedDate?: string | null;
  approvedDate?: string | null;
  expiryDate?: string | null;
  visaNumber?: string | null;
  processingFee?: number | null;
  currencyCode?: string | null;
  vendorId?: string | null;
  vendorName?: string | null;
  notes?: string | null;
}

export interface CreateStaffTravelVisaApplication {
  staffTravelRequestId: string;
  employeeId: string;
  destinationCountryId: string;
  visaType?: string | null;
  status: VisaApplicationStatus;
  submittedDate?: string | null;
  approvedDate?: string | null;
  expiryDate?: string | null;
  visaNumber?: string | null;
  processingFee?: number | null;
  /** Optional, but a present one must be a currency Finance holds. */
  currencyCode?: string | null;
  vendorId?: string | null;
  notes?: string | null;
}

export type UpdateStaffTravelVisaApplication =
  Omit<CreateStaffTravelVisaApplication, 'staffTravelRequestId' | 'employeeId'> & { id: string };

export interface StaffTravelVisaRequirement extends AuditFields {
  originCountryId: string;
  originCountryName?: string | null;
  destinationCountryId: string;
  destinationCountryName?: string | null;
  visaRequired: boolean;
  visaOnArrival: boolean;
  eVisaAvailable: boolean;
  processingDays?: number | null;
  validityDays?: number | null;
  notes?: string | null;
  isActive: boolean;
}

// ── Risk assessments ─────────────────────────────────────────────────────────

export type TravelRiskLevel = 'Low' | 'Medium' | 'High' | 'Critical' | 'Prohibited';

export type TravelRiskCategory =
  | 'Security'
  | 'Health'
  | 'NaturalDisaster'
  | 'PoliticalInstability'
  | 'Infrastructure'
  | 'Crime'
  | 'Other';

export interface StaffTravelRiskAssessment extends AuditFields {
  staffTravelRequestId: string;
  destinationCountryId: string;
  destinationCountryName?: string | null;
  destinationCity?: string | null;
  riskLevel: TravelRiskLevel;
  riskLevelName: string;
  riskCategory: TravelRiskCategory;
  riskCategoryName: string;
  assessmentSource?: string | null;
  assessmentSummary?: string | null;
  mitigationRequired: boolean;
  mitigationNotes?: string | null;
  dutyOfCareBriefingSent: boolean;
  /** Only the traveller can set this, and only for themselves. */
  employeeAcknowledged: boolean;
  acknowledgedAt?: string | null;
  /** Server-assigned: who judged the destination. Not a client input. */
  assessedById?: string | null;
  assessedByName?: string | null;
  assessedAt?: string | null;
  validUntil?: string | null;
}

/** ⚠ No `assessedById` — the assessor is the caller. */
export interface CreateStaffTravelRiskAssessment {
  staffTravelRequestId: string;
  destinationCountryId: string;
  destinationCity?: string | null;
  riskLevel: TravelRiskLevel;
  riskCategory: TravelRiskCategory;
  assessmentSource?: string | null;
  assessmentSummary?: string | null;
  mitigationRequired: boolean;
  mitigationNotes?: string | null;
  dutyOfCareBriefingSent: boolean;
  validUntil?: string | null;
}

export type UpdateStaffTravelRiskAssessment =
  Omit<CreateStaffTravelRiskAssessment, 'staffTravelRequestId'> & { id: string };

// ── Destination alerts ───────────────────────────────────────────────────────

export type TravelAlertType =
  | 'Security'
  | 'HealthOutbreak'
  | 'Weather'
  | 'PoliticalUnrest'
  | 'TransportDisruption'
  | 'NaturalDisaster';

export type TravelAlertSeverity = 'Info' | 'Warning' | 'Critical' | 'Emergency';

export interface StaffTravelAlert extends AuditFields {
  alertType: TravelAlertType;
  alertTypeName: string;
  severity: TravelAlertSeverity;
  severityName: string;
  countryId: string;
  countryName?: string | null;
  city?: string | null;
  title: string;
  body?: string | null;
  source?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
}

export interface CreateStaffTravelAlert {
  alertType: TravelAlertType;
  severity: TravelAlertSeverity;
  countryId: string;
  city?: string | null;
  title: string;
  body?: string | null;
  source?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
}

export type UpdateStaffTravelAlert = CreateStaffTravelAlert & { id: string };

export interface StaffTravelAlertNotification extends AuditFields {
  travelAlertId: string;
  staffTravelRequestId: string;
  employeeId: string;
  /** Server-stamped when the alert is actually dispatched — not a caller's assertion (F-09). */
  notificationSentAt?: string | null;
  isAcknowledged: boolean;
  acknowledgedAt?: string | null;
}

// ── Insurance ────────────────────────────────────────────────────────────────

export type TravelInsuranceType = 'CorporateGroup' | 'Individual' | 'TopUp' | 'Statutory';

export type TravelInsuranceCoverageType =
  | 'Medical'
  | 'TripCancellation'
  | 'Baggage'
  | 'PersonalLiability'
  | 'EmergencyEvacuation'
  | 'Comprehensive';

export interface StaffTravelInsurancePolicy extends AuditFields {
  staffTravelRequestId: string;
  vendorId?: string | null;
  vendorName?: string | null;
  policyNumber?: string | null;
  insuranceType: TravelInsuranceType;
  insuranceTypeName: string;
  coverageType: TravelInsuranceCoverageType;
  coverageTypeName: string;
  coverageStart: string;
  coverageEnd: string;
  sumInsured: number;
  currencyCode: string;
  premium: number;
  emergencyContact?: string | null;
}

export interface CreateStaffTravelInsurancePolicy {
  staffTravelRequestId: string;
  vendorId?: string | null;
  policyNumber?: string | null;
  insuranceType: TravelInsuranceType;
  coverageType: TravelInsuranceCoverageType;
  coverageStart: string;
  coverageEnd: string;
  sumInsured: number;
  currencyCode: string;
  premium: number;
  emergencyContact?: string | null;
}

export type UpdateStaffTravelInsurancePolicy =
  Omit<CreateStaffTravelInsurancePolicy, 'staffTravelRequestId'> & { id: string };

// ── Health requirements ──────────────────────────────────────────────────────

export type TravelHealthRequirementType =
  | 'Vaccination'
  | 'PcrTest'
  | 'RapidAntigenTest'
  | 'MedicalClearance'
  | 'HealthDeclaration'
  | 'Quarantine';

export interface StaffTravelHealthRequirement extends AuditFields {
  countryId: string;
  countryName?: string | null;
  requirementType: TravelHealthRequirementType;
  requirementTypeName: string;
  description?: string | null;
  isMandatory: boolean;
  validityDays?: number | null;
  isActive: boolean;
}

export type CreateStaffTravelHealthRequirement =
  Omit<StaffTravelHealthRequirement, keyof AuditFields | 'countryName' | 'requirementTypeName'>;

// ── Policies ─────────────────────────────────────────────────────────────────

export type FlightCabinClassName = 'Economy' | 'PremiumEconomy' | 'Business' | 'First';

export interface StaffTravelPolicySummary {
  id: string;
  policyName: string;
  versionNumber: number;
  isCurrentVersion: boolean;
  effectiveFrom: string;
  effectiveTo?: string | null;
  maxSingleTripBudget: number;
  /** Rules attached to the policy. Was always 0 on the in-force list until the include was added. */
  ruleCount: number;
  approvedById?: string | null;
  approvedByName?: string | null;
  /** Null means the policy is a DRAFT and enforces nothing. */
  approvedAt?: string | null;
}

/**
 * ⚠ `isCurrentVersion` and `approvedById`/`approvedAt` are all server-assigned. A policy becomes
 * current *by being approved*; there is no separate activation, because "approved but not in
 * force" is not a state anyone asked for and remembering to do it separately is a way to get it
 * wrong.
 */
export interface StaffTravelPolicy extends AuditFields {
  policyName: string;
  versionNumber: number;
  isCurrentVersion: boolean;
  appliesToLevelFromId?: string | null;
  appliesToLevelFromName?: string | null;
  appliesToLevelToId?: string | null;
  appliesToLevelToName?: string | null;
  appliesToOrganizationUnitId?: string | null;
  appliesToOrganizationUnitName?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  maxFlightClassDomestic: FlightCabinClassName;
  maxFlightClassInternational: FlightCabinClassName;
  maxHotelRateDomestic: number;
  maxHotelRateInternational: number;
  advanceBookingDaysFlight: number;
  advanceBookingDaysHotel: number;
  requiresCheapestFare: boolean;
  preferredVendorMandatory: boolean;
  maxSingleTripBudget: number;
  maxAnnualTravelBudget: number;
  receiptRequiredAbove: number;
  expenseSubmissionDays: number;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvedAt?: string | null;
  rules?: StaffTravelPolicyRule[];
}

export interface CreateStaffTravelPolicy {
  policyName: string;
  versionNumber?: number;
  appliesToLevelFromId?: string | null;
  appliesToLevelToId?: string | null;
  appliesToOrganizationUnitId?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  maxFlightClassDomestic: FlightCabinClassName;
  maxFlightClassInternational: FlightCabinClassName;
  maxHotelRateDomestic: number;
  maxHotelRateInternational: number;
  advanceBookingDaysFlight: number;
  advanceBookingDaysHotel: number;
  requiresCheapestFare: boolean;
  preferredVendorMandatory: boolean;
  maxSingleTripBudget: number;
  maxAnnualTravelBudget: number;
  receiptRequiredAbove: number;
  expenseSubmissionDays: number;
}

export type UpdateStaffTravelPolicy = CreateStaffTravelPolicy & { id: string };

export type TravelPolicyRuleType =
  | 'HardLimit'
  | 'SoftLimit'
  | 'Warning'
  | 'Mandatory'
  | 'Preferred'
  | 'Prohibited';

export interface StaffTravelPolicyRule extends AuditFields {
  policyId: string;
  ruleCode: string;
  ruleName: string;
  ruleType: TravelPolicyRuleType;
  ruleTypeName: string;
  limitValue?: number | null;
  exceptionAllowed: boolean;
  exceptionRequiresApproval: boolean;
  isActive: boolean;
}

export type TravelPolicyExceptionStatus = 'Pending' | 'Approved' | 'Rejected' | 'Expired';

export interface StaffTravelPolicyException extends AuditFields {
  staffTravelRequestId: string;
  policyRuleId: string;
  exceptionReason?: string | null;
  requestedValue?: number | null;
  policyLimit?: number | null;
  status: TravelPolicyExceptionStatus;
  statusName: string;
  approvedById?: string | null;
  approvedByName?: string | null;
  decidedAt?: string | null;
}
