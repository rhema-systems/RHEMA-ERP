import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  BatchAssessmentResult,
  Competency,
  CompetencySkillIndicator,
  CreateJobCompetency,
  CreateJobDescription,
  CreateJobDutyItem,
  CreateJobEquipmentTool,
  CreateJobEquipmentTraining,
  CreateJobMedicalRequirement,
  CreateJobPhysicalDemand,
  CreateJobPpeRequirement,
  CreateJobQualification,
  CreateJobReportingRelationship,
  CreateJobResponsibility,
  CreateJobResponsibilityKpi,
  CreateJobWorkingCondition,
  EmployeeCompetency,
  EmployeeCompetencyProfile,
  EmployeePositionCompetencyGapSummary,
  JobAnalytics,
  JobCompetency,
  JobDescription,
  JobDescriptionDetail,
  JobDescriptionStatus,
  JobDescriptionSummary,
  JobDutyItem,
  JobEquipmentTool,
  JobEquipmentTraining,
  JobFamily,
  JobLevel,
  JobMedicalRequirement,
  JobPhysicalDemand,
  JobPpeRequirement,
  JobQualification,
  JobReportingRelationship,
  JobResponsibility,
  JobResponsibilityKpi,
  JobSubFamily,
  JobValuationSummary,
  JobWorkingCondition,
  ManpowerBudget,
  ManpowerPlanningBaseline,
  PositionSalaryReference,
  ManpowerBudgetLine,
  OrganisationCompetencyGap,
  PositionCompetency,
  PositionEstablishment,
  UncoveredPosition,
  UpdateJobCompetency,
  UpdateJobDescription,
  UpdateJobDutyItem,
  UpdateJobEquipmentTool,
  UpdateJobEquipmentTraining,
  UpdateJobMedicalRequirement,
  UpdateJobPhysicalDemand,
  UpdateJobPpeRequirement,
  UpdateJobQualification,
  UpdateJobReportingRelationship,
  UpdateJobResponsibility,
  UpdateJobResponsibilityKpi,
  UpdateJobWorkingCondition,
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

  /**
   * What the role is worth, from the money attached to its qualifications and competencies plus its
   * intrinsic value, blended with any industry benchmark and banded at ±10%.
   *
   * Safe: it computes and returns, and changes nothing — so it belongs in a `useQuery` like any
   * other read. Until 2026-09-07 it was not safe, and that is the whole point of the pair below:
   * this GET persisted what it computed, so a retry, a cache revalidation or a refetch-on-focus was
   * an UPDATE, and `SaveChanges` stamped `UpdatedAt` on the record every time.
   */
  getValuation(id: string) {
    return apiService.get<JobValuationSummary>(`${this.jobs}/descriptions/${id}/valuation`);
  }

  /**
   * Work the valuation out and STORE it on the job description.
   *
   * ⚠ `HR.JobArchitecture.Write`, and the API refuses an approved record — storing an estimate is
   * an edit to the document, and an approved one is the version in force for its position.
   */
  recalculateValuation(id: string) {
    return apiService.post<JobValuationSummary>(`${this.jobs}/descriptions/${id}/valuation`);
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

  // ── the twelve child collections ───────────────────────────────────────────

  /*
   * Every collection below follows one shape, and three things about it are not guessable from
   * the route:
   *
   * 1. **The parent id goes in the URL only.** `AddDutyItem` and its siblings all begin
   *    `dto.JobDescriptionId = jobDescriptionId;` — the controller overwrites whatever the body
   *    carried, so putting it in the payload is noise.
   * 2. **The update body must repeat the id.** It is inherited from `UpdateDtoBase`, so it does
   *    not appear in the C# class at all, and every controller opens with
   *    `if (id != dto.Id) return BadRequest("ID mismatch")`. Each `update*` below folds the id in
   *    for the caller, because a caller reading the DTO would never know to send it.
   * 3. **The verbs are not on one tier.** POST and PUT sit on `JobArchitectureWritePolicy`, which
   *    the HR role holds; **every DELETE sits on `JobArchitectureAdminPolicy`, which it does not**.
   *    An HR author can add a duty and edit it but cannot remove it — see `canDeleteChildRows` in
   *    the authoring panels, which hides the affordance rather than offering a 403.
   *
   * The nested pair are addressed differently on purpose: equipment training hangs off an
   * equipment TOOL and KPIs hang off a RESPONSIBILITY, so neither takes a job-description id.
   */

  // duty items

  getDutyItems(jobDescriptionId: string) {
    return apiService.get<JobDutyItem[]>(`${this.jobs}/descriptions/${jobDescriptionId}/duty-items`);
  }

  /** ⚠ `sequenceNumber: 0` asks the server for the next number; any other value is honoured. */
  addDutyItem(jobDescriptionId: string, payload: CreateJobDutyItem) {
    return apiService.post<JobDutyItem>(`${this.jobs}/descriptions/${jobDescriptionId}/duty-items`, payload);
  }

  updateDutyItem(id: string, payload: Omit<UpdateJobDutyItem, 'id'>) {
    return apiService.put<JobDutyItem>(`${this.jobs}/duty-items/${id}`, { id, ...payload });
  }

  /** ⚠ Admin-tier, like every other delete in this family. */
  deleteDutyItem(id: string) {
    return apiService.delete(`${this.jobs}/duty-items/${id}`);
  }

  // responsibilities

  getResponsibilities(jobDescriptionId: string) {
    return apiService.get<JobResponsibility[]>(`${this.jobs}/descriptions/${jobDescriptionId}/responsibilities`);
  }

  /**
   * ⚠ The create DTO also accepts nested `qualifications` and `competencies` arrays. This method
   * deliberately does not expose them: those rows are authored in their own panels, where they can
   * be edited and removed afterwards, and a row created through the nested arrays is
   * indistinguishable from one created directly.
   */
  addResponsibility(jobDescriptionId: string, payload: CreateJobResponsibility) {
    return apiService.post<JobResponsibility>(
      `${this.jobs}/descriptions/${jobDescriptionId}/responsibilities`,
      payload,
    );
  }

  updateResponsibility(id: string, payload: Omit<UpdateJobResponsibility, 'id'>) {
    return apiService.put<JobResponsibility>(`${this.jobs}/responsibilities/${id}`, { id, ...payload });
  }

  deleteResponsibility(id: string) {
    return apiService.delete(`${this.jobs}/responsibilities/${id}`);
  }

  // KPIs — nested under a responsibility, NOT under the job description

  getResponsibilityKpis(responsibilityId: string) {
    return apiService.get<JobResponsibilityKpi[]>(`${this.jobs}/responsibilities/${responsibilityId}/kpis`);
  }

  addResponsibilityKpi(responsibilityId: string, payload: CreateJobResponsibilityKpi) {
    return apiService.post<JobResponsibilityKpi>(
      `${this.jobs}/responsibilities/${responsibilityId}/kpis`,
      payload,
    );
  }

  /** ⚠ The route is `kpis/{id}` — it does not repeat the responsibility. */
  updateResponsibilityKpi(id: string, payload: Omit<UpdateJobResponsibilityKpi, 'id'>) {
    return apiService.put<JobResponsibilityKpi>(`${this.jobs}/kpis/${id}`, { id, ...payload });
  }

  deleteResponsibilityKpi(id: string) {
    return apiService.delete(`${this.jobs}/kpis/${id}`);
  }

  // qualifications

  getQualifications(jobDescriptionId: string) {
    return apiService.get<JobQualification[]>(`${this.jobs}/descriptions/${jobDescriptionId}/qualifications`);
  }

  addQualification(jobDescriptionId: string, payload: CreateJobQualification) {
    return apiService.post<JobQualification>(
      `${this.jobs}/descriptions/${jobDescriptionId}/qualifications`,
      payload,
    );
  }

  /** ⚠ No `jobResponsibilityId` — the update DTO has no such field, so the link is create-only. */
  updateQualification(id: string, payload: Omit<UpdateJobQualification, 'id'>) {
    return apiService.put<JobQualification>(`${this.jobs}/qualifications/${id}`, { id, ...payload });
  }

  deleteQualification(id: string) {
    return apiService.delete(`${this.jobs}/qualifications/${id}`);
  }

  // competencies required by the job

  getJobCompetencies(jobDescriptionId: string) {
    return apiService.get<JobCompetency[]>(`${this.jobs}/descriptions/${jobDescriptionId}/competencies`);
  }

  addJobCompetency(jobDescriptionId: string, payload: CreateJobCompetency) {
    return apiService.post<JobCompetency>(`${this.jobs}/descriptions/${jobDescriptionId}/competencies`, payload);
  }

  /** ⚠ No `jobResponsibilityId`, same as qualifications. */
  updateJobCompetency(id: string, payload: Omit<UpdateJobCompetency, 'id'>) {
    return apiService.put<JobCompetency>(`${this.jobs}/competencies/${id}`, { id, ...payload });
  }

  deleteJobCompetency(id: string) {
    return apiService.delete(`${this.jobs}/competencies/${id}`);
  }

  // physical demands

  getPhysicalDemands(jobDescriptionId: string) {
    return apiService.get<JobPhysicalDemand[]>(`${this.jobs}/descriptions/${jobDescriptionId}/physical-demands`);
  }

  addPhysicalDemand(jobDescriptionId: string, payload: CreateJobPhysicalDemand) {
    return apiService.post<JobPhysicalDemand>(
      `${this.jobs}/descriptions/${jobDescriptionId}/physical-demands`,
      payload,
    );
  }

  updatePhysicalDemand(id: string, payload: Omit<UpdateJobPhysicalDemand, 'id'>) {
    return apiService.put<JobPhysicalDemand>(`${this.jobs}/physical-demands/${id}`, { id, ...payload });
  }

  deletePhysicalDemand(id: string) {
    return apiService.delete(`${this.jobs}/physical-demands/${id}`);
  }

  // working conditions

  getWorkingConditions(jobDescriptionId: string) {
    return apiService.get<JobWorkingCondition[]>(
      `${this.jobs}/descriptions/${jobDescriptionId}/working-conditions`,
    );
  }

  addWorkingCondition(jobDescriptionId: string, payload: CreateJobWorkingCondition) {
    return apiService.post<JobWorkingCondition>(
      `${this.jobs}/descriptions/${jobDescriptionId}/working-conditions`,
      payload,
    );
  }

  updateWorkingCondition(id: string, payload: Omit<UpdateJobWorkingCondition, 'id'>) {
    return apiService.put<JobWorkingCondition>(`${this.jobs}/working-conditions/${id}`, { id, ...payload });
  }

  deleteWorkingCondition(id: string) {
    return apiService.delete(`${this.jobs}/working-conditions/${id}`);
  }

  // equipment and tools

  getEquipmentTools(jobDescriptionId: string) {
    return apiService.get<JobEquipmentTool[]>(`${this.jobs}/descriptions/${jobDescriptionId}/equipment-tools`);
  }

  addEquipmentTool(jobDescriptionId: string, payload: CreateJobEquipmentTool) {
    return apiService.post<JobEquipmentTool>(
      `${this.jobs}/descriptions/${jobDescriptionId}/equipment-tools`,
      payload,
    );
  }

  updateEquipmentTool(id: string, payload: Omit<UpdateJobEquipmentTool, 'id'>) {
    return apiService.put<JobEquipmentTool>(`${this.jobs}/equipment-tools/${id}`, { id, ...payload });
  }

  /** ⚠ Cascades to the tool's training requirements — they hang off it, not off the description. */
  deleteEquipmentTool(id: string) {
    return apiService.delete(`${this.jobs}/equipment-tools/${id}`);
  }

  // equipment training — nested under a TOOL

  getEquipmentTrainings(equipmentToolId: string) {
    return apiService.get<JobEquipmentTraining[]>(`${this.jobs}/equipment-tools/${equipmentToolId}/training`);
  }

  addEquipmentTraining(equipmentToolId: string, payload: CreateJobEquipmentTraining) {
    return apiService.post<JobEquipmentTraining>(
      `${this.jobs}/equipment-tools/${equipmentToolId}/training`,
      payload,
    );
  }

  /** ⚠ The write routes are `equipment-training` (singular); the read is `.../training`. */
  updateEquipmentTraining(id: string, payload: Omit<UpdateJobEquipmentTraining, 'id'>) {
    return apiService.put<JobEquipmentTraining>(`${this.jobs}/equipment-training/${id}`, { id, ...payload });
  }

  deleteEquipmentTraining(id: string) {
    return apiService.delete(`${this.jobs}/equipment-training/${id}`);
  }

  // reporting relationships

  getReportingRelationships(jobDescriptionId: string) {
    return apiService.get<JobReportingRelationship[]>(
      `${this.jobs}/descriptions/${jobDescriptionId}/reporting-relationships`,
    );
  }

  addReportingRelationship(jobDescriptionId: string, payload: CreateJobReportingRelationship) {
    return apiService.post<JobReportingRelationship>(
      `${this.jobs}/descriptions/${jobDescriptionId}/reporting-relationships`,
      payload,
    );
  }

  updateReportingRelationship(id: string, payload: Omit<UpdateJobReportingRelationship, 'id'>) {
    return apiService.put<JobReportingRelationship>(`${this.jobs}/reporting-relationships/${id}`, {
      id,
      ...payload,
    });
  }

  deleteReportingRelationship(id: string) {
    return apiService.delete(`${this.jobs}/reporting-relationships/${id}`);
  }

  // PPE requirements

  getPpeRequirements(jobDescriptionId: string) {
    return apiService.get<JobPpeRequirement[]>(`${this.jobs}/descriptions/${jobDescriptionId}/ppe-requirements`);
  }

  addPpeRequirement(jobDescriptionId: string, payload: CreateJobPpeRequirement) {
    return apiService.post<JobPpeRequirement>(
      `${this.jobs}/descriptions/${jobDescriptionId}/ppe-requirements`,
      payload,
    );
  }

  updatePpeRequirement(id: string, payload: Omit<UpdateJobPpeRequirement, 'id'>) {
    return apiService.put<JobPpeRequirement>(`${this.jobs}/ppe-requirements/${id}`, { id, ...payload });
  }

  deletePpeRequirement(id: string) {
    return apiService.delete(`${this.jobs}/ppe-requirements/${id}`);
  }

  // medical requirements

  getMedicalRequirements(jobDescriptionId: string) {
    return apiService.get<JobMedicalRequirement[]>(
      `${this.jobs}/descriptions/${jobDescriptionId}/medical-requirements`,
    );
  }

  addMedicalRequirement(jobDescriptionId: string, payload: CreateJobMedicalRequirement) {
    return apiService.post<JobMedicalRequirement>(
      `${this.jobs}/descriptions/${jobDescriptionId}/medical-requirements`,
      payload,
    );
  }

  updateMedicalRequirement(id: string, payload: Omit<UpdateJobMedicalRequirement, 'id'>) {
    return apiService.put<JobMedicalRequirement>(`${this.jobs}/medical-requirements/${id}`, { id, ...payload });
  }

  deleteMedicalRequirement(id: string) {
    return apiService.delete(`${this.jobs}/medical-requirements/${id}`);
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

  /**
   * Removes an assessment outright.
   *
   * ⚠ **A re-assessment is not a removal, and that is the whole reason this exists.** Correcting a
   * level snapshots the previous one to history, so an assessment recorded against the wrong person
   * or the wrong competency stayed in that person's record for ever, reading as a real judgement
   * somebody once made. This is the only way to take one back.
   *
   * ⚠ Admin tier — `HR.Policy.CompetencyAdmin`, not the assessor's own permission. Gate the
   * affordance to match, or the desk gets a 403 from a button the screen offered it.
   */
  deleteEmployeeCompetency(id: string) {
    return apiService.delete(`${this.employeeCompetencies}/${id}`);
  }

  /**
   * Records several competency levels for one employee in a single call.
   *
   * ⚠ Handles BOTH cases: omit `employeeCompetencyId` and give `competencyId` for a first
   * assessment; give `employeeCompetencyId` to re-assess, which snapshots the previous values to
   * history rather than overwriting them.
   *
   * ⚠ The result is per-row, not pass/fail — `succeeded`, `failed` and an `errors` list keyed by
   * competency. The screen must show every skipped row and its reason: a batch that reports success
   * for work it did not do is worse than one that refuses. Proven by
   * `hr-tierb-tail/probe-lane3-groupA.mjs`, which submits one good row and one bad one and asserts
   * the good row still lands.
   *
   * ⚠ The assessor is the token's employee id. An administrator whose account is not linked to an
   * employee record gets a 400, not a 403 — established by that probe rather than assumed.
   */
  batchAssess(employeeId: string, updates: Array<{
    employeeCompetencyId?: string | null;
    competencyId?: string | null;
    newLevel: number;
    assessmentMethod: string;
    evidenceNotes?: string | null;
    changeReason?: string | null;
  }>): Promise<BatchAssessmentResult> {
    return apiService.post<BatchAssessmentResult>(
      `${this.employeeCompetencies}/batch-assess`, { employeeId, updates });
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

  /**
   * Correct a budget.
   *
   * ⚠ The update is a REPLACE: every figure the DTO names is written, so a form that omits one
   * writes a zero over it. The dialog sends the whole set, seeded from the budget it is editing.
   *
   * ⚠ `HR.ManpowerBudget.Write` — but the DELETE below is Admin. Established by a 403, not
   * assumed (`hr-tierb-tail/probe-lane2-groupB.mjs`).
   */
  updateBudget(id: string, payload: Partial<ManpowerBudget> & {
    id: string; periodStartDate: string; periodEndDate: string;
  }) {
    return apiService.put<ManpowerBudget>(`${this.jobs}/budgets/${id}`, payload);
  }

  /** ⚠ `HR.ManpowerBudget.Admin`, a rung above the edit. */
  deleteBudget(id: string) {
    return apiService.delete<void>(`${this.jobs}/budgets/${id}`);
  }

  /**
   * Correct a line.
   *
   * ⚠ Note the route: a line is CREATED under its budget and then addressed at
   * `JobAnalysis/lines/{id}` — the same asymmetry the bank branches have.
   */
  updateBudgetLine(lineId: string, payload: ManpowerBudgetLineWrite & { id: string }) {
    return apiService.put<ManpowerBudgetLine>(`${this.jobs}/lines/${lineId}`, payload);
  }

  /** ⚠ `HR.ManpowerBudget.Admin`, like the budget delete. */
  deleteBudgetLine(lineId: string) {
    return apiService.delete<void>(`${this.jobs}/lines/${lineId}`);
  }

  addBudgetLine(budgetId: string, payload: ManpowerBudgetLineWrite & { positionId: string }) {
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

  /**
   * The planning baseline for a unit's subtree over a period (round 2b, R2): serving headcount,
   * estimated salary cost, exits due, each post against its establishment. Dates as `YYYY-MM-DD`.
   */
  getPlanningBaseline(organizationUnitId: string, periodStart: string, periodEnd: string) {
    return apiService.get<ManpowerPlanningBaseline>(`${this.jobs}/budgets/planning-baseline`, {
      organizationUnitId,
      periodStart,
      periodEnd,
    });
  }

  /** The grade a position carries, for the budget line's scale picker (R3). */
  getPositionSalaryReference(positionId: string) {
    return apiService.get<PositionSalaryReference>(`${this.jobs}/positions/${positionId}/salary-reference`);
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

/**
 * A budget line as written (R3). `plannedAverageSalary: null` means "read it from the scale
 * named by the grade/level/notch ids"; a number is kept as typed. The total is never sent.
 */
export type ManpowerBudgetLineWrite = Omit<Partial<ManpowerBudgetLine>, 'plannedAverageSalary'> & {
  plannedAverageSalary?: number | null;
};

export const jobArchitectureService = new JobArchitectureService();
