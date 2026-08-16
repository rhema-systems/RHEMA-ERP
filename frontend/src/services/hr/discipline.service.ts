import { apiService } from '../api.service';
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
  DisciplineHearing,
  StaffOffense,
  StaffOffenseSummary,
  DisciplinaryActionTypeSummary,
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

export const disciplineService = new DisciplineService();
export const disciplineProcessService = new DisciplineProcessService();
export const disciplineLookupService = new DisciplineLookupService();
