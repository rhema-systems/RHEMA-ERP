import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  SuccessionPlan,
  SuccessionCandidate,
  SuccessionCandidateGap,
  SuccessionCandidateFeedback,
  SuccessionDevelopmentActivitySummary,
  ReadinessLevel,
  RetentionRisk,
  CreateSuccessionCandidate,
  UpdateSuccessionCandidate,
  AssessCandidate,
  CandidateRankUpdate,
  CreateSuccessionCandidateFeedback,
  CreateSuccessionCandidateGap,
  SuccessionDevelopmentActivity,
  SuccessionDevelopmentMilestone,
  DevelopmentActivityStatus,
  CreateSuccessionDevelopmentActivity,
  UpdateSuccessionDevelopmentActivity,
  CreateSuccessionDevelopmentMilestone,
  TalentPool,
  TalentPoolSummary,
  TalentPoolMember,
  TalentPoolTypeDefinition,
  CreateTalentPool,
  UpdateTalentPool,
  CreateTalentPoolMember,
  RemoveTalentPoolMember,
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

/**
 * Succession candidates. Backend route: api/succession-candidates.
 *
 * ⚠ **Recommendation is the gate on selection.** `selectCandidate` refuses a candidate who has not
 * been assessed and recommended, so a screen that offers Select without showing whether the
 * candidate is recommended is offering a button that will fail.
 *
 * ⚠ The assessor and the assessment date are **not** on {@link AssessCandidate}. They come from the
 * token and the clock — see the type's note for why that mattered here more than elsewhere.
 */
class SuccessionCandidateService {
  private readonly baseUrl = '/succession-candidates';

  getById(id: string) {
    return apiService.get<SuccessionCandidate>(`${this.baseUrl}/${id}`);
  }

  getByPlan(planId: string) {
    return apiService.get<SuccessionCandidate[]>(`${this.baseUrl}/plan/${planId}`);
  }

  getByEmployee(employeeId: string) {
    return apiService.get<SuccessionCandidate[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getByReadiness(planId: string, readiness: ReadinessLevel) {
    return apiService.get<SuccessionCandidate[]>(`${this.baseUrl}/plan/${planId}/readiness/${readiness}`);
  }

  getReadyNow(planId: string) {
    return apiService.get<SuccessionCandidate[]>(`${this.baseUrl}/plan/${planId}/ready-now`);
  }

  getEmergency(planId: string) {
    return apiService.get<SuccessionCandidate[]>(`${this.baseUrl}/plan/${planId}/emergency`);
  }

  /** At most one per plan — selecting a second deselects the first. */
  getSelected(planId: string) {
    return apiService.get<SuccessionCandidate | null>(`${this.baseUrl}/plan/${planId}/selected`);
  }

  getByRetentionRisk(planId: string, risk: RetentionRisk) {
    return apiService.get<SuccessionCandidate[]>(`${this.baseUrl}/plan/${planId}/retention-risk/${risk}`);
  }

  create(data: CreateSuccessionCandidate) {
    return apiService.post<SuccessionCandidate>(this.baseUrl, data);
  }

  update(id: string, data: UpdateSuccessionCandidate) {
    return apiService.put<SuccessionCandidate>(`${this.baseUrl}/${id}`, data);
  }

  /** ⚠ Admin, like every other delete in this area. */
  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  assess(id: string, data: AssessCandidate) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/assess`, data);
  }

  /** ⚠ Refused with 400 unless the candidate has been recommended. */
  select(id: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/select`, {});
  }

  /** ⚠ PATCH, not PUT, and it takes a bare array rather than a wrapper object. */
  bulkUpdateRanks(updates: CandidateRankUpdate[]) {
    return apiService.patch<{ message: string }>(`${this.baseUrl}/bulk-rank`, updates);
  }

  // ── Competency gaps ────────────────────────────────────────────────────────

  getGaps(id: string) {
    return apiService.get<SuccessionCandidateGap[]>(`${this.baseUrl}/${id}/gaps`);
  }

  getUnaddressedGaps(id: string) {
    return apiService.get<SuccessionCandidateGap[]>(`${this.baseUrl}/${id}/gaps/unaddressed`);
  }

  addGap(id: string, data: CreateSuccessionCandidateGap) {
    return apiService.post<SuccessionCandidateGap>(`${this.baseUrl}/${id}/gaps`, data);
  }

  updateGap(gapId: string, data: CreateSuccessionCandidateGap) {
    return apiService.put<SuccessionCandidateGap>(`${this.baseUrl}/gaps/${gapId}`, data);
  }

  removeGap(gapId: string) {
    return apiService.delete<void>(`${this.baseUrl}/gaps/${gapId}`);
  }

  /**
   * Derives gaps by comparing the candidate against the plan's competency requirements.
   *
   * ⚠ Returns an **empty array** today, and will keep doing so until area 17 populates the
   * competency library. That is not a failure — the screen must say so rather than showing a
   * silent no-op.
   */
  generateGapsFromPosition(id: string) {
    return apiService.post<SuccessionCandidateGap[]>(`${this.baseUrl}/${id}/gaps/generate-from-position`, {});
  }

  // ── Feedback ───────────────────────────────────────────────────────────────

  getFeedback(id: string) {
    return apiService.get<SuccessionCandidateFeedback[]>(`${this.baseUrl}/${id}/feedback`);
  }

  /** The reviewer is the signed-in user; there is no field to name someone else. */
  addFeedback(id: string, data: CreateSuccessionCandidateFeedback) {
    return apiService.post<SuccessionCandidateFeedback>(`${this.baseUrl}/${id}/feedback`, data);
  }

  /** ⚠ Admin. */
  removeFeedback(feedbackId: string) {
    return apiService.delete<void>(`${this.baseUrl}/feedback/${feedbackId}`);
  }

  getDevelopmentActivities(id: string) {
    return apiService.get<SuccessionDevelopmentActivitySummary[]>(
      `${this.baseUrl}/${id}/development-activities`,
    );
  }
}

export const successionCandidateService = new SuccessionCandidateService();

/**
 * Development activities and their milestones. Backend route: api/succession-development.
 *
 * ⚠ **An activity belongs to exactly one owner** — a succession candidate or a talent pool member,
 * never both and never neither. Sending zero or two owners is refused with a 400 that says so.
 *
 * ⚠ **There is money here**, which the area's first survey missed because it is not on the plan or
 * the candidate: `estimatedCost` / `actualCost` with a `currencyCode`. The code is validated against
 * Finance's currency master, so an unknown one is a 400 naming the code — `"ZZZ"` used to be stored
 * happily. No GL posting: that waits for the module-wide sweep.
 */
class SuccessionDevelopmentService {
  private readonly baseUrl = '/succession-development';

  getById(id: string) {
    return apiService.get<SuccessionDevelopmentActivity>(`${this.baseUrl}/${id}`);
  }

  /** Summaries — no milestones, no costs. Use {@link getByCandidateFull} for the whole record. */
  getByCandidate(candidateId: string) {
    return apiService.get<SuccessionDevelopmentActivitySummary[]>(
      `${this.baseUrl}/candidate/${candidateId}`,
    );
  }

  getByCandidateFull(candidateId: string) {
    return apiService.get<SuccessionDevelopmentActivity[]>(
      `${this.baseUrl}/candidate/${candidateId}/full`,
    );
  }

  getByMember(memberId: string) {
    return apiService.get<SuccessionDevelopmentActivitySummary[]>(`${this.baseUrl}/member/${memberId}`);
  }

  getByMemberFull(memberId: string) {
    return apiService.get<SuccessionDevelopmentActivity[]>(`${this.baseUrl}/member/${memberId}/full`);
  }

  getByStatus(status: DevelopmentActivityStatus) {
    return apiService.get<SuccessionDevelopmentActivitySummary[]>(`${this.baseUrl}/status/${status}`);
  }

  /** Activities past their planned end date that are neither Completed nor Cancelled. */
  getOverdue() {
    return apiService.get<SuccessionDevelopmentActivitySummary[]>(`${this.baseUrl}/overdue`);
  }

  create(data: CreateSuccessionDevelopmentActivity) {
    return apiService.post<SuccessionDevelopmentActivity>(this.baseUrl, data);
  }

  update(id: string, data: UpdateSuccessionDevelopmentActivity) {
    return apiService.put<SuccessionDevelopmentActivity>(`${this.baseUrl}/${id}`, data);
  }

  /** ⚠ Admin. */
  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  getMilestones(activityId: string) {
    return apiService.get<SuccessionDevelopmentMilestone[]>(`${this.baseUrl}/${activityId}/milestones`);
  }

  /** Across every activity, not just one — this is a desk queue. */
  getOverdueMilestones() {
    return apiService.get<SuccessionDevelopmentMilestone[]>(`${this.baseUrl}/milestones/overdue`);
  }

  addMilestone(activityId: string, data: CreateSuccessionDevelopmentMilestone) {
    return apiService.post<SuccessionDevelopmentMilestone>(
      `${this.baseUrl}/${activityId}/milestones`,
      data,
    );
  }

  updateMilestone(milestoneId: string, data: CreateSuccessionDevelopmentMilestone) {
    return apiService.put<SuccessionDevelopmentMilestone>(
      `${this.baseUrl}/milestones/${milestoneId}`,
      data,
    );
  }

  /**
   * ⚠ Completing every milestone does **not** advance the activity's own status — that stays
   * whatever it was set to. Measured, and left alone deliberately: whether an activity is finished
   * is a supervisor's judgement, not an arithmetic consequence of its milestones. See D-6.
   */
  completeMilestone(milestoneId: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/milestones/${milestoneId}/complete`, {});
  }

  /** ⚠ Admin. */
  removeMilestone(milestoneId: string) {
    return apiService.delete<void>(`${this.baseUrl}/milestones/${milestoneId}`);
  }
}

export const successionDevelopmentService = new SuccessionDevelopmentService();

/**
 * Employee talent pools. Backend route: api/talent-pools.
 *
 * ⚠ **Not to be confused with `api/talent-pool` (singular)**, which is recruitment's *candidate*
 * CRM — segments, engagement events, vacancy matching. Two unrelated concepts sharing a word; area 6
 * owns that one. See §4 of the build plan.
 *
 * ⚠ **One pool is written by area 5, not by this UI.** Approving an appraisal outcome recommendation
 * of type `SuccessionNomination` find-or-creates the pool named in appraisal settings (by default
 * "Appraisal Nominations") and adds the appraised employee to it. Members can arrive here without
 * anyone opening this screen.
 */
class TalentPoolService {
  private readonly baseUrl = '/talent-pools';

  getPaged(params: { pageNumber?: number; pageSize?: number } = {}) {
    return apiService.get<PagedResult<TalentPoolSummary>>(this.baseUrl, params);
  }

  getAll() {
    return apiService.get<TalentPoolSummary[]>(`${this.baseUrl}/all`);
  }

  getActive() {
    return apiService.get<TalentPoolSummary[]>(`${this.baseUrl}/active`);
  }

  /** Carries its members and a correct `currentMemberCount`. */
  getById(id: string) {
    return apiService.get<TalentPool>(`${this.baseUrl}/${id}`);
  }

  getWithMembers(id: string) {
    return apiService.get<TalentPool>(`${this.baseUrl}/${id}/with-members`);
  }

  getByType(poolTypeId: string) {
    return apiService.get<TalentPoolSummary[]>(`${this.baseUrl}/type/${poolTypeId}`);
  }

  getByOwner(ownerEmployeeId: string) {
    return apiService.get<TalentPoolSummary[]>(`${this.baseUrl}/owner/${ownerEmployeeId}`);
  }

  getMembers(id: string) {
    return apiService.get<TalentPoolMember[]>(`${this.baseUrl}/${id}/members`);
  }

  getMemberById(memberId: string) {
    return apiService.get<TalentPoolMember>(`${this.baseUrl}/members/${memberId}`);
  }

  getMembersByReadiness(id: string, readiness: ReadinessLevel) {
    return apiService.get<TalentPoolMember[]>(`${this.baseUrl}/${id}/members/readiness/${readiness}`);
  }

  /** A desk queue across every pool, not one pool's members. */
  getMembersDueForReview() {
    return apiService.get<TalentPoolMember[]>(`${this.baseUrl}/members/due-for-review`);
  }

  create(data: CreateTalentPool) {
    return apiService.post<TalentPool>(this.baseUrl, data);
  }

  update(id: string, data: UpdateTalentPool) {
    return apiService.put<TalentPool>(`${this.baseUrl}/${id}`, data);
  }

  /** ⚠ Admin. */
  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /**
   * ⚠ **409** if the employee is already an active member — the message names the pool.
   * Re-adding someone previously removed revives their old membership instead of duplicating it.
   */
  addMember(id: string, data: CreateTalentPoolMember) {
    return apiService.post<TalentPoolMember>(`${this.baseUrl}/${id}/members`, data);
  }

  updateMember(memberId: string, data: Partial<TalentPoolMember> & { id: string }) {
    return apiService.put<TalentPoolMember>(`${this.baseUrl}/members/${memberId}`, data);
  }

  /** A soft removal: the row stays with `isActive: false`, keeping the history. */
  removeMember(memberId: string, data: RemoveTalentPoolMember) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/members/${memberId}/remove`, data);
  }
}

export const talentPoolService = new TalentPoolService();

/** Tenant-configurable pool types. Six are seeded. ⚠ All writes are Admin. */
class TalentPoolTypeService {
  private readonly baseUrl = '/talent-pool-types';

  getAll() {
    return apiService.get<TalentPoolTypeDefinition[]>(this.baseUrl);
  }

  getById(id: string) {
    return apiService.get<TalentPoolTypeDefinition>(`${this.baseUrl}/${id}`);
  }

  create(data: Omit<TalentPoolTypeDefinition, 'id'>) {
    return apiService.post<TalentPoolTypeDefinition>(this.baseUrl, data);
  }

  update(id: string, data: TalentPoolTypeDefinition) {
    return apiService.put<TalentPoolTypeDefinition>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const talentPoolTypeService = new TalentPoolTypeService();
