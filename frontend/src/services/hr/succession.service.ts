import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  SuccessionPlan,
  SuccessionPlanSummary,
  SuccessionPlanStatus,
  PositionCriticality,
  SuccessionRisk,
  SuccessionDashboard,
  SuccessionCompetencyRequirement,
  SuccessionActionSummary,
  SuccessionDocument,
  CompetencyLookup,
  CreateSuccessionPlan,
  UpdateSuccessionPlan,
  ReviewSuccessionPlan,
  ApproveSuccessionPlan,
} from '@/types/hr/succession';

/**
 * Succession plans. Backend route: api/succession-plans.
 *
 * <b>There is no self-service surface here, deliberately.</b> Every method on this class needs
 * `HR.Succession.Read` at least. Succession inverts the rule the rest of HR follows: a candidate's
 * readiness, retention risk and nine-box placement are assessments made *about* them, not records
 * belonging to them, so an employee has no view of their own succession record and none should be
 * added by pointing a screen at a desk endpoint with the signed-in user's id — it answers 403,
 * which is the point. See decision D-2 in `plans/HR-Area-13-Succession-Build-Plan.md`.
 *
 * ⚠ Three rungs, not two. Reading is `HR.Succession.Read`; authoring is `Write`; but **review,
 * approve, delete, and confidential documents are `Admin`** — naming a successor to a post is a
 * management act. An HR-role user can build a plan and will be refused when they try to approve it,
 * so a screen must not offer those buttons on the strength of being able to see the plan.
 */
class SuccessionService {
  private readonly baseUrl = '/succession-plans';

  // ── Reads (HR.Succession.Read) ─────────────────────────────────────────────

  /** ⚠ Paged: returns {items,totalCount,...}. Use {@link getAll} for a plain array. */
  getPaged(params: { pageNumber?: number; pageSize?: number } = {}) {
    return apiService.get<PagedResult<SuccessionPlanSummary>>(this.baseUrl, params);
  }

  getAll() {
    return apiService.get<SuccessionPlanSummary[]>(`${this.baseUrl}/all`);
  }

  getById(id: string) {
    return apiService.get<SuccessionPlan>(`${this.baseUrl}/${id}`);
  }

  getByNumber(planNumber: string) {
    return apiService.get<SuccessionPlan | null>(`${this.baseUrl}/number/${planNumber}`);
  }

  /** The live plan for a position, or null. Only an *approved* plan is the active version. */
  getActiveForPosition(positionId: string) {
    return apiService.get<SuccessionPlan | null>(`${this.baseUrl}/position/${positionId}/active`);
  }

  getVersionsForPosition(positionId: string) {
    return apiService.get<SuccessionPlanSummary[]>(`${this.baseUrl}/position/${positionId}/versions`);
  }

  getByStatus(status: SuccessionPlanStatus) {
    return apiService.get<SuccessionPlanSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getByYear(planYear: number) {
    return apiService.get<SuccessionPlanSummary[]>(`${this.baseUrl}/year/${planYear}`);
  }

  getByCriticality(criticality: PositionCriticality) {
    return apiService.get<SuccessionPlanSummary[]>(`${this.baseUrl}/criticality/${criticality}`);
  }

  getByRiskLevel(riskLevel: SuccessionRisk) {
    return apiService.get<SuccessionPlanSummary[]>(`${this.baseUrl}/risk/${riskLevel}`);
  }

  getDueForReview(daysAhead = 30) {
    return apiService.get<SuccessionPlanSummary[]>(`${this.baseUrl}/due-for-review`, { daysAhead });
  }

  /** Plans whose successors all need time — the bench exists but nobody could step up today. */
  getWithNoReadyNowSuccessor() {
    return apiService.get<SuccessionPlanSummary[]>(`${this.baseUrl}/no-ready-now-successor`);
  }

  /** Plans with no candidates at all — an empty plan is worse than a thin one. */
  getWithNoSuccessors() {
    return apiService.get<SuccessionPlanSummary[]>(`${this.baseUrl}/no-successors`);
  }

  getByIncumbent(incumbentEmployeeId: string) {
    return apiService.get<SuccessionPlanSummary[]>(`${this.baseUrl}/incumbent/${incumbentEmployeeId}`);
  }

  getWithImpendingVacancy(daysAhead = 90) {
    return apiService.get<SuccessionPlanSummary[]>(`${this.baseUrl}/impending-vacancy`, { daysAhead });
  }

  getDashboard(planYear?: number) {
    return apiService.get<SuccessionDashboard>(
      `${this.baseUrl}/dashboard`,
      planYear ? { planYear } : undefined,
    );
  }

  // ── Writes (HR.Succession.Write) ───────────────────────────────────────────

  /**
   * Creates a **draft**. It does not become the position's active version until it is approved.
   *
   * ⚠ Answers **409** when the position already has a plan in progress; the message names the
   * blocking plan and is worth showing verbatim.
   */
  create(data: CreateSuccessionPlan) {
    return apiService.post<SuccessionPlan>(this.baseUrl, data);
  }

  /** ⚠ Refused once the plan is Approved — create a new version instead. */
  update(id: string, data: UpdateSuccessionPlan) {
    return apiService.put<SuccessionPlan>(`${this.baseUrl}/${id}`, data);
  }

  /** Draft → UnderReview. The submitter is the token; the call takes no body. */
  submit(id: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/submit`, {});
  }

  // ── Decisions (HR.Succession.Admin) ────────────────────────────────────────

  /** ⚠ Admin, not Write. Carries no reviewer id — the reviewer is the signed-in user. */
  review(id: string, data: ReviewSuccessionPlan) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/review`, data);
  }

  /**
   * ⚠ Admin, not Write. Carries no approver id.
   *
   * Approving archives whatever plan the position had before, points it at this one via
   * `supersededByPlanId`, and makes this the active version.
   */
  approve(id: string, data: ApproveSuccessionPlan) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/approve`, data);
  }

  /** ⚠ Admin. Refused for an approved plan: it is a record, not a draft. */
  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Children ───────────────────────────────────────────────────────────────

  /** ⚠ Empty until area 17 (competency) lands. Render "none defined", not a spinner. */
  getCompetencyLookup() {
    return apiService.get<CompetencyLookup[]>(`${this.baseUrl}/competency-lookup`);
  }

  getCompetencyRequirements(id: string) {
    return apiService.get<SuccessionCompetencyRequirement[]>(
      `${this.baseUrl}/${id}/competency-requirements`,
    );
  }

  getActions(id: string) {
    return apiService.get<SuccessionActionSummary[]>(`${this.baseUrl}/${id}/actions`);
  }

  getDocuments(id: string) {
    return apiService.get<SuccessionDocument[]>(`${this.baseUrl}/${id}/documents`);
  }

  /** ⚠ Admin-only, and a separate list from {@link getDocuments} — not a filter over it. */
  getConfidentialDocuments(id: string) {
    return apiService.get<SuccessionDocument[]>(`${this.baseUrl}/${id}/documents/confidential`);
  }
}

export const successionService = new SuccessionService();
