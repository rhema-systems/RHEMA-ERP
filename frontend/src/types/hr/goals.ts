/**
 * The goal cascade — area 5's first slice.
 *
 * Strategic goal (multi-year) → company goal (per cycle) → unit goal (per org unit) →
 * employee goal, plus the goal library templates and KPI definitions those draw on.
 *
 * Routes are per-controller and inconsistent by design (the port keeps HRApi's shapes):
 *   api/StrategicGoals, api/CompanyGoals, api/UnitGoals, api/EmployeeGoals,
 *   api/GoalLibrary, api/KpiDefinitions          — `pageNumber`/`pageSize` query paging
 *   api/performance/team-goals                    — manager-scoped, read-only, no paging
 *   api/performance/goals-at-risk                 — HR/Admin, org-wide, no paging
 *   api/performance/goal-risk-settings            — the thresholds behind every at-risk read
 *
 * Enums serialize as strings (JsonStringEnumConverter), so every union below is the enum
 * member name and is what the API expects back.
 */
import type { AuditFields } from './common';

// ── Enums ────────────────────────────────────────────────────────────────────────

export type GoalPriority = 'Low' | 'Medium' | 'High' | 'Critical';

/**
 * One enum spanning two concerns, which is why the manager workspace splits them:
 * Draft/PendingApproval/Approved/Rejected/Locked are the approval lifecycle, while
 * InProgress/OnTrack/AtRisk/Completed describe execution. A goal in an execution state
 * has already passed approval.
 */
export type GoalStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Locked'
  | 'InProgress'
  | 'AtRisk'
  | 'OnTrack'
  | 'Completed';

/** Set by the server from whichever parent FK is populated — never sent by the client. */
export type GoalParentType = 'Company' | 'Unit';

export type GoalProgressStatus =
  | 'NotStarted'
  | 'InProgress'
  | 'OnTrack'
  | 'AtRisk'
  | 'Completed'
  | 'Cancelled';

export type MeasurementType = 'NumericAbsolute' | 'PercentageTarget' | 'Boolean' | 'Range';

/** The slice of the cycle a target applies to, for orgs running quarterly reviews. */
export type GoalPeriod = 'FullCycle' | 'Q1' | 'Q2' | 'H1' | 'Q3' | 'Q4' | 'H2';

/** Governance completeness for one team member's goal set. Derived from counts and weights only. */
export type TeamGovernanceStatus =
  | 'NotStarted'
  | 'InProgress'
  | 'AwaitingApproval'
  | 'InvalidWeight'
  | 'StructurallyComplete'
  /** Fewer goals than the cycle requires (closure B2). */
  | 'BelowMinimum'
  /** More goals than the cycle allows (closure B2). */
  | 'AboveMaximum';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

export const GOAL_PRIORITY_OPTIONS = opts<GoalPriority>([
  ['Critical', 'Critical'],
  ['High', 'High'],
  ['Medium', 'Medium'],
  ['Low', 'Low'],
]);

export const MEASUREMENT_TYPE_OPTIONS = opts<MeasurementType>([
  ['NumericAbsolute', 'Numeric (absolute)'],
  ['PercentageTarget', 'Percentage of target'],
  ['Boolean', 'Yes / no'],
  ['Range', 'Range (min–max)'],
]);

export const GOAL_PERIOD_OPTIONS = opts<GoalPeriod>([
  ['FullCycle', 'Full cycle'],
  ['Q1', 'Q1'],
  ['Q2', 'Q2'],
  ['H1', 'H1'],
  ['Q3', 'Q3'],
  ['Q4', 'Q4'],
  ['H2', 'H2'],
]);

export const GOAL_PROGRESS_STATUS_OPTIONS = opts<GoalProgressStatus>([
  ['NotStarted', 'Not started'],
  ['InProgress', 'In progress'],
  ['OnTrack', 'On track'],
  ['AtRisk', 'At risk'],
  ['Completed', 'Completed'],
  ['Cancelled', 'Cancelled'],
]);

// ── Appraisal cycle (read-only here) ─────────────────────────────────────────────
// Cycles themselves are a later slice of area 5; the goal screens only need to pick one.

/** The API's statuses (`types/hr/appraisal.ts` has the same union); `InProgress` went with D-14, and `Archived` never existed. */
export type AppraisalCycleStatus = 'Draft' | 'Open' | 'Closed';

export interface AppraisalCycleOption extends AuditFields {
  cycleCode: string;
  cycleName: string;
  year: number;
  appraisalType: string;
  startDate: string;
  endDate: string;
  status: AppraisalCycleStatus | string;
  goalSettingOpenDate?: string | null;
  goalSettingDeadline?: string | null;
}

// ── KPI definitions ──────────────────────────────────────────────────────────────

export interface KpiDefinition extends AuditFields {
  tenantId: string;
  kpiName: string;
  description?: string | null;
  measurementType: MeasurementType;
  unit?: string | null;
  tolerancePercent?: number | null;
  isActive: boolean;
}

export interface CreateKpiDefinition {
  kpiName: string;
  description?: string | null;
  measurementType: MeasurementType;
  unit?: string | null;
  tolerancePercent?: number | null;
  isActive: boolean;
}

export type UpdateKpiDefinition = CreateKpiDefinition & { id: string };

// ── Goal library ─────────────────────────────────────────────────────────────────

export interface GoalLibraryItem extends AuditFields {
  tenantId: string;
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  positionId?: string | null;
  positionTitle?: string | null;
  isActive: boolean;
}

export interface GoalLibraryDetails extends GoalLibraryItem {
  totalGoals: number;
  uniqueEmployees: number;
  cycleCount: number;
}

export interface GoalLibraryUsageStats {
  totalGoals: number;
  uniqueEmployees: number;
  cycleCount: number;
}

export interface GoalLibraryUsageRow {
  employeeGoalId: string;
  employeeName: string;
  appraisalCycleName: string;
  goalTitle: string;
  status: GoalStatus;
  progressPercent: number;
}

/** Projection for the picker: `scopeSummary` is pre-rendered ("Global", "Unit: Finance", …). */
export interface GoalLibrarySelectorItem {
  id: string;
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  scopeSummary: string;
  isActive: boolean;
}

export interface CreateGoalLibraryItem {
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  positionId?: string | null;
  isActive: boolean;
}

export type UpdateGoalLibraryItem = CreateGoalLibraryItem & { id: string };

// ── Strategic goals (multi-year, cycle-independent) ──────────────────────────────

export interface StrategicGoal extends AuditFields {
  tenantId: string;
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  priority: GoalPriority;
  startYear: number;
  endYear: number;
  isActive: boolean;
  /** Company goals linked to this strategic goal. Blocks deletion while non-zero. */
  yearlyObjectiveCount: number;
}

export interface CreateStrategicGoal {
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  priority: GoalPriority;
  startYear: number;
  endYear: number;
  isActive: boolean;
}

export type UpdateStrategicGoal = CreateStrategicGoal & { id: string };

// ── Company goals (one cycle's organisation-wide objectives) ─────────────────────

export interface CompanyGoal extends AuditFields {
  tenantId: string;
  appraisalCycleId: string;
  cycleCode?: string | null;
  strategicGoalId?: string | null;
  strategicGoalTitle?: string | null;
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  priority: GoalPriority;
  targetValue?: number | null;
  unit?: string | null;
  dueDate?: string | null;
  /** Whether employees outside HR can see the goal when aligning their own. */
  isVisible: boolean;
}

/** Dashboard projection — carries cascade counts, and only a 200-char description preview. */
export interface CompanyGoalListItem {
  id: string;
  appraisalCycleId: string;
  cycleCode?: string | null;
  title: string;
  descriptionPreview?: string | null;
  successCriteria?: string | null;
  priority: GoalPriority;
  targetValue?: number | null;
  unit?: string | null;
  dueDate?: string | null;
  isVisible: boolean;
  unitGoalCount: number;
  employeeGoalCount: number;
}

export interface CompanyGoalDashboardMetrics {
  cycleId: string;
  totalGoals: number;
  totalUnitGoalsCascaded: number;
  totalEmployeeGoalsAligned: number;
  visibleGoalsCount: number;
  visibleGoalPercent: number;
}

export interface CompanyGoalCascadeStats {
  companyGoalId: string;
  title: string;
  unitGoalsCount: number;
  employeeGoalsCount: number;
  totalGoalsCount: number;
  averageEmployeeProgress?: number | null;
}

export interface CreateCompanyGoal {
  appraisalCycleId: string;
  strategicGoalId?: string | null;
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  priority: GoalPriority;
  targetValue?: number | null;
  unit?: string | null;
  dueDate?: string | null;
  isVisible: boolean;
}

export type UpdateCompanyGoal = CreateCompanyGoal & { id: string };

// ── Unit goals (a company goal cascaded onto an org unit) ────────────────────────

export interface UnitGoal extends AuditFields {
  tenantId: string;
  appraisalCycleId: string;
  cycleCode?: string | null;
  parentCompanyGoalId?: string | null;
  parentGoalTitle?: string | null;
  parentUnitGoalId?: string | null;
  parentUnitGoalTitle?: string | null;
  organizationLevelId: string;
  organizationLevelName: string;
  organizationUnitId: string;
  organizationUnitName: string;
  createdByManagerId: string;
  managerName: string;
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  priority: GoalPriority;
  targetValue?: number | null;
  unit?: string | null;
  dueDate?: string | null;
}

export interface UnitGoalListItem {
  id: string;
  appraisalCycleId: string;
  cycleCode?: string | null;
  parentCompanyGoalId?: string | null;
  parentCompanyGoalTitle?: string | null;
  organizationUnitId: string;
  organizationUnitName: string;
  organizationLevelName: string;
  createdByManagerId: string;
  managerName: string;
  title: string;
  descriptionPreview?: string | null;
  priority: GoalPriority;
  targetValue?: number | null;
  unit?: string | null;
  dueDate?: string | null;
  employeeGoalsCount: number;
}

export interface UnitGoalDashboardMetrics {
  cycleId: string;
  totalUnitGoals: number;
  linkedToCompanyGoal: number;
  unlinkedCount: number;
  totalEmployeeGoalsCascaded: number;
}

/**
 * The cascade in numbers — open to everyone who can read the unit goal. The per-employee rows
 * (`…/employee-goals`) are the HR desk's and the unit line's only (performance closure P11).
 */
export interface UnitGoalCascadeStats {
  goalId: string;
  employeeGoalsCount: number;
  /** Mean progress across the aligned employee goals; null when there are none. */
  averageProgressPercent?: number | null;
}

export interface UnitGoalEmployeeGoalSummary {
  id: string;
  employeeId: string;
  employeeName: string;
  title: string;
  status: GoalStatus;
  priority: GoalPriority;
  progressPercent: number;
  dueDate: string;
}

export interface CreateUnitGoal {
  appraisalCycleId: string;
  parentCompanyGoalId?: string | null;
  parentUnitGoalId?: string | null;
  organizationLevelId: string;
  organizationUnitId: string;
  createdByManagerId: string;
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  priority: GoalPriority;
  targetValue?: number | null;
  unit?: string | null;
  dueDate?: string | null;
}

export type UpdateUnitGoal = CreateUnitGoal & { id: string };

// ── Employee goals ───────────────────────────────────────────────────────────────

export interface EmployeeGoal extends AuditFields {
  tenantId: string;
  employeeId: string;
  employeeName: string;
  appraisalCycleId: string;
  cycleCode?: string | null;
  performanceAppraisalId?: string | null;
  companyGoalId?: string | null;
  unitGoalId?: string | null;
  /** Self-referencing: another EmployeeGoal, not a company or unit goal. */
  parentGoalId?: string | null;
  parentGoalTitle?: string | null;
  parentType?: GoalParentType | null;
  goalLibraryId?: string | null;
  libraryItemTitle?: string | null;
  kpiDefinitionId?: string | null;
  kpiName?: string | null;
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  /** Percent of the appraisal this goal carries. The set should total 100 per cycle. */
  weight: number;
  priority: GoalPriority;
  status: GoalStatus;
  measurementType: MeasurementType;
  period: GoalPeriod;
  targetValue?: number | null;
  minValue?: number | null;
  maxValue?: number | null;
  unit?: string | null;
  startDate: string;
  dueDate: string;
  progressPercent: number;
  submittedToManagerId?: string | null;
  managerName?: string | null;
  submittedDate?: string | null;
  approvalDate?: string | null;
  managerFeedback?: string | null;
  isLocked: boolean;
  lockedDate?: string | null;

  /**
   * Year-end assessment of this goal, scoped to one appraisal.
   *
   * ⚠ Only populated by `getByAppraisal` — the by-employee and paged reads leave these null,
   * because an assessment belongs to an appraisal rather than to the goal. Both sides are stored
   * separately and on purpose: the point of the year-end conversation is comparing what the
   * employee claimed with what the manager concluded, so neither overwrites the other.
   */
  selfFinalProgressPercent?: number | null;
  selfFinalStatus?: GoalProgressStatus | null;
  selfFinalActualValue?: number | null;
  selfAssessmentNotes?: string | null;
  selfEvidenceLinks?: string | null;

  managerFinalProgressPercent?: number | null;
  managerFinalStatus?: GoalProgressStatus | null;
  managerFinalActualValue?: number | null;
  managerAssessmentNotes?: string | null;
  managerEvidenceLinks?: string | null;
}

/**
 * Create/update payloads carry goal *content* only. Status, the target manager and manager
 * feedback move exclusively through submit / approve / reject / lock — the API ignores them here.
 */
export interface CreateEmployeeGoal {
  employeeId: string;
  appraisalCycleId: string;
  companyGoalId?: string | null;
  unitGoalId?: string | null;
  parentGoalId?: string | null;
  goalLibraryId?: string | null;
  kpiDefinitionId?: string | null;
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  weight: number;
  priority: GoalPriority;
  measurementType: MeasurementType;
  period: GoalPeriod;
  targetValue?: number | null;
  minValue?: number | null;
  maxValue?: number | null;
  unit?: string | null;
  startDate: string;
  dueDate: string;
}

export type UpdateEmployeeGoal = Omit<CreateEmployeeGoal, 'goalLibraryId'> & {
  id: string;
  progressPercent: number;
};

export interface EmployeeGoalSummary {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  cycleId: string;
  cycleName: string;
  totalGoals: number;
  draftGoals: number;
  pendingApprovalGoals: number;
  approvedGoals: number;
  inProgressGoals: number;
  completedGoals: number;
  atRiskGoals: number;
  overallProgressPercent: number;
  goalSettingComplete: boolean;
  meetsMinGoalCount: boolean;
}

// ── Progress entries ─────────────────────────────────────────────────────────────

export interface GoalProgressEntry extends AuditFields {
  tenantId: string;
  employeeGoalId: string;
  goalTitle: string;
  progressPercent?: number | null;
  actualValue?: number | null;
  status: GoalProgressStatus;
  challenges?: string | null;
  notes?: string | null;
  recordedById: string;
  recordedByName: string;
  entryDate: string;
  reviewEventId?: string | null;
}

/**
 * No `recordedById` — the recorder is taken from the caller's token on every write path
 * (`EmployeeGoals` and `AppraisalReviewEvents`). It used to be a required field on the payload,
 * which meant a progress claim could be attributed to someone who never made it.
 */
export interface CreateGoalProgressEntry {
  employeeGoalId: string;
  progressPercent?: number | null;
  actualValue?: number | null;
  status: GoalProgressStatus;
  challenges?: string | null;
  notes?: string | null;
  reviewEventId?: string | null;
}

export type UpdateGoalProgressEntry = CreateGoalProgressEntry & {
  id: string;
};

// ── Manager workspace (api/performance/team-goals) ───────────────────────────────

/** What the manager's *lock goal set* did: how many goals it locked, of how many in the set. */
export interface GoalSetLockResult {
  employeeId: string;
  appraisalCycleId: string;
  goalsLocked: number;
  goalsInSet: number;
}

export interface TeamMemberOverview {
  employeeId: string;
  employeeName: string;
  totalGoals: number;
  draftCount: number;
  pendingApprovalCount: number;
  /** Approved, InProgress, OnTrack, AtRisk, Completed and Locked together. */
  approvedWorkflowCount: number;
  rejectedCount: number;
  lockedCount: number;
  inProgressCount: number;
  atRiskCount: number;
  completedCount: number;
  overdueCount: number;
  totalWeight: number;
  isWeightBalanced: boolean;
  /** Every goal but a rejected one — what the cycle's bounds count (closure B2). */
  liveGoalCount: number;
  minGoals: number;
  maxGoals?: number | null;
  meetsMinGoalCount: boolean;
  withinMaxGoalCount: boolean;
  governanceStatus: TeamGovernanceStatus;
}

/** One goal row on the awaiting-approval / at-risk / overdue / locked tabs. */
export interface TeamGoalFlat {
  goalId: string;
  employeeId: string;
  employeeName: string;
  title: string;
  status: GoalStatus;
  weight: number;
  progressPercent: number;
  dueDate: string;
  submittedDate?: string | null;
  approvalDate?: string | null;
  /** Negative once past due. Computed server-side against a UTC clock. */
  daysRemaining: number;
  isOverdue: boolean;
  daysPendingApproval: number;
  daysOverdue?: number | null;
  isAtRisk: boolean;
  riskReason?: string | null;
  /** 0 when not at risk; higher is more urgent. Rows arrive sorted by this. */
  riskSeverityScore: number;
  lockedDate?: string | null;
}

export interface TeamProgressGoalItem {
  goalId: string;
  title: string;
  progressPercent: number;
  status: GoalStatus;
  weight: number;
  dueDate: string;
  isOverdue: boolean;
  daysOverdue?: number | null;
}

export interface TeamGoalProgress {
  employeeId: string;
  employeeName: string;
  totalGoals: number;
  notStartedCount: number;
  inProgressCount: number;
  onTrackCount: number;
  atRiskCount: number;
  completedCount: number;
  overdueCount: number;
  averageProgressPercent: number;
  goals: TeamProgressGoalItem[];
}

/** Flattened detail for the manager's goal drawer — one call, no follow-up queries. */
export interface GoalDetail {
  goalId: string;
  title: string;
  description?: string | null;
  successCriteria?: string | null;
  weight: number;
  priority: GoalPriority;
  status: GoalStatus;
  startDate?: string | null;
  dueDate: string;
  progressPercent: number;
  measurementType?: MeasurementType | null;
  targetValue?: number | null;
  minValue?: number | null;
  maxValue?: number | null;
  unit?: string | null;
  parentType?: GoalParentType | null;
  parentGoalTitle?: string | null;
  goalLibraryTitle?: string | null;
  kpiDefinitionName?: string | null;
  submittedDate?: string | null;
  approvalDate?: string | null;
  managerFeedback?: string | null;
  isLocked: boolean;
  lockedDate?: string | null;
  submittedToManagerName?: string | null;
  progressEntryCount: number;
  lastProgressUpdateDate?: string | null;
  journalEntryCount: number;
}

// ── Goal risk thresholds (api/performance/goal-risk-settings) ────────────────────

export interface GoalRiskSettings {
  /** Null while the tenant is running on the built-in defaults. */
  id?: string | null;
  isConfigured: boolean;
  daysRemainingThreshold: number;
  minimumProgressPercent: number;
  expectedProgressTolerancePercent: number;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface UpdateGoalRiskSettings {
  daysRemainingThreshold: number;
  minimumProgressPercent: number;
  expectedProgressTolerancePercent: number;
}
