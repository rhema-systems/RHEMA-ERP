import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  DisciplinaryCase,
  DisciplinaryCaseSummary,
  DisciplinaryStatus,
  StaffOffenseSeverity,
  CreateDisciplinaryCaseRequest,
  UpdateDisciplinaryCaseRequest,
  RecordDecisionRequest,
  CloseCaseRequest,
  DisciplineDashboard,
  DisciplineProcessClock,
  DisciplineInvestigation,
  DisciplineAppeal,
  FileAppealRequest,
  ScheduleAppealHearingRequest,
  RecordAppealOutcomeRequest,
  DisciplineHearing,
  StaffOffense,
  StaffOffenseSummary,
  DisciplinaryActionTypeSummary,
  DisciplineActionStep,
  UpdateDisciplineActionStep,
  DisciplineLegalReview,
  CreateDisciplineLegalReview,
  UpdateDisciplineLegalReview,
  DisciplineCorrectiveAction,
  CreateDisciplineCorrectiveAction,
  UpdateDisciplineCorrectiveAction,
  DisciplineCorrectiveActionItem,
  CreateDisciplineCorrectiveActionItem,
  UpdateDisciplineCorrectiveActionItem,
  DisciplineWitness,
  CreateDisciplineWitness,
  UpdateDisciplineWitness,
  DisciplineDocument,
  UploadDisciplineDocumentFields,
  DisciplineNote,
  CreateDisciplineNote,
  UpdateDisciplineNote,
  DisciplineNotification,
  CreateDisciplineNotification,
} from '@/types/hr/discipline';

/**
 * Staff discipline cases. Backend route: `api/discipline/cases`.
 *
 * HR-gated per action, with two exceptions that belong to the employee rather than to HR:
 * `getMine`, and reading a case you are the subject of. The server enforces both — and enforces
 * more besides, which the UI must not contradict: filing an appeal is refused for anyone but the
 * subject (HR included), because an appeal is the subject's own act rather than an administrative
 * one. See `appeal.service` when slice 5 lands.
 */
class DisciplineService {
  private readonly baseUrl = '/discipline/cases';

  // ── Queries ────────────────────────────────────────────────────────────────

  getPaged(params: { pageNumber?: number; pageSize?: number } = {}): Promise<PagedResult<DisciplinaryCaseSummary>> {
    return apiService.get<PagedResult<DisciplinaryCaseSummary>>(this.baseUrl, params);
  }

  getOpen(): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/open`);
  }

  getById(id: string): Promise<DisciplinaryCase> {
    return apiService.get<DisciplinaryCase>(`${this.baseUrl}/${id}`);
  }

  getByCaseNumber(caseNumber: string): Promise<DisciplinaryCase | null> {
    return apiService.get<DisciplinaryCase | null>(
      `${this.baseUrl}/number/${encodeURIComponent(caseNumber)}`,
    );
  }

  /** The caller's own cases — the token supplies the employee, so there is no id to pass. */
  getMine(): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/mine`);
  }

  getByEmployee(employeeId: string): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getOpenCountForEmployee(employeeId: string): Promise<number> {
    return apiService.get<number>(`${this.baseUrl}/employee/${employeeId}/open-count`);
  }

  getByStatus(status: DisciplinaryStatus): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getByOffense(offenseId: string): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/offense/${offenseId}`);
  }

  /** Returns cases at or above the given severity, not only that severity. */
  getBySeverity(severity: StaffOffenseSeverity): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/severity/${severity}`);
  }

  getByDateRange(from: string, to: string, field: 'incident' | 'reported' = 'incident'): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/date-range`, { from, to, field });
  }

  // ── Work queues ────────────────────────────────────────────────────────────

  getPendingInvestigation(): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/pending/investigation`);
  }

  getPendingHearing(): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/pending/hearing`);
  }

  getPendingClosure(): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/pending/closure`);
  }

  getWithActiveWarning(): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/with/active-warning`);
  }

  getWithActiveSuspension(): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/with/active-suspension`);
  }

  getWithOutstandingFine(): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/with/outstanding-fine`);
  }

  getWithPendingTermination(): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/with/pending-termination`);
  }

  getWithActiveAppeal(): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/with/active-appeal`);
  }

  getWithActiveLegalReview(): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/with/active-legal-review`);
  }

  getDashboard(): Promise<DisciplineDashboard> {
    return apiService.get<DisciplineDashboard>(`${this.baseUrl}/dashboard`);
  }

  /**
   * How the case stands against FR-HR-177's 48-hour written query and FR-HR-178's four-week
   * investigation. Advisory — nothing here refuses an action. Readable by the case's subject as well
   * as HR: the clocks exist to protect the person being investigated.
   */
  getProcessClock(id: string): Promise<DisciplineProcessClock> {
    return apiService.get<DisciplineProcessClock>(`${this.baseUrl}/${id}/process-clock`);
  }

  /**
   * Acknowledges a notice served on the caller — the SUBJECT's own act; the server refuses anyone
   * else, HR included, and stamps the date itself. Notice ids arrive on the case detail's
   * `notifications` rows (area 25 slice 9). Answers `{ message }`, not the notification — refetch.
   */
  acknowledgeNotification(notificationId: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(
      `/discipline/notifications/${notificationId}/acknowledge`,
      { notificationId },
    );
  }

  // ── Mutations ──────────────────────────────────────────────────────────────

  create(payload: CreateDisciplinaryCaseRequest): Promise<DisciplinaryCase> {
    return apiService.post<DisciplinaryCase>(this.baseUrl, payload);
  }

  update(id: string, payload: UpdateDisciplinaryCaseRequest): Promise<DisciplinaryCase> {
    return apiService.put<DisciplinaryCase>(`${this.baseUrl}/${id}`, payload);
  }

  /** Only a Draft case can be deleted; anything further along is closed or dismissed instead. */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Lifecycle ──────────────────────────────────────────────────────────────

  submit(id: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/submit`, {});
  }

  startReview(id: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/start-review`, {});
  }

  /**
   * Proposes a sanction and sends it for confirmation. This does NOT finalise the decision: the
   * case moves to AwaitingDecision and an approval instance starts. It becomes DecisionMade only
   * when the assigned approver confirms it.
   */
  recordDecision(id: string, payload: RecordDecisionRequest): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/decision`, payload);
  }

  /**
   * The cases whose proposed decision the caller may confirm. Token-derived — the engine decides
   * what is in it. Open to non-HR callers by design: the confirming officer is a head of department
   * or the MD, and the register answers 403 for them, so this is the only place their work appears.
   */
  getAwaitingMyApproval(): Promise<DisciplinaryCaseSummary[]> {
    return apiService.get<DisciplinaryCaseSummary[]>(`${this.baseUrl}/awaiting-my-approval`);
  }

  approveDecision(id: string, comments?: string | null): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/approve-decision`, { comments });
  }

  /** Refusing a sanction returns the case to review; it does not refuse the allegation. */
  rejectDecision(id: string, comments?: string | null): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/reject-decision`, { comments });
  }

  recallDecision(id: string, comments?: string | null): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/recall-decision`, { comments });
  }

  /**
   * Records that the employee could not be given a chance to answer the written query, so a decision
   * may proceed without it.
   *
   * This is the documented exception to the rule that nobody is sanctioned unheard. The reason is
   * required and goes onto the case record — it is not a dismissible confirmation.
   */
  waiveQueryOpportunity(id: string, reason: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/waive-query-opportunity`, { reason });
  }

  close(id: string, payload: CloseCaseRequest): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/close`, payload);
  }

  hold(id: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/hold`, {});
  }

  reactivate(id: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/reactivate`, {});
  }

  dismiss(id: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/dismiss`, {});
  }
}

/**
 * Appeals. Backend routes nested under `api/discipline`.
 *
 * Filing is the appellant's own act — the server refuses it from anyone but the case's subject, HR
 * included — while scheduling and deciding are HR's. The UI must not offer HR a "file on their
 * behalf" affordance, because there isn't one and there should not be.
 */
class DisciplineAppealService {
  getForCase(caseId: string): Promise<DisciplineAppeal | null> {
    return apiService.get<DisciplineAppeal | null>(`/discipline/cases/${caseId}/appeal`);
  }

  /** The caller's own appeals — token-derived, no id segment. */
  getMine(): Promise<DisciplineAppeal[]> {
    return apiService.get<DisciplineAppeal[]>('/discipline/appeals/mine');
  }

  getPendingHearing(): Promise<DisciplineAppeal[]> {
    return apiService.get<DisciplineAppeal[]>('/discipline/appeals/pending-hearing');
  }

  getAwaitingOutcome(): Promise<DisciplineAppeal[]> {
    return apiService.get<DisciplineAppeal[]>('/discipline/appeals/awaiting-outcome');
  }

  /**
   * Files an appeal. Refused after FR-HR-180's five-working-day window has closed, and refused from
   * anyone but the employee the case was brought against.
   */
  file(caseId: string, payload: FileAppealRequest): Promise<DisciplineAppeal> {
    return apiService.post<DisciplineAppeal>(`/discipline/cases/${caseId}/appeal`, payload);
  }

  scheduleHearing(caseId: string, payload: ScheduleAppealHearingRequest): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`/discipline/cases/${caseId}/appeal/schedule-hearing`, payload);
  }

  recordOutcome(caseId: string, payload: RecordAppealOutcomeRequest): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`/discipline/cases/${caseId}/appeal/outcome`, payload);
  }
}

/**
 * The investigation and hearing work queues. All HR-gated.
 */
class DisciplineProcessService {
  getOpenInvestigations(): Promise<DisciplineInvestigation[]> {
    return apiService.get<DisciplineInvestigation[]>('/discipline/investigations/open');
  }

  /**
   * Investigations past FR-HR-178's four weeks.
   *
   * `maxDays` is deliberately not passed: the server defaults to the rule, and the whole point of
   * moving that constant server-side was to stop this queue and the case advisory answering the
   * same question differently. Pass one only for a genuine ad-hoc sweep.
   */
  getOverdueInvestigations(): Promise<DisciplineInvestigation[]> {
    return apiService.get<DisciplineInvestigation[]>('/discipline/investigations/overdue');
  }

  getUpcomingHearings(): Promise<DisciplineHearing[]> {
    return apiService.get<DisciplineHearing[]>('/discipline/hearings/upcoming');
  }

  getHearingsAwaitingOutcome(): Promise<DisciplineHearing[]> {
    return apiService.get<DisciplineHearing[]>('/discipline/hearings/awaiting-outcome');
  }
}

/**
 * The offence and action-type catalogs. Setup data, HR-gated at class level — an ordinary employee
 * gets a 403 from every method here, so screens that offer them must be behind the same gate.
 */
class DisciplineLookupService {
  getOffenses(): Promise<StaffOffenseSummary[]> {
    return apiService.get<StaffOffenseSummary[]>('/discipline/offenses');
  }

  getActiveOffenses(): Promise<StaffOffenseSummary[]> {
    return apiService.get<StaffOffenseSummary[]>('/discipline/offenses/active');
  }

  getOffenseWithProcedures(id: string): Promise<StaffOffense> {
    return apiService.get<StaffOffense>(`/discipline/offenses/${id}/with-procedures`);
  }

  getActionTypes(): Promise<DisciplinaryActionTypeSummary[]> {
    return apiService.get<DisciplinaryActionTypeSummary[]>('/discipline/action-types');
  }

  getActiveActionTypes(): Promise<DisciplinaryActionTypeSummary[]> {
    return apiService.get<DisciplinaryActionTypeSummary[]>('/discipline/action-types/active');
  }
}

/**
 * The procedural ladder a case has to climb: the offence's own steps, instantiated per case.
 *
 * ⚠ **The steps do not exist until someone initialises them.** `initialise` copies the offence's
 * `StaffOffenseProcedure` rows onto the case and stamps the due dates from each step's
 * `ExpectedCompletionDays`. Until it is called the case has no steps at all, which is what the
 * detail screen's "the procedure has not been initialised for this case" empty state means — it
 * had no button behind it before this service existed.
 *
 * ⚠ **Complete and skip are transitions, not status values.** Both stamp the actor from the token
 * and a date; `complete` also takes notes, `skip` demands a reason. Setting `status: 'Completed'`
 * through `update` instead writes the word and leaves `completedDate` and `actionedById` empty, so
 * the step reads as done and the audit trail says nobody did it. The screen offers the transitions
 * and keeps those two values out of the edit form.
 *
 * ⚠ **Both take a BARE JSON STRING as the body**, not an object. `[FromBody] string notes` binds
 * `"text"`, not `{ "notes": "text" }` — the second yields a 400 that names no field.
 */
class DisciplineActionStepService {
  private readonly base = '/discipline';

  getForCase(caseId: string): Promise<DisciplineActionStep[]> {
    return apiService.get<DisciplineActionStep[]>(`${this.base}/cases/${caseId}/action-steps`);
  }

  getPendingForCase(caseId: string): Promise<DisciplineActionStep[]> {
    return apiService.get<DisciplineActionStep[]>(`${this.base}/cases/${caseId}/action-steps/pending`);
  }

  getOverdue(): Promise<DisciplineActionStep[]> {
    return apiService.get<DisciplineActionStep[]>(`${this.base}/action-steps/overdue`);
  }

  /** Instantiates the offence's procedure onto the case. Returns the steps it created. */
  initialise(caseId: string): Promise<DisciplineActionStep[]> {
    return apiService.post<DisciplineActionStep[]>(`${this.base}/cases/${caseId}/action-steps/initialise`, {});
  }

  /** ⚠ The payload field is `stepId`, not `id`. */
  update(stepId: string, payload: Omit<UpdateDisciplineActionStep, 'stepId'>): Promise<DisciplineActionStep> {
    return apiService.put<DisciplineActionStep>(`${this.base}/action-steps/${stepId}`, { stepId, ...payload });
  }

  /** ⚠ Bare string body. Stamps completedDate and the actor from the token. */
  complete(stepId: string, notes: string) {
    return apiService.post(`${this.base}/action-steps/${stepId}/complete`, notes);
  }

  /** ⚠ Bare string body. A skipped step is a departure from procedure — the reason is the record. */
  skip(stepId: string, reason: string) {
    return apiService.post(`${this.base}/action-steps/${stepId}/skip`, reason);
  }
}

/**
 * Referrals of a case to legal, and what came back.
 *
 * ⚠ **Read these, not the case detail's copy.** `DisciplinaryCaseDetail.legalReviews` is a
 * seven-field summary with no advice, no counsel and no costs; `getForCase` returns the full
 * record. A panel built on the detail's copy renders a row of blanks.
 *
 * ⚠ **`referredById` is stamped from the token and is not in the create payload.** It used to be
 * accepted from the body and copied onto the entity verbatim while the token's employee id went
 * only to `CreatedBy`, so any HR user could record a colleague as the referrer. Removed when this
 * screen was built; nothing had ever sent it.
 *
 * ⚠ **Delete is Admin-tier** (`DisciplineAdminPolicy`) while create, update and complete are Write,
 * so an HR author can refer a case and record the advice but cannot erase the referral.
 *
 * ⚠ **`isConfidential` defaults to TRUE.** A legal review is privileged unless someone says
 * otherwise, so the form must default it on rather than leaving a switch at its React default.
 */
class DisciplineLegalReviewService {
  private readonly base = '/discipline';

  getForCase(caseId: string): Promise<DisciplineLegalReview[]> {
    return apiService.get<DisciplineLegalReview[]>(`${this.base}/cases/${caseId}/legal-reviews`);
  }

  getById(id: string): Promise<DisciplineLegalReview> {
    return apiService.get<DisciplineLegalReview>(`${this.base}/legal-reviews/${id}`);
  }

  /** Total legal spend on a case. A plain number, not an object. */
  getTotalCostsForCase(caseId: string): Promise<number> {
    return apiService.get<number>(`${this.base}/cases/${caseId}/legal-reviews/total-costs`);
  }

  getOpen(): Promise<DisciplineLegalReview[]> {
    return apiService.get<DisciplineLegalReview[]>(`${this.base}/legal-reviews/open`);
  }

  getRequiringExternalCounsel(): Promise<DisciplineLegalReview[]> {
    return apiService.get<DisciplineLegalReview[]>(`${this.base}/legal-reviews/requiring-external-counsel`);
  }

  /** "Refer this case to legal." */
  refer(caseId: string, payload: CreateDisciplineLegalReview): Promise<DisciplineLegalReview> {
    return apiService.post<DisciplineLegalReview>(`${this.base}/cases/${caseId}/legal-reviews`, payload);
  }

  update(id: string, payload: Omit<UpdateDisciplineLegalReview, 'id'>): Promise<DisciplineLegalReview> {
    return apiService.put<DisciplineLegalReview>(`${this.base}/legal-reviews/${id}`, { id, ...payload });
  }

  /** Stamps the completion date and the actor. No body. */
  complete(id: string) {
    return apiService.post(`${this.base}/legal-reviews/${id}/complete`, {});
  }

  /** ⚠ Admin-tier. */
  remove(id: string) {
    return apiService.delete(`${this.base}/legal-reviews/${id}`);
  }
}

/**
 * The corrective action plan on a case, and the items that make it up.
 *
 * ⚠ **A case has AT MOST ONE plan** — `cases/{id}/corrective-action` is singular and returns the
 * record or null, so this is a form-plus-list, not a collection. Items hang off the plan.
 *
 * ⚠ **`employeeId` is not derived from the case.** `CreateAsync` copies it from the body, so the
 * caller must pass the case's own subject; nothing on the server checks that it matches.
 *
 * ⚠ **The API enforces the lock, unusually.** `UpdateAsync` throws on a Completed or Cancelled
 * plan — one of the few places in HR where a lifecycle rule lives on the server rather than only on
 * the screen. See `LOCKED_CORRECTIVE_ACTION_STATUSES`, which mirrors it.
 */
class DisciplineCorrectiveActionService {
  private readonly base = '/discipline';

  /** ⚠ Singular, and null when the case has no plan. */
  getForCase(caseId: string): Promise<DisciplineCorrectiveAction | null> {
    return apiService.get<DisciplineCorrectiveAction | null>(`${this.base}/cases/${caseId}/corrective-action`);
  }

  getById(id: string): Promise<DisciplineCorrectiveAction> {
    return apiService.get<DisciplineCorrectiveAction>(`${this.base}/corrective-actions/${id}`);
  }

  getOverdue(): Promise<DisciplineCorrectiveAction[]> {
    return apiService.get<DisciplineCorrectiveAction[]>(`${this.base}/corrective-actions/overdue`);
  }

  getDueForReview(): Promise<DisciplineCorrectiveAction[]> {
    return apiService.get<DisciplineCorrectiveAction[]>(`${this.base}/corrective-actions/due-for-review`);
  }

  create(caseId: string, payload: CreateDisciplineCorrectiveAction): Promise<DisciplineCorrectiveAction> {
    return apiService.post<DisciplineCorrectiveAction>(`${this.base}/cases/${caseId}/corrective-action`, payload);
  }

  update(id: string, payload: Omit<UpdateDisciplineCorrectiveAction, 'id'>): Promise<DisciplineCorrectiveAction> {
    return apiService.put<DisciplineCorrectiveAction>(`${this.base}/corrective-actions/${id}`, { id, ...payload });
  }

  /** No body. Stamps the completion date and the actor. */
  complete(id: string) {
    return apiService.post(`${this.base}/corrective-actions/${id}/complete`, {});
  }

  /** ⚠ Admin-tier. */
  remove(id: string) {
    return apiService.delete(`${this.base}/corrective-actions/${id}`);
  }

  // ── the plan's items ───────────────────────────────────────────────────────

  addItem(correctiveActionId: string, payload: CreateDisciplineCorrectiveActionItem): Promise<DisciplineCorrectiveActionItem> {
    return apiService.post<DisciplineCorrectiveActionItem>(
      `${this.base}/corrective-actions/${correctiveActionId}/items`,
      payload,
    );
  }

  /** ⚠ The route is `corrective-action-items/{id}` — singular "action", and no plan id. */
  updateItem(itemId: string, payload: Omit<UpdateDisciplineCorrectiveActionItem, 'id'>): Promise<DisciplineCorrectiveActionItem> {
    return apiService.put<DisciplineCorrectiveActionItem>(
      `${this.base}/corrective-action-items/${itemId}`,
      { id: itemId, ...payload },
    );
  }

  /** ⚠ Bare string body, like the action-step transitions. Stamps the date and the actor. */
  completeItem(itemId: string, completionNotes: string) {
    return apiService.post(`${this.base}/corrective-action-items/${itemId}/complete`, completionNotes);
  }

  /** ⚠ Admin-tier. */
  removeItem(itemId: string) {
    return apiService.delete(`${this.base}/corrective-action-items/${itemId}`);
  }
}

/**
 * People who saw what happened, and what they said.
 *
 * ⚠ **`isEmployee` and `employeeId` are two fields, and the server trusts both as given.** Nothing
 * clears `employeeId` when `isEmployee` is false, so a row can claim to be external while still
 * pointing at a staff record. The panel keeps them consistent.
 *
 * ⚠ **The case detail's copy is a summary without `contactInfo`** — the one field the closure
 * ledger listed as unreachable on both the create and update DTOs. Use `getForCase` for the record.
 */
class DisciplineWitnessService {
  private readonly base = '/discipline';

  getForCase(caseId: string): Promise<DisciplineWitness[]> {
    return apiService.get<DisciplineWitness[]>(`${this.base}/cases/${caseId}/witnesses`);
  }

  /** Those still owing a statement — the follow-up list. */
  getWithoutStatement(caseId: string): Promise<DisciplineWitness[]> {
    return apiService.get<DisciplineWitness[]>(`${this.base}/cases/${caseId}/witnesses/without-statement`);
  }

  getById(id: string): Promise<DisciplineWitness> {
    return apiService.get<DisciplineWitness>(`${this.base}/witnesses/${id}`);
  }

  add(caseId: string, payload: CreateDisciplineWitness): Promise<DisciplineWitness> {
    return apiService.post<DisciplineWitness>(`${this.base}/cases/${caseId}/witnesses`, payload);
  }

  update(id: string, payload: Omit<UpdateDisciplineWitness, 'id'>): Promise<DisciplineWitness> {
    return apiService.put<DisciplineWitness>(`${this.base}/witnesses/${id}`, { id, ...payload });
  }

  /** ⚠ Admin-tier. */
  remove(id: string) {
    return apiService.delete(`${this.base}/witnesses/${id}`);
  }
}

/**
 * The case's documents.
 *
 * ⚠ **Files go through the controlled gate, and only through it.** `upload` is multipart; the file
 * is scanned, registered in the DMS and stored outside the web root. There is a second route,
 * `POST cases/{id}/documents`, which this service deliberately does NOT expose: it rejects every
 * file-location field the caller could send, so through the API it can only create a row naming a
 * file that does not exist. It survives for the legacy migration utility, not for screens.
 *
 * ⚠ **`filePath` is not a URL.** Download is a token-bearing fetch of `documents/{id}/download`,
 * never an `href` — the stored path is a server-side location and rendering it as a link both
 * fails and leaks.
 *
 * ⚠ **The scope is validated before any bytes are stored.** `ActionStep` demands an action-step id
 * and `Appeal` an appeal id; an incoherent pair is a 400 with a message, not a silent default.
 */
class DisciplineDocumentService {
  private readonly base = '/discipline';

  getForCase(caseId: string): Promise<DisciplineDocument[]> {
    return apiService.get<DisciplineDocument[]>(`${this.base}/cases/${caseId}/documents`);
  }

  getForActionStep(actionStepId: string): Promise<DisciplineDocument[]> {
    return apiService.get<DisciplineDocument[]>(`${this.base}/action-steps/${actionStepId}/documents`);
  }

  getForAppeal(appealId: string): Promise<DisciplineDocument[]> {
    return apiService.get<DisciplineDocument[]>(`${this.base}/appeals/${appealId}/documents`);
  }

  getById(id: string): Promise<DisciplineDocument> {
    return apiService.get<DisciplineDocument>(`${this.base}/documents/${id}`);
  }

  /** ⚠ Multipart, 10 MB cap, scanned. The only supported way to attach a file. */
  upload(caseId: string, file: File, fields: UploadDisciplineDocumentFields): Promise<DisciplineDocument> {
    return hrDocumentService.upload<DisciplineDocument>(
      `${this.base}/cases/${caseId}/documents/upload`,
      file,
      {
        scope: fields.scope,
        category: fields.category,
        actionStepId: fields.actionStepId ?? undefined,
        appealId: fields.appealId ?? undefined,
        description: fields.description ?? undefined,
      },
    );
  }

  /** Streams the file with the bearer token attached and saves it under its own name. */
  download(id: string, fileName: string): Promise<void> {
    return hrDocumentService.download(`${this.base}/documents/${id}/download`, fileName);
  }

  /** ⚠ Admin-tier. */
  remove(id: string) {
    return apiService.delete(`${this.base}/documents/${id}`);
  }
}

/**
 * HR's own notes on the case.
 *
 * ⚠ **The author is stamped from the token** and the create payload has no field for it. It used
 * to be accepted from the body and copied verbatim, so a note could be attributed to a colleague —
 * on a record that is evidence of what HR knew and when. Ledger D-08.
 *
 * ⚠ **`isConfidential` is honoured by the reads.** A confidential note is withheld from the case's
 * subject, so the flag is a disclosure decision rather than a label.
 */
class DisciplineNoteService {
  private readonly base = '/discipline';

  getForCase(caseId: string): Promise<DisciplineNote[]> {
    return apiService.get<DisciplineNote[]>(`${this.base}/cases/${caseId}/notes`);
  }

  getById(id: string): Promise<DisciplineNote> {
    return apiService.get<DisciplineNote>(`${this.base}/notes/${id}`);
  }

  add(caseId: string, payload: CreateDisciplineNote): Promise<DisciplineNote> {
    return apiService.post<DisciplineNote>(`${this.base}/cases/${caseId}/notes`, payload);
  }

  /** ⚠ Only the text and the confidentiality flag — neither author nor date can be rewritten. */
  update(id: string, payload: Omit<UpdateDisciplineNote, 'id'>): Promise<DisciplineNote> {
    return apiService.put<DisciplineNote>(`${this.base}/notes/${id}`, { id, ...payload });
  }

  /** ⚠ Admin-tier. */
  remove(id: string) {
    return apiService.delete(`${this.base}/notes/${id}`);
  }
}

/**
 * Formal notices issued to the subject of the case.
 *
 * ⚠ **Acknowledgement is the SUBJECT's act and nobody else's.** `AcknowledgeAsync` throws
 * "Only the employee a notification was issued to can acknowledge it" — so an HR screen must show
 * the acknowledged state and must NOT offer a button to set it. Offering one would put a control
 * in front of the only people guaranteed to be refused by it.
 *
 * ⚠ **There is no update and no delete.** A notice, once issued, is a fact about what the employee
 * was told; the follow-up is a second notice rather than an edit to the first.
 *
 * ⚠ **`sentById` is server-stamped** and absent from the create payload — this one was already
 * correct, unlike the note author and the legal-review referrer.
 */
class DisciplineNotificationService {
  private readonly base = '/discipline';

  getForCase(caseId: string): Promise<DisciplineNotification[]> {
    return apiService.get<DisciplineNotification[]>(`${this.base}/cases/${caseId}/notifications`);
  }

  getUnacknowledged(caseId: string): Promise<DisciplineNotification[]> {
    return apiService.get<DisciplineNotification[]>(`${this.base}/cases/${caseId}/notifications/unacknowledged`);
  }

  getById(id: string): Promise<DisciplineNotification> {
    return apiService.get<DisciplineNotification>(`${this.base}/notifications/${id}`);
  }

  send(caseId: string, payload: CreateDisciplineNotification): Promise<DisciplineNotification> {
    return apiService.post<DisciplineNotification>(`${this.base}/cases/${caseId}/notifications`, payload);
  }

  /**
   * ⚠ For the SUBJECT's own surface only — HR is refused. Kept here so a self-service screen has
   * it, and deliberately not called by the HR case panel.
   */
  acknowledge(id: string) {
    return apiService.post(`${this.base}/notifications/${id}/acknowledge`, { notificationId: id });
  }

  /** Chases an unacknowledged notice. `followupDate` defaults to now on the server. */
  sendFollowup(id: string, followupDate?: string) {
    return apiService.post(`${this.base}/notifications/${id}/followup`, {
      notificationId: id,
      ...(followupDate ? { followupDate } : {}),
    });
  }
}

export const disciplineService = new DisciplineService();
export const disciplineAppealService = new DisciplineAppealService();
export const disciplineProcessService = new DisciplineProcessService();
export const disciplineLookupService = new DisciplineLookupService();
export const disciplineActionStepService = new DisciplineActionStepService();
export const disciplineLegalReviewService = new DisciplineLegalReviewService();
export const disciplineCorrectiveActionService = new DisciplineCorrectiveActionService();
export const disciplineWitnessService = new DisciplineWitnessService();
export const disciplineDocumentService = new DisciplineDocumentService();
export const disciplineNoteService = new DisciplineNoteService();
export const disciplineNotificationService = new DisciplineNotificationService();
