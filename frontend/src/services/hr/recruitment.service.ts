import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type {
  CreateJobVacancy,
  CreateStaffRequisition,
  HrPagedResult,
  JobPosting,
  JobPostingForm,
  JobPostingStatus,
  JobPostingSummary,
  JobVacancy,
  JobVacancyStatus,
  JobVacancyStatusHistoryEntry,
  JobVacancySummary,
  PositionEstablishment,
  PositionVacancyStats,
  PositionVacancySummary,
  RaiseRequisitionResult,
  RequisitionAttachment,
  RequisitionBudgetCheck,
  RequisitionComment,
  RequisitionCost,
  RequisitionCostForm,
  RequisitionHistoryEntry,
  ShortlistingCriteria,
  ShortlistingCriteriaForm,
  StaffRequisition,
  StaffRequisitionStatus,
  StaffRequisitionStatusSummary,
  StaffRequisitionSummary,
  UpdateJobVacancy,
  UpdateStaffRequisition,
  VacancyAttachment,
  VacancyClosureReason,
} from '@/types/hr/recruitment';

/**
 * api/StaffRequisitions — headcount requisitions.
 *
 * **Approval runs on the workflow engine.** `submit`, `approve`, `reject` and `recall` drive it;
 * the screen must never set a status itself — refetch and let the adapter decide. Reuse
 * `WorkflowApprovalActions` / `WorkflowRecordTab` rather than building buttons.
 *
 * ⚠ Submit and approve are inoperable until a `StaffRequisition` workflow definition has been
 * published and `POST api/Workflow/entity-types/seed` re-run.
 *
 * **Write endpoints are role-gated**, reads are not: cancelling, holding, fulfilling, deleting and
 * costing are HR's; editing, submitting and recalling belong to the requester (or HR). Approving
 * carries no role gate at all — authority comes from the published definition.
 */
class StaffRequisitionService {
  private readonly baseUrl = '/StaffRequisitions';

  // ── reads ────────────────────────────────────────────────────────────────

  getPaged(pageNumber = 1, pageSize = 20): Promise<HrPagedResult<StaffRequisitionSummary>> {
    return apiService.get<HrPagedResult<StaffRequisitionSummary>>(this.baseUrl, { pageNumber, pageSize });
  }

  getAll(): Promise<StaffRequisitionSummary[]> {
    return apiService.get<StaffRequisitionSummary[]>(`${this.baseUrl}/all`);
  }

  getById(id: string): Promise<StaffRequisition> {
    return apiService.get<StaffRequisition>(`${this.baseUrl}/${id}`);
  }

  getStatusSummary(): Promise<StaffRequisitionStatusSummary> {
    return apiService.get<StaffRequisitionStatusSummary>(`${this.baseUrl}/summary`);
  }

  getByStatus(status: StaffRequisitionStatus): Promise<StaffRequisitionSummary[]> {
    return apiService.get<StaffRequisitionSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getPendingReview(): Promise<StaffRequisitionSummary[]> {
    return apiService.get<StaffRequisitionSummary[]>(`${this.baseUrl}/pending-review`);
  }

  getOpen(): Promise<StaffRequisitionSummary[]> {
    return apiService.get<StaffRequisitionSummary[]>(`${this.baseUrl}/open`);
  }

  getOverdue(): Promise<StaffRequisitionSummary[]> {
    return apiService.get<StaffRequisitionSummary[]>(`${this.baseUrl}/overdue`);
  }

  /**
   * Compares the requisition against its position's approved manpower budget for the fiscal year.
   * Worth showing before submit: in Block mode an over-budget requisition simply cannot be sent.
   */
  checkBudget(id: string): Promise<RequisitionBudgetCheck> {
    return apiService.get<RequisitionBudgetCheck>(`${this.baseUrl}/${id}/budget-check`);
  }

  // ── writes ───────────────────────────────────────────────────────────────

  create(payload: CreateStaffRequisition): Promise<StaffRequisition> {
    return apiService.post<StaffRequisition>(this.baseUrl, payload);
  }

  update(id: string, payload: UpdateStaffRequisition): Promise<StaffRequisition> {
    return apiService.put<StaffRequisition>(`${this.baseUrl}/${id}`, payload);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── workflow ─────────────────────────────────────────────────────────────
  // Each returns the requisition as it ended up: with a multi-step definition an approval step is
  // not necessarily the last one, so the resulting status is the engine's answer, not ours.

  submit(id: string, notes?: string | null): Promise<StaffRequisition> {
    return apiService.post<StaffRequisition>(`${this.baseUrl}/${id}/submit`, { requisitionId: id, notes: notes ?? null });
  }

  approve(id: string, comments?: string | null): Promise<StaffRequisition> {
    return apiService.post<StaffRequisition>(`${this.baseUrl}/${id}/approve`, { requisitionId: id, comments: comments ?? null });
  }

  reject(id: string, comments?: string | null): Promise<StaffRequisition> {
    return apiService.post<StaffRequisition>(`${this.baseUrl}/${id}/reject`, { requisitionId: id, comments: comments ?? null });
  }

  /** Withdraws a requisition awaiting approval back to Draft. Only the requester may. */
  recall(id: string, reason?: string | null): Promise<StaffRequisition> {
    return apiService.post<StaffRequisition>(`${this.baseUrl}/${id}/recall`, { reason: reason ?? null });
  }

  hold(id: string, reason: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/hold`, { requisitionId: id, reason });
  }

  cancel(id: string, cancellationReason: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/cancel`, { requisitionId: id, cancellationReason });
  }

  fulfill(id: string, positionsFilled: number, fulfilledDate?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/fulfill`, {
      requisitionId: id,
      positionsFilled,
      fulfilledDate: fulfilledDate ?? null,
    });
  }

  linkToVacancy(id: string, jobVacancyId: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/link-vacancy`, { requisitionId: id, jobVacancyId });
  }

  // ── costs ────────────────────────────────────────────────────────────────

  getCosts(id: string): Promise<RequisitionCost[]> {
    return apiService.get<RequisitionCost[]>(`${this.baseUrl}/${id}/costs`);
  }

  getTotalCost(id: string): Promise<number> {
    return apiService.get<number>(`${this.baseUrl}/${id}/costs/total`);
  }

  addCost(id: string, payload: RequisitionCostForm): Promise<RequisitionCost> {
    return apiService.post<RequisitionCost>(`${this.baseUrl}/${id}/costs`, { ...payload, requisitionId: id });
  }

  updateCost(costId: string, payload: RequisitionCostForm): Promise<RequisitionCost> {
    return apiService.put<RequisitionCost>(`${this.baseUrl}/costs/${costId}`, { ...payload, id: costId });
  }

  deleteCost(costId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/costs/${costId}`);
  }

  // ── comments ─────────────────────────────────────────────────────────────

  /** Threaded: top-level comments only, each carrying its own `replies`. */
  getComments(id: string): Promise<RequisitionComment[]> {
    return apiService.get<RequisitionComment[]>(`${this.baseUrl}/${id}/comments`);
  }

  addComment(id: string, body: string, parentCommentId?: string | null): Promise<RequisitionComment> {
    return apiService.post<RequisitionComment>(`${this.baseUrl}/${id}/comments`, {
      requisitionId: id,
      body,
      parentCommentId: parentCommentId ?? null,
    });
  }

  updateComment(commentId: string, body: string): Promise<RequisitionComment> {
    return apiService.put<RequisitionComment>(`${this.baseUrl}/comments/${commentId}`, { id: commentId, body });
  }

  deleteComment(commentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/comments/${commentId}`);
  }

  // ── history ──────────────────────────────────────────────────────────────

  getHistory(id: string): Promise<RequisitionHistoryEntry[]> {
    return apiService.get<RequisitionHistoryEntry[]>(`${this.baseUrl}/${id}/history`);
  }

  // ── attachments ──────────────────────────────────────────────────────────
  // ⚠ Multipart through the controlled-upload gate — never a JSON filePath. The old JSON endpoint
  // stored no file and recorded whatever path the caller posted; it is gone.

  getAttachments(id: string): Promise<RequisitionAttachment[]> {
    return apiService.get<RequisitionAttachment[]>(`${this.baseUrl}/${id}/attachments`);
  }

  uploadAttachment(id: string, file: File, description: string | null): Promise<RequisitionAttachment> {
    return hrDocumentService.upload<RequisitionAttachment>(
      `${this.baseUrl}/${id}/attachments`,
      file,
      { description },
    );
  }

  /** Streamed by an endpoint that checks entitlement per request — never a public URL. */
  downloadAttachment(id: string, attachmentId: string): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/${id}/attachments/${attachmentId}/download`);
  }

  deleteAttachment(attachmentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/attachments/${attachmentId}`);
  }
}

/**
 * api/job-vacancies — the advertised role a requisition becomes.
 *
 * **Vacancy status is deliberately NOT on the workflow engine** (it has many writers), but it does
 * have a real state machine server-side: both `transition` and `changeStatus` refuse an illegal
 * move with a 422 whose message names the legal next states. Surface that message rather than
 * guessing the rules here.
 */
class JobVacancyService {
  private readonly baseUrl = '/job-vacancies';

  getPaged(pageNumber = 1, pageSize = 20): Promise<HrPagedResult<JobVacancySummary>> {
    return apiService.get<HrPagedResult<JobVacancySummary>>(this.baseUrl, { pageNumber, pageSize });
  }

  getAll(): Promise<JobVacancySummary[]> {
    return apiService.get<JobVacancySummary[]>(`${this.baseUrl}/all`);
  }

  getById(id: string): Promise<JobVacancy> {
    return apiService.get<JobVacancy>(`${this.baseUrl}/${id}`);
  }

  getByStatus(status: JobVacancyStatus): Promise<JobVacancySummary[]> {
    return apiService.get<JobVacancySummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getActive(): Promise<JobVacancySummary[]> {
    return apiService.get<JobVacancySummary[]>(`${this.baseUrl}/active`);
  }

  getByRequisition(requisitionId: string): Promise<JobVacancySummary[]> {
    return apiService.get<JobVacancySummary[]>(`${this.baseUrl}/requisition/${requisitionId}`);
  }

  getDeadlineApproaching(daysAhead = 7): Promise<JobVacancySummary[]> {
    return apiService.get<JobVacancySummary[]>(`${this.baseUrl}/deadline-approaching`, { daysAhead });
  }

  create(payload: CreateJobVacancy): Promise<JobVacancy> {
    return apiService.post<JobVacancy>(this.baseUrl, payload);
  }

  update(id: string, payload: UpdateJobVacancy): Promise<JobVacancy> {
    return apiService.put<JobVacancy>(`${this.baseUrl}/${id}`, payload);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /**
   * Applies field edits and a status move in one transaction. Preferred over `changeStatus` from an
   * edit form — a single unit of work removes the partial-failure window between the two.
   */
  transition(id: string, payload: UpdateJobVacancy & { newStatus: JobVacancyStatus; reason?: string | null; comments?: string | null }): Promise<JobVacancy> {
    return apiService.post<JobVacancy>(`${this.baseUrl}/${id}/transition`, payload);
  }

  /** Status only, for the buttons on a detail page. */
  changeStatus(id: string, newStatus: JobVacancyStatus, reason?: string | null, comments?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/change-status`, {
      vacancyId: id, newStatus, reason: reason ?? null, comments: comments ?? null,
    });
  }

  /** Cancels the vacancy outright. Terminal — it cannot be revived afterwards. */
  close(id: string, closureReason: VacancyClosureReason, closureNotes?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/close`, { vacancyId: id, closureReason, closureNotes: closureNotes ?? null });
  }

  /** Stops accepting applications but keeps the vacancy alive for shortlisting. */
  closeForApplications(id: string, reason: VacancyClosureReason, notes?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/close-for-applications`, { vacancyId: id, reason, notes: notes ?? null });
  }

  getStatusHistory(id: string): Promise<JobVacancyStatusHistoryEntry[]> {
    return apiService.get<JobVacancyStatusHistoryEntry[]>(`${this.baseUrl}/${id}/status-history`);
  }

  // ── shortlisting criteria ────────────────────────────────────────────────

  getCriteria(vacancyId: string): Promise<ShortlistingCriteria[]> {
    return apiService.get<ShortlistingCriteria[]>(`${this.baseUrl}/${vacancyId}/criteria`);
  }

  addCriteria(vacancyId: string, payload: ShortlistingCriteriaForm): Promise<ShortlistingCriteria> {
    return apiService.post<ShortlistingCriteria>(`${this.baseUrl}/${vacancyId}/criteria`, { ...payload, jobVacancyId: vacancyId });
  }

  updateCriteria(criteriaId: string, payload: ShortlistingCriteriaForm): Promise<ShortlistingCriteria> {
    return apiService.put<ShortlistingCriteria>(`${this.baseUrl}/criteria/${criteriaId}`, { ...payload, id: criteriaId });
  }

  deleteCriteria(criteriaId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/criteria/${criteriaId}`);
  }

  // ── attachments ──────────────────────────────────────────────────────────
  // ⚠ Multipart through the controlled-upload gate — never a JSON filePath.

  getAttachments(vacancyId: string): Promise<VacancyAttachment[]> {
    return apiService.get<VacancyAttachment[]>(`${this.baseUrl}/${vacancyId}/attachments`);
  }

  uploadAttachment(vacancyId: string, file: File, description: string | null): Promise<VacancyAttachment> {
    return hrDocumentService.upload<VacancyAttachment>(
      `${this.baseUrl}/${vacancyId}/attachments`,
      file,
      { description },
    );
  }

  downloadAttachment(vacancyId: string, attachmentId: string): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/${vacancyId}/attachments/${attachmentId}/download`);
  }

  deleteAttachment(attachmentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/attachments/${attachmentId}`);
  }
}

/**
 * api/job-postings — a vacancy's advert on one channel.
 *
 * ⚠ Publishing the *vacancy* auto-creates internal and/or external postings from its audience
 * flags, and closing or unpublishing it expires them again. Postings created here by hand are the
 * extra channels — an agency, a newspaper — and each still refuses to publish unless the vacancy
 * itself is Published.
 */
class JobPostingService {
  private readonly baseUrl = '/job-postings';

  getById(id: string): Promise<JobPosting> {
    return apiService.get<JobPosting>(`${this.baseUrl}/${id}`);
  }

  getByVacancy(vacancyId: string): Promise<JobPostingSummary[]> {
    return apiService.get<JobPostingSummary[]>(`${this.baseUrl}/vacancy/${vacancyId}`);
  }

  getActive(): Promise<JobPostingSummary[]> {
    return apiService.get<JobPostingSummary[]>(`${this.baseUrl}/active`);
  }

  getByStatus(status: JobPostingStatus): Promise<JobPostingSummary[]> {
    return apiService.get<JobPostingSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getExpiredActive(): Promise<JobPostingSummary[]> {
    return apiService.get<JobPostingSummary[]>(`${this.baseUrl}/expired-active`);
  }

  create(vacancyId: string, payload: JobPostingForm): Promise<JobPosting> {
    return apiService.post<JobPosting>(this.baseUrl, { ...payload, jobVacancyId: vacancyId });
  }

  update(id: string, payload: JobPostingForm): Promise<JobPosting> {
    return apiService.put<JobPosting>(`${this.baseUrl}/${id}`, { ...payload, id });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  publish(id: string, actualPublishDate?: string | null): Promise<JobPosting> {
    return apiService.post<JobPosting>(`${this.baseUrl}/${id}/publish`, { actualPublishDate: actualPublishDate ?? null });
  }

  expire(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/expire`, {});
  }
}

/**
 * api/position-vacancies — establishment vs. actual headcount, and the vacancies that gap implies.
 *
 * This is the front of the funnel: `reconcile` opens a vacancy wherever a position is below
 * establishment, and `raiseRequisition` turns one into a draft requisition.
 */
class PositionVacancyService {
  private readonly baseUrl = '/position-vacancies';

  getEstablishment(organizationUnitId?: string | null, onlyVacant = false): Promise<PositionEstablishment[]> {
    return apiService.get<PositionEstablishment[]>(`${this.baseUrl}/establishment`, {
      organizationUnitId: organizationUnitId ?? undefined,
      onlyVacant,
    });
  }

  getVacancies(params: {
    status?: string;
    organizationUnitId?: string;
    reason?: string;
    classification?: string;
    includeClosed?: boolean;
  } = {}): Promise<PositionVacancySummary[]> {
    return apiService.get<PositionVacancySummary[]>(this.baseUrl, params);
  }

  getStats(): Promise<PositionVacancyStats> {
    return apiService.get<PositionVacancyStats>(`${this.baseUrl}/stats`);
  }

  raiseRequisition(vacancyId: string, payload: {
    requisitionTitle?: string | null;
    numberOfPositions?: number | null;
    priority?: string | null;
    desiredStartDate?: string | null;
    businessJustification?: string | null;
  }): Promise<RaiseRequisitionResult> {
    return apiService.post<RaiseRequisitionResult>(`${this.baseUrl}/${vacancyId}/raise-requisition`, payload);
  }

  reconcile(): Promise<{ opened: number; closed: number; scanned: number }> {
    return apiService.post<{ opened: number; closed: number; scanned: number }>(`${this.baseUrl}/reconcile`, {});
  }
}

export const staffRequisitionService = new StaffRequisitionService();
export const jobVacancyService = new JobVacancyService();
export const jobPostingService = new JobPostingService();
export const positionVacancyService = new PositionVacancyService();
