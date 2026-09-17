/**
 * Leave setup — leave types and the four collections that hang off them.
 * Backend route: api/hr/leave-types (LeaveTypesController).
 *
 * Note the nested collections are addressed from the CONTROLLER root, not from the
 * parent: `GET {leaveTypeId}/sub-types` to read, but `POST sub-types` to create (the
 * parent id travels in the body). Enums serialize as strings.
 */

export type EncashmentRateBasis = 'DerivedFromEmoluments' | 'Manual';
export type LeaveEligibilityType = 'Gender' | 'OrganizationLevel' | 'OrganizationUnit' | 'Position';
export type AccrualFrequency = 'None' | 'Monthly' | 'Annual' | 'PerPayPeriod' | 'Quarterly' | 'SemiAnnual';
export type AccrualMode = 'AccrueIncrementally' | 'FullGrantOnEligibility';

export const ENCASHMENT_RATE_BASIS_OPTIONS: { value: EncashmentRateBasis; label: string }[] = [
  { value: 'DerivedFromEmoluments', label: 'Derived from emoluments' },
  { value: 'Manual', label: 'Manual rate' },
];

export const LEAVE_ELIGIBILITY_TYPE_OPTIONS: { value: LeaveEligibilityType; label: string }[] = [
  { value: 'Gender', label: 'Gender' },
  { value: 'OrganizationLevel', label: 'Organization level' },
  { value: 'OrganizationUnit', label: 'Organization unit' },
  { value: 'Position', label: 'Position' },
];

export const ACCRUAL_FREQUENCY_OPTIONS: { value: AccrualFrequency; label: string }[] = [
  { value: 'None', label: 'None' },
  { value: 'Monthly', label: 'Monthly' },
  { value: 'Quarterly', label: 'Quarterly' },
  { value: 'SemiAnnual', label: 'Semi-annual' },
  { value: 'Annual', label: 'Annual' },
  { value: 'PerPayPeriod', label: 'Per pay period' },
];

export const ACCRUAL_MODE_OPTIONS: { value: AccrualMode; label: string }[] = [
  { value: 'AccrueIncrementally', label: 'Accrue incrementally' },
  { value: 'FullGrantOnEligibility', label: 'Full grant on eligibility' },
];

// ── Leave type ──────────────────────────────────────────────────────────────────

export interface LeaveType {
  id: string;
  name: string;
  code: string;
  description?: string | null;
  isPaid: boolean;
  defaultDaysPerYear: number;
  maxDaysPerYear: number;
  minDaysNotice?: number | null;
  requiresApproval: boolean;
  calendarColor?: string | null;
  hasSubTypes: boolean;
  allowCarryOver: boolean;
  maxCarryOverDays?: number | null;
  countWeekendsAsLeave: boolean;
  countHolidaysAsLeave: boolean;
  allowCashConversion: boolean;
  requiresReliever: boolean;
  minServiceMonthsToAccess?: number | null;
  carryOverExpiryMonths?: number | null;
  forfeitUnusedAfterMonths?: number | null;
  mandatoryAnnualLeave: boolean;
  encashmentRateBasis: EncashmentRateBasis;
  encashmentRatePerDay?: number | null;
  encashmentWorkingDaysPerMonth: number;

  /**
   * Excuse duty and the medical board (R-15a). ⚠ All three live on the LEAVE TYPE, not the
   * tenant, because they are rules about a KIND of leave — sick leave needs a certificate,
   * annual leave does not, and every client has both.
   */
  requiresMedicalCertificate: boolean;
  /** Days takeable on the employee's own word. ⚠ 3 is a starting value, not anyone's rule. */
  selfCertificationDays: number;
  /** Cumulative days in a year past which a board must sit. Null = never. ⚠ Counted per YEAR. */
  medicalBoardThresholdDays?: number | null;
  isActive: boolean;
}

export interface LeaveTypeDetail extends LeaveType {
  subTypes: LeaveSubType[];
  allocations: LeaveCategoryAllocation[];
  eligibilities: LeaveTypeEligibility[];
  accrualPolicies: LeaveAccrualPolicy[];
  allowanceComponentIds: string[];
}

export interface CreateLeaveTypeRequest {
  name: string;
  code: string;
  description?: string | null;
  isPaid: boolean;
  defaultDaysPerYear: number;
  maxDaysPerYear: number;
  minDaysNotice?: number | null;
  requiresApproval: boolean;
  calendarColor?: string | null;
  hasSubTypes: boolean;
  allowCarryOver: boolean;
  maxCarryOverDays?: number | null;
  countWeekendsAsLeave: boolean;
  countHolidaysAsLeave: boolean;
  allowCashConversion: boolean;
  requiresReliever: boolean;
  minServiceMonthsToAccess?: number | null;
  carryOverExpiryMonths?: number | null;
  forfeitUnusedAfterMonths?: number | null;
  mandatoryAnnualLeave: boolean;
  encashmentRateBasis: EncashmentRateBasis;
  encashmentRatePerDay?: number | null;
  encashmentWorkingDaysPerMonth: number;

  /**
   * Excuse duty and the medical board (R-15a). ⚠ All three live on the LEAVE TYPE, not the
   * tenant, because they are rules about a KIND of leave — sick leave needs a certificate,
   * annual leave does not, and every client has both.
   */
  requiresMedicalCertificate: boolean;
  /** Days takeable on the employee's own word. ⚠ 3 is a starting value, not anyone's rule. */
  selfCertificationDays: number;
  /** Cumulative days in a year past which a board must sit. Null = never. ⚠ Counted per YEAR. */
  medicalBoardThresholdDays?: number | null;
  /** Pay components an encashment pays through — owned by the Emoluments area. */
  allowanceComponentIds: string[];
}

export interface UpdateLeaveTypeRequest extends CreateLeaveTypeRequest {
  isActive: boolean;
}

// ── Sub-types ───────────────────────────────────────────────────────────────────

export interface LeaveSubType {
  id: string;
  leaveTypeId: string;
  leaveTypeName: string;
  subTypeName: string;
  description?: string | null;
  maxDaysAllowed?: number | null;
  isActive: boolean;
}

/** The backend uses this same DTO for create and update. */
export interface LeaveSubTypeRequest {
  leaveTypeId: string;
  subTypeName: string;
  description?: string | null;
  maxDaysAllowed?: number | null;
  isActive: boolean;
}

// ── Category allocations (days per staff level) ─────────────────────────────────

export interface LeaveCategoryAllocation {
  id: string;
  leaveTypeId: string;
  leaveTypeName: string;
  leaveSubTypeId?: string | null;
  leaveSubTypeName?: string | null;
  staffLevelId: string;
  staffLevelName: string;
  allocationDays: number;
  /** DateOnly */
  effectiveFrom: string;
  effectiveTo?: string | null;
}

export interface LeaveCategoryAllocationRequest {
  leaveTypeId: string;
  leaveSubTypeId?: string | null;
  staffLevelId: string;
  allocationDays: number;
  effectiveFrom: string;
  effectiveTo?: string | null;
}

// ── Eligibility rules ───────────────────────────────────────────────────────────

export interface LeaveTypeEligibility {
  id: string;
  leaveTypeId: string;
  leaveTypeName: string;
  eligibilityType: LeaveEligibilityType;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  positionId?: string | null;
  positionName?: string | null;
  gender?: string | null;
}

/** Create only — eligibility rules are added and removed, never edited in place. */
export interface CreateLeaveTypeEligibilityRequest {
  leaveTypeId: string;
  eligibilityType: LeaveEligibilityType;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  positionId?: string | null;
  gender?: string | null;
}

// ── Accrual policies ────────────────────────────────────────────────────────────

export interface LeaveAccrualPolicy {
  id: string;
  leaveTypeId: string;
  leaveTypeName: string;
  frequency: AccrualFrequency;
  mode: AccrualMode;
  accrualRate: number;
  minServiceMonths?: number | null;
  proRateOnJoin: boolean;
  proRateOnExit: boolean;
  isActive: boolean;
}

/** The backend uses this same DTO for create and update. */
export interface LeaveAccrualPolicyRequest {
  leaveTypeId: string;
  frequency: AccrualFrequency;
  mode: AccrualMode;
  accrualRate: number;
  minServiceMonths?: number | null;
  proRateOnJoin: boolean;
  proRateOnExit: boolean;
}
