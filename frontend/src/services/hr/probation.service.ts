import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  ProbationPeriod,
  ProbationPeriodDetail,
  ProbationPeriodSummary,
  ProbationPolicy,
  ProbationReview,
  ProbationExtension,
  ProbationStatus,
  ProbationConfirmationLetter,
  ProbationConfirmationRepairResult,
  ProbationConfirmingAuthority,
  ResolvedConfirmingAuthority,
  CreateProbationPeriod,
  CreateProbationReview,
  CreateProbationExtension,
  CreateProbationConfirmingAuthority,
  UpdateProbationConfirmingAuthority,
  SubmitProbationReview,
  AcknowledgeProbationReview,
  ApproveProbationReview,
  TerminateProbationPeriod,
  ProbationReminderRun,
  ProbationReminderRunResult,
  ProbationReminderPreviewItem,
  ProbationReminderLogEntry,
  EmployeeOathOfSecrecy,
  AffirmOathOfSecrecy,
  RecordAdministeredOath,
  OathOutstandingEmployee,
} from '@/types/hr/probation';

/**
 * Probation & confirmation. Backend route: `api/probations`.
 *
 * ⚠ **Three rungs, and the split is not the usual one.** Reading and record-keeping are
 * `HR.Probation.Read`/`Write`, but the three OUTCOME decisions — confirm, extend, terminate — and
 * deletion need `Admin`, because FR-HR-032 makes confirmation a management act ("head confirms →
 * HR issues the letter"). An HR-role user can open a probation and record its reviews, and will be
 * refused when they try to confirm it, so a screen must not offer those buttons merely because the
 * record is visible.
 *
 * ⚠ **Five endpoints are deliberately NOT permission-gated** — `reviews/mine`, `reviews/{id}/submit`,
 * `reviews/{id}/complete`, `reviews/{id}/acknowledge` and the reviewer queue. A probation review is
 * conducted by the employee's line manager, who holds no HR permission at all; entitlement for those
 * is read off the record instead. Gating them on `Write` made them reachable by nobody, which is how
 * they were first written.
 */
class ProbationService {
  private readonly baseUrl = '/probations';

  // ── Reads ──────────────────────────────────────────────────────────────────

  /** The register. Active first, then by end date. */
  getPaged(params: {
    page?: number;
    pageSize?: number;
    status?: ProbationStatus;
    employeeId?: string;
    search?: string;
  } = {}) {
    return apiService.get<PagedResult<ProbationPeriodSummary>>(this.baseUrl, params);
  }

  getById(id: string) {
    return apiService.get<ProbationPeriod>(`${this.baseUrl}/${id}`);
  }

  getWithReviews(id: string) {
    return apiService.get<ProbationPeriodDetail>(`${this.baseUrl}/${id}/with-reviews`);
  }

  /**
   * An employee's whole probation history, newest first.
   *
   * ⚠ An ARRAY. Until slice 1 the service behind this returned a single object while the endpoint
   * declared a collection — a type written from the endpoint name would have compiled and thrown.
   */
  getForEmployee(employeeId: string) {
    return apiService.get<ProbationPeriodSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getActive() {
    return apiService.get<ProbationPeriodSummary[]>(`${this.baseUrl}/active`);
  }

  getByStatus(status: ProbationStatus) {
    return apiService.get<ProbationPeriodSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getEndingWithin(daysAhead = 30) {
    return apiService.get<ProbationPeriodSummary[]>(`${this.baseUrl}/ending-within`, { daysAhead });
  }

  /** What length applies to this employee, and who will confirm it. Read this before rendering a create form. */
  getPolicyForEmployee(employeeId: string) {
    return apiService.get<ProbationPolicy>(`${this.baseUrl}/policy/${employeeId}`);
  }

  // ── Probation lifecycle ───────────────────────────────────────────────────

  /** ⚠ Omit `durationMonths` and the staff-category length is applied (FR-HR-031). */
  create(payload: CreateProbationPeriod) {
    return apiService.post<ProbationPeriod>(this.baseUrl, payload);
  }

  /** Admin only. */
  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Admin only. Returns the audit row, not a message. */
  extend(id: string, payload: CreateProbationExtension) {
    return apiService.post<ProbationExtension>(`${this.baseUrl}/${id}/extend`, payload);
  }

  getExtensions(id: string) {
    return apiService.get<ProbationExtension[]>(`${this.baseUrl}/${id}/extensions`);
  }

  /**
   * Admin only. Records the confirmation on the probation AND the employee.
   *
   * ⚠ Refused when a probation workflow definition is published for the tenant — confirmation must
   * then come through the confirming authority's approval. The screen should offer
   * {@link submitForConfirmation} instead in that case; the refusal says so.
   */
  confirm(id: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/confirm`, {});
  }

  /**
   * Confirms, by rule, the employees whose probation ended before they were entered — the imported
   * workforce (HR finish plan lane 11). Admin only. ⚠ Run with `dryRun` first: it changes hundreds
   * of records at once.
   */
  repairImportedConfirmations(dryRun: boolean) {
    const params = new URLSearchParams({ dryRun: String(dryRun) });
    return apiService.post<ProbationConfirmationRepairResult>(
      `${this.baseUrl}/repair-imported-confirmations?${params.toString()}`,
      {},
    );
  }

  /** Admin only. Records the decision and hands off — entitlements belong to the separation module. */
  terminate(id: string, payload: TerminateProbationPeriod) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/terminate`, payload);
  }

  // ── Confirmation on the workflow engine ───────────────────────────────────

  /** Refused when no confirming authority covers the employee — there would be nobody to send it to. */
  submitForConfirmation(id: string) {
    return apiService.post<ProbationPeriod>(`${this.baseUrl}/${id}/submit-for-confirmation`, {});
  }

  /** The named authority approves. Stops at ConfirmationApproved; HR then confirms. */
  approveConfirmation(id: string) {
    return apiService.post<ProbationPeriod>(`${this.baseUrl}/${id}/confirmation/approve`, {});
  }

  /** Declining returns the probation to Active — it does not end anything. */
  rejectConfirmation(id: string, notes?: string) {
    return apiService.post<ProbationPeriod>(`${this.baseUrl}/${id}/confirmation/reject`, {
      probationId: id,
      notes,
    });
  }

  recallConfirmation(id: string, notes?: string) {
    return apiService.post<ProbationPeriod>(`${this.baseUrl}/${id}/confirmation/recall`, {
      probationId: id,
      notes,
    });
  }

  /** FR-HR-032. Only for a confirmed probation; returns a self-contained HTML document. */
  getConfirmationLetter(id: string) {
    return apiService.get<ProbationConfirmationLetter>(`${this.baseUrl}/${id}/confirmation-letter`);
  }

  // ── Reviews ───────────────────────────────────────────────────────────────

  getReviews(probationId: string) {
    return apiService.get<ProbationReview[]>(`${this.baseUrl}/${probationId}/reviews`);
  }

  getReviewsByStatus(status: string) {
    return apiService.get<ProbationReview[]>(`${this.baseUrl}/reviews/status/${status}`);
  }

  getOverdueReviews() {
    return apiService.get<ProbationReview[]>(`${this.baseUrl}/reviews/overdue`);
  }

  /** A reviewer's own queue — matches the named reviewer OR the second reviewer. */
  getReviewsByReviewer(reviewerEmployeeId: string) {
    return apiService.get<ProbationReview[]>(`${this.baseUrl}/reviews/reviewer/${reviewerEmployeeId}`);
  }

  /** The reviews of the caller's own probation. Token-derived — no id to point elsewhere. */
  getMyReviews() {
    return apiService.get<ProbationReview[]>(`${this.baseUrl}/reviews/mine`);
  }

  /**
   * The reviews the caller must CONDUCT.
   *
   * ⚠ Use this, not {@link getReviewsByReviewer}. The browser has no employee id for the signed-in
   * user — the client `User` type carries roles and tenants but no employee link — so the by-id
   * endpoint cannot be called by the line managers whose queue this is.
   */
  getMyReviewerQueue() {
    return apiService.get<ProbationReview[]>(`${this.baseUrl}/reviews/to-conduct`);
  }

  /** HR schedules a review and names who will conduct it. */
  addReview(probationId: string, payload: CreateProbationReview) {
    return apiService.post<ProbationReview>(`${this.baseUrl}/${probationId}/reviews`, payload);
  }

  /** Rescheduling only — the assessment goes through {@link submitReview}. */
  updateReview(reviewId: string, payload: { id: string; scheduledDate?: string; secondReviewerId?: string }) {
    return apiService.put<ProbationReview>(`${this.baseUrl}/reviews/${reviewId}`, payload);
  }

  /** The reviewer records what they found. Only the named reviewer or second reviewer may. */
  submitReview(reviewId: string, payload: SubmitProbationReview) {
    return apiService.post<ProbationReview>(`${this.baseUrl}/reviews/${reviewId}/submit`, payload);
  }

  /** ⚠ Only the employee the review is about. Not HR, not the reviewer. */
  acknowledgeReview(reviewId: string, payload: AcknowledgeProbationReview) {
    return apiService.post<ProbationReview>(`${this.baseUrl}/reviews/${reviewId}/acknowledge`, payload);
  }

  /** HR sign-off. The approver comes from the token; the payload carries only comments. */
  hrApproveReview(reviewId: string, payload: ApproveProbationReview = {}) {
    return apiService.post<ProbationReview>(`${this.baseUrl}/reviews/${reviewId}/hr-approve`, payload);
  }

  /** Refused unless the review carries an assessment. */
  completeReview(reviewId: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/reviews/${reviewId}/complete`, {});
  }
}

/**
 * The confirming-authority map (FR-HR-032, decision D-2). Backend: `api/probations/confirming-authorities`.
 *
 * ⚠ Admin-gated throughout, reads included: this decides who may end someone's probation.
 */
class ProbationConfirmingAuthorityService {
  private readonly baseUrl = '/probations/confirming-authorities';

  getAll() {
    return apiService.get<ProbationConfirmingAuthority[]>(this.baseUrl);
  }

  getById(id: string) {
    return apiService.get<ProbationConfirmingAuthority>(`${this.baseUrl}/${id}`);
  }

  /** ⚠ `isResolved: false` is a real answer with a reason — show it rather than rendering a blank. */
  resolveForEmployee(employeeId: string) {
    return apiService.get<ResolvedConfirmingAuthority>(`${this.baseUrl}/resolve/${employeeId}`);
  }

  create(payload: CreateProbationConfirmingAuthority) {
    return apiService.post<ProbationConfirmingAuthority>(this.baseUrl, payload);
  }

  update(id: string, payload: UpdateProbationConfirmingAuthority) {
    return apiService.put<ProbationConfirmingAuthority>(`${this.baseUrl}/${id}`, payload);
  }

  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** The reminder engine (FR-HR-032 / FR-HR-140). Admin-gated, reads included. */
class ProbationReminderService {
  private readonly baseUrl = '/probations/reminders';

  run() {
    return apiService.post<ProbationReminderRunResult>(`${this.baseUrl}/run`, {});
  }

  /** `asOf` lets HR ask "what lands next month?". A preview claims nothing. */
  preview(asOf?: string) {
    return apiService.get<ProbationReminderPreviewItem[]>(`${this.baseUrl}/preview`, asOf ? { asOf } : undefined);
  }

  getRuns(count = 20) {
    return apiService.get<ProbationReminderRun[]>(`${this.baseUrl}/runs`, { count });
  }

  getLog(days = 14) {
    return apiService.get<ProbationReminderLogEntry[]>(`${this.baseUrl}/log`, { days });
  }
}

/**
 * Oaths of secrecy (FR-HR-030). Backend: `api/hr/oaths-of-secrecy`.
 *
 * ⚠ There is no "record an affirmation for someone else" call, and there must never be one. HR
 * records paper oaths via {@link recordAdministered}, which requires a witness; the employee's own
 * affirmation is {@link affirm}, which takes no employee id at all.
 */
class OathOfSecrecyService {
  private readonly baseUrl = '/hr/oaths-of-secrecy';

  getForEmployee(employeeId: string) {
    return apiService.get<EmployeeOathOfSecrecy[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getMine() {
    return apiService.get<EmployeeOathOfSecrecy[]>(`${this.baseUrl}/mine`);
  }

  /** Active employees with no oath on record. */
  getOutstanding() {
    return apiService.get<OathOutstandingEmployee[]>(`${this.baseUrl}/outstanding`);
  }

  affirm(payload: AffirmOathOfSecrecy = {}) {
    return apiService.post<EmployeeOathOfSecrecy>(`${this.baseUrl}/affirm`, payload);
  }

  recordAdministered(payload: RecordAdministeredOath) {
    return apiService.post<EmployeeOathOfSecrecy>(`${this.baseUrl}/administered`, payload);
  }

  /**
   * Attaches the signed scan.
   *
   * ⚠ Multipart, and the ONLY route by which a file reaches an oath — the JSON writes carry no path
   * and no upload id. The upload runs the virus-scan gate and registers the document centrally.
   */
  attachScan(oathId: string, file: File, description?: string) {
    const form = new FormData();
    form.append('file', file);
    if (description) form.append('description', description);
    return apiService.post<EmployeeOathOfSecrecy>(`${this.baseUrl}/${oathId}/scan`, form);
  }

  scanDownloadUrl(oathId: string) {
    return `${this.baseUrl}/${oathId}/scan`;
  }
}

export const probationService = new ProbationService();
export const probationConfirmingAuthorityService = new ProbationConfirmingAuthorityService();
export const probationReminderService = new ProbationReminderService();
export const oathOfSecrecyService = new OathOfSecrecyService();
