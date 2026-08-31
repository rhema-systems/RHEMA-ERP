import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  SeparationListItem,
  SeparationDetail,
  SeparationQuery,
  CreateSeparation,
  UpdateSeparation,
  SubmitSeparation,
  ApproveSeparation,
  RecordNoticeDecision,
  RejectSeparation,
  CancelSeparation,
  SeparationDocument,
  SeparationDocumentCategory,
  ClearanceTemplate,
  CreateClearanceTemplate,
  UpdateClearanceTemplate,
  SeparationClearance,
  ClearanceItem,
  RecordClearanceItem,
  SeparationSettlement,
  SettlementLine,
  AddSettlementLine,
  UpdateSettlementLine,
  FinaliseSettlement,
  ReviewSettlement,
  UpcomingRetirement,
  UpcomingContractExpiry,
  SweepResult,
  SeparationReminderItem,
  SeparationExitInterview,
  RecordExitInterviewPayload,
  SeparationAnalytics,
  ExitInterviewThemes,
  SeparationReminderRunResult,
  DisciplinaryOrphanRepair,
} from '@/types/hr/separation';

/**
 * Separation, clearance & exit. Backend route: `api/hr/separations`.
 *
 * ⚠ **Three gates a screen must respect, and none of them is a permission on the caller alone.**
 *
 * 1. **FR-HR-092 — who signs.** `approve` and `reject` carry a plain `[Authorize]`; the service
 *    decides. The Managing Director may sign any separation, HR only a *procedural* one (a
 *    termination for absence beyond the tenant's threshold). `requiresManagingDirectorSignature`
 *    on the detail says which — route the record rather than letting somebody find out by being
 *    refused. A tenant administrator holds every permission this area defines and still cannot sign.
 *
 * 2. **FR-HR-185 — who passes the money.** The settlement review is `TDC_INTERNAL_AUDIT` only.
 *    HR *and the MD* are both refused. ⚠ Nobody holds that role on the reference tenant, so on live
 *    data every finalised settlement will sit unreviewed — the control holding, not a bug.
 *
 * 3. **FR-HR-091 — clearance before money.** A settlement cannot be prepared until clearance is
 *    complete, and cannot be finalised while any line reads `CannotCompute`.
 *
 * ⚠ **Null amounts are not zeros.** A settlement line the system could not value carries `null`
 * with `computation: 'CannotCompute'`. Render it as "not computed", never as 0.00 — a zero on a
 * statement somebody signs is a claim about money that nobody checked.
 */
class SeparationService {
  private readonly baseUrl = '/hr/separations';

  // ── The register ───────────────────────────────────────────────────────────

  getPaged(query: SeparationQuery = {}) {
    // An interface (as opposed to a type alias) has no index signature, so it is not assignable to
    // Record<string, unknown>. Spreading gives the object literal type the call wants without
    // widening SeparationQuery itself, which is worth keeping strict — it is the filter contract.
    return apiService.get<PagedResult<SeparationListItem>>(this.baseUrl, { ...query });
  }

  getById(id: string) {
    return apiService.get<SeparationDetail>(`${this.baseUrl}/${id}`);
  }

  /** Every separation on record for one employee, newest first. An ARRAY, not a single record. */
  getForEmployee(employeeId: string) {
    return apiService.get<SeparationListItem[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  create(payload: CreateSeparation) {
    return apiService.post<SeparationDetail>(this.baseUrl, payload);
  }

  update(id: string, payload: UpdateSeparation) {
    return apiService.put<SeparationDetail>(`${this.baseUrl}/${id}`, payload);
  }

  /** Draft → PendingApproval. Derives the last working day from the notice where it can. */
  submit(id: string, payload: SubmitSeparation = {}) {
    return apiService.post<SeparationDetail>(`${this.baseUrl}/${id}/submit`, payload);
  }

  cancel(id: string, payload: CancelSeparation) {
    return apiService.post<SeparationDetail>(`${this.baseUrl}/${id}/cancel`, payload);
  }

  /** Administration: a completed separation is refused outright. */
  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── FR-HR-092: the decision ────────────────────────────────────────────────

  approve(id: string, payload: ApproveSeparation = {}) {
    return apiService.post<SeparationDetail>(`${this.baseUrl}/${id}/approve`, payload);
  }

  /**
   * Record what happens to unserved notice. Same entitlement as approving — the service reads it
   * off the record — and an empty payload means "neither applies", which is a real answer.
   */
  recordNoticeDecision(id: string, payload: RecordNoticeDecision = {}) {
    return apiService.post<SeparationDetail>(`${this.baseUrl}/${id}/notice-decision`, payload);
  }

  reject(id: string, payload: RejectSeparation) {
    return apiService.post<SeparationDetail>(`${this.baseUrl}/${id}/reject`, payload);
  }

  /** From SettlementApproved only — the employee record follows the payment. */
  complete(id: string) {
    return apiService.post<SeparationDetail>(`${this.baseUrl}/${id}/complete`, {});
  }

  // ── Documents ──────────────────────────────────────────────────────────────

  getDocuments(id: string) {
    return apiService.get<SeparationDocument[]>(`${this.baseUrl}/${id}/documents`);
  }

  /**
   * Attach a file. Multipart — the browser sets the boundary itself, so do not set Content-Type.
   *
   * ⚠ A medical retirement cannot be SUBMITTED without a `MedicalReport`, and a death without a
   * `DeathCertificate`. The category is what makes that work, so a picker that defaults everything
   * to `Other` silently blocks those two routes.
   */
  attachDocument(id: string, file: File, category: SeparationDocumentCategory, description?: string) {
    const form = new FormData();
    form.append('file', file);
    form.append('category', category);
    if (description) form.append('description', description);
    // apiService.post detects FormData and leaves Content-Type to the browser, so the multipart
    // boundary is set correctly. There is no separate postForm.
    return apiService.post<SeparationDocument>(`${this.baseUrl}/${id}/documents`, form);
  }

  /** Only while the separation is a draft — afterwards the attachments are part of the record. */
  deleteDocument(documentId: string) {
    return apiService.delete<void>(`${this.baseUrl}/documents/${documentId}`);
  }

  downloadDocumentUrl(documentId: string) {
    return `${this.baseUrl}/documents/${documentId}/download`;
  }

  // ── Clearance (FR-HR-091 / FR-HR-183) ──────────────────────────────────────

  getClearanceTemplates(includeInactive = false) {
    return apiService.get<ClearanceTemplate[]>(`${this.baseUrl}/clearance-templates`, { includeInactive });
  }

  createClearanceTemplate(payload: CreateClearanceTemplate) {
    return apiService.post<ClearanceTemplate>(`${this.baseUrl}/clearance-templates`, payload);
  }

  updateClearanceTemplate(id: string, payload: UpdateClearanceTemplate) {
    return apiService.put<ClearanceTemplate>(`${this.baseUrl}/clearance-templates/${id}`, payload);
  }

  deleteClearanceTemplate(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/clearance-templates/${id}`);
  }

  /** FR-HR-183's seven defaults. Safe to run twice — it skips what already exists by name. */
  seedDefaultClearanceTemplates() {
    return apiService.post<ClearanceTemplate[]>(`${this.baseUrl}/clearance-templates/seed-defaults`, {});
  }

  /** Refused before the separation is approved, and where no active templates exist. */
  startClearance(id: string) {
    return apiService.post<SeparationClearance>(`${this.baseUrl}/${id}/clearance/start`, {});
  }

  getClearance(id: string) {
    return apiService.get<SeparationClearance>(`${this.baseUrl}/${id}/clearance`);
  }

  recordClearanceItem(itemId: string, payload: RecordClearanceItem) {
    return apiService.post<ClearanceItem>(`${this.baseUrl}/clearance-items/${itemId}`, payload);
  }

  /**
   * Re-reads the HR Assets register onto an in-progress clearance form (FR-HR-183).
   *
   * Adds a line for anything issued to the leaver since the form was drawn, and reprices the lines
   * nobody has answered yet. ⚠ **Answered lines are never touched** — proven by
   * `hr-separation/probe-lane3-refresh-assets.mjs`, which answers a line, refreshes, and asserts
   * both its status and its note survive. Running it twice adds nothing, so it is safe to offer as
   * a plain button.
   */
  refreshClearanceAssets(id: string) {
    return apiService.post<SeparationClearance>(`${this.baseUrl}/${id}/clearance/refresh-assets`, {});
  }

  /** FR-HR-091's gate. Refused while any mandatory line is pending or blocked. */
  completeClearance(id: string) {
    return apiService.post<SeparationDetail>(`${this.baseUrl}/${id}/clearance/complete`, {});
  }

  // ── Settlement (FR-HR-184) ─────────────────────────────────────────────────

  prepareSettlement(id: string) {
    return apiService.post<SeparationSettlement>(`${this.baseUrl}/${id}/settlement/prepare`, {});
  }

  getSettlement(id: string) {
    return apiService.get<SeparationSettlement>(`${this.baseUrl}/${id}/settlement`);
  }

  addSettlementLine(id: string, payload: AddSettlementLine) {
    return apiService.post<SettlementLine>(`${this.baseUrl}/${id}/settlement/lines`, payload);
  }

  /** Supplying an amount requires a source reference — Internal Audit has to be able to check it. */
  updateSettlementLine(lineId: string, payload: UpdateSettlementLine) {
    return apiService.put<SettlementLine>(`${this.baseUrl}/settlement-lines/${lineId}`, payload);
  }

  deleteSettlementLine(lineId: string) {
    return apiService.delete<void>(`${this.baseUrl}/settlement-lines/${lineId}`);
  }

  finaliseSettlement(id: string, payload: FinaliseSettlement = {}) {
    return apiService.post<SeparationSettlement>(`${this.baseUrl}/${id}/settlement/finalise`, payload);
  }

  // ── FR-HR-185: Internal Audit only ─────────────────────────────────────────

  approveSettlementReview(id: string, payload: ReviewSettlement = {}) {
    return apiService.post<SeparationSettlement>(`${this.baseUrl}/${id}/settlement/review/approve`, payload);
  }

  /** Findings are required — a control that refuses without saying why cannot be acted on. */
  returnSettlement(id: string, payload: ReviewSettlement) {
    return apiService.post<SeparationSettlement>(`${this.baseUrl}/${id}/settlement/review/return`, payload);
  }

  // ── Queues and sweeps ──────────────────────────────────────────────────────

  /**
   * ⚠ Empty on live data, and correctly so — employee ages run 34 to 48, so nobody is within twelve
   * years of retiring. An empty queue is a fact about the data, not a broken call.
   */
  getUpcomingRetirements(withinDays?: number, includeOverdue = true) {
    return apiService.get<UpcomingRetirement[]>(`${this.baseUrl}/retirements/upcoming`, {
      withinDays, includeOverdue,
    });
  }

  /**
   * ⚠ **This RAISES separations — it is not a preview.** One per person due within the horizon who
   * does not already have one. Read `getUpcomingRetirements` first if you want to know what it
   * would do.
   */
  runRetirementSweep(withinDays?: number) {
    // ⚠ Query params go in the URL: apiService.post takes (endpoint, data) only — there is no
    // third params argument. Assuming one is the same class of mistake as inventing a DTO field.
    // ⚠ The PATH is kept as its own literal and the query appended after, purely so the route reads
    // as itself. ⚠ **This does NOT make it visible to instrument 01** — tried on 2026-08-31 and it
    // changed nothing, because that matcher requires the FIRST ARGUMENT to be a literal and a
    // ternary makes it an identifier. Any computed URL is invisible to it, which is the same blind
    // spot that reads EmployeesController as 73/81 unwired. Recorded as a FALSE disposition in the
    // ledger instead; do not "fix" the call site again for coverage's sake.
    const path = `${this.baseUrl}/retirements/sweep`;
    return apiService.post<SweepResult>(
      withinDays == null ? path : `${path}?withinDays=${withinDays}`, {});
  }

  /** ⚠ Also empty on live data: 0 of 202 active contracts carry an end date. */
  getUpcomingContractExpiries(withinDays?: number, includeOverdue = true) {
    return apiService.get<UpcomingContractExpiry[]>(`${this.baseUrl}/contract-expiries/upcoming`, {
      withinDays, includeOverdue,
    });
  }

  /** ⚠ Raises separations, as the retirement sweep does. Not a preview. */
  runContractExpirySweep(withinDays?: number) {
    const path = `${this.baseUrl}/contract-expiries/sweep`;
    return apiService.post<SweepResult>(
      withinDays == null ? path : `${path}?withinDays=${withinDays}`, {});
  }

  // ── Reminders (FR-HR-111) ──────────────────────────────────────────────────

  previewReminders() {
    return apiService.get<SeparationReminderItem[]>(`${this.baseUrl}/reminders/preview`);
  }

  runReminderSweep() {
    return apiService.post<SeparationReminderRunResult>(`${this.baseUrl}/reminders/run`, {});
  }

  // ── Exit interview ─────────────────────────────────────────────────────────

  /**
   * ⚠ Answers **204 No Content** when no interview has been recorded, so this resolves to `null`,
   * not a throw. A 404 would say the separation was missing, which is a different and considerably
   * more alarming thing than "nobody has done the interview yet".
   */
  async getExitInterview(separationId: string): Promise<SeparationExitInterview | null> {
    const result = await apiService.get<SeparationExitInterview | null>(
      `${this.baseUrl}/${separationId}/exit-interview`);

    // ⚠ A 204 carries no content-type, so apiService falls through to `response.text()` and hands
    // back an empty STRING — which `?? null` does not catch, because '' is not nullish. Without
    // this the screen would receive '' and render an interview that does not exist.
    return result && typeof result === 'object' ? result : null;
  }

  /**
   * Records the interview, or records that it was declined. One per separation — calling this again
   * amends the existing record rather than adding a second one.
   *
   * ⚠ Sending `wasDeclined: true` CLEARS every answer server-side. That is deliberate: a
   * part-filled form later marked declined must not leave ratings behind to be averaged as though a
   * real interview had produced them. A screen should warn before doing it.
   */
  recordExitInterview(separationId: string, payload: RecordExitInterviewPayload) {
    return apiService.put<SeparationExitInterview>(
      `${this.baseUrl}/${separationId}/exit-interview`, payload);
  }

  // ── Analytics ──────────────────────────────────────────────────────────────

  /** Defaults to the twelve months ending today; both bounds are inclusive. */
  getAnalytics(from?: string, to?: string) {
    return apiService.get<SeparationAnalytics>(`${this.baseUrl}/analytics`, { from, to });
  }

  getExitInterviewThemes(from?: string, to?: string) {
    return apiService.get<ExitInterviewThemes>(`${this.baseUrl}/analytics/exit-interviews`, {
      from, to,
    });
  }

  // ── Repair ─────────────────────────────────────────────────────────────────

  /**
   * Disciplinary decisions that never produced an exit. Defaults to a dry run, and a screen should
   * keep it that way until somebody has read the report — it writes across many records at once.
   */
  repairDisciplinaryOrphans(dryRun = true) {
    return apiService.post<DisciplinaryOrphanRepair>(
      `${this.baseUrl}/repair/disciplinary-orphans?dryRun=${dryRun}`, {});
  }
}

export const separationService = new SeparationService();
