import { apiService } from '../api.service';
import type {
  AddJobInterviewExternalPanelist,
  AddJobInterviewPanelist,
  AddJobInterviewee,
  ApportionSlotsRequest,
  CancelJobInterview,
  CommitInterviewQuestions,
  CreateInterviewQuestion,
  CreateInterviewQuestionPreset,
  CreateInterviewQuestionType,
  CreateJobInterview,
  CreateJobInterviewQuestionPlan,
  CreateJobInterviewScoreSummary,
  InterviewPaper,
  InterviewPaperRequest,
  InterviewQuestion,
  InterviewQuestionPreset,
  InterviewQuestionPresetSummary,
  InterviewQuestionType,
  InterviewQuestionTypeSummary,
  InterviewScoreDraft,
  InterviewSlotPlan,
  JobInterview,
  JobInterviewDetail,
  JobInterviewExternalPanelist,
  JobInterviewPanelist,
  JobInterviewQuestionPlan,
  JobInterviewScoreEntry,
  JobInterviewScoreSummary,
  JobInterviewScoreSummaryDetail,
  JobInterviewStatus,
  JobInterviewSummary,
  JobInterviewee,
  PanelSlotSuggestion,
  PanelSlotSuggestionQuery,
  PanelistAvailabilityCheck,
  PanelistAvailabilityQuery,
  PanelistScorecardWorklistItem,
  QuestionPlanPreview,
  RescheduleJobInterview,
  SaveInterviewScoreDraft,
  SendInterviewInvitesResult,
  SendPanelistNotifications,
  SendPanelistNotificationsResult,
  UpdateInterviewQuestion,
  UpdateInterviewQuestionPreset,
  UpdateInterviewQuestionType,
  UpdateJobInterview,
  UpdateJobInterviewExternalPanelist,
  UpdateJobInterviewPanelist,
  UpdateJobInterviewQuestionPlan,
} from '@/types/hr/interviews';

/**
 * api/job-interviews — scheduling, the panel, the question plan and the panel's scorecards.
 *
 * ⚠ **Authorization here is per record, not per role.** HR can do everything; a panelist sitting on
 * a given interview can read *that* interview and score as themselves; everyone else gets a 403.
 * The consequence for callers: the list reads (`getByVacancy`, `getByStatus`, `getByDateRange`,
 * `getByRound`) are HR-only and will 403 for a panelist — use `getMyPanelSlots` for their diary.
 *
 * ⚠ A scorecard is **upserted**, not appended: posting a second one for the same panelist replaces
 * the first, and the server refuses once it has been finalised. There is no update endpoint by
 * design.
 */
class JobInterviewService {
  private readonly baseUrl = '/job-interviews';

  // ── queries ──────────────────────────────────────────────────────────────

  getById(id: string): Promise<JobInterview> {
    return apiService.get<JobInterview>(`${this.baseUrl}/${id}`);
  }

  getDetails(id: string): Promise<JobInterviewDetail> {
    return apiService.get<JobInterviewDetail>(`${this.baseUrl}/${id}/details`);
  }

  getByNumber(interviewNumber: string): Promise<JobInterview | null> {
    return apiService.get<JobInterview | null>(`${this.baseUrl}/number/${interviewNumber}`);
  }

  /** HR-only. A panelist gets 403 — see the class note. */
  getByVacancy(vacancyId: string): Promise<JobInterviewSummary[]> {
    return apiService.get<JobInterviewSummary[]>(`${this.baseUrl}/vacancy/${vacancyId}`);
  }

  /** HR-only. */
  getByStatus(status: JobInterviewStatus): Promise<JobInterviewSummary[]> {
    return apiService.get<JobInterviewSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  /** HR-only. `from`/`to` are required — the backend takes non-nullable dates. */
  getByDateRange(from: string, to: string): Promise<JobInterviewSummary[]> {
    return apiService.get<JobInterviewSummary[]>(`${this.baseUrl}/date-range`, { from, to });
  }

  /** HR-only. */
  getByRound(vacancyId: string, round: number): Promise<JobInterviewSummary[]> {
    return apiService.get<JobInterviewSummary[]>(`${this.baseUrl}/vacancy/${vacancyId}/round/${round}`);
  }

  /**
   * Advisory clash check — overlapping sessions the panelists already sit on, plus their approved
   * or pending leave and travel. **Never blocks a booking**; surface it as a warning. HR-only,
   * because it exposes other people's leave and travel.
   */
  checkAvailability(query: PanelistAvailabilityQuery): Promise<PanelistAvailabilityCheck> {
    const params: Record<string, unknown> = {
      date: query.date,
      start: query.start,
      end: query.end,
    };
    if (query.panelistIds?.length) params.panelistIds = query.panelistIds;
    if (query.externalPanelistIds?.length) params.externalPanelistIds = query.externalPanelistIds;
    if (query.excludeInterviewId) params.excludeInterviewId = query.excludeInterviewId;
    return apiService.get<PanelistAvailabilityCheck>(`${this.baseUrl}/panelist-availability`, params);
  }

  /**
   * Round 4, D4 — the windows in a range where the WHOLE panel is free.
   *
   * ⚠ A slot carrying soft conflicts still comes back, flagged. Filtering those out client-side
   * would throw away the windows HR most often wants: the ones where the only obstacle is a
   * day-granular record that may not apply to the hour.
   */
  suggestSlots(query: PanelSlotSuggestionQuery): Promise<PanelSlotSuggestion[]> {
    const params: Record<string, unknown> = { from: query.from, to: query.to };
    if (query.panelistIds?.length) params.panelistIds = query.panelistIds;
    if (query.externalPanelistIds?.length) params.externalPanelistIds = query.externalPanelistIds;
    if (query.dayStart) params.dayStart = query.dayStart;
    if (query.dayEnd) params.dayEnd = query.dayEnd;
    if (query.durationMinutes) params.durationMinutes = query.durationMinutes;
    if (query.excludeInterviewId) params.excludeInterviewId = query.excludeInterviewId;
    if (query.maxSuggestions) params.maxSuggestions = query.maxSuggestions;
    return apiService.get<PanelSlotSuggestion[]>(`${this.baseUrl}/suggest-slots`, params);
  }

  // ── scheduling ───────────────────────────────────────────────────────────

  create(payload: CreateJobInterview): Promise<JobInterview> {
    return apiService.post<JobInterview>(this.baseUrl, payload);
  }

  /** ⚠ `status` is not on the payload by design — use reschedule / cancel / complete. */
  update(id: string, payload: UpdateJobInterview): Promise<JobInterview> {
    return apiService.put<JobInterview>(`${this.baseUrl}/${id}`, { ...payload, id });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Moves the session, records the original date, and re-issues every candidate's token. */
  reschedule(id: string, payload: RescheduleJobInterview): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/reschedule`, { ...payload, interviewId: id });
  }

  cancel(id: string, payload: CancelJobInterview): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/cancel`, { ...payload, interviewId: id });
  }

  complete(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/complete`, {});
  }

  // ── panel ────────────────────────────────────────────────────────────────

  getPanelists(interviewId: string): Promise<JobInterviewPanelist[]> {
    return apiService.get<JobInterviewPanelist[]>(`${this.baseUrl}/${interviewId}/panelists`);
  }

  addPanelist(interviewId: string, payload: AddJobInterviewPanelist): Promise<JobInterviewPanelist> {
    return apiService.post<JobInterviewPanelist>(`${this.baseUrl}/${interviewId}/panelists`, {
      ...payload,
      jobInterviewId: interviewId,
    });
  }

  updatePanelist(panelistId: string, payload: UpdateJobInterviewPanelist): Promise<JobInterviewPanelist> {
    return apiService.put<JobInterviewPanelist>(`${this.baseUrl}/panelists/${panelistId}`, {
      ...payload,
      id: panelistId,
    });
  }

  removePanelist(panelistId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/panelists/${panelistId}`);
  }

  /** Only the panelist themselves, or HR on their behalf. */
  confirmPanelist(panelistId: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/panelists/${panelistId}/confirm`, {});
  }

  recordPanelistAttendance(panelistId: string, attended: boolean, noShowReason?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/panelists/${panelistId}/attendance`, { attended, noShowReason });
  }

  getExternalPanelists(interviewId: string): Promise<JobInterviewExternalPanelist[]> {
    return apiService.get<JobInterviewExternalPanelist[]>(`${this.baseUrl}/${interviewId}/external-panelists`);
  }

  addExternalPanelist(
    interviewId: string,
    payload: AddJobInterviewExternalPanelist,
  ): Promise<JobInterviewExternalPanelist> {
    return apiService.post<JobInterviewExternalPanelist>(`${this.baseUrl}/${interviewId}/external-panelists`, {
      ...payload,
      jobInterviewId: interviewId,
    });
  }

  updateExternalPanelist(
    externalPanelistId: string,
    payload: UpdateJobInterviewExternalPanelist,
  ): Promise<JobInterviewExternalPanelist> {
    return apiService.put<JobInterviewExternalPanelist>(
      `${this.baseUrl}/external-panelists/${externalPanelistId}`,
      { ...payload, id: externalPanelistId },
    );
  }

  removeExternalPanelist(externalPanelistId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/external-panelists/${externalPanelistId}`);
  }

  recordExternalPanelistAttendance(
    externalPanelistId: string,
    attended: boolean,
    noShowReason?: string | null,
  ): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/external-panelists/${externalPanelistId}/attendance`, {
      attended,
      noShowReason,
    });
  }

  /** The caller's own panel assignments — the diary a panelist opens. Never 403s for an employee. */
  getMyPanelSlots(): Promise<JobInterviewPanelist[]> {
    return apiService.get<JobInterviewPanelist[]>(`${this.baseUrl}/me/panelist-slots`);
  }

  /** HR-only unless the id is the caller's own. */
  getPanelSlotsFor(employeeId: string): Promise<JobInterviewPanelist[]> {
    return apiService.get<JobInterviewPanelist[]>(`${this.baseUrl}/employee/${employeeId}/panelist-slots`);
  }

  // ── candidates in the session ────────────────────────────────────────────

  getInterviewees(interviewId: string): Promise<JobInterviewee[]> {
    return apiService.get<JobInterviewee[]>(`${this.baseUrl}/${interviewId}/interviewees`);
  }

  addInterviewee(interviewId: string, payload: AddJobInterviewee): Promise<JobInterviewee> {
    return apiService.post<JobInterviewee>(`${this.baseUrl}/${interviewId}/interviewees`, {
      ...payload,
      jobInterviewId: interviewId,
    });
  }

  removeInterviewee(intervieweeId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/interviewees/${intervieweeId}`);
  }

  updateIntervieweeSlot(
    intervieweeId: string,
    slotStartTime?: string | null,
    slotEndTime?: string | null,
  ): Promise<void> {
    return apiService.patch<void>(`${this.baseUrl}/interviewees/${intervieweeId}/slot`, {
      intervieweeId,
      slotStartTime,
      slotEndTime,
    });
  }

  // ── Slot apportionment (round 4, lane C) ─────────────────────────────────

  /**
   * What the day would look like at this interval. **Writes nothing** — call it as often as the
   * user changes the numbers.
   *
   * ⚠ Times go over the wire as `HH:mm:ss`. `<input type="time">` yields `HH:mm`, so append
   * `:00` — the same correction the scheduling form already makes for the session window.
   */
  previewSlots(interviewId: string, plan: ApportionSlotsRequest): Promise<InterviewSlotPlan> {
    return apiService.post<InterviewSlotPlan>(`${this.baseUrl}/${interviewId}/slots/preview`, {
      ...plan,
      interviewId,
    });
  }

  /** Writes the timetable onto the session's candidates. HR only. */
  applySlots(interviewId: string, plan: ApportionSlotsRequest): Promise<InterviewSlotPlan> {
    return apiService.post<InterviewSlotPlan>(`${this.baseUrl}/${interviewId}/slots/apply`, {
      ...plan,
      interviewId,
    });
  }

  /**
   * The caller's own scorecard worklist — the sessions they sit on, the candidates on each, and how
   * far their own card for each has got. Backs `/me/panel` (round 4, lane F5).
   *
   * ⚠ Takes the employee from the token, like `getMyPanelSlots`. Returns the caller's **own** cards
   * only; a colleague's mark never appears on it.
   */
  getMyScorecardWorklist(): Promise<PanelistScorecardWorklistItem[]> {
    return apiService.get<PanelistScorecardWorklistItem[]>(`${this.baseUrl}/me/scorecard-worklist`);
  }

  // ── The printed paper (round 4, lane F) ──────────────────────────────────

  /**
   * The interview paper as HTML, ready to print.
   *
   * ⚠ Gated on **read** access, not on HR — a panelist on this interview may print their own
   * sheets. That is the point of the sheet: it goes to the person doing the scoring.
   *
   * `intervieweeIds` goes over the wire as a repeated query key, which is what `[FromQuery] Guid[]`
   * binds; `apiService` already serialises an array that way.
   */
  getPaper(interviewId: string, request: InterviewPaperRequest = {}): Promise<InterviewPaper> {
    return apiService.get<InterviewPaper>(`${this.baseUrl}/${interviewId}/paper`, {
      variant: request.variant ?? 'ScoreSheet',
      panelistId: request.panelistId ?? undefined,
      intervieweeIds: request.intervieweeIds?.length ? request.intervieweeIds : undefined,
    });
  }

  recordIntervieweeAttendance(
    intervieweeId: string,
    attended: boolean,
    noShowReason?: string | null,
  ): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/interviewees/${intervieweeId}/attendance`, {
      intervieweeId,
      attended,
      noShowReason,
    });
  }

  /** Refused for a candidate recorded as a no-show — there is no verdict to give. */
  recordIntervieweeOutcome(intervieweeId: string, outcome: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/interviewees/${intervieweeId}/outcome`, {
      intervieweeId,
      outcome,
    });
  }

  getInterviewsForApplication(applicationId: string): Promise<JobInterviewee[]> {
    return apiService.get<JobInterviewee[]>(`${this.baseUrl}/application/${applicationId}/interviewees`);
  }

  // ── invitations ──────────────────────────────────────────────────────────

  /** An empty `applicationIds` sends to every candidate in the session. */
  sendInvites(interviewId: string, applicationIds: string[] = []): Promise<SendInterviewInvitesResult> {
    return apiService.post<SendInterviewInvitesResult>(`${this.baseUrl}/${interviewId}/send-invites`, {
      applicationIds,
    });
  }

  /** ⚠ `null` broadcasts to a group, `[]` skips it entirely — the difference is load-bearing. */
  sendPanelistNotifications(
    interviewId: string,
    payload: SendPanelistNotifications,
  ): Promise<SendPanelistNotificationsResult> {
    return apiService.post<SendPanelistNotificationsResult>(
      `${this.baseUrl}/${interviewId}/panelist-notifications`,
      payload,
    );
  }

  // ── question plans ───────────────────────────────────────────────────────

  getQuestionPlans(interviewId: string): Promise<JobInterviewQuestionPlan[]> {
    return apiService.get<JobInterviewQuestionPlan[]>(`${this.baseUrl}/${interviewId}/question-plans`);
  }

  addQuestionPlan(interviewId: string, payload: CreateJobInterviewQuestionPlan): Promise<JobInterviewQuestionPlan> {
    return apiService.post<JobInterviewQuestionPlan>(`${this.baseUrl}/${interviewId}/question-plans`, {
      ...payload,
      jobInterviewId: interviewId,
    });
  }

  updateQuestionPlan(
    planId: string,
    payload: UpdateJobInterviewQuestionPlan,
  ): Promise<JobInterviewQuestionPlan> {
    return apiService.put<JobInterviewQuestionPlan>(`${this.baseUrl}/question-plans/${planId}`, {
      ...payload,
      id: planId,
    });
  }

  deleteQuestionPlan(planId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/question-plans/${planId}`);
  }

  /** Dry run — proposes a draw per plan without saving, and reports where the bank is too thin. */
  previewQuestions(interviewId: string): Promise<QuestionPlanPreview[]> {
    return apiService.post<QuestionPlanPreview[]>(`${this.baseUrl}/${interviewId}/preview-select-questions`, {});
  }

  /** Re-randomises every plan in place, discarding the current selections. */
  redrawQuestions(interviewId: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${interviewId}/select-questions`, {});
  }

  /**
   * Saves the confirmed selection per plan. The **order matters** — it is the order the panel will
   * ask and score in. Refused wholesale if any question belongs to a different type, leaving the
   * existing selection intact.
   */
  commitQuestions(interviewId: string, payload: CommitInterviewQuestions): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${interviewId}/commit-questions`, payload);
  }

  addSelectedQuestion(planId: string, questionDetailId: string): Promise<void> {
    return apiService.post<void>(
      `${this.baseUrl}/question-plans/${planId}/selected-questions/${questionDetailId}`,
      {},
    );
  }

  removeSelectedQuestion(planId: string, questionDetailId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/question-plans/${planId}/selected-questions/${questionDetailId}`);
  }

  // ── scorecards ───────────────────────────────────────────────────────────

  getScoreSummaries(intervieweeId: string): Promise<JobInterviewScoreSummary[]> {
    return apiService.get<JobInterviewScoreSummary[]>(`${this.baseUrl}/interviewees/${intervieweeId}/score-summaries`);
  }

  getFinalizedScores(intervieweeId: string): Promise<JobInterviewScoreSummary[]> {
    return apiService.get<JobInterviewScoreSummary[]>(
      `${this.baseUrl}/interviewees/${intervieweeId}/finalized-scores`,
    );
  }

  getScoreSummary(scoreSummaryId: string): Promise<JobInterviewScoreSummaryDetail> {
    return apiService.get<JobInterviewScoreSummaryDetail>(`${this.baseUrl}/score-summaries/${scoreSummaryId}`);
  }

  getScoreEntries(scoreSummaryId: string): Promise<JobInterviewScoreEntry[]> {
    return apiService.get<JobInterviewScoreEntry[]>(`${this.baseUrl}/score-summaries/${scoreSummaryId}/entries`);
  }

  /** Upsert: replaces this panelist's unfinalised card for the candidate. */
  saveScoreSummary(
    intervieweeId: string,
    payload: CreateJobInterviewScoreSummary,
  ): Promise<JobInterviewScoreSummary> {
    return apiService.post<JobInterviewScoreSummary>(
      `${this.baseUrl}/interviewees/${intervieweeId}/score-summaries`,
      { ...payload, jobIntervieweeId: intervieweeId },
    );
  }

  /** Signs the card off. Refused unless each plan's `requiredQuestionCount` is met. */
  finalizeScore(scoreSummaryId: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/score-summaries/${scoreSummaryId}/finalize`, {});
  }

  /** The caller's own only, unless HR. */
  getScoresByPanelist(panelistId: string): Promise<JobInterviewScoreSummary[]> {
    return apiService.get<JobInterviewScoreSummary[]>(`${this.baseUrl}/panelists/${panelistId}/scores`);
  }

  getScoresByExternalPanelist(externalPanelistId: string): Promise<JobInterviewScoreSummary[]> {
    return apiService.get<JobInterviewScoreSummary[]>(
      `${this.baseUrl}/external-panelists/${externalPanelistId}/scores`,
    );
  }

  // ── drafts ───────────────────────────────────────────────────────────────

  /**
   * ⚠ Answers **204 No Content with an empty body** when there is no draft — not 200 with null. The
   * shared client turns that into `null`, but anything parsing the response itself must expect it.
   * A draft is deleted when its scorecard is finalised, so this legitimately goes empty.
   */
  async getScoreDraft(
    interviewId: string,
    intervieweeId: string,
    panelist: { internalPanelistId?: string | null; externalPanelistId?: string | null },
  ): Promise<InterviewScoreDraft | null> {
    const params: Record<string, unknown> = {};
    if (panelist.internalPanelistId) params.internalPanelistId = panelist.internalPanelistId;
    if (panelist.externalPanelistId) params.externalPanelistId = panelist.externalPanelistId;

    const draft = await apiService.get<InterviewScoreDraft | null>(
      `${this.baseUrl}/${interviewId}/score-drafts/${intervieweeId}`,
      params,
    );
    return draft ?? null;
  }

  saveScoreDraft(
    interviewId: string,
    intervieweeId: string,
    payload: SaveInterviewScoreDraft,
  ): Promise<InterviewScoreDraft> {
    return apiService.put<InterviewScoreDraft>(`${this.baseUrl}/${interviewId}/score-drafts/${intervieweeId}`, {
      ...payload,
      jobIntervieweeId: intervieweeId,
    });
  }
}

/**
 * api/interview-question-bank — the questions candidates are asked and the bands they are marked
 * against. **HR-only, reads included**: an employee who could read this could read the questions
 * for their own next internal interview.
 *
 * ⚠ The list reads return inactive rows too, deliberately — a deactivated type or question has to
 * stay visible on the screen that can bring it back. Filter on `isActive` for pickers.
 */
class InterviewQuestionBankService {
  private readonly baseUrl = '/interview-question-bank';

  getTypes(): Promise<InterviewQuestionTypeSummary[]> {
    return apiService.get<InterviewQuestionTypeSummary[]>(`${this.baseUrl}/types`);
  }

  getType(id: string): Promise<InterviewQuestionType> {
    return apiService.get<InterviewQuestionType>(`${this.baseUrl}/types/${id}`);
  }

  createType(payload: CreateInterviewQuestionType): Promise<InterviewQuestionType> {
    return apiService.post<InterviewQuestionType>(`${this.baseUrl}/types`, payload);
  }

  updateType(id: string, payload: UpdateInterviewQuestionType): Promise<InterviewQuestionType> {
    return apiService.put<InterviewQuestionType>(`${this.baseUrl}/types/${id}`, { ...payload, id });
  }

  deleteType(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/types/${id}`);
  }

  getQuestions(): Promise<InterviewQuestion[]> {
    return apiService.get<InterviewQuestion[]>(`${this.baseUrl}/questions`);
  }

  /** Every question of a type, active or not. */
  getQuestionsByType(questionTypeId: string): Promise<InterviewQuestion[]> {
    return apiService.get<InterviewQuestion[]>(`${this.baseUrl}/types/${questionTypeId}/questions`);
  }

  /** Active questions only — what a draw would actually pull from. */
  getActiveQuestions(questionTypeId?: string): Promise<InterviewQuestion[]> {
    return apiService.get<InterviewQuestion[]>(
      `${this.baseUrl}/questions/active`,
      questionTypeId ? { questionTypeId } : undefined,
    );
  }

  createQuestion(payload: CreateInterviewQuestion): Promise<InterviewQuestion> {
    return apiService.post<InterviewQuestion>(`${this.baseUrl}/questions`, payload);
  }

  updateQuestion(id: string, payload: UpdateInterviewQuestion): Promise<InterviewQuestion> {
    return apiService.put<InterviewQuestion>(`${this.baseUrl}/questions/${id}`, { ...payload, id });
  }

  deleteQuestion(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/questions/${id}`);
  }
}

/**
 * api/interview-question-presets — named templates ("which question types, how many of each").
 * Applying one when scheduling scaffolds the question plans and draws the questions. HR-only.
 */
class InterviewQuestionPresetService {
  private readonly baseUrl = '/interview-question-presets';

  getAll(): Promise<InterviewQuestionPresetSummary[]> {
    return apiService.get<InterviewQuestionPresetSummary[]>(this.baseUrl);
  }

  getById(id: string): Promise<InterviewQuestionPreset> {
    return apiService.get<InterviewQuestionPreset>(`${this.baseUrl}/${id}`);
  }

  create(payload: CreateInterviewQuestionPreset): Promise<InterviewQuestionPreset> {
    return apiService.post<InterviewQuestionPreset>(this.baseUrl, payload);
  }

  /** ⚠ Replace-set: `items` is the whole list, and anything omitted is deleted. */
  update(id: string, payload: UpdateInterviewQuestionPreset): Promise<InterviewQuestionPreset> {
    return apiService.put<InterviewQuestionPreset>(`${this.baseUrl}/${id}`, { ...payload, id });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/**
 * api/external-associates — the picker for external panel members, who have no ERP login.
 *
 * ⚠ Re-exported from `external-associate.service`, not declared again. This file used to hold its
 * own two-method copy, and the copy was already wrong: its `getActive()` was typed
 * `ExternalAssociateSearchResult[]` while the endpoint answers summary rows. Slice 8 built the full
 * register, and one endpoint deserves one client.
 */
export { externalAssociateService } from './external-associate.service';

export const jobInterviewService = new JobInterviewService();
export const interviewQuestionBankService = new InterviewQuestionBankService();
export const interviewQuestionPresetService = new InterviewQuestionPresetService();
