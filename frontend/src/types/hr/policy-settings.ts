/**
 * Company-wide HR policy settings — one record per tenant.
 *
 * Transcribed from a live `GET api/hr/policy-settings`
 * (`SLICE=2 node probe-ui-payloads.mjs` in dev-harness/hr-tierb-tail).
 *
 * These are not preferences. Eleven services across eight closed HR areas read them: probation
 * confirmation dates, separation notice and retirement, succession candidate ranking and "service
 * years left", staff requisition budget checks, the long-service sweep, and every reminder lead
 * time. Changing one changes behaviour somewhere else.
 */

/** Mirrors `BudgetEnforcementMode` (HREnums.cs:5373). Off=1, Warn=2, Block=3 — there is no zero. */
export type BudgetEnforcementMode = 'Off' | 'Warn' | 'Block';

export interface CompanyHrPolicySettings {
  id: string;
  tenantId: string;

  // Retirement
  compulsoryRetirementAge: number;
  voluntaryRetirementAge: number;
  useGenderSpecificRetirementAge: boolean;
  maleRetirementAge: number | null;
  femaleRetirementAge: number | null;

  // Probation & notice
  defaultProbationMonths: number;
  defaultResignationNoticeDays: number;
  defaultTerminationNoticeDays: number;
  /** FR-HR-092. Days of unauthorised absence beyond which HR may terminate without the MD's signature. */
  proceduralAbsenceDays: number;

  // Alert / reminder lead times
  vacancyAlertLeadDays: number;
  reviewDueLeadDays: number;
  contractExpiryLeadDays: number;
  probationEndLeadDays: number;
  retirementCountdownLeadDays: number;

  // Org-wide defaults
  longServiceMilestoneYears: string;
  defaultCurrencyCode: string;
  fiscalYearStartMonth: number;
  minimumWorkingAge: number;

  // Enforcement
  budgetEnforcementMode: BudgetEnforcementMode;
  /** FR-HR-136. Defaults to Block, unlike the budget ladder — see the screen for why. */
  establishmentEnforcementMode: BudgetEnforcementMode;

  // Succession fit weights (relative — they need not sum to anything)
  fitWeightPerformance: number;
  fitWeightCompetency: number;
  fitWeightPotential: number;
  fitWeightTenure: number;

  successionPlanNumberPrefix: string;

  // Answers TDC has not given yet (finish plan, lane 2a). Each default is what the code did as a
  // constant before it moved here, so adopting these changed no behaviour — only who can change it.
  writtenQueryHours: number;
  queryResponseWindowHours: number;
  investigationDays: number;
  disciplineBacklogHorizonDays: number;
  /** ⚠ Moves money: 365 calendar / 360 thirty-day / 264 working — a 38% spread on the same facts. */
  settlementDaysPerYear: number;
  attendanceRateIncludesApprovedLeave: boolean;

  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

/**
 * `UpdateCompanyHrPolicySettingsDto`.
 *
 * Unlike the company profile, this is very nearly the read model: it drops only `id`, `tenantId`
 * and the four BaseDto audit columns. Every other key round-trips.
 */
export type UpdateCompanyHrPolicySettingsRequest = Omit<
  CompanyHrPolicySettings,
  'id' | 'tenantId' | 'createdAt' | 'createdBy' | 'updatedAt' | 'updatedBy'
>;

export const ENFORCEMENT_MODES: { value: BudgetEnforcementMode; label: string }[] = [
  { value: 'Off', label: 'Off — no checking' },
  { value: 'Warn', label: 'Warn — allowed, but flagged' },
  { value: 'Block', label: 'Block — rejected' },
];

export const FISCAL_YEAR_MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
].map((label, i) => ({ value: String(i + 1), label }));
