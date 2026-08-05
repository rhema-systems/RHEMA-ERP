/**
 * Benefit policies, employee enrolments and claims — the benefits half of area 4.
 *
 * Routes: api/hr/benefit-policies, api/hr/employee-benefit-enrollments.
 *
 * ⚠ Most of this area's enums are NOT hardcoded here. `GET api/hr/benefit-policies/lookups`
 * returns every one as `{ value, name, label }`, so the selects are data-driven and stay in step
 * with the backend automatically. Only the few statuses the UI branches on are typed as unions
 * below, because code has to compare against them.
 */
import type { AuditFields } from './common';
import type { DependentRelationship } from './employee-subresources';

// ── Statuses the UI actually branches on ─────────────────────────────────────────

export type EnrollmentStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Active'
  | 'Suspended'
  | 'Terminated'
  | 'Expired'
  | 'Rejected';

export type BenefitClaimStatus = 'Pending' | 'Approved' | 'Rejected' | 'Paid' | 'Cancelled';

export type BenefitEnrollmentSource = 'Position' | 'Grade' | 'Manual' | 'Mandatory';

export type BenefitUtilizationType = 'Expense' | 'Reimbursement' | 'Adjustment';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

export const ENROLLMENT_STATUS_OPTIONS = opts<EnrollmentStatus>([
  ['Draft', 'Draft'],
  ['PendingApproval', 'Pending approval'],
  ['Active', 'Active'],
  ['Suspended', 'Suspended'],
  ['Terminated', 'Terminated'],
  ['Expired', 'Expired'],
  ['Rejected', 'Rejected'],
]);

export const CLAIM_STATUS_OPTIONS = opts<BenefitClaimStatus>([
  ['Pending', 'Pending'],
  ['Approved', 'Approved'],
  ['Rejected', 'Rejected'],
  ['Paid', 'Paid'],
  ['Cancelled', 'Cancelled'],
]);

// ── Lookups ──────────────────────────────────────────────────────────────────────

/**
 * One option from the lookups endpoint. `name` is the enum member and is what the API expects
 * back (enums serialize as strings); `value` is the numeric backing and `label` is for display.
 */
export interface EnumOption {
  value: number;
  name: string;
  label: string;
}

export interface PayComponentLookup {
  id: string;
  code: string;
  name: string;
}

export interface BenefitPolicyLookups {
  policyTypes: EnumOption[];
  recipients: EnumOption[];
  relationTypes: EnumOption[];
  limitPeriods: EnumOption[];
  deliveryTypes: EnumOption[];
  taxTreatments: EnumOption[];
  valuationMethods: EnumOption[];
  calculationBases: EnumOption[];
  contributionResponsibilities: EnumOption[];
  frequencies: EnumOption[];
  enrollmentStatuses: EnumOption[];
  payComponents: PayComponentLookup[];
}

// ── Benefit policy ───────────────────────────────────────────────────────────────

/**
 * The classification, valuation and payroll-link block. Nested under `definition` on the wire,
 * not flattened, so it round-trips as one object.
 */
export interface BenefitDefinitionFields {
  deliveryType: string;
  currency: string;
  frequency: string;
  calculationBasis: string;
  isTaxable: boolean;
  taxTreatment: string;
  taxablePercentage?: number | null;
  valuationMethod: string;
  flatValue?: number | null;
  valuationRate?: number | null;
  valuationCap?: number | null;
  taxExemptThreshold?: number | null;
  isPensionable: boolean;
  affectsGrossPay: boolean;
  affectsNetPay: boolean;
  contributionResponsibility: string;
  employerContributionRate?: number | null;
  employeeContributionRate?: number | null;
  minServiceMonths?: number | null;
  availableDuringProbation: boolean;
  /** Optional link to the pay component payroll resolves this benefit's money through. */
  payComponentId?: string | null;
}

/** Per-grade or per-staff-level value and eligibility. One of the two ids is set, not both. */
export interface BenefitGradeValue extends AuditFields {
  salaryGradeId?: string | null;
  staffLevelId?: string | null;
  amount?: number | null;
  rate?: number | null;
  coverageLimit?: number | null;
  isActive: boolean;
}

export interface CreateBenefitGradeValue {
  salaryGradeId?: string | null;
  staffLevelId?: string | null;
  amount?: number | null;
  rate?: number | null;
  coverageLimit?: number | null;
  isActive: boolean;
}

export interface BenefitPolicyRelation extends AuditFields {
  relationType: string;
  maxCount?: number | null;
  maxAge?: number | null;
  isActive: boolean;
}

export interface BenefitPolicyListItem {
  id: string;
  policyName: string;
  policyCode?: string | null;
  policyType: string;
  recipient: string;
  isActive: boolean;
  effectiveFrom: string;
  effectiveTo?: string | null;
}

export interface BenefitPolicy extends AuditFields {
  policyType: string;
  policyName: string;
  policyCode?: string | null;
  description?: string | null;
  recipient: string;
  maxDependents?: number | null;
  employeeContribution?: number | null;
  employerContribution?: number | null;
  coverageLimit: number;
  limitPeriod: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isMandatory: boolean;
  isActive: boolean;
  definition: BenefitDefinitionFields;
  relations: BenefitPolicyRelation[];
  gradeValues: BenefitGradeValue[];
}

export interface CreateBenefitPolicy {
  policyType: string;
  policyName: string;
  policyCode?: string | null;
  description?: string | null;
  recipient: string;
  maxDependents?: number | null;
  employeeContribution?: number | null;
  employerContribution?: number | null;
  coverageLimit: number;
  limitPeriod: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isMandatory: boolean;
  definition: BenefitDefinitionFields;
}

export interface UpdateBenefitPolicy extends CreateBenefitPolicy {
  id: string;
  isActive: boolean;
}

// ── Employee enrolment ───────────────────────────────────────────────────────────

export interface EmployeeBenefitEnrollmentListItem {
  id: string;
  employeeId: string;
  employeeName: string;
  benefitPolicyId: string;
  benefitPolicyName: string;
  status: EnrollmentStatus;
  source: BenefitEnrollmentSource;
  assessedValue: number;
  taxableValue: number;
  currency: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  coverageLimit: number;
  utilizedAmount: number;
  remainingAmount: number;
  currentPeriodStart?: string | null;
  currentPeriodEnd?: string | null;
}

export interface EnrollmentDependent extends AuditFields {
  employeeDependentId: string;
  /** Resolved from the dependant's own record — the list has nothing else to show. */
  dependentName: string;
  relationship: DependentRelationship;
  policyId: string;
  /** DateOnly */
  enrolledDate: string;
  coverageStartDate?: string | null;
  coverageEndDate?: string | null;
  /** False means cover has ended: no further claims can be recorded for them. */
  isActive: boolean;
  /** How much of the enrolment's limit this dependant has consumed. */
  benefitAmountUsed: number;
}

export interface UpdateEnrollmentDependent {
  coverageStartDate?: string | null;
  coverageEndDate?: string | null;
  isActive: boolean;
}

/**
 * `DELETE .../dependents/{id}` — `deleted` distinguishes the two outcomes. A dependant with claims
 * against the enrolment is kept as inactive so the claim ledger keeps its attribution.
 */
export interface RemoveDependentResult {
  deleted: boolean;
  message: string;
}

export interface BenefitBeneficiary extends AuditFields {
  fullName: string;
  relationship: string;
  employeeDependentId?: string | null;
  phoneNumber?: string | null;
  /** Beneficiary shares are expected to total 100 across an enrolment. */
  percentage: number;
  isActive: boolean;
}

export interface EmployeeBenefitEnrollment extends AuditFields {
  employeeId: string;
  employeeName: string;
  benefitPolicyId: string;
  benefitPolicyName: string;
  enrollmentDate: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  status: EnrollmentStatus;
  source: BenefitEnrollmentSource;
  sourcePositionBenefitId?: string | null;
  assessedValue: number;
  taxableValue: number;
  employerContribution: number;
  employeeContribution: number;
  currency: string;
  utilizedAmount: number;
  coverageLimit: number;
  remainingAmount: number;
  currentPeriodStart?: string | null;
  currentPeriodEnd?: string | null;
  /** True when someone set the value by hand instead of taking the policy's valuation. */
  isValueOverridden: boolean;
  approvedById?: string | null;
  approvedDate?: string | null;
  terminationReason?: string | null;
  notes?: string | null;
  dependents: EnrollmentDependent[];
  beneficiaries: BenefitBeneficiary[];
}

export interface CreateEnrollmentDependent {
  employeeDependentId: string;
  coverageStartDate?: string | null;
  coverageEndDate?: string | null;
}

export interface CreateBenefitBeneficiary {
  fullName: string;
  relationship: string;
  employeeDependentId?: string | null;
  phoneNumber?: string | null;
  percentage: number;
}

/**
 * Body for `PUT .../beneficiaries`. The whole set goes in one call: shares must total 100, and no
 * sequence of single-row edits can get from one valid split to another without passing through a
 * state that doesn't add up. An empty list clears the nomination.
 */
export interface ReplaceBenefitBeneficiaries {
  beneficiaries: CreateBenefitBeneficiary[];
}

export interface CreateEmployeeBenefitEnrollment {
  employeeId: string;
  benefitPolicyId: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  /** Replaces the policy's own valuation when set. */
  assessedValueOverride?: number | null;
  notes?: string | null;
  dependents: CreateEnrollmentDependent[];
  beneficiaries: CreateBenefitBeneficiary[];
}

export interface UpdateEmployeeBenefitEnrollment {
  effectiveFrom: string;
  effectiveTo?: string | null;
  assessedValueOverride?: number | null;
  notes?: string | null;
}

export interface EnrollmentStatusChange {
  status: EnrollmentStatus;
  reason?: string | null;
}

// ── Claims / utilisation ─────────────────────────────────────────────────────────

export interface BenefitUtilization extends AuditFields {
  enrollmentId: string;
  employeeDependentId?: string | null;
  dependentName?: string | null;
  /** DateOnly */
  claimDate: string;
  amount: number;
  type: BenefitUtilizationType;
  status: BenefitClaimStatus;
  description?: string | null;
  referenceNumber?: string | null;
  approvedDate?: string | null;
  rejectionReason?: string | null;
}

export interface CreateBenefitUtilization {
  enrollmentId: string;
  employeeDependentId?: string | null;
  claimDate: string;
  amount: number;
  type: BenefitUtilizationType;
  description?: string | null;
  referenceNumber?: string | null;
}

export interface ClaimStatusChange {
  status: BenefitClaimStatus;
  reason?: string | null;
}

export interface EnrollmentBalance {
  enrollmentId: string;
  coverageLimit: number;
  utilizedAmount: number;
  remainingAmount: number;
  limitPeriod: string;
  periodStart?: string | null;
  periodEnd?: string | null;
  currency: string;
}

/**
 * `GET api/hr/employee-benefit-enrollments/payroll-lines` — the hand-off payroll picks up.
 * Note there is no employee *name* here: it is a machine feed keyed by id, not a report.
 */
export interface EmployeeBenefitPayrollLine {
  enrollmentId: string;
  employeeId: string;
  benefitPolicyId: string;
  benefitName: string;
  payComponentCode?: string | null;
  deliveryType: string;
  grossValue: number;
  taxableValue: number;
  employerContribution: number;
  employeeContribution: number;
  isPensionable: boolean;
  affectsGrossPay: boolean;
  affectsNetPay: boolean;
  frequency: string;
  currency: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
}
