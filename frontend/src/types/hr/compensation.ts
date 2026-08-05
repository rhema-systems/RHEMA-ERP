/**
 * Compensation & Benefits — area 4.
 *
 * Routes: api/hr/pay-components, api/hr/emoluments. Same wire-format rules as the rest of HR
 * (string enums, camelCase, DateOnly as "YYYY-MM-DD"). Benefit policies and enrolments live in
 * `benefits.ts`.
 *
 * ⚠ Pay components have SPLIT OWNERSHIP. Payroll is the source of truth for the component
 * master and HR mirrors it; `isPayrollDefined` tells you which rows those are. On a mirrored
 * row the payroll-owned fields are read-only and every write to them returns 409 — only the
 * HR-owned block can be edited, through PATCH /hr-attributes. Components HR defined itself
 * (the six the emolument seeder creates) remain fully editable.
 */

// ── Enums ────────────────────────────────────────────────────────────────────────

export type PayComponentType = 'Allowance' | 'Deduction' | 'BenefitInKindNotional';

export type PayComponentCalculationBasis = 'FixedAmount' | 'PercentageOfBasic';

export type TaxTreatmentType = 'None' | 'PAYE' | 'WithholdingTax';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

export const PAY_COMPONENT_TYPE_OPTIONS = opts<PayComponentType>([
  ['Allowance', 'Allowance'],
  ['Deduction', 'Deduction'],
  ['BenefitInKindNotional', 'Benefit in kind (notional)'],
]);

export const CALCULATION_BASIS_OPTIONS = opts<PayComponentCalculationBasis>([
  ['FixedAmount', 'Fixed amount'],
  ['PercentageOfBasic', 'Percentage of basic'],
]);

export const TAX_TREATMENT_OPTIONS = opts<TaxTreatmentType>([
  ['PAYE', 'PAYE'],
  ['WithholdingTax', 'Withholding tax'],
  ['None', 'Not taxed'],
]);

// ── Pay component master ─────────────────────────────────────────────────────────

export interface PayComponent {
  id: string;

  /** Payroll-owned — overwritten on every projection pass when `isPayrollDefined`. */
  code: string;
  name: string;
  description?: string | null;
  componentType: PayComponentType;
  calculationBasis: PayComponentCalculationBasis;
  /** Meaning follows `calculationBasis`: a sum, or a percentage of basic. */
  defaultAmount?: number | null;
  isTaxable: boolean;
  isActive: boolean;

  /** HR-owned — payroll models none of these, so the projection never touches them. */
  isPensionable: boolean;
  affectsGrossPay: boolean;
  statutoryTreatment: TaxTreatmentType;
  /** DateTime. Emoluments and the leave-encashment rate filter on this window. */
  effectiveFrom: string;
  effectiveTo?: string | null;

  /** True when this row mirrors a payroll component; the payroll-owned block is then read-only. */
  isPayrollDefined: boolean;
}

/** The only edit permitted on a mirrored component. */
export interface UpdatePayComponentHrAttributes {
  isPensionable: boolean;
  affectsGrossPay: boolean;
  statutoryTreatment: TaxTreatmentType;
  effectiveFrom: string;
  effectiveTo?: string | null;
}

/** Full edit — accepted only for HR-defined components; a mirrored one returns 409. */
export interface UpdatePayComponent {
  code: string;
  name: string;
  description?: string | null;
  componentType: PayComponentType;
  calculationBasis: PayComponentCalculationBasis;
  defaultAmount?: number | null;
  isTaxable: boolean;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
}

export interface PayComponentProjectionResult {
  created: number;
  updated: number;
  deactivated: number;
  skippedAsUnchanged: boolean;
  /** Duplicate payroll codes, employer contributions skipped, and HR/payroll code clashes. */
  warnings: string[];
}

// ── Position-level assignment ────────────────────────────────────────────────────

export interface PositionPayComponent {
  id: string;
  positionId: string;
  positionTitle: string;
  payComponentId: string;
  payComponentName: string;
  componentType: PayComponentType;
  /** Overrides the component's `defaultAmount` for this position when set. */
  amount?: number | null;
  defaultAmount?: number | null;
  isActive: boolean;
}

export interface CreatePositionPayComponent {
  positionId: string;
  payComponentId: string;
  amount?: number | null;
}

// ── Employee-level assignment / override ─────────────────────────────────────────

export interface EmployeePayComponent {
  id: string;
  employeeId: string;
  employeeName: string;
  payComponentId: string;
  payComponentName: string;
  componentType: PayComponentType;
  amount: number;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
}

export interface CreateEmployeePayComponent {
  employeeId: string;
  payComponentId: string;
  amount: number;
  effectiveFrom: string;
  effectiveTo?: string | null;
}

export interface UpdateEmployeePayComponent extends CreateEmployeePayComponent {
  isActive: boolean;
}

// ── Consolidated emolument view ──────────────────────────────────────────────────

export interface EffectivePayComponent {
  payComponentId: string;
  code: string;
  name: string;
  componentType: PayComponentType;
  calculationBasis: PayComponentCalculationBasis;
  /** Already resolved to money — percentages have been applied to basic. */
  amount: number;
  isTaxable: boolean;
  /** "Position" (inherited), "Employee" (override or addition), or "Default". */
  source: string;
}

export interface EmployeeEmolumentSummary {
  employeeId: string;
  employeeName: string;
  positionTitle?: string | null;
  /** DateOnly */
  asOfDate: string;
  monthlyBasicPay: number;
  totalAllowances: number;
  totalDeductions: number;
  /** basic + allowances */
  grossMonthly: number;
  /** basic + allowances − deductions */
  netMonthly: number;
  components: EffectivePayComponent[];
}

/**
 * `GET api/hr/emoluments/encashment-rate` — the per-day rate leave encashment pays at, and the
 * payout for a given number of days. Pass `days` to get `amount`; it is 0 without one.
 */
export interface EncashmentRateResult {
  dailyRate: number;
  days: number;
  amount: number;
}

/** Body for `PUT api/hr/emoluments/positions/components/{id}`. */
export interface UpdatePositionPayComponentRequest {
  amount?: number | null;
  isActive: boolean;
}
