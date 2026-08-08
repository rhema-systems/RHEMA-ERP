import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  AppraisalCycleOption,
  CompanyGoal,
  CompanyGoalCascadeStats,
  CompanyGoalDashboardMetrics,
  CompanyGoalListItem,
  CreateCompanyGoal,
  CreateEmployeeGoal,
  CreateGoalLibraryItem,
  CreateGoalProgressEntry,
  CreateKpiDefinition,
  CreateStrategicGoal,
  CreateUnitGoal,
  EmployeeGoal,
  EmployeeGoalSummary,
  GoalDetail,
  GoalLibraryDetails,
  GoalLibraryItem,
  GoalLibrarySelectorItem,
  GoalLibraryUsageRow,
  GoalLibraryUsageStats,
  GoalPriority,
  GoalProgressEntry,
  GoalRiskSettings,
  KpiDefinition,
  StrategicGoal,
  TeamGoalFlat,
  TeamGoalProgress,
  TeamMemberOverview,
  UnitGoal,
  UnitGoalCascadeStats,
  UnitGoalDashboardMetrics,
  UnitGoalEmployeeGoalSummary,
  UnitGoalListItem,
  UpdateCompanyGoal,
  UpdateEmployeeGoal,
  UpdateGoalLibraryItem,
  UpdateGoalProgressEntry,
  UpdateGoalRiskSettings,
  UpdateKpiDefinition,
  UpdateStrategicGoal,
  UpdateUnitGoal,
} from '@/types/hr/goals';

/**
 * The goal cascade: strategic → company → unit → employee, plus the library and KPI
 * definitions goals are built from, and the manager workspace that governs them.
 *
 * Paging is `pageNumber`/`pageSize` on most endpoints but `page`/`pageSize` on the two
 * GoalLibrary projections — the ported controllers differ and are left as they are, so each
 * method below matches its own endpoint.
 */

/**
 * `UnitGoals/dashboard/paged` binds `priority` as an int while every other endpoint takes the
 * enum name. Sending "High" there silently drops the filter, so it is converted here.
 */
const PRIORITY_ORDINALS: Record<GoalPriority, number> = {
  Low: 1,
  Medium: 2,
  High: 3,
  Critical: 4,
};

// ── Appraisal cycles (read-only: cycles are their own slice of area 5) ──────────────

/** api/AppraisalCycle — only the reads the goal screens need to scope themselves to a cycle. */
class AppraisalCycleLookupService {
  private readonly baseUrl = '/AppraisalCycle';

  getAll(): Promise<AppraisalCycleOption[]> {
    return apiService.get<AppraisalCycleOption[]>(this.baseUrl);
  }

  /** Cycles currently accepting work. Empty until HR opens one, which is not an error. */
  getActive(): Promise<AppraisalCycleOption[]> {
    return apiService.get<AppraisalCycleOption[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<AppraisalCycleOption> {
    return apiService.get<AppraisalCycleOption>(`${this.baseUrl}/${id}`);
  }
}

// ── KPI definitions ────────────────────────────────────────────────────────────────

/** api/KpiDefinitions — the measurable definitions an employee goal can be scored against. */
class KpiDefinitionService {
  private readonly baseUrl = '/KpiDefinitions';

  getAll(): Promise<KpiDefinition[]> {
    return apiService.get<KpiDefinition[]>(this.baseUrl);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<KpiDefinition>> {
    return apiService.get<PagedResult<KpiDefinition>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<KpiDefinition> {
    return apiService.get<KpiDefinition>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateKpiDefinition): Promise<KpiDefinition> {
    return apiService.post<KpiDefinition>(this.baseUrl, data);
  }

  update(id: string, data: UpdateKpiDefinition): Promise<KpiDefinition> {
    return apiService.put<KpiDefinition>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }
}

// ── Goal library ───────────────────────────────────────────────────────────────────

/**
 * api/GoalLibrary — reusable goal templates, optionally scoped to an org level, unit or
 * position. Copying one onto an employee goal records the provenance, which is what the
 * usage reads below count.
 */
class GoalLibraryService {
  private readonly baseUrl = '/GoalLibrary';

  getAll(): Promise<GoalLibraryItem[]> {
    return apiService.get<GoalLibraryItem[]>(this.baseUrl);
  }

  getActive(): Promise<GoalLibraryItem[]> {
    return apiService.get<GoalLibraryItem[]>(`${this.baseUrl}/active`);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<GoalLibraryItem>> {
    return apiService.get<PagedResult<GoalLibraryItem>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<GoalLibraryItem> {
    return apiService.get<GoalLibraryItem>(`${this.baseUrl}/${id}`);
  }

  /** Item plus its usage counts, in one call. */
  getDetails(id: string): Promise<GoalLibraryDetails> {
    return apiService.get<GoalLibraryDetails>(`${this.baseUrl}/${id}/details`);
  }

  getUsageStats(id: string): Promise<GoalLibraryUsageStats> {
    return apiService.get<GoalLibraryUsageStats>(`${this.baseUrl}/${id}/usage-stats`);
  }

  /** Note: `page`, not `pageNumber` — this endpoint and the selector are the two exceptions. */
  getUsage(id: string, page = 1, pageSize = 10): Promise<PagedResult<GoalLibraryUsageRow>> {
    return apiService.get<PagedResult<GoalLibraryUsageRow>>(`${this.baseUrl}/${id}/usage`, {
      page,
      pageSize,
    });
  }

  /** Projection for the picker dialog; `scopeSummary` arrives pre-rendered. */
  getSelector(params: {
    search?: string;
    activeOnly?: boolean;
    organizationLevelId?: string | null;
    organizationUnitId?: string | null;
    positionId?: string | null;
    page?: number;
    pageSize?: number;
  }): Promise<PagedResult<GoalLibrarySelectorItem>> {
    return apiService.get<PagedResult<GoalLibrarySelectorItem>>(`${this.baseUrl}/selector`, {
      search: params.search,
      activeOnly: params.activeOnly ?? true,
      organizationLevelId: params.organizationLevelId,
      organizationUnitId: params.organizationUnitId,
      positionId: params.positionId,
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 10,
    });
  }

  create(data: CreateGoalLibraryItem): Promise<GoalLibraryItem> {
    return apiService.post<GoalLibraryItem>(this.baseUrl, data);
  }

  update(id: string, data: UpdateGoalLibraryItem): Promise<GoalLibraryItem> {
    return apiService.put<GoalLibraryItem>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Body is a bare boolean, not an object — the endpoint binds `[FromBody] bool`. */
  setActive(id: string, isActive: boolean): Promise<void> {
    return apiService.patch<void>(`${this.baseUrl}/${id}/active-status`, isActive);
  }
}

// ── Strategic goals ────────────────────────────────────────────────────────────────

/**
 * api/StrategicGoals — multi-year company intent, independent of any appraisal cycle.
 * Company goals link back to one, which is how a cycle's objectives inherit a strategy.
 */
class StrategicGoalService {
  private readonly baseUrl = '/StrategicGoals';

  getAll(activeOnly = false): Promise<StrategicGoal[]> {
    return apiService.get<StrategicGoal[]>(this.baseUrl, { activeOnly });
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<StrategicGoal>> {
    return apiService.get<PagedResult<StrategicGoal>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<StrategicGoal> {
    return apiService.get<StrategicGoal>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateStrategicGoal): Promise<StrategicGoal> {
    return apiService.post<StrategicGoal>(this.baseUrl, data);
  }

  update(id: string, data: UpdateStrategicGoal): Promise<StrategicGoal> {
    return apiService.put<StrategicGoal>(`${this.baseUrl}/${id}`, data);
  }

  /** Refused with 400 while company goals still link to it. */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  setActive(id: string, isActive: boolean): Promise<void> {
    return apiService.patch<void>(`${this.baseUrl}/${id}/active-status`, isActive);
  }
}

// ── Company goals ──────────────────────────────────────────────────────────────────

/** api/CompanyGoals — one cycle's organisation-wide objectives, the top of the cascade. */
class CompanyGoalService {
  private readonly baseUrl = '/CompanyGoals';

  getByCycle(cycleId: string): Promise<CompanyGoal[]> {
    return apiService.get<CompanyGoal[]>(`${this.baseUrl}/by-cycle/${cycleId}`);
  }

  /** Only the goals employees are allowed to align to. */
  getVisible(cycleId: string): Promise<CompanyGoal[]> {
    return apiService.get<CompanyGoal[]>(`${this.baseUrl}/visible/${cycleId}`);
  }

  getPaged(pageNumber = 1, pageSize = 20, cycleId?: string): Promise<PagedResult<CompanyGoal>> {
    return apiService.get<PagedResult<CompanyGoal>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
      cycleId,
    });
  }

  /** Strategy-dashboard projection with cascade counts. HR/Admin only. */
  getDashboardPaged(params: {
    cycleId: string;
    search?: string;
    priority?: GoalPriority | null;
    isVisible?: boolean | null;
    dueDateTo?: string | null;
    pageNumber?: number;
    pageSize?: number;
  }): Promise<PagedResult<CompanyGoalListItem>> {
    return apiService.get<PagedResult<CompanyGoalListItem>>(`${this.baseUrl}/dashboard/paged`, {
      cycleId: params.cycleId,
      search: params.search,
      priority: params.priority,
      isVisible: params.isVisible,
      dueDateTo: params.dueDateTo,
      pageNumber: params.pageNumber ?? 1,
      pageSize: params.pageSize ?? 12,
    });
  }

  getDashboardMetrics(cycleId: string): Promise<CompanyGoalDashboardMetrics> {
    return apiService.get<CompanyGoalDashboardMetrics>(`${this.baseUrl}/dashboard/metrics`, {
      cycleId,
    });
  }

  getById(id: string): Promise<CompanyGoal> {
    return apiService.get<CompanyGoal>(`${this.baseUrl}/${id}`);
  }

  /** Unit and employee goals cascaded from this one, plus their average progress. */
  getCascadeStats(id: string): Promise<CompanyGoalCascadeStats> {
    return apiService.get<CompanyGoalCascadeStats>(`${this.baseUrl}/${id}/cascade-stats`);
  }

  create(data: CreateCompanyGoal): Promise<CompanyGoal> {
    return apiService.post<CompanyGoal>(this.baseUrl, data);
  }

  update(id: string, data: UpdateCompanyGoal): Promise<CompanyGoal> {
    return apiService.put<CompanyGoal>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  setVisibility(id: string, isVisible: boolean): Promise<void> {
    return apiService.patch<void>(`${this.baseUrl}/${id}/visibility`, isVisible);
  }
}

// ── Unit goals ─────────────────────────────────────────────────────────────────────

/** api/UnitGoals — a company goal taken up by one org unit, or a unit goal of its own. */
class UnitGoalService {
  private readonly baseUrl = '/UnitGoals';

  getByCycle(cycleId: string): Promise<UnitGoal[]> {
    return apiService.get<UnitGoal[]>(`${this.baseUrl}/by-cycle/${cycleId}`);
  }

  getByOrgUnit(orgUnitId: string): Promise<UnitGoal[]> {
    return apiService.get<UnitGoal[]>(`${this.baseUrl}/by-org-unit/${orgUnitId}`);
  }

  getByManager(managerId: string, cycleId?: string): Promise<UnitGoal[]> {
    return apiService.get<UnitGoal[]>(`${this.baseUrl}/by-manager/${managerId}`, { cycleId });
  }

  getByCompanyGoal(companyGoalId: string): Promise<UnitGoal[]> {
    return apiService.get<UnitGoal[]>(`${this.baseUrl}/by-company-goal/${companyGoalId}`);
  }

  getPaged(pageNumber = 1, pageSize = 20, cycleId?: string): Promise<PagedResult<UnitGoal>> {
    return apiService.get<PagedResult<UnitGoal>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
      cycleId,
    });
  }

  /**
   * Alignment-dashboard projection. A caller holding only the Manager role is narrowed
   * server-side to the goals they created, so the same call returns less for them.
   */
  getDashboardPaged(params: {
    cycleId: string;
    search?: string;
    priority?: GoalPriority | null;
    orgUnitId?: string | null;
    isLinked?: boolean | null;
    pageNumber?: number;
    pageSize?: number;
  }): Promise<PagedResult<UnitGoalListItem>> {
    return apiService.get<PagedResult<UnitGoalListItem>>(`${this.baseUrl}/dashboard/paged`, {
      cycleId: params.cycleId,
      search: params.search,
      priority: params.priority ? PRIORITY_ORDINALS[params.priority] : undefined,
      orgUnitId: params.orgUnitId,
      isLinked: params.isLinked,
      pageNumber: params.pageNumber ?? 1,
      pageSize: params.pageSize ?? 12,
    });
  }

  getDashboardMetrics(cycleId: string): Promise<UnitGoalDashboardMetrics> {
    return apiService.get<UnitGoalDashboardMetrics>(`${this.baseUrl}/dashboard/metrics`, {
      cycleId,
    });
  }

  getById(id: string): Promise<UnitGoal> {
    return apiService.get<UnitGoal>(`${this.baseUrl}/${id}`);
  }

  getCascadeStats(id: string): Promise<UnitGoalCascadeStats> {
    return apiService.get<UnitGoalCascadeStats>(`${this.baseUrl}/${id}/cascade-stats`);
  }

  getEmployeeGoalSummaries(id: string): Promise<UnitGoalEmployeeGoalSummary[]> {
    return apiService.get<UnitGoalEmployeeGoalSummary[]>(`${this.baseUrl}/${id}/employee-goals`);
  }

  create(data: CreateUnitGoal): Promise<UnitGoal> {
    return apiService.post<UnitGoal>(this.baseUrl, data);
  }

  update(id: string, data: UpdateUnitGoal): Promise<UnitGoal> {
    return apiService.put<UnitGoal>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

// ── Employee goals ─────────────────────────────────────────────────────────────────

/**
 * api/EmployeeGoals — the bottom of the cascade and the only level with an approval workflow.
 *
 * Content and lifecycle are separate on purpose: `update` carries goal content, while
 * submit/approve/reject/lock are the only way status moves. The server takes the acting
 * employee from the token and the target manager from the employee's HR record, so neither
 * is ever sent.
 */
class EmployeeGoalService {
  private readonly baseUrl = '/EmployeeGoals';

  getByEmployee(employeeId: string, cycleId?: string): Promise<EmployeeGoal[]> {
    return apiService.get<EmployeeGoal[]>(`${this.baseUrl}/by-employee/${employeeId}`, { cycleId });
  }

  getByAppraisal(appraisalId: string): Promise<EmployeeGoal[]> {
    return apiService.get<EmployeeGoal[]>(`${this.baseUrl}/by-appraisal/${appraisalId}`);
  }

  getPendingApproval(managerId: string, cycleId?: string): Promise<EmployeeGoal[]> {
    return apiService.get<EmployeeGoal[]>(`${this.baseUrl}/pending-approval/${managerId}`, {
      cycleId,
    });
  }

  getPaged(params: {
    pageNumber?: number;
    pageSize?: number;
    employeeId?: string | null;
    cycleId?: string | null;
  }): Promise<PagedResult<EmployeeGoal>> {
    return apiService.get<PagedResult<EmployeeGoal>>(`${this.baseUrl}/paged`, {
      pageNumber: params.pageNumber ?? 1,
      pageSize: params.pageSize ?? 20,
      employeeId: params.employeeId,
      cycleId: params.cycleId,
    });
  }

  /** Counts by status, overall progress and whether the goal set is complete for the cycle. */
  getSummary(employeeId: string, cycleId: string): Promise<EmployeeGoalSummary> {
    return apiService.get<EmployeeGoalSummary>(`${this.baseUrl}/summary/${employeeId}/${cycleId}`);
  }

  getById(id: string): Promise<EmployeeGoal> {
    return apiService.get<EmployeeGoal>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateEmployeeGoal): Promise<EmployeeGoal> {
    return apiService.post<EmployeeGoal>(this.baseUrl, data);
  }

  update(id: string, data: UpdateEmployeeGoal): Promise<EmployeeGoal> {
    return apiService.put<EmployeeGoal>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Approval lifecycle ───────────────────────────────────────────────────────────
  // Draft/Rejected → PendingApproval → Approved → Locked. All four return 204 and take
  // no ids: the caller is resolved from the token. 403 means "not your goal" or "not the
  // employee's manager"; 422 means the transition is not allowed from the current status.

  /** Owner action. Fails 422 when the employee has no manager on their HR record. */
  submit(goalId: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${goalId}/submit`);
  }

  /** Manager action. The optional comment is stored as manager feedback. */
  approve(goalId: string, feedback?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${goalId}/approve`, {
      feedback: feedback ?? null,
    });
  }

  /** Manager action. Feedback is mandatory — a bare rejection is refused with 422. */
  reject(goalId: string, feedback: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${goalId}/reject`, { feedback });
  }

  /** Manager action, from any post-approval status. Nothing may change afterwards. */
  lock(goalId: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${goalId}/lock`);
  }

  unlock(goalId: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${goalId}/unlock`);
  }

  // ── Progress entries ─────────────────────────────────────────────────────────────
  // Each entry carries the goal's progress forward: the latest entry's percent becomes the
  // goal's, and 100% completes it. Only accepted while the goal is approved, in progress
  // or at risk.

  getProgressEntries(goalId: string): Promise<GoalProgressEntry[]> {
    return apiService.get<GoalProgressEntry[]>(`${this.baseUrl}/${goalId}/progress`);
  }

  addProgressEntry(goalId: string, data: CreateGoalProgressEntry): Promise<GoalProgressEntry> {
    return apiService.post<GoalProgressEntry>(`${this.baseUrl}/${goalId}/progress`, data);
  }

  updateProgressEntry(
    goalId: string,
    entryId: string,
    data: UpdateGoalProgressEntry,
  ): Promise<GoalProgressEntry> {
    return apiService.put<GoalProgressEntry>(
      `${this.baseUrl}/${goalId}/progress/${entryId}`,
      data,
    );
  }

  removeProgressEntry(goalId: string, entryId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${goalId}/progress/${entryId}`);
  }
}

// ── Manager workspace ──────────────────────────────────────────────────────────────

/**
 * api/performance/team-goals — read-only governance queries, always scoped server-side to the
 * calling manager's direct reports. There are no mutations here: approve and reject live on
 * the employee-goal endpoints above.
 *
 * A 401 from any of these means the caller has no direct reports for that cycle, not that
 * their session expired.
 */
class TeamGoalsService {
  private readonly baseUrl = '/performance/team-goals';

  /** One row per direct report: goal counts, weight balance and a governance verdict. */
  getOverview(cycleId: string): Promise<TeamMemberOverview[]> {
    return apiService.get<TeamMemberOverview[]>(`${this.baseUrl}/overview/${cycleId}`);
  }

  getAwaitingApproval(cycleId: string): Promise<TeamGoalFlat[]> {
    return apiService.get<TeamGoalFlat[]>(`${this.baseUrl}/awaiting-approval/${cycleId}`);
  }

  /** Flagged by the goal-risk thresholds, or already sitting in AtRisk status. */
  getAtRisk(cycleId: string): Promise<TeamGoalFlat[]> {
    return apiService.get<TeamGoalFlat[]>(`${this.baseUrl}/at-risk/${cycleId}`);
  }

  getOverdue(cycleId: string): Promise<TeamGoalFlat[]> {
    return apiService.get<TeamGoalFlat[]>(`${this.baseUrl}/overdue/${cycleId}`);
  }

  getLocked(cycleId: string): Promise<TeamGoalFlat[]> {
    return apiService.get<TeamGoalFlat[]>(`${this.baseUrl}/locked/${cycleId}`);
  }

  /** Every goal for one direct report. 401 when they are not a direct report. */
  getEmployeeGoals(employeeId: string, cycleId: string): Promise<TeamGoalFlat[]> {
    return apiService.get<TeamGoalFlat[]>(`${this.baseUrl}/employee/${employeeId}/${cycleId}`);
  }

  /** 404 covers both "no such goal" and "not your report" — deliberately indistinguishable. */
  getGoalDetail(goalId: string): Promise<GoalDetail> {
    return apiService.get<GoalDetail>(`${this.baseUrl}/goal-detail/${goalId}`);
  }

  getProgress(cycleId: string): Promise<TeamGoalProgress[]> {
    return apiService.get<TeamGoalProgress[]>(`${this.baseUrl}/progress/${cycleId}`);
  }
}

// ── Org-wide at-risk ───────────────────────────────────────────────────────────────

/**
 * api/performance/goals-at-risk — the same risk rules as the manager tab but with no manager
 * scope, so it spans the organisation. HR/Admin only. Rows arrive sorted by severity.
 */
class AtRiskGoalsService {
  private readonly baseUrl = '/performance/goals-at-risk';

  get(
    cycleId: string,
    filters: { organizationUnitId?: string | null; organizationLevelId?: string | null } = {},
  ): Promise<TeamGoalFlat[]> {
    return apiService.get<TeamGoalFlat[]>(`${this.baseUrl}/${cycleId}`, {
      organizationUnitId: filters.organizationUnitId,
      organizationLevelId: filters.organizationLevelId,
    });
  }
}

// ── Goal risk thresholds ───────────────────────────────────────────────────────────

/**
 * api/performance/goal-risk-settings — what "at risk" means for this tenant.
 *
 * Nothing seeds these, so `isConfigured` is false until someone saves; the evaluator runs on
 * the documented defaults until then rather than failing.
 */
class GoalRiskSettingsService {
  private readonly baseUrl = '/performance/goal-risk-settings';

  get(): Promise<GoalRiskSettings> {
    return apiService.get<GoalRiskSettings>(this.baseUrl);
  }

  save(data: UpdateGoalRiskSettings): Promise<GoalRiskSettings> {
    return apiService.put<GoalRiskSettings>(this.baseUrl, data);
  }

  reset(): Promise<GoalRiskSettings> {
    return apiService.post<GoalRiskSettings>(`${this.baseUrl}/reset`);
  }
}

export const appraisalCycleLookupService = new AppraisalCycleLookupService();
export const kpiDefinitionService = new KpiDefinitionService();
export const goalLibraryService = new GoalLibraryService();
export const strategicGoalService = new StrategicGoalService();
export const companyGoalService = new CompanyGoalService();
export const unitGoalService = new UnitGoalService();
export const employeeGoalService = new EmployeeGoalService();
export const teamGoalsService = new TeamGoalsService();
export const atRiskGoalsService = new AtRiskGoalsService();
export const goalRiskSettingsService = new GoalRiskSettingsService();
