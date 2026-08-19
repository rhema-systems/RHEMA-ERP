import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  Competency,
  CompetencySkillIndicator,
  CreateJobDescription,
  EmployeeCompetency,
  EmployeeCompetencyProfile,
  EmployeePositionCompetencyGapSummary,
  JobAnalytics,
  JobDescription,
  JobDescriptionDetail,
  JobDescriptionStatus,
  JobDescriptionSummary,
  JobFamily,
  JobLevel,
  JobSubFamily,
  ManpowerBudget,
  ManpowerBudgetLine,
  OrganisationCompetencyGap,
  PositionCompetency,
  PositionEstablishment,
  UncoveredPosition,
  UpdateJobDescription,
} from '@/types/hr/job-architecture';

/**
 * Job architecture, competency and manpower budget. Backend routes: `api/JobAnalysis`,
 * `api/hr/job-architecture`, `api/competencies`, `api/competency-skill-indicators`,
 * `api/position-competencies`, `api/employee-competencies`.
 *
 * ⚠ **Three permission families, and a screen must respect all three.** HR holds Read + Write on
 * `HR.JobArchitecture.*`, `HR.Competency.*` and `HR.ManpowerBudget.*`; **Admin** is required to
 * approve a job description, approve or reject a manpower budget, set or withdraw an establishment,
 * and to delete anything. An HR user can author a job description and will be refused when they
 * approve it, so do not render an approve button merely because the record is visible.
 *
 * ⚠ **The workflow endpoints take a plain `[Authorize]` and are gated by the ENGINE, not by a
 * permission.** The approver of a job description or a manpower budget is whoever the tenant named
 * in the workflow definition — a job-family owner, a department head, the Managing Director — and
 * they typically hold no HR permission at all. Read `canCurrentUserApprove` from
 * `Workflow/entity-summary` to decide whether to show those actions; a permission check would hide
 * them from exactly the people who need them.
 *
 * ⚠ **Once a tenant publishes a workflow definition the direct approve/reject routes 409.** Both
 * are kept because a tenant that has not configured a chain still needs to approve things.
 */
class JobArchitectureService {
  private readonly jobs = '/JobAnalysis';
  private readonly architecture = '/hr/job-architecture';
  private readonly competencies = '/competencies';
  private readonly indicators = '/competency-skill-indicators';
  private readonly positionCompetencies = '/position-competencies';
  private readonly employeeCompetencies = '/employee-competencies';

  // ── job descriptions ───────────────────────────────────────────────────────

  /** The register. Carries the FULL DTO, unlike the by-position and by-status reads. */
  getJobDescriptionsPaged(params: { pageNumber?: number; pageSize?: number } = {}) {
    return apiService.get<PagedResult<JobDescription>>(`${this.jobs}/descriptions/paged`, params);
  }

  getJobDescription(id: string) {
    return apiService.get<JobDescription>(`${this.jobs}/descriptions/${id}`);
  }

  /** Everything in one payload — the detail screen reads this, not twelve endpoints. */
  getJobDescriptionDetail(id: string) {
    return apiService.get<JobDescriptionDetail>(`${this.jobs}/descriptions/${id}/details`);
  }

  /** ⚠ Returns SUMMARIES, not full job descriptions. */
  getJobDescriptionsForPosition(positionId: string) {
    return apiService.get<JobDescriptionSummary[]>(`${this.jobs}/descriptions/position/${positionId}`);
  }

  /** The one in force for a position (FR-HR-134). Null when the position has none approved. */
  getCurrentForPosition(positionId: string) {
    return apiService.get<JobDescription>(`${this.jobs}/descriptions/position/${positionId}/current`);
  }

  getVersionHistory(positionId: string) {
    return apiService.get<JobDescriptionSummary[]>(`${this.jobs}/descriptions/position/${positionId}/history`);
  }

  /** ⚠ Summaries again. */
  getJobDescriptionsByStatus(status: JobDescriptionStatus) {
    return apiService.get<JobDescriptionSummary[]>(`${this.jobs}/descriptions/status/${status}`);
  }

  getDueForReview() {
    return apiService.get<JobDescriptionSummary[]>(`${this.jobs}/descriptions/due-review`);
  }

  createJobDescription(payload: CreateJobDescription) {
    return apiService.post<JobDescription>(`${this.jobs}/descriptions`, payload);
  }

  updateJobDescription(id: string, payload: UpdateJobDescription) {
    return apiService.put<JobDescription>(`${this.jobs}/descriptions/${id}`, payload);
  }

  submitJobDescription(id: string) {
    return apiService.post(`${this.jobs}/descriptions/${id}/submit`, { jobDescriptionId: id });
  }

  reviewJobDescription(id: string, isApproved: boolean, comments?: string) {
    return apiService.post(`${this.jobs}/descriptions/${id}/review`, {
      jobDescriptionId: id,
      isApproved,
      comments,
    });
  }

  /** ⚠ Admin-tier, and 409s once a workflow definition is published. */
  approveJobDescription(id: string, comments?: string) {
    return apiService.post(`${this.jobs}/descriptions/${id}/approve`, {
      jobDescriptionId: id,
      comments,
    });
  }

  /** The engine route. Gated by `CanUserApproveAsync`, not by a permission. */
  approveJobDescriptionOnWorkflow(id: string) {
    return apiService.post(`${this.jobs}/descriptions/${id}/workflow/approve`, {});
  }

  rejectJobDescriptionOnWorkflow(id: string, reason?: string) {
    return apiService.post(`${this.jobs}/descriptions/${id}/workflow/reject`, { reason });
  }

  /** A new version inherits the classification and valuation; the original is retired on approval. */
  createNewVersion(id: string, revisionReason: string) {
    return apiService.post<JobDescription>(`${this.jobs}/descriptions/${id}/version`, {
      originalJobDescriptionId: id,
      revisionReason,
    });
  }

  cloneJobDescription(id: string) {
    return apiService.post<JobDescription>(`${this.jobs}/descriptions/${id}/clone`, {});
  }

  deleteJobDescription(id: string) {
    return apiService.delete(`${this.jobs}/descriptions/${id}`);
  }

  // ── coverage & analytics ───────────────────────────────────────────────────

  getAnalytics() {
    return apiService.get<JobAnalytics>(`${this.jobs}/analytics`);
  }

  /** Positions with no approved job description, most-populated first. */
  getUncoveredPositions() {
    return apiService.get<UncoveredPosition[]>(`${this.jobs}/positions/uncovered`);
  }

  // ── taxonomy ───────────────────────────────────────────────────────────────

  getJobFamilies() {
    return apiService.get<JobFamily[]>(`${this.architecture}/families`);
  }

  getActiveJobFamilies() {
    return apiService.get<JobFamily[]>(`${this.architecture}/families/active`);
  }

  createJobFamily(payload: { code: string; name: string; description?: string; isActive: boolean }) {
    return apiService.post<JobFamily>(`${this.architecture}/families`, payload);
  }

  updateJobFamily(id: string, payload: { id: string; code: string; name: string; description?: string; isActive: boolean }) {
    return apiService.put<JobFamily>(`${this.architecture}/families/${id}`, payload);
  }

  /** ⚠ 409s when sub-families or job descriptions still hang off it, naming both in one message. */
  deleteJobFamily(id: string) {
    return apiService.delete(`${this.architecture}/families/${id}`);
  }

  getSubFamilies(familyId: string) {
    return apiService.get<JobSubFamily[]>(`${this.architecture}/families/${familyId}/sub-families`);
  }

  getActiveSubFamilies() {
    return apiService.get<JobSubFamily[]>(`${this.architecture}/sub-families/active`);
  }

  createSubFamily(familyId: string, payload: { code: string; name: string; description?: string; isActive: boolean }) {
    return apiService.post<JobSubFamily>(`${this.architecture}/families/${familyId}/sub-families`, payload);
  }

  updateSubFamily(id: string, payload: { id: string; jobFamilyId: string; code: string; name: string; description?: string; isActive: boolean }) {
    return apiService.put<JobSubFamily>(`${this.architecture}/sub-families/${id}`, payload);
  }

  deleteSubFamily(id: string) {
    return apiService.delete(`${this.architecture}/sub-families/${id}`);
  }

  getJobLevels() {
    return apiService.get<JobLevel[]>(`${this.architecture}/levels`);
  }

  getActiveJobLevels() {
    return apiService.get<JobLevel[]>(`${this.architecture}/levels/active`);
  }

  /** ⚠ `rank` must be unique — two levels at one rank make the ladder unanswerable. */
  createJobLevel(payload: { code: string; name: string; rank: number; description?: string; salaryGradeId?: string | null; isActive: boolean }) {
    return apiService.post<JobLevel>(`${this.architecture}/levels`, payload);
  }

  updateJobLevel(id: string, payload: { id: string; code: string; name: string; rank: number; description?: string; salaryGradeId?: string | null; isActive: boolean }) {
    return apiService.put<JobLevel>(`${this.architecture}/levels/${id}`, payload);
  }

  deleteJobLevel(id: string) {
    return apiService.delete(`${this.architecture}/levels/${id}`);
  }

  // ── competency framework ───────────────────────────────────────────────────

  getCompetencies() {
    return apiService.get<Competency[]>(`${this.competencies}/all`);
  }

  getActiveCompetencies() {
    return apiService.get<Competency[]>(`${this.competencies}/active`);
  }

  getCompetency(id: string) {
    return apiService.get<Competency>(`${this.competencies}/${id}`);
  }

  createCompetency(payload: {
    code: string;
    name: string;
    description: string;
    competencyCategory: string;
    proficiencyScaleMax: number;
    isActive: boolean;
  }) {
    return apiService.post<Competency>(this.competencies, payload);
  }

  updateCompetency(id: string, payload: { id: string; code: string; name: string; description: string; competencyCategory: string; proficiencyScaleMax: number; isActive: boolean }) {
    return apiService.put<Competency>(`${this.competencies}/${id}`, payload);
  }

  deleteCompetency(id: string) {
    return apiService.delete(`${this.competencies}/${id}`);
  }

  getSkillIndicators(competencyId: string) {
    return apiService.get<CompetencySkillIndicator[]>(`${this.competencies}/${competencyId}/skill-indicators`);
  }

  addSkillIndicator(competencyId: string, payload: { skillId: string; minimumSkillLevelRequired: string; rationale?: string }) {
    return apiService.post<CompetencySkillIndicator>(`${this.competencies}/${competencyId}/skill-indicators`, {
      competencyId,
      ...payload,
    });
  }

  /** ⚠ `id` is inherited from `UpdateDtoBase` and IS required — omit it and every update 400s. */
  updateSkillIndicator(id: string, payload: { minimumSkillLevelRequired: string; rationale?: string }) {
    return apiService.put<CompetencySkillIndicator>(`${this.indicators}/${id}`, { id, ...payload });
  }

  deleteSkillIndicator(id: string) {
    return apiService.delete(`${this.indicators}/${id}`);
  }

  // ── position requirements ──────────────────────────────────────────────────

  getPositionCompetencies(positionId: string) {
    return apiService.get<PositionCompetency[]>(`${this.positionCompetencies}/position/${positionId}`);
  }

  /**
   * Replaces a position's whole requirement set.
   *
   * ⚠ A REPLACE, not a merge: anything omitted is removed. Send the complete set every time.
   */
  bulkSetPositionCompetencies(positionId: string, competencies: Array<{ competencyId: string; requiredProficiencyLevel: number; notes?: string }>) {
    return apiService.put<PositionCompetency[]>(`${this.positionCompetencies}/position/${positionId}/bulk-set`, {
      positionId,
      competencies,
    });
  }

  // ── employee assessment ────────────────────────────────────────────────────

  getEmployeeCompetencies(employeeId: string) {
    return apiService.get<EmployeeCompetency[]>(`${this.employeeCompetencies}/employee/${employeeId}`);
  }

  getEmployeeProfile(employeeId: string) {
    return apiService.get<EmployeeCompetencyProfile>(`${this.employeeCompetencies}/employee/${employeeId}/profile`);
  }

  /** ⚠ An OBJECT with `competencyGaps` inside it, not a list. */
  getEmployeeGaps(employeeId: string) {
    return apiService.get<EmployeePositionCompetencyGapSummary>(`${this.employeeCompetencies}/employee/${employeeId}/gaps`);
  }

  /**
   * The signed-in employee's own profile and gaps.
   *
   * ⚠ These exist because the client `User` object carries NO employee link, so a screen has no id
   * to put in the by-employee routes on the caller's own behalf. Plain `[Authorize]`.
   */
  getMyProfile() {
    return apiService.get<EmployeeCompetencyProfile>(`${this.employeeCompetencies}/me/profile`);
  }

  getMyGaps() {
    return apiService.get<EmployeePositionCompetencyGapSummary>(`${this.employeeCompetencies}/me/gaps`);
  }

  /** ⚠ The assessor defaults to the caller when `assessedById` is omitted. */
  assessEmployee(payload: {
    employeeId: string;
    competencyId: string;
    currentProficiencyLevel: number;
    assessmentDate: string;
    assessedById?: string | null;
    assessmentMethod?: string;
    evidenceNotes?: string;
  }) {
    return apiService.post<EmployeeCompetency>(this.employeeCompetencies, payload);
  }

  /** ⚠ `id` inherited from `UpdateDtoBase`; a re-assessment snapshots the previous values. */
  reassessEmployee(id: string, payload: {
    currentProficiencyLevel: number;
    assessmentDate: string;
    assessmentMethod?: string;
    evidenceNotes?: string;
    changeReason?: string;
  }) {
    return apiService.put<EmployeeCompetency>(`${this.employeeCompetencies}/${id}`, { id, ...payload });
  }

  /** Where the organisation is short — the training-needs view. */
  getOrganisationGaps() {
    return apiService.get<OrganisationCompetencyGap[]>(`${this.employeeCompetencies}/gaps/organisation`);
  }

  // ── manpower budget ────────────────────────────────────────────────────────

  getBudgetsPaged(params: { pageNumber?: number; pageSize?: number } = {}) {
    return apiService.get<PagedResult<ManpowerBudget>>(`${this.jobs}/budgets/paged`, params);
  }

  getBudget(id: string) {
    return apiService.get<ManpowerBudget>(`${this.jobs}/budgets/${id}`);
  }

  getBudgetLines(id: string) {
    return apiService.get<ManpowerBudgetLine[]>(`${this.jobs}/budgets/${id}/lines`);
  }

  getBudgetsForYear(fiscalYear: number) {
    return apiService.get<ManpowerBudget[]>(`${this.jobs}/budgets/year/${fiscalYear}`);
  }

  getPendingBudgetApprovals() {
    return apiService.get<ManpowerBudget[]>(`${this.jobs}/budgets/pending-approvals`);
  }

  createBudget(payload: Partial<ManpowerBudget> & { fiscalYear: number; periodStartDate: string; periodEndDate: string }) {
    return apiService.post<ManpowerBudget>(`${this.jobs}/budgets`, payload);
  }

  addBudgetLine(budgetId: string, payload: Partial<ManpowerBudgetLine> & { positionId: string }) {
    return apiService.post<ManpowerBudgetLine>(`${this.jobs}/budgets/${budgetId}/lines`, {
      manpowerBudgetId: budgetId,
      ...payload,
    });
  }

  /** ⚠ 409s on a budget with no lines: it authorises no posts, so there is nothing to approve. */
  submitBudget(id: string) {
    return apiService.post(`${this.jobs}/budgets/${id}/submit`, {});
  }

  /** ⚠ Admin-tier, and 409s once a workflow definition is published. */
  approveBudget(id: string, comments?: string) {
    return apiService.post(`${this.jobs}/budgets/${id}/approve`, { budgetId: id, comments });
  }

  rejectBudget(id: string, reason: string) {
    return apiService.post(`${this.jobs}/budgets/${id}/reject`, { reason });
  }

  /** FR-HR-135's chain. Gated by the engine; the last approval is what establishes the posts. */
  approveBudgetOnWorkflow(id: string) {
    return apiService.post(`${this.jobs}/budgets/${id}/workflow/approve`, {});
  }

  rejectBudgetOnWorkflow(id: string, reason?: string) {
    return apiService.post(`${this.jobs}/budgets/${id}/workflow/reject`, { reason });
  }

  // ── establishment (FR-HR-136) ──────────────────────────────────────────────

  getPositionEstablishment(positionId: string) {
    return apiService.get<PositionEstablishment>(`${this.jobs}/establishment/position/${positionId}`);
  }

  /** ⚠ Admin-tier. Refuses a headcount below the number already in post. */
  setPositionEstablishment(positionId: string, expectedHeadcount: number, reason: string) {
    return apiService.put<PositionEstablishment>(`${this.jobs}/establishment/position/${positionId}`, {
      expectedHeadcount,
      reason,
    });
  }

  /** Returns the post to unconstrained. `expectedHeadcount` is kept as a planning figure. */
  withdrawPositionEstablishment(positionId: string, reason: string) {
    return apiService.delete<PositionEstablishment>(
      `${this.jobs}/establishment/position/${positionId}?reason=${encodeURIComponent(reason)}`,
    );
  }
}

export const jobArchitectureService = new JobArchitectureService();
