/**
 * Leave setup — leave types and the four collections that hang off them.
 * Backend route: api/hr/leave-types (LeaveTypesController).
 *
 * Note the nested collections are addressed from the CONTROLLER root, not from the
 * parent: `GET {leaveTypeId}/sub-types` to read, but `POST sub-types` to create (the
 * parent id travels in the body). Enums serialize as strings.
 */

export type LeaveEligibilityType = 'Gender' | 'OrganizationLevel' | 'OrganizationUnit' | 'Position';
export type AccrualFrequency = 'None' | 'Monthly' | 'Annual' | 'PerPayPeriod' | 'Quarterly' | 'SemiAnnual';
export type AccrualMode = 'AccrueIncrementally' | 'FullGrantOnEligibility';

/**
 * What the year-end runs count as a person's unused days (entitlement plan B2).
 *
 * ⚠ It governs **both** carry-over and forfeiture, which is why it is not called a carry-over
 * basis. Both readings are ordinary employer policy; the product answered the question silently as
 * `Granted` until 2026-09-18.
 */
export type LeaveYearEndBasis = 'Granted' | 'Earned';

export const LEAVE_YEAR_END_BASIS_OPTIONS: { value: LeaveYearEndBasis; label: string }[] = [
  { value: 'Granted', label: 'What the year granted them' },
  { value: 'Earned', label: 'What they actually earned' },
];

/**
 * What kind of leave a leave type is (round 5, decision A4). The kind decides which rules apply and
 * what the leave-type form asks for. At most one ACTIVE type may be Annual.
 */
export type LeaveTypeCategory = 'Annual' | 'Maternity' | 'Other';

export const LEAVE_TYPE_CATEGORY_OPTIONS: {
  value: LeaveTypeCategory;
  label: string;
  /** What choosing it drives, said where the choice is made. */
  drives: string;
}[] = [
  {
    value: 'Annual',
    label: 'Annual leave',
    drives:
      'Earned by service. Only annual leave can be planned or cashed in; the compliance register and the untaken-leave reminder read it, and it is the balance on the portal’s home page. Only one active leave type can be Annual.',
  },
  {
    value: 'Maternity',
    label: 'Maternity leave',
    drives:
      'Statutory (Labour Act s.57). No notice rule applies, because a birth can come early, and an approver confirms or rejects it but never moves its dates. Switch its medical certificate on, from the first day, with no board.',
  },
  {
    value: 'Other',
    label: 'Other',
    drives:
      'Everything else: sick, casual, compassionate, study. Each has a limit per year, shown to staff as “limit · used · left”, and HR can give fewer days by suggesting other dates.',
  },
];

export const LEAVE_ELIGIBILITY_TYPE_OPTIONS: { value: LeaveEligibilityType; label: string }[] = [
  { value: 'Gender', label: 'Gender' },
  { value: 'OrganizationLevel', label: 'Organization level' },
  { value: 'OrganizationUnit', label: 'Organization unit' },
  { value: 'Position', label: 'Position' },
];

/**
 * ⚠ **`PerPayPeriod` is deliberately NOT offered** (entitlement plan B5, decision D-6).
 *
 * The accrual engine maps it to twelve periods a year and counts elapsed *months* for it, so it was
 * a synonym for `Monthly` wearing a more specific label — and on a fortnightly or weekly payroll
 * the label was a claim the product could not honour. The API refuses it too, so this is not a
 * client-side-only rule.
 *
 * ⚠ The type keeps the value, because rows already carrying it still read and still accrue. What is
 * retired is choosing it anew. Accruing on a real pay cycle needs the pay calendar, which payroll
 * owns — it is a cross-module contract, not an HR setting.
 */
export const ACCRUAL_FREQUENCY_OPTIONS: { value: AccrualFrequency; label: string }[] = [
  { value: 'Monthly', label: 'Monthly' },
  { value: 'Quarterly', label: 'Quarterly' },
  { value: 'SemiAnnual', label: 'Semi-annual' },
  { value: 'Annual', label: 'Annual' },
];

/**
 * Values the picker no longer offers, kept for a policy that already carries one: `PerPayPeriod`
 * is retired (decision D-6), and `None` accrues nothing, so it is no policy and the server refuses
 * it (leave settings audit 2, L-92).
 */
export const ACCRUAL_FREQUENCY_NOT_OFFERED: { value: AccrualFrequency; label: string }[] = [
  { value: 'None', label: 'None (accrues nothing)' },
  { value: 'PerPayPeriod', label: 'Per pay period (retired — accrues monthly)' },
];

/**
 * For DISPLAY only — the picker's options plus the values it no longer offers, so a policy that
 * still carries one reads as words in a table rather than as a raw enum name.
 */
export const ACCRUAL_FREQUENCY_DISPLAY: { value: AccrualFrequency; label: string }[] = [
  ...ACCRUAL_FREQUENCY_OPTIONS,
  ...ACCRUAL_FREQUENCY_NOT_OFFERED,
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

  /**
   * ⚠ Governs **both** year-end runs, carry-over and forfeiture (entitlement plan B2).
   * `Granted` is the default and is what both did before the setting existed.
   */
  yearEndBasis: LeaveYearEndBasis;
  /**
   * Scales a joiner's first-year entitlement to the months they were present.
   * ⚠ The API refuses it alongside an incremental accrual policy — the two deduct for the same
   * months, and the derived per-period rate would deduct for them a third time.
   */
  proRateFirstYearEntitlement: boolean;
  /** Annual, Maternity or Other (round 5, A4). Replaced mandatoryAnnualLeave. */
  category: LeaveTypeCategory;
  /**
   * Days asked for beyond this leave's limit may be charged to annual leave, HR deciding at the
   * final approval (round 5, decision A5). Other kinds only.
   */
  allowOffsetAgainstAnnual: boolean;
  // The encashment rate fields left with leave settings audit 2 (L-73): Finance values leave.

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

  /**
   * ⚠ Governs **both** year-end runs, carry-over and forfeiture (entitlement plan B2).
   * `Granted` is the default and is what both did before the setting existed.
   */
  yearEndBasis: LeaveYearEndBasis;
  /**
   * Scales a joiner's first-year entitlement to the months they were present.
   * ⚠ The API refuses it alongside an incremental accrual policy — the two deduct for the same
   * months, and the derived per-period rate would deduct for them a third time.
   */
  proRateFirstYearEntitlement: boolean;
  /** Omit to leave the kind unchanged on update; Other on create (round 5, A4). */
  category?: LeaveTypeCategory | null;
  /**
   * Omit to leave it unchanged on update; off on create (round 5, A5). The API refuses it on an
   * Annual or Maternity type, and on a type that does not require approval.
   */
  allowOffsetAgainstAnnual?: boolean | null;

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
  staffLevelId: string;
  staffLevelName: string;
  allocationDays: number;
  /** DateOnly */
  effectiveFrom: string;
  effectiveTo?: string | null;
  /**
   * On a save's reply only (leave settings audit 2, L-89): how many of this type's balances in the
   * current leave year the save re-worked; null when the allocation, before or after, is not in
   * force in that year, so none was looked at.
   */
  balancesUpdated?: number | null;
}

export interface LeaveCategoryAllocationRequest {
  leaveTypeId: string;
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
  /** Round 5, lane N1. Omitted: in force on create, unchanged on update. */
  isActive?: boolean;
}
