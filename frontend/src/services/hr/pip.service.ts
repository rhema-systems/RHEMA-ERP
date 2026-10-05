import { apiService } from '../api.service';
import type {
  CreatePip,
  EmployeeSearchResult,
  Pip,
  PipDashboard,
  PipDetail,
  PipGoal,
  PipGoalRequest,
  PipMeetingForm,
  PipMeetingSchedule,
  PipOutcome,
  PipPrepare,
  PipReviewMeeting,
  PipStatus,
  UpdatePip,
} from '@/types/hr/pip';
import { PIP_OUTCOME_VALUES } from '@/types/hr/pip';

/**
 * api/Pip — performance improvement plans.
 *
 * **Approval runs on the workflow engine.** `submit`, `approve`, `reject` and `recall` drive it;
 * the screen must never set a status itself — refetch and let the adapter decide. Reuse
 * `WorkflowApprovalActions` / `WorkflowRecordTab` rather than building buttons here.
 *
 * With no published definition the server's fallback rules on them; the decider is the record's
 * rule either way (whoever submitted does not decide — see `types/hr/pip.ts`).
 *
 * **Reads are entitlement-scoped.** Anything keyed on a plan id is 403 unless the caller is HR,
 * the employee, the supervisor or the HR owner. `getMine` and `getSupervising` need no id at all.
 */
class PipService {
  private readonly baseUrl = '/Pip';

  /** HR's org-wide list. 403 for everyone else — use `getMine` / `getSupervising`. */
  getAll(): Promise<Pip[]> {
    return apiService.get<Pip[]>(this.baseUrl);
  }

  getById(id: string): Promise<Pip> {
    return apiService.get<Pip>(`${this.baseUrl}/${id}`);
  }

  /** Plan, goals, attachments and meetings in one call — what the detail screen reads. */
  getDetail(id: string): Promise<PipDetail> {
    return apiService.get<PipDetail>(`${this.baseUrl}/${id}/detail`);
  }

  /** The signed-in employee's own plans. `[]` when the account has no employee link. */
  getMine(): Promise<Pip[]> {
    return apiService.get<Pip[]>(`${this.baseUrl}/mine`);
  }

  /** Plans the signed-in employee owns as supervisor or HR owner. */
  getSupervising(): Promise<Pip[]> {
    return apiService.get<Pip[]>(`${this.baseUrl}/supervising`);
  }

  getByEmployee(employeeId: string): Promise<Pip[]> {
    return apiService.get<Pip[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  /**
   * Pre-fills a new plan from the employee, and from the appraisal when one is named — the score
   * and grade that prompted it.
   */
  prepare(employeeId: string, appraisalId?: string): Promise<PipPrepare> {
    return apiService.get<PipPrepare>(`${this.baseUrl}/prepare`, {
      employeeId,
      ...(appraisalId ? { appraisalId } : {}),
    });
  }

  /** Returns the new plan's id. It is created as a Draft and is not yet in force. */
  create(data: CreatePip): Promise<string> {
    return apiService.post<string>(this.baseUrl, data);
  }

  /** 422 while the plan is out for approval or once it is closed. */
  update(id: string, data: UpdatePip): Promise<Pip> {
    return apiService.put<Pip>(`${this.baseUrl}/${id}`, data);
  }

  /** Only a draft can be deleted; a plan that has been in force is cancelled, not erased. */
  delete(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }

  /** Moves a live plan between its running states. Draft/PendingApproval/Active are refused. */
  updateStatus(id: string, status: PipStatus): Promise<boolean> {
    return apiService.patch<boolean>(`${this.baseUrl}/${id}/status`, { pipId: id, status });
  }

  // ── Approval workflow ───────────────────────────────────────────────────────

  submit(id: string): Promise<Pip> {
    return apiService.post<Pip>(`${this.baseUrl}/${id}/submit`);
  }

  approve(id: string): Promise<Pip> {
    return apiService.post<Pip>(`${this.baseUrl}/${id}/approve`);
  }

  reject(id: string, reason?: string | null): Promise<Pip> {
    return apiService.post<Pip>(`${this.baseUrl}/${id}/reject`, { reason: reason ?? null });
  }

  recall(id: string): Promise<Pip> {
    return apiService.post<Pip>(`${this.baseUrl}/${id}/recall`);
  }

  // ── Outcome ─────────────────────────────────────────────────────────────────

  /**
   * Records the outcome. `Extended` is the one that does not close the plan — it needs
   * `newEndDate` and pushes the end date out instead.
   */
  recordOutcome(
    id: string,
    outcome: PipOutcome,
    notes?: string | null,
    newEndDate?: string | null,
  ): Promise<boolean> {
    return apiService.post<boolean>(`${this.baseUrl}/${id}/outcome`, {
      outcome: PIP_OUTCOME_VALUES[outcome],
      notes: notes ?? null,
      completionDate: new Date().toISOString(),
      newEndDate: newEndDate ?? null,
    });
  }

  // ── Goals ───────────────────────────────────────────────────────────────────

  getGoals(pipId: string): Promise<PipGoal[]> {
    return apiService.get<PipGoal[]>(`${this.baseUrl}/${pipId}/goals`);
  }

  addGoal(pipId: string, data: PipGoalRequest): Promise<PipGoal> {
    return apiService.post<PipGoal>(`${this.baseUrl}/${pipId}/goals`, data);
  }

  /** Note the flat route: goals are keyed by their own id once created, not by plan. */
  updateGoal(goalId: string, data: PipGoalRequest): Promise<PipGoal> {
    return apiService.put<PipGoal>(`${this.baseUrl}/goals/${goalId}`, data);
  }

  deleteGoal(goalId: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/goals/${goalId}`);
  }

  // ── Review meetings ─────────────────────────────────────────────────────────

  getReviewMeetings(pipId: string): Promise<PipReviewMeeting[]> {
    return apiService.get<PipReviewMeeting[]>(`${this.baseUrl}/${pipId}/review-meetings`);
  }

  deleteReviewMeeting(pipId: string, meetingId: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${pipId}/review-meetings/${meetingId}`);
  }

  // ── Attachments ─────────────────────────────────────────────────────────────

  /**
   * Improvement plans are sensitive employment records, so attachments are never public URLs —
   * they are streamed by an endpoint that checks entitlement per request.
   */
  downloadAttachment(attachmentId: string): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/attachments/${attachmentId}/download`);
  }

  deleteAttachment(attachmentId: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/attachments/${attachmentId}`);
  }

  // ── Employee search ─────────────────────────────────────────────────────────

  /** Two characters minimum; shorter terms come back empty rather than as an error. */
  searchEmployees(q: string): Promise<EmployeeSearchResult[]> {
    return apiService.get<EmployeeSearchResult[]>(`${this.baseUrl}/employees/search`, { q });
  }
}

/** api/PipDashboard — HR's org-wide roll-up. 403 for anyone who is not HR. */
class PipDashboardService {
  getDashboard(): Promise<PipDashboard> {
    return apiService.get<PipDashboard>('/PipDashboard');
  }
}

/**
 * api/PipMeeting — the review meeting form.
 *
 * The form is both the read and the write shape: `prepare` hands back a blank one with the plan's
 * context and its goals, and `create` / `update` take the same object back. Goal progress typed
 * into `goalUpdates` is applied to the goals themselves when the form is saved.
 */
class PipMeetingService {
  private readonly baseUrl = '/PipMeeting';

  getMeeting(meetingId: string, pipId: string): Promise<PipMeetingForm> {
    return apiService.get<PipMeetingForm>(`${this.baseUrl}/${meetingId}`, { pipId });
  }

  prepare(pipId: string): Promise<PipMeetingForm> {
    return apiService.get<PipMeetingForm>(`${this.baseUrl}/prepare`, { pipId });
  }

  /** Books a meeting with just a date. Omit `conductedById` to be recorded as holding it. */
  schedule(pipId: string, meetingDate: string, conductedById?: string): Promise<string> {
    return apiService.post<string>(`${this.baseUrl}/schedule`, {
      pipId,
      meetingDate,
      conductedById: conductedById ?? '00000000-0000-0000-0000-000000000000',
    });
  }

  create(form: PipMeetingForm): Promise<PipMeetingForm> {
    return apiService.post<PipMeetingForm>(this.baseUrl, form);
  }

  update(id: string, form: PipMeetingForm): Promise<PipMeetingForm> {
    return apiService.put<PipMeetingForm>(`${this.baseUrl}/${id}`, form);
  }

  /** Record meeting: saves the record and stores the meeting as Held (closure D-73). */
  complete(id: string, form: PipMeetingForm): Promise<PipMeetingForm> {
    return apiService.post<PipMeetingForm>(`${this.baseUrl}/${id}/complete`, form);
  }

  /** Cancel a booked meeting that will not take place; never a held one (closure D-73). */
  cancel(id: string, pipId: string): Promise<PipReviewMeeting> {
    return apiService.post<PipReviewMeeting>(
      `${this.baseUrl}/${id}/cancel?pipId=${encodeURIComponent(pipId)}`,
    );
  }

  getSchedule(pipId: string): Promise<PipMeetingSchedule> {
    return apiService.get<PipMeetingSchedule>(`${this.baseUrl}/schedule/${pipId}`);
  }

  /** The employee's right of reply on a meeting they attended. */
  addComment(meetingId: string, comment: string): Promise<boolean> {
    return apiService.post<boolean>(`${this.baseUrl}/${meetingId}/comment`, { comment });
  }
}

export const pipService = new PipService();
export const pipDashboardService = new PipDashboardService();
export const pipMeetingService = new PipMeetingService();
