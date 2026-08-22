import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  AwardBudget,
  AwardCandidatePage,
  AwardCommittee,
  AwardCommitteeReview,
  AwardCycleSummary,
  AwardEligibilityResult,
  AwardEligibilityVerdict,
  AwardLevel,
  AwardNomination,
  AwardNominationSummary,
  AwardPendingReview,
  AwardType,
  AwardTypeSummary,
  CreateAwardNomination,
  CreateLongServiceMilestone,
  EmployeeAwardSummary,
  LongServiceAwardSummary,
  LongServiceLadderSeedResult,
  LongServiceMilestone,
  LongServiceStanding,
  LongServiceSweepResult,
  SeedLongServiceLadder,
  UpdateLongServiceMilestone,
} from '@/types/hr/awards';

/**
 * Staff Awards & Recognition. Backend routes: `api/Awards` (the desk) and `api/awards/me` (the
 * employee).
 *
 * ⚠ **Every route below was verified against a running API** by
 * `dev-harness/hr-awards/probe-ui-payloads.mjs`. Four of the first draft's routes were invented and
 * 404'd — `levels/award-type/{id}`, `budgets/award-type/{id}`, `me/cycles`, `me/awards` — and each
 * would have compiled perfectly.
 *
 * ⚠ **Two gates, and they are not the usual split.** Reading is `HR.Awards.Read`, record-keeping is
 * `Write`, and the *catalogue* — award types, levels, targets, budgets, committees, the long-service
 * ladder and the sweep — is `Admin`. But **nominating and voting are behind no HR permission at
 * all**: every employee does both from `api/awards/me`, where entitlement is read off the record
 * rather than granted. A screen must not offer a desk action merely because the record is visible,
 * and must not hide the self-service ones because the user holds no HR permission.
 */
class AwardsService {
  private readonly base = '/Awards';
  private readonly me = '/awards/me';

  // ── the catalogue ─────────────────────────────────────────────────────────

  /** ⚠ An unpaged array. Measured at 426 rows on the working tenant. */
  getTypes() {
    return apiService.get<AwardTypeSummary[]>(`${this.base}/types`);
  }

  getType(id: string) {
    return apiService.get<AwardType>(`${this.base}/types/${id}`);
  }

  getLevels(awardTypeId: string) {
    return apiService.get<AwardLevel[]>(`${this.base}/types/${awardTypeId}/levels`);
  }

  getBudgets(awardTypeId: string) {
    return apiService.get<AwardBudget[]>(`${this.base}/types/${awardTypeId}/budgets`);
  }

  getCommittees() {
    return apiService.get<AwardCommittee[]>(`${this.base}/committees`);
  }

  /** ⚠ `members` is populated only here; the plain list leaves it empty. */
  getCommitteeWithMembers(id: string) {
    return apiService.get<AwardCommittee>(`${this.base}/committees/${id}/with-members`);
  }

  // ── cycles ────────────────────────────────────────────────────────────────

  getCycles(awardTypeId: string) {
    return apiService.get<AwardCycleSummary[]>(`${this.base}/types/${awardTypeId}/cycles`);
  }

  // ── nominations, from the desk ────────────────────────────────────────────

  getNominations() {
    return apiService.get<AwardNominationSummary[]>(`${this.base}/nominations`);
  }

  getNomination(id: string) {
    return apiService.get<AwardNomination>(`${this.base}/nominations/${id}`);
  }

  // ── conferred awards ──────────────────────────────────────────────────────

  getAwardsPaged(params: { pageNumber?: number; pageSize?: number; searchTerm?: string } = {}) {
    return apiService.get<PagedResult<EmployeeAwardSummary>>(`${this.base}/paged`, params);
  }

  // ── eligibility (D-9) ─────────────────────────────────────────────────────

  /**
   * Who qualifies, one page at a time, with the ineligible and their reasons.
   *
   * ⚠ The three counts on the result are computed over **everybody**, never over the filter or the
   * page. Render them beside the table, not derived from `items.length` — "12 eligible" beside a
   * page of 12 rows tells the reader nothing.
   */
  getEligible(
    awardTypeId: string,
    params: {
      filter?: 'all' | 'eligible' | 'ineligible';
      page?: number;
      pageSize?: number;
      search?: string;
      asOf?: string;
    } = {},
  ) {
    return apiService.get<AwardEligibilityResult>(`${this.base}/types/${awardTypeId}/eligible`, params);
  }

  getEmployeeEligibility(awardTypeId: string, employeeId: string) {
    return apiService.get<AwardEligibilityVerdict>(
      `${this.base}/types/${awardTypeId}/eligible/${employeeId}`,
    );
  }

  // ── long service (AWD-14 / AWD-15) ────────────────────────────────────────

  getLadder(awardTypeId: string) {
    return apiService.get<LongServiceMilestone[]>(
      `${this.base}/types/${awardTypeId}/long-service/milestones`,
    );
  }

  createMilestone(payload: CreateLongServiceMilestone) {
    return apiService.post<LongServiceMilestone>(`${this.base}/long-service/milestones`, payload);
  }

  updateMilestone(id: string, payload: UpdateLongServiceMilestone) {
    return apiService.put<LongServiceMilestone>(`${this.base}/long-service/milestones/${id}`, payload);
  }

  deleteMilestone(id: string) {
    return apiService.delete<void>(`${this.base}/long-service/milestones/${id}`);
  }

  /** Additive and never destructive — a rung HR has already priced survives a second press. */
  seedLadder(awardTypeId: string, payload: SeedLongServiceLadder = {}) {
    return apiService.post<LongServiceLadderSeedResult>(
      `${this.base}/types/${awardTypeId}/long-service/milestones/seed`,
      payload,
    );
  }

  /**
   * Who has reached a milestone. Writes nothing.
   *
   * ⚠ The preview and the run are the **same calculation** with a commit flag, so whatever this
   * shows is exactly what {@link runLongServiceSweep} will grant. Do not re-derive eligibility on
   * the client; that is how a screen comes to promise something the button does not do.
   */
  previewLongServiceSweep(awardTypeId: string, asOf?: string) {
    return apiService.get<LongServiceSweepResult>(
      `${this.base}/types/${awardTypeId}/long-service/sweep/preview`,
      asOf ? { asOf } : undefined,
    );
  }

  /** Grants the awards. Re-running is safe: a milestone already granted is not granted twice. */
  runLongServiceSweep(awardTypeId: string, asOf?: string) {
    return apiService.post<LongServiceSweepResult>(
      `${this.base}/types/${awardTypeId}/long-service/sweep${asOf ? `?asOf=${encodeURIComponent(asOf)}` : ''}`,
      {},
    );
  }

  getLongServiceAwards() {
    return apiService.get<LongServiceAwardSummary[]>(`${this.base}/long-service`);
  }

  // ── the employee's own surface ────────────────────────────────────────────
  //
  // ⚠ No route here carries an employee id. The actor is the token, always — and someone else's
  // nomination is a 404 rather than a 403, because a 403 confirms the id exists and turns this
  // surface into a way of enumerating nomination ids.

  /** Awards the caller has received. Did not exist before slice 11. */
  getMyAwards() {
    return apiService.get<EmployeeAwardSummary[]>(`${this.me}/awards`);
  }

  getMyLongServiceAwards() {
    return apiService.get<LongServiceAwardSummary[]>(`${this.me}/awards/long-service`);
  }

  getMyOpenCycles() {
    return apiService.get<AwardCycleSummary[]>(`${this.me}/cycles/open`);
  }

  getMyVotingCycles() {
    return apiService.get<AwardCycleSummary[]>(`${this.me}/cycles/voting`);
  }

  /**
   * Who the caller may put forward.
   *
   * ⚠ A paged envelope, and searchable — the qualified set can be most of the workforce, and nobody
   * scrolls five thousand names to find a colleague. It deliberately carries **no** ineligible
   * count and **no** reasons: an employee has no business reading why a colleague failed a rule.
   */
  getCandidates(
    awardTypeId: string,
    params: { search?: string; page?: number; pageSize?: number } = {},
  ) {
    return apiService.get<AwardCandidatePage>(`${this.me}/awards/${awardTypeId}/candidates`, params);
  }

  getMyNominations() {
    return apiService.get<AwardNominationSummary[]>(`${this.me}/nominations`);
  }

  getMyNomination(id: string) {
    return apiService.get<AwardNomination>(`${this.me}/nominations/${id}`);
  }

  createNomination(payload: CreateAwardNomination) {
    return apiService.post<AwardNomination>(`${this.me}/nominations`, payload);
  }

  submitNomination(id: string) {
    return apiService.post<AwardNomination>(`${this.me}/nominations/${id}/submit`, {});
  }

  withdrawNomination(id: string) {
    return apiService.post<AwardNomination>(`${this.me}/nominations/${id}/withdraw`, {});
  }

  /** What the caller still owes a score on. Empty for anyone on no committee. */
  getMyPendingReviews() {
    return apiService.get<AwardPendingReview[]>(`${this.me}/reviews/pending`);
  }

  /** Scores the caller has already given. */
  getMyReviews() {
    return apiService.get<AwardCommitteeReview[]>(`${this.me}/reviews`);
  }
}

export const awardsService = new AwardsService();

/** Display labels for the long-service standings. The enum names are not reader-facing. */
export const LONG_SERVICE_STANDING_LABEL: Record<LongServiceStanding, string> = {
  Eligible: 'Eligible now',
  Exempt: 'Exempt — disciplinary record',
  AlreadyGranted: 'Already granted',
  NotYetAtMilestone: 'Not yet at a milestone',
  ServiceUnknown: 'Service unknown',
};
