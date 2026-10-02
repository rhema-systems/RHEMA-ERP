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
import type { StaffTravelType, TravelRiskLevel } from './travel';
import type { TravelExpenseCategory } from './travel-finance';

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

/**
 * What every visa LIST returns — `StaffTravelVisaApplicationSummaryDto`, not the full record.
 *
 * ⚠ The list reads were typed as the full record, so the Compliance tab read `visaNumber` and
 * `processingFee` off a shape that had neither and showed "—" for both, always (travel final
 * closure, lane 0 — finding E3). The number is masked here; the full one is on the single read.
 */
export interface StaffTravelVisaApplicationSummary {
  id: string;
  employeeId: string;
  employeeName: string;
  destinationCountryName?: string | null;
  visaType?: string | null;
  status: VisaApplicationStatus;
  statusName: string;
  submittedDate?: string | null;
  approvedDate?: string | null;
  expiryDate?: string | null;
  /** All but the last four characters masked, e.g. `••••••1234`. */
  visaNumberMasked?: string | null;
  processingFee?: number | null;
  currencyCode?: string | null;
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

// ── Visa requirements ────────────────────────────────────────────────────────
// ⚠ Rewritten 2026-09-01 from a live response. What stood here declared `originCountryId`,
// `visaRequired`, `visaOnArrival`, `eVisaAvailable`, `validityDays` and `isActive` — SIX fields
// that exist on neither the entity nor either DTO. It type-checked for as long as it did because
// no screen ever called these methods (ledger § E2, finding 2). Only `destinationCountryId`,
// `processingDays` and `notes` were real.

/** HREnums.cs `VisaRequirementType` — 1..6. */
export const VISA_REQUIREMENT_TYPES = [
  'VisaFree',
  'VisaOnArrival',
  'EVisa',
  'EmbassyVisa',
  'Prohibited',
  'Conditional',
] as const;
export type VisaRequirementType = (typeof VISA_REQUIREMENT_TYPES)[number];

export const VISA_REQUIREMENT_TYPE_LABELS: Record<VisaRequirementType, string> = {
  VisaFree: 'No visa needed',
  VisaOnArrival: 'Visa on arrival',
  EVisa: 'e-Visa',
  EmbassyVisa: 'Embassy visa',
  Prohibited: 'Travel prohibited',
  Conditional: 'Conditional',
};

export interface StaffTravelVisaRequirement extends AuditFields {
  passportCountryId: string;
  /**
   * ⚠ Resolved by the lookup and the by-destination list. It is **null on a create or update
   * response**, which maps a freshly-built entity whose navigation is not loaded — refetch rather
   * than rendering the write response.
   */
  passportCountryName?: string | null;
  destinationCountryId: string;
  destinationCountryName?: string | null;
  visaRequirementType: VisaRequirementType;
  visaRequirementTypeName: string;
  /** Free text — the insurer's own name for the category, e.g. "Standard visitor". */
  visaCategory?: string | null;
  maxStayDays?: number | null;
  processingDays?: number | null;
  officialSourceUrl?: string | null;
  /** `DateOnly` — 'YYYY-MM-DD'. When the entry was last checked against the official source. */
  lastVerifiedAt?: string | null;
  notes?: string | null;
}

/** The create payload. Both countries are required and fix the pair for good. */
export interface CreateStaffTravelVisaRequirement {
  passportCountryId: string;
  destinationCountryId: string;
  visaRequirementType: VisaRequirementType;
  visaCategory?: string | null;
  maxStayDays?: number | null;
  processingDays?: number | null;
  officialSourceUrl?: string | null;
  lastVerifiedAt?: string | null;
  notes?: string | null;
}

/**
 * The update payload.
 *
 * ⚠ It carries **no country fields** — deliberately, on the server. A requirement cannot be moved
 * onto a different country pair; it is retired and a new one entered.
 */
export interface UpdateStaffTravelVisaRequirement {
  id: string;
  visaRequirementType: VisaRequirementType;
  visaCategory?: string | null;
  maxStayDays?: number | null;
  processingDays?: number | null;
  officialSourceUrl?: string | null;
  lastVerifiedAt?: string | null;
  notes?: string | null;
}

// ── Risk assessments ─────────────────────────────────────────────────────────

/**
 * One definition for the area, in `travel.ts`. There used to be two exports of this name with
 * different members — this one correct, the request's offering `Extreme` — and which one a file got
 * depended on its import line (travel final closure, lane 0).
 */
export type { TravelRiskLevel };

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

/**
 * The row `alerts/active` and `alerts/country/{id}` return.
 *
 * ⚠ **No `body`.** Those two are cross-record lists and stay summaries; the alert's text lives on
 * the full record. `alerts/country/{id}/current` — the read that tells a traveller what is
 * happening at their destination — returns {@link StaffTravelAlert} in full, because the body IS
 * the alert. The client used to type all three as the full record, which is why the compliance
 * panel could bind `body` against a payload that never carried it and TypeScript said nothing.
 */
export interface StaffTravelAlertSummary {
  id: string;
  alertType: TravelAlertType;
  alertTypeName: string;
  severity: TravelAlertSeverity;
  severityName: string;
  countryName?: string | null;
  city?: string | null;
  title: string;
  effectiveFrom: string;
  isActive: boolean;
}

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

/**
 * A destination alert sent to one traveller for one trip.
 *
 * ⚠ `alertTitle`, `requestNumber` and `employeeName` are on the DTO and were missing here, so the
 * desk's screen could not name the alert, the trip or the person without the compiler objecting.
 *
 * ⚠ **Acknowledging is the traveller's act alone.** The desk route sits on Travel.Write, which no
 * `Employee` holds, while the service refuses anyone but the addressee — so the acknowledgement
 * runs through `staff-travel/me/alert-notifications/{id}/acknowledge` and nothing else.
 */
export interface StaffTravelAlertNotification extends AuditFields {
  travelAlertId: string;
  alertTitle?: string | null;
  /**
   * The alert's own text and severity.
   *
   * ⚠ Both were absent from this type AND from the DTO behind it, so `MyTravelAlertsPanel` had
   * nothing to render and showed a traveller the title alone — D-31 recurring on the surface that
   * asks them to confirm they have read a security briefing. Added to both, lane 3.
   */
  alertBody?: string | null;
  severity?: TravelAlertSeverity | null;
  staffTravelRequestId: string;
  requestNumber?: string | null;
  employeeId: string;
  employeeName: string;
  /** Server-stamped when the alert is actually dispatched — not a caller's assertion (F-09). */
  notificationSentAt?: string | null;
  isAcknowledged: boolean;
  acknowledgedAt?: string | null;
}

/** What the desk sends: an alert, a trip, and the traveller on it. */
export interface CreateStaffTravelAlertNotification {
  travelAlertId: string;
  staffTravelRequestId: string;
  employeeId: string;
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
  /** The currency the money limits are set in (lane 4, C3/T-9); the base currency on an old policy. */
  currencyCode?: string | null;
  advanceBookingDaysFlight: number;
  advanceBookingDaysHotel: number;
  preferredVendorMandatory: boolean;
  maxSingleTripBudget: number;
  receiptRequiredAbove: number;
  expenseSubmissionDays: number;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvedAt?: string | null;
  rules?: StaffTravelPolicyRule[];
}

/**
 * Lane 4: the version is the server's (the next for the name), and so is whether the policy is in force (approval).
 * `currencyCode` empty takes the base currency. "Cheapest fare" and "max per year" left the contract (D-1).
 */
export interface CreateStaffTravelPolicy {
  policyName: string;
  currencyCode?: string | null;
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
  preferredVendorMandatory: boolean;
  maxSingleTripBudget: number;
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

/** What happens when a rule is breached. */
export type TravelPolicyViolationAction =
  | 'Block'
  | 'Warn'
  | 'FlagForReview'
  | 'RequireJustification'
  | 'EscalateToApprover';

/**
 * A rule on a travel policy.
 *
 * ⚠ **Recorded, not enforced.** `StaffTravelPolicyService` is the only consumer of the rules
 * table anywhere in the codebase: `StaffTravelPolicyGuard` refuses bookings on the policy's own
 * scalar caps (`maxFlightClass*`, `maxHotelRate*`) and never reads a rule. So `ruleType`,
 * `limitValue`, `violationAction` and the two exception flags describe an enforcement mechanism
 * that does not currently run, and the screen says so rather than implying otherwise.
 *
 * ⚠ This interface was six fields short of the DTO — `expenseCategory`, `travelType`, `limitUnit`
 * and `violationAction` with their resolved names — which is why `limitUnit` and `violationAction`
 * are two of the fields the closure ledger's section E lists as settable by no form.
 */
export interface StaffTravelPolicyRule extends AuditFields {
  policyId: string;
  ruleCode: string;
  ruleName: string;
  ruleType: TravelPolicyRuleType;
  ruleTypeName: string;
  expenseCategory?: TravelExpenseCategory | null;
  expenseCategoryName?: string | null;
  travelType?: StaffTravelType | null;
  travelTypeName?: string | null;
  limitValue?: number | null;
  /** Free text on the API — 'GHS per night', 'days', 'percent'. */
  limitUnit?: string | null;
  exceptionAllowed: boolean;
  exceptionRequiresApproval: boolean;
  violationAction: TravelPolicyViolationAction;
  violationActionName: string;
  isActive: boolean;
}

/**
 * ⚠ The client typed this as `Omit<StaffTravelPolicyRule, keyof StaffTravelPolicy>` — subtracting
 * a policy's keys from a rule, which is a guess that compiles and describes nothing. Written from
 * `CreateStaffTravelPolicyRuleDto`.
 *
 * ⚠ `ruleCode` is unique per policy, and re-using the code of a rule that was removed **revives
 * that row** rather than creating a second one, because the unique index counts soft-deleted rows.
 */
export interface CreateStaffTravelPolicyRule {
  policyId?: string;
  ruleCode: string;
  ruleName: string;
  ruleType: TravelPolicyRuleType;
  expenseCategory?: TravelExpenseCategory | null;
  travelType?: StaffTravelType | null;
  limitValue?: number | null;
  limitUnit?: string | null;
  exceptionAllowed: boolean;
  exceptionRequiresApproval: boolean;
  violationAction: TravelPolicyViolationAction;
  isActive: boolean;
}

export type UpdateStaffTravelPolicyRule = CreateStaffTravelPolicyRule & { id: string };

export type TravelPolicyExceptionStatus = 'Pending' | 'Approved' | 'Rejected' | 'Expired';

export interface StaffTravelPolicyException extends AuditFields {
  staffTravelRequestId: string;
  policyRuleId: string;
  policyRuleName?: string | null;
  exceptionReason?: string | null;
  requestedValue?: number | null;
  policyLimit?: number | null;
  status: TravelPolicyExceptionStatus;
  statusName: string;
  approvedById?: string | null;
  approvedByName?: string | null;
  decidedAt?: string | null;
  /**
   * Why the decision went the way it did. The client had been sending a `notes` field since the
   * service layer was written and no such column existed, so every decision's reasoning was
   * dropped by the model binder — a matched route says nothing about the body.
   */
  decisionNotes?: string | null;
}

/** The requester's side of an exception: what they want, against which rule, and why. */
export interface CreateStaffTravelPolicyException {
  staffTravelRequestId: string;
  policyRuleId: string;
  exceptionReason?: string | null;
  requestedValue?: number | null;
  policyLimit?: number | null;
}
