import { apiService } from '../api.service';
import type {
  AppealListItem,
  AppealPageData,
  AppealReview,
  AppealStatusView,
  AppraisalAppeal,
  AppraisalAppealStatus,
  EmployeeAppealOutcome,
  ExtendRemandDeadline,
  PostRemandFinalDecision,
  PostRemandReview,
  ResolveAppeal,
  SubmitAppeal,
} from '@/types/hr/appeals';

/**
 * Appraisal appeals. Every route hangs off `api/PerformanceAppraisals` and is keyed by the
 * **appraisal** id — there is at most one appeal per appraisal, so the appeal id is never needed
 * as a path parameter.
 *
 * **The actor is never sent.** The appellant's three reads resolve the employee from the token
 * (they used to take it from the query string, which let anyone read anyone's appeal), and the
 * HR reads and decisions are role-gated server-side. A non-HR caller gets 403 on those.
 *
 * **Business rules answer 422 or 400** with `{ message }` — appealing an appraisal that is not
 * complete, appealing twice, resolving an already-final appeal, modifying scores on a cycle
 * whose settings forbid it. `apiService` surfaces `.message`.
 */
class AppraisalAppealService {
  private readonly baseUrl = '/PerformanceAppraisals';

  // ── The appellant ────────────────────────────────────────────────────────────────

  /**
   * What the signed-in employee may appeal, and whether they still can.
   *
   * ⚠ Check `canAppeal` before rendering the form: `cannotAppealReason` explains why not (the
   * appraisal is not complete, or an appeal has already been filed) and is written for the
   * employee to read.
   */
  getAppealPageData(appraisalId: string): Promise<AppealPageData> {
    return apiService.get<AppealPageData>(`${this.baseUrl}/${appraisalId}/appeal-page-data`);
  }

  /** At least one appealed item is required. 400 with the rule's message otherwise. */
  submitAppeal(appraisalId: string, data: SubmitAppeal): Promise<AppraisalAppeal> {
    return apiService.post<AppraisalAppeal>(`${this.baseUrl}/${appraisalId}/submit-appeal`, data);
  }

  /** The appellant's own view, live through the whole appeal and after it is decided. */
  getAppealStatus(appraisalId: string): Promise<AppealStatusView> {
    return apiService.get<AppealStatusView>(`${this.baseUrl}/${appraisalId}/appeal-status`);
  }

  /**
   * The final outcome, for the appellant. 400 until the appeal reaches Upheld or Rejected —
   * a remand is a process state, not a verdict.
   */
  getAppealOutcome(appraisalId: string): Promise<EmployeeAppealOutcome> {
    return apiService.get<EmployeeAppealOutcome>(`${this.baseUrl}/${appraisalId}/appeal-outcome`);
  }

  // ── HR ───────────────────────────────────────────────────────────────────────────

  /** The appeals queue. HR only. */
  getAppealsList(cycleId?: string, status?: AppraisalAppealStatus): Promise<AppealListItem[]> {
    const params: Record<string, string> = {};
    if (cycleId) params.cycleId = cycleId;
    if (status) params.status = status;
    return apiService.get<AppealListItem[]>(
      `${this.baseUrl}/appeals`,
      Object.keys(params).length ? params : undefined,
    );
  }

  /**
   * Everything needed to rule: the appealed items with all three evaluation legs, the scores,
   * and `hrCanModifyScores` from the cycle's settings profile.
   */
  getAppealReview(appraisalId: string): Promise<AppealReview> {
    return apiService.get<AppealReview>(`${this.baseUrl}/${appraisalId}/appeal-review`);
  }

  /**
   * Picks the appeal up: Submitted → UnderReview, stamping the reviewer. 422 if it has already
   * been picked up or decided. Nothing else depends on this — it exists so a queue of untouched
   * appeals is distinguishable from ones already being worked through.
   */
  beginReview(appraisalId: string): Promise<AppraisalAppeal> {
    return apiService.post<AppraisalAppeal>(`${this.baseUrl}/${appraisalId}/begin-appeal-review`);
  }

  /**
   * Rules on the appeal.
   *
   * `Upheld` and `Rejected` are final: the appraisal returns to Completed and the appellant is
   * notified. `Remanded` is not — it freezes a snapshot of the manager's evaluation, reopens that
   * evaluation until a re-evaluation deadline and notifies the manager; the appraisal stays under
   * appeal. The final call then happens on the post-remand screen.
   *
   * Score modifications are only accepted with `Upheld`, and only when the review reported
   * `hrCanModifyScores`. An officer who is the appellant, or wrote the contested evaluation, is
   * refused (403).
   */
  resolveAppeal(appraisalId: string, data: ResolveAppeal): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(
      `${this.baseUrl}/${appraisalId}/resolve-appeal`,
      data,
    );
  }

  /**
   * Where a remand stands — the deadline, whether it has passed, what HR may do — and, once the
   * manager has re-submitted, the pre- versus post-remand comparison.
   */
  getPostRemandReview(appraisalId: string): Promise<PostRemandReview> {
    return apiService.get<PostRemandReview>(`${this.baseUrl}/${appraisalId}/post-remand-review`);
  }

  /**
   * Moves the re-evaluation deadline to a later day while the manager has not re-evaluated
   * (closure D-34). The manager is notified with the reason.
   */
  extendRemand(appraisalId: string, data: ExtendRemandDeadline): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${appraisalId}/extend-remand`, data);
  }

  /**
   * Upheld or Rejected only. 422 while the manager's re-evaluation is due and its deadline has not
   * passed; after a lapsed deadline the decision stands on the scores from before the remand.
   */
  finalizePostRemand(
    appraisalId: string,
    data: PostRemandFinalDecision,
  ): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(
      `${this.baseUrl}/${appraisalId}/finalize-post-remand-appeal`,
      data,
    );
  }
}

export const appraisalAppealService = new AppraisalAppealService();
