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
  /**
   * Round 2, lane F2. Deliberately its own figure rather than sharing `reviewDueLeadDays`: a
   * committee action item is something somebody does on Tuesday, not a date they plan a month
   * around, so its default is 3 days where the others are weeks. 0 means "only once it is due".
   */
  teamTaskReminderLeadDays: number;
  /** Round 3, lane S. A change of pay goes through an approved salary change request. */
  salaryChangeRequiresApproval: boolean;
  /**
   * Lane C2's, wired through by F2. The fallback for a credential that carries no lead days of its
   * own — `Certification.expiryNotificationLeadDays` overrides it per credential.
   */
  certificationExpiryLeadDays: number;

  // Org-wide defaults
  longServiceMilestoneYears: string;
  defaultCurrencyCode: string;
  fiscalYearStartMonth: number;
  minimumWorkingAge: number;

  // Enforcement
  budgetEnforcementMode: BudgetEnforcementMode;
  /** FR-HR-136. Defaults to Block, unlike the budget ladder — see the screen for why. */
  establishmentEnforcementMode: BudgetEnforcementMode;

  // Salary structure (lane G)
  /** Two-tier (grade → notch) or three-tier (grade → level → notch). Screens and rules, not schema. */
  salaryStructureTiers: SalaryStructureTiers;
  /** Who maintains the scale. Payroll = HR is a read-only mirror; Hr = HR's own screens open. */
  salaryStructureSource: SalaryStructureSource;

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

  /**
   * ⚠ Settles a requirements conflict, not a preference. FR-HR-046 says leave is encashed only on
   * exit; the module ships an in-service path and the seed flags annual leave convertible. Both
   * readings were live at once. Defaults to the FRD's reading for a new tenant; the seeded demo
   * tenant has it ON, because the encashment screen has already been demonstrated.
   */
  allowInServiceEncashment: boolean;
  /**
   * ⚠ Read beside `settlementDaysPerYear` and expect them to disagree — 22 working days a month
   * against 365 calendar days a year is roughly 38% apart on the same salary. They are different
   * money events and deliberately not merged; the settings screen shows both with a worked example
   * so the gap is met there rather than in a payout.
   */
  encashmentWorkingDaysPerMonth: number;

  /** The leave reminder cadence. All five were private constants before. */
  leaveStartingReminderDays: number;
  leaveClosureGraceDays: number;
  leaveUndecidedChaseDays: number;
  mandatoryLeaveChaseFromMonth: number;
  leaveCarryOverExpiryReminderDays: number;
  /**
   * ⚠ The month the LEAVE year begins. 1 = January, and not the same field as
   * `fiscalYearStartMonth` — the finance year and the leave year are different facts.
   *
   * ⚠ Change-once-at-setup: the API refuses it once the tenant holds any leave data (D-9).
   */
  leaveYearStartMonth: number;

  /**
   * Round 4, lane K — the orientation & onboarding reminder windows, read by the sweep that
   * delivers (an in-app notification and an email) rather than only logging.
   */
  onboardingTaskDueLeadDays: number;
  orientationDueLeadDays: number;
  orientationCertificateExpiryLeadDays: number;
  /** Days a task awaiting sign-off, an unattempted assessment or an unsigned acknowledgement waits before it is chased. */
  orientationChaseAfterDays: number;

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

/** Mirrors `SalaryStructureTiers` (HREnums.cs). GradeAndNotch=2, GradeLevelAndNotch=3 — there is no zero. */
export type SalaryStructureTiers = 'GradeAndNotch' | 'GradeLevelAndNotch';

/** Mirrors `SalaryStructureSource` (HREnums.cs). Payroll=1, Hr=2. */
export type SalaryStructureSource = 'Payroll' | 'Hr';

export const SALARY_STRUCTURE_TIERS: { value: SalaryStructureTiers; label: string }[] = [
  { value: 'GradeAndNotch', label: 'Two-tier — grade and notch' },
  { value: 'GradeLevelAndNotch', label: 'Three-tier — grade, level and notch' },
];

export const SALARY_STRUCTURE_SOURCES: { value: SalaryStructureSource; label: string }[] = [
  { value: 'Payroll', label: 'Payroll — HR mirrors it, read-only' },
  { value: 'Hr', label: 'HR — maintained under Pay & Benefits → Salary Structure' },
];

export const ENFORCEMENT_MODES: { value: BudgetEnforcementMode; label: string }[] = [
  { value: 'Off', label: 'Off — no checking' },
  { value: 'Warn', label: 'Warn — allowed, but flagged' },
  { value: 'Block', label: 'Block — rejected' },
];

export const FISCAL_YEAR_MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
].map((label, i) => ({ value: String(i + 1), label }));
