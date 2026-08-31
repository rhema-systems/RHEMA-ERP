import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  AwardBudget,
  AwardCandidatePage,
  AwardCommittee,
  AwardCommitteeMember,
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
  MyAwardType,
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
  AddCommitteeMember,
  AwardBallot,
  AwardCommitteeResult,
  AwardTypeTarget,
  AwardVote,
  AwardVoteResult,
  CastAwardVote,
  ConferDirectly,
  ConferFromNomination,
  CreateLongServiceAward,
  EmployeeAward,
  RecordAwardPayment,
  RecordAwardPresentation,
  SubmitCommitteeReview,
  UpdateAwardNomination,
  UpdateContribution,
  UpdateLongServiceAward,
  UpdateTeamNominee,
  UpsertAwardBudget,
  UpsertAwardCommittee,
  UpsertAwardCycle,
  UpsertAwardLevel,
  UpsertAwardTarget,
  UpsertAwardType,
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

  /** The scores a committee has given one nomination. */
  getNominationReviews(nominationId: string) {
    return apiService.get<AwardCommitteeReview[]>(`${this.base}/nominations/${nominationId}/reviews`);
  }

  getTeamNominees(nominationId: string) {
    return apiService.get<{
      id: string;
      employeeId: string;
      employeeName: string;
      role: string | null;
      contributionSummary: string | null;
      rewardPercentage: number | null;
    }[]>(`${this.base}/nominations/${nominationId}/team-nominees`);
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

  /**
   * What an award is, for a nominator (area 25 slice 9). The desk `getType` needs
   * `HR.Awards.Read`, which a plain employee does not hold — the portal nomination form 403'd
   * the moment a cycle was picked. This is the lean self projection.
   */
  getMyType(awardTypeId: string) {
    return apiService.get<MyAwardType>(`${this.me}/types/${awardTypeId}`);
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

  /**
   * ⚠ The backend route is `DELETE nominations/{id}` (204, drafts only). This method used to POST
   * `nominations/{id}/withdraw` — a route that never existed, so the detail page's Withdraw button
   * had 404'd forever (found in area 25 slice 9; the fiction-that-type-checks shape again).
   */
  withdrawNomination(id: string) {
    return apiService.delete<void>(`${this.me}/nominations/${id}`);
  }

  /** What the caller still owes a score on. Empty for anyone on no committee. */
  getMyPendingReviews() {
    return apiService.get<AwardPendingReview[]>(`${this.me}/reviews/pending`);
  }

  /** Scores the caller has already given. */
  getMyReviews() {
    return apiService.get<AwardCommitteeReview[]>(`${this.me}/reviews`);
  }

  // ── catalogue writes (HR.Awards.Admin) ────────────────────────────────────

  createType(payload: UpsertAwardType) {
    return apiService.post<AwardType>(`${this.base}/types`, payload);
  }

  updateType(id: string, payload: UpsertAwardType & { id: string }) {
    return apiService.put<AwardType>(`${this.base}/types/${id}`, payload);
  }

  /** Whether anything references this type. Check before offering a delete. */
  isTypeInUse(id: string) {
    return apiService.get<boolean>(`${this.base}/types/${id}/in-use`);
  }

  createLevel(payload: UpsertAwardLevel) {
    return apiService.post<AwardLevel>(`${this.base}/levels`, payload);
  }

  updateLevel(id: string, payload: UpsertAwardLevel & { id: string }) {
    return apiService.put<AwardLevel>(`${this.base}/levels/${id}`, payload);
  }

  getTargets(awardTypeId: string) {
    return apiService.get<AwardTypeTarget[]>(`${this.base}/types/${awardTypeId}/targets`);
  }

  /**
   * Scope an award.
   *
   * ⚠ `purpose` separates two different questions: who may WIN (Eligibility) and who may VOTE
   * (Electorate). An award with no electorate target is voted on by everyone, which is the "or all
   * of them" half of TDC's note - so an empty electorate list is not an empty electorate.
   */
  createTarget(payload: UpsertAwardTarget) {
    return apiService.post<AwardTypeTarget>(`${this.base}/targets`, payload);
  }

  /**
   * Correct a target.
   *
   * ⚠ `HR.Awards.Admin`, like the rest of the catalogue — the desk's Write tier is refused here,
   * established by a 403 rather than assumed (probe-lane2-edits.mjs).
   *
   * ⚠ An edit here has teeth: flipping `isExclusion` on an eligibility target makes the named
   * employee ineligible, and the nomination refuses by name. The probe hit exactly that.
   */
  updateTarget(id: string, payload: UpsertAwardTarget & { id: string }) {
    return apiService.put<AwardTypeTarget>(`${this.base}/targets/${id}`, payload);
  }

  createBudget(payload: UpsertAwardBudget) {
    return apiService.post<AwardBudget>(`${this.base}/budgets`, payload);
  }

  updateBudget(id: string, payload: UpsertAwardBudget & { id: string }) {
    return apiService.put<AwardBudget>(`${this.base}/budgets/${id}`, payload);
  }

  // ── cycles ────────────────────────────────────────────────────────────────

  /**
   * ⚠ A voting window is required if - and only if - the award is decided by `StaffVote`. Supplying
   * one for a committee award is refused, and so is omitting one for a voted award. Voting also
   * cannot open before nominations close.
   */
  createCycle(payload: UpsertAwardCycle) {
    return apiService.post<AwardCycleSummary>(`${this.base}/cycles`, payload);
  }

  updateCycle(id: string, payload: UpsertAwardCycle & { id: string }) {
    return apiService.put<AwardCycleSummary>(`${this.base}/cycles/${id}`, payload);
  }

  /** A cycle is invisible to employees until it is published. */
  publishCycle(id: string) {
    return apiService.post<AwardCycleSummary>(`${this.base}/cycles/${id}/publish`, {});
  }

  cancelCycle(id: string, reason: string) {
    return apiService.post<AwardCycleSummary>(`${this.base}/cycles/${id}/cancel`, { reason });
  }

  /** Put forward everyone the award's performance triggers match (AWD-08). */
  generateCandidates(cycleId: string) {
    return apiService.post<{
      cycleId: string;
      created: number;
      skippedIneligible: number;
      skippedAlreadyNominated: number;
      appraisalsExamined: number;
      goalsExamined: number;
      noTriggerConfigured: boolean;
      note?: string | null;
      nominees: { nominationId: string; nominationNumber: string; employeeId: string; employeeName: string }[];
    }>(`${this.base}/cycles/${cycleId}/generate-candidates`, {});
  }

  getVoteResults(cycleId: string) {
    return apiService.get<AwardVoteResult>(`${this.base}/cycles/${cycleId}/results`);
  }

  getCommitteeResults(cycleId: string) {
    return apiService.get<AwardCommitteeResult>(`${this.base}/cycles/${cycleId}/committee-result`);
  }

  // ── committees ────────────────────────────────────────────────────────────

  createCommittee(payload: UpsertAwardCommittee) {
    return apiService.post<AwardCommittee>(`${this.base}/committees`, payload);
  }

  updateCommittee(id: string, payload: UpsertAwardCommittee & { id: string }) {
    return apiService.put<AwardCommittee>(`${this.base}/committees/${id}`, payload);
  }

  addCommitteeMember(committeeId: string, payload: AddCommitteeMember) {
    return apiService.post<AwardCommitteeMember>(`${this.base}/committees/${committeeId}/members`, payload);
  }

  /**
   * Correct a member's role or dates. `HR.Awards.Admin`.
   *
   * ⚠ Not the same act as deactivating: deactivation ends their entitlement to score and keeps
   * the scores they gave, while this fixes what the record says about them.
   */
  updateCommitteeMember(memberId: string, payload: AddCommitteeMember & { id: string }) {
    return apiService.put<AwardCommitteeMember>(`${this.base}/committee-members/${memberId}`, payload);
  }

  deactivateCommitteeMember(memberId: string) {
    return apiService.post<void>(`${this.base}/committee-members/${memberId}/deactivate`, {});
  }

  hasQuorum(committeeId: string) {
    return apiService.get<boolean>(`${this.base}/committees/${committeeId}/has-quorum`);
  }

  // ── the desk's decisions on a nomination ──────────────────────────────────

  /** Membership of this committee becomes the gate on who may score the nomination. */
  assignToCommittee(nominationId: string, committeeId: string) {
    return apiService.post<AwardNomination>(
      `${this.base}/nominations/${nominationId}/committee/${committeeId}`, {});
  }

  /**
   * Turn a decided nomination into an award.
   *
   * ⚠ This is the only path from a decision to an award. It reserves against the year's budget;
   * payment later turns that reservation into spend.
   */
  conferFromNomination(nominationId: string, payload: ConferFromNomination) {
    return apiService.post<EmployeeAward>(
      `${this.base}/nominations/${nominationId}/confer`, payload);
  }

  /** ⚠ Only for a `ManagementDirect` award - anything else is refused (AWD-07). */
  conferDirectly(payload: ConferDirectly) {
    return apiService.post<EmployeeAward>(this.base, payload);
  }

  getAward(id: string) {
    return apiService.get<EmployeeAward>(`${this.base}/${id}`);
  }

  /** Releases what was PROMISED and books what was PAID. The two can differ. */
  recordPayment(awardId: string, payload: RecordAwardPayment) {
    return apiService.post<EmployeeAward>(`${this.base}/${awardId}/payment`, payload);
  }

  recordPresentation(awardId: string, payload: RecordAwardPresentation) {
    return apiService.post<EmployeeAward>(`${this.base}/${awardId}/presentation`, payload);
  }

  getAvailableBudget(awardTypeId: string, year: number) {
    return apiService.get<{ availableAmount: number }>(
      `${this.base}/types/${awardTypeId}/budgets/${year}/available`);
  }

  // ── the employee's own acts ───────────────────────────────────────────────

  updateNomination(id: string, payload: UpdateAwardNomination) {
    return apiService.put<AwardNomination>(`${this.me}/nominations/${id}`, payload);
  }

  getBallot(cycleId: string) {
    return apiService.get<AwardBallot>(`${this.me}/cycles/${cycleId}/ballot`);
  }

  /** Casting again replaces the earlier vote rather than adding one. */
  castVote(cycleId: string, payload: CastAwardVote) {
    return apiService.post<AwardVote>(`${this.me}/cycles/${cycleId}/vote`, payload);
  }

  /** ⚠ 404 when the caller has not voted. That is the answer, not a fault. */
  getMyVote(cycleId: string) {
    return apiService.get<AwardVote>(`${this.me}/cycles/${cycleId}/vote`);
  }

  /**
   * Score a nomination as a committee member.
   *
   * ⚠ The reviewer is the token. The desk has no route for this on purpose - a member scores from
   * their own surface and never on somebody else's behalf.
   */
  scoreNomination(nominationId: string, payload: SubmitCommitteeReview) {
    return apiService.post<AwardCommitteeReview>(
      `${this.me}/nominations/${nominationId}/score`, payload);
  }

  /** What the committee decided, as a member may see it for their own committee. */
  getMyCommitteeResult(cycleId: string) {
    return apiService.get<AwardCommitteeResult>(`${this.me}/cycles/${cycleId}/committee-result`);
  }

  // ── a nomination's evidence ───────────────────────────────────────────────

  getContributions(nominationId: string) {
    return apiService.get<{ id: string; description: string; createdAt: string }[]>(
      `${this.base}/nominations/${nominationId}/contributions`);
  }

  addContribution(nominationId: string, description: string) {
    return apiService.post<{ id: string; description: string }>(
      `${this.base}/nominations/${nominationId}/contributions`, { description });
  }

  /**
   * Name a member of a team nomination.
   *
   * ⚠ A team nomination has no single nominee, so it cannot be conferred like an individual one.
   * Its members are recorded here instead — this is the route the nominate form promises exists.
   */
  addTeamNominee(nominationId: string, payload: {
    employeeId: string;
    role?: string | null;
    contributionSummary?: string | null;
    rewardPercentage?: number | null;
  }) {
    return apiService.post(`${this.base}/nominations/${nominationId}/team-nominees`, payload);
  }

  // ── long service, once granted ────────────────────────────────────────────

  /**
   * Correct a team member's share. `HR.Awards.Write`.
   *
   * The reward percentage decides what each member of a team award is paid, so a typo here is money.
   */
  updateTeamNominee(id: string, payload: UpdateTeamNominee & { id: string }) {
    return apiService.put<void>(`${this.base}/team-nominees/${id}`, payload);
  }

  /** Reword a contribution. `HR.Awards.Write`. */
  updateContribution(id: string, payload: UpdateContribution & { id: string }) {
    return apiService.put<void>(`${this.base}/contributions/${id}`, payload);
  }

  /**
   * Record a long-service award by hand.
   *
   * ⚠ Until this, a long-service award could only come into being through the sweep, so one
   * granted with the wrong value had no counterpart to correct it against — and it is money. The
   * sweep remains the normal path; this is the correction and the exception.
   */
  createLongServiceAward(payload: CreateLongServiceAward) {
    return apiService.post<LongServiceAwardSummary>(`${this.base}/long-service`, payload);
  }

  /** Correct one. Same reasoning as the create. */
  updateLongServiceAward(id: string, payload: UpdateLongServiceAward & { id: string }) {
    return apiService.put<LongServiceAwardSummary>(`${this.base}/long-service/${id}`, payload);
  }

  /** Mark a long-service award as processed, with the presentation details. */
  processLongServiceAward(id: string, payload: {
    awardId: string;
    presentationDate?: string | null;
    presentationNotes?: string | null;
  }) {
    return apiService.post<void>(`${this.base}/long-service/${id}/process`, payload);
  }

  getLongServiceAward(id: string) {
    return apiService.get<{
      id: string;
      employeeName: string;
      employeeNumber: string | null;
      awardTypeName: string | null;
      yearsOfService: number;
      serviceStartDate: string;
      milestoneDate: string;
      awardDescription: string;
      monetaryAmount: number | null;
      leaveDaysBonus: number | null;
      otherBenefits: string | null;
      isProcessed: boolean;
      processedDate: string | null;
      presentationDate: string | null;
      presentationNotes: string | null;
      paymentProcessed: boolean;
      paymentDate: string | null;
      paymentReference: string | null;
    }>(`${this.base}/long-service/${id}`);
  }

  /** Long-service awards granted but not yet processed. */
  getLongServicePendingProcessing() {
    return apiService.get<LongServiceAwardSummary[]>(`${this.base}/long-service/pending-processing`);
  }

  // ── removal ───────────────────────────────────────────────────────────────
  //
  // ⚠ These are SOFT deletes throughout — the row is flagged, not dropped — so an award that was
  // conferred and then removed is still there to explain itself. Two consequences a screen must
  // respect: a filtered unique index means a deleted record frees its key (a retired 20-year rung
  // lets a new one take that year), and "deleted" is not "never happened".

  /** ⚠ Check {@link isTypeInUse} first. Removing a type that awards reference is refused. */
  deleteType(id: string) {
    return apiService.delete<void>(`${this.base}/types/${id}`);
  }

  deleteLevel(id: string) {
    return apiService.delete<void>(`${this.base}/levels/${id}`);
  }

  deleteTarget(id: string) {
    return apiService.delete<void>(`${this.base}/targets/${id}`);
  }

  deleteBudget(id: string) {
    return apiService.delete<void>(`${this.base}/budgets/${id}`);
  }

  deleteCommittee(id: string) {
    return apiService.delete<void>(`${this.base}/committees/${id}`);
  }

  /**
   * ⚠ Prefer {@link deactivateCommitteeMember}. Deleting a member removes the membership row;
   * deactivating keeps it, which is what makes the scores they already gave still attributable.
   */
  deleteCommitteeMember(id: string) {
    return apiService.delete<void>(`${this.base}/committee-members/${id}`);
  }

  deleteCycle(id: string) {
    return apiService.delete<void>(`${this.base}/cycles/${id}`);
  }

  deleteNomination(id: string) {
    return apiService.delete<void>(`${this.base}/nominations/${id}`);
  }

  deleteContribution(id: string) {
    return apiService.delete<void>(`${this.base}/contributions/${id}`);
  }

  deleteTeamNominee(id: string) {
    return apiService.delete<void>(`${this.base}/team-nominees/${id}`);
  }

  /** ⚠ Removes a conferred award. The budget reservation or spend it made is not unwound here. */
  deleteAward(id: string) {
    return apiService.delete<void>(`${this.base}/${id}`);
  }

  deleteLongServiceAward(id: string) {
    return apiService.delete<void>(`${this.base}/long-service/${id}`);
  }

  /** Withdraw a vote entirely, as opposed to changing it by casting again. */
  withdrawVote(cycleId: string) {
    return apiService.delete<void>(`${this.me}/cycles/${cycleId}/vote`);
  }

  // ── planning views ────────────────────────────────────────────────────────

  /**
   * Milestones falling due soon.
   *
   * ⚠ Reads the long-service awards ALREADY GRANTED whose milestone date is ahead, not employees
   * who are about to qualify — those come from the sweep preview. The distinction matters: this is
   * "what have we committed to that is coming up", not "who will qualify next".
   */
  getUpcomingMilestones(daysAhead = 90) {
    return apiService.get<LongServiceAwardSummary[]>(
      `${this.base}/long-service/upcoming`, { daysAhead });
  }

  /** Conferred awards with no presentation recorded yet. */
  getPendingPresentations() {
    return apiService.get<EmployeeAwardSummary[]>(`${this.base}/pending/presentations`);
  }

  /** Budgets across every award for a span of years — the spend picture, not one award's. */
  getBudgetsForYears(startYear: number, endYear: number) {
    return apiService.get<AwardBudget[]>(`${this.base}/budgets/year-range`, { startYear, endYear });
  }

  /** Edit a conferred award — its citation, reason or date. */
  updateAward(id: string, payload: {
    id: string;
    awardDate: string;
    reason?: string | null;
    citation?: string | null;
    monetaryAmount?: number | null;
    awardLevelId?: string | null;
  }) {
    return apiService.put<EmployeeAward>(`${this.base}/${id}`, payload);
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
