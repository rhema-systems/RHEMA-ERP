import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  LeaveRequest,
  CreateLeaveRequest,
  ApproveLeaveRequest,
  RejectLeaveRequest,
  CloseLeaveRequest,
  LeaveBalance,
  LeaveRelieverClash,
  LeaveBalanceDetail,
  RecalculateLeaveBalanceRequest,
  LeaveAdjustment,
  CreateLeaveAdjustmentRequest,
  UpdateLeaveAdjustmentRequest,
  MandatoryLeaveCompliance,
  LeaveRequestAttachment,
  LeaveEvidenceKind,
  LeavePlan,
  CreateLeavePlanRequest,
  SuggestLeavePlanChangesRequest,
  RespondToLeaveSuggestionRequest,
  UpdateLeavePlanRelieversRequest,
  SuggestLeaveRequestChanges,
  RecallLeaveRequest,
  ReportResumptionRequest,
  RescheduleLeaveRequest,
  LeaveCalendarScope,
  LeaveCalendarData,
  LeaveRegisterFilter,
  BulkLeaveDecision,
  HrBulkActionResult,
  LeaveEncashment,
  CreateLeaveEncashmentRequest,
  ProcessLeaveEncashmentRequest,
  LeaveYearEndResult,
  LeaveBulkRecalculationResult,
  LeaveEntitlementRepairResult,
  LeaveAccrualStatement,
  LeaveOwedReport,
  LeaveYearInfo,
  LeaveExcessPreview,
} from '@/types/hr/leave-request';

/**
 * Leave operations. Backend route: api/Leaves — note the capital L and no `/hr` prefix.
 *
 * Approvals are workflow-driven: `submit` starts a workflow instance and `approve` /
 * `reject` process the current step. The status the row lands on is decided by the engine
 * and applied by LeaveRequestWorkflowStatusAdapter, so callers should refetch rather than
 * assume a resulting status.
 */
class LeaveService {
  private readonly baseUrl = '/Leaves';

  // ── Requests ──────────────────────────────────────────────────────────────────

  getById(id: string): Promise<LeaveRequest> {
    return apiService.get<LeaveRequest>(`${this.baseUrl}/${id}`);
  }

  getByNumber(requestNumber: string): Promise<LeaveRequest> {
    return apiService.get<LeaveRequest>(`${this.baseUrl}/by-number/${requestNumber}`);
  }

  /**
   * What these dates cost the leave type, and whether the days beyond its limit could be charged to
   * annual leave (round 5, lane H). The request forms read it before anything is saved.
   */
  getExcessPreview(params: {
    employeeId: string;
    leaveTypeId: string;
    startDate: string;
    endDate: string;
    leaveSubTypeId?: string | null;
  }): Promise<LeaveExcessPreview> {
    return apiService.get<LeaveExcessPreview>(`${this.baseUrl}/excess-preview`, {
      employeeId: params.employeeId,
      leaveTypeId: params.leaveTypeId,
      startDate: params.startDate,
      endDate: params.endDate,
      leaveSubTypeId: params.leaveSubTypeId || undefined,
    });
  }

  /**
   * One employee's requests for a year. `status` is filtered server-side so the paging and the
   * count agree with it — filtering the fetched page in the browser under-reported silently
   * (closure plan L-7).
   */
  getEmployeeHistory(
    employeeId: string,
    year = 0,
    pageNumber = 1,
    pageSize = 20,
    status?: string,
  ): Promise<PagedResult<LeaveRequest>> {
    return apiService.get<PagedResult<LeaveRequest>>(
      `${this.baseUrl}/employee/${employeeId}/history`,
      { year, pageNumber, pageSize, ...(status ? { status } : {}) },
    );
  }

  /** Requests awaiting this manager's decision. */
  getPendingApprovals(
    managerId: string,
    pageNumber = 1,
    pageSize = 20,
  ): Promise<PagedResult<LeaveRequest>> {
    return apiService.get<PagedResult<LeaveRequest>>(
      `${this.baseUrl}/pending-approvals/${managerId}`,
      { pageNumber, pageSize },
    );
  }

  create(data: CreateLeaveRequest): Promise<LeaveRequest> {
    return apiService.post<LeaveRequest>(this.baseUrl, data);
  }

  /** Only permitted while the request is still a draft. */
  updateDraft(id: string, data: CreateLeaveRequest): Promise<LeaveRequest> {
    return apiService.put<LeaveRequest>(`${this.baseUrl}/${id}/draft`, data);
  }

  /** Starts the approval workflow. */
  submit(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/submit`);
  }

  /**
   * Send a submitted request back to the employee with dates of your own — the third decision verb
   * beside approve and reject. Leave plans have always had it; requests did not (closure plan R-3).
   */
  suggestChanges(id: string, data: SuggestLeaveRequestChanges): Promise<LeaveRequest> {
    return apiService.put<LeaveRequest>(`${this.baseUrl}/${id}/suggest-changes`, data);
  }

  /** The employee accepts the suggested dates, or counters with their own. Either way it re-submits. */
  respondToSuggestion(id: string, data: RespondToLeaveSuggestionRequest): Promise<LeaveRequest> {
    return apiService.put<LeaveRequest>(`${this.baseUrl}/${id}/respond-suggestion`, data);
  }

  /**
   * Move an APPROVED request to different dates, keeping its number and history.
   * ⚠ This re-opens the approval — an approval is an approval of dates (decision D-5).
   */
  reschedule(id: string, data: RescheduleLeaveRequest): Promise<LeaveRequest> {
    return apiService.put<LeaveRequest>(`${this.baseUrl}/${id}/reschedule`, data);
  }

  /**
   * Call an employee back before their leave ends (R-14).
   *
   * ⚠ Unlike {@link reschedule}, this does NOT re-open the approval — the leave was validly
   * approved and then interrupted, so the employer stands on the approval it already gave. Days up
   * to the recall stand as taken; the rest go back to the balance.
   *
   * ⚠ HR-only. The server gates this on the write permission outright rather than self-or-HR, so an
   * employee cannot recall themselves and hand back their own days.
   */
  recall(id: string, data: RecallLeaveRequest): Promise<LeaveRequest> {
    return apiService.put<LeaveRequest>(`${this.baseUrl}/${id}/recall`, data);
  }

  /**
   * Point this request at the medical board that ruled on the absence, or unlink it (G4).
   *
   * ⚠ Leave READS the board; it never writes one. This records WHICH board a request rests on,
   * and the evidence gate then checks it.
   *
   * ⚠ A board that is only Requested or Convened CAN be linked — a board is usually asked for
   * before it sits, and the request should be able to say which one it is waiting on. The gate is
   * what insists on Concluded. Linking records intent; the gate enforces the rule.
   *
   * ⚠ Unlink passes `null`, and `apiService.put` DROPS a null body rather than serialising it
   * — so the wire request is a PUT with no body at all. The endpoint binds the parameter with
   * `EmptyBodyBehavior.Allow` for exactly that reason; without it the Unlink button would take a
   * 400 from model binding — "A non-empty request body is required" — while the same call from curl
   * worked. Do not "tidy" either half without the other.
   */
  linkMedicalBoard(id: string, medicalBoardId: string | null): Promise<LeaveRequest> {
    return apiService.put<LeaveRequest>(`${this.baseUrl}/${id}/medical-board`, medicalBoardId);
  }

  /**
   * Recalculate EVERY balance in the tenant for a year, optionally one leave type.
   *
   * ⚠ Admin tier, and heavy — it walks the whole tenant. Finding L-19's complaint was that a
   * policy correction reaching 900 people had NO ROUTE THROUGH THE UI; an endpoint alone did not
   * answer that, which is why this client and the button that calls it exist.
   *
   * Safe to run and safe to re-run: it DERIVES the counters from requests and adjustments that
   * already exist, never invents a figure, and never touches entitled or carried-over days. That
   * is why it has no dry run, unlike the year-end jobs.
   */
  recalculateAllBalances(year: number, leaveTypeId?: string): Promise<LeaveBulkRecalculationResult> {
    const params = new URLSearchParams({ year: String(year) });
    if (leaveTypeId) params.set('leaveTypeId', leaveTypeId);
    return apiService.post<LeaveBulkRecalculationResult>(
      `${this.baseUrl}/balances/recalculate-all?${params.toString()}`,
    );
  }

  /**
   * Re-derive stored entitlements from the rulebook, or preview what that would change (A3).
   *
   * ⚠ **Always call this with `dryRun: true` first and show the result.** Unlike the recalculation
   * above, this OVERWRITES `EntitledDays` — a figure that is written once when a balance is created
   * and never refreshed, so the row may have been born before the allocation that should govern it.
   * The preview names every row and both figures; that list is the thing a person decides on.
   *
   * ⚠ Admin tier, like the year-end jobs and for the same reason: it changes what people are owed,
   * in bulk.
   */
  repairEntitlements(
    year: number,
    opts: { leaveTypeId?: string; employeeId?: string; dryRun?: boolean } = {},
  ): Promise<LeaveEntitlementRepairResult> {
    const params = new URLSearchParams({ year: String(year) });
    if (opts.leaveTypeId) params.set('leaveTypeId', opts.leaveTypeId);
    if (opts.employeeId) params.set('employeeId', opts.employeeId);
    if (opts.dryRun) params.set('dryRun', 'true');
    return apiService.post<LeaveEntitlementRepairResult>(
      `${this.baseUrl}/balances/repair-entitlements?${params.toString()}`,
    );
  }

  /**
   * Requests awaiting YOUR decision, as the workflow engine sees it.
   *
   * ⚠ Not the same as the old `getPendingApprovals(managerId)`, which asked "this manager's direct
   * reports' pending requests". Under the two-stage ladder that answered the wrong question in both
   * directions — it kept showing a manager what they had already approved, and never showed HR the
   * confirmation step at all (closure plan L-10).
   */
  getMyApprovals(pageNumber = 1, pageSize = 20): Promise<PagedResult<LeaveRequest>> {
    return apiService.get<PagedResult<LeaveRequest>>(`${this.baseUrl}/my-approvals`, {
      pageNumber,
      pageSize,
    });
  }

  /**
   * The organisation-wide register. Until this existed the only listing was one employee at a
   * time, so "who is off in December" could not be asked at all (closure plan L-6).
   */
  getRegister(
    filter: LeaveRegisterFilter,
    pageNumber = 1,
    pageSize = 25,
  ): Promise<PagedResult<LeaveRequest>> {
    return apiService.get<PagedResult<LeaveRequest>>(`${this.baseUrl}/register`, {
      ...filter,
      pageNumber,
      pageSize,
    });
  }

  /** The register as a CSV, with the same filters. */
  exportRegister(filter: LeaveRegisterFilter): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/register/export`, { ...filter });
  }

  exportBalances(year: number, employeeId?: string, leaveTypeId?: string): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/balances/export`, {
      year,
      employeeId,
      leaveTypeId,
    });
  }

  exportCompliance(year: number): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/compliance/export`, { year });
  }

  /**
   * Decide several requests at once.
   *
   * ⚠ The server loops the real per-request service call, so each item is authorized on its own and
   * a refusal on one does not abandon the rest — read `results` for the ones that did not go
   * through, rather than assuming success from a 200.
   */
  bulkApprove(data: BulkLeaveDecision): Promise<HrBulkActionResult> {
    return apiService.post<HrBulkActionResult>(`${this.baseUrl}/bulk-approve`, data);
  }

  bulkReject(data: BulkLeaveDecision): Promise<HrBulkActionResult> {
    return apiService.post<HrBulkActionResult>(`${this.baseUrl}/bulk-reject`, data);
  }

  /**
   * Leave drawn as time. The scope decides who is visible AND how the caller is authorized:
   * `Mine` and `Team` resolve from the token, `Organisation` needs the leave read tier.
   */
  getCalendar(params: {
    from: string;
    to: string;
    scope: LeaveCalendarScope;
    leaveTypeId?: string;
    /** Organisation scope only — the unit and every unit beneath it. */
    organizationUnitId?: string;
    /** One person's leave. Narrows any scope; never widens it (round 5 lane F). */
    employeeId?: string;
  }): Promise<LeaveCalendarData> {
    return apiService.get<LeaveCalendarData>(`${this.baseUrl}/calendar`, params);
  }

  /** Record that approved leave is still going ahead. Moves no days, changes no status. */
  confirmObservance(id: string): Promise<LeaveRequest> {
    return apiService.put<LeaveRequest>(`${this.baseUrl}/${id}/confirm-observance`, {});
  }

  approve(id: string, data: ApproveLeaveRequest): Promise<LeaveRequest> {
    return apiService.put<LeaveRequest>(`${this.baseUrl}/${id}/approve`, data);
  }

  reject(id: string, data: RejectLeaveRequest): Promise<LeaveRequest> {
    return apiService.put<LeaveRequest>(`${this.baseUrl}/${id}/reject`, data);
  }

  /** The cancellation reason is posted as a bare JSON string, not an object. */
  cancel(id: string, cancellationReason: string): Promise<void> {
    return apiService.put<void>(`${this.baseUrl}/${id}/cancel`, cancellationReason);
  }

  /** Confirming the employee's return, which closes the leave (round 5, B3). */
  close(id: string, data: CloseLeaveRequest): Promise<LeaveRequest> {
    return apiService.put<LeaveRequest>(`${this.baseUrl}/${id}/close`, data);
  }

  /** "I'm back at work" — the employee's own report; the day defaults to today (round 5, B3). */
  reportResumption(id: string, data: ReportResumptionRequest): Promise<LeaveRequest> {
    return apiService.put<LeaveRequest>(`${this.baseUrl}/${id}/report-resumption`, data);
  }

  /** Returns waiting for the caller to confirm, as the employee's supervisor or head of department. */
  getResumptionsToConfirm(): Promise<LeaveRequest[]> {
    return apiService.get<LeaveRequest[]>(`${this.baseUrl}/resumptions-to-confirm`);
  }

  // ── Balances ──────────────────────────────────────────────────────────────────

  getBalances(
    year = 0,
    employeeId?: string,
    leaveTypeId?: string,
  ): Promise<LeaveBalance[]> {
    return apiService.get<LeaveBalance[]>(`${this.baseUrl}/balances`, {
      year,
      employeeId,
      leaveTypeId,
    });
  }

  getEmployeeBalances(employeeId: string, year = 0): Promise<LeaveBalance[]> {
    return apiService.get<LeaveBalance[]>(`${this.baseUrl}/employee/${employeeId}/balances`, {
      year,
    });
  }

  getBalanceDetail(id: string): Promise<LeaveBalanceDetail> {
    return apiService.get<LeaveBalanceDetail>(`${this.baseUrl}/balances/${id}`);
  }

  /**
   * How a balance's accrual is worked out as at a date (round 5, lane C2). Self-or-HR: the portal
   * reads the employee's own, the desk anybody's. `asOf` defaults to today on the server.
   */
  getAccrualStatement(balanceId: string, asOf?: string): Promise<LeaveAccrualStatement> {
    return apiService.get<LeaveAccrualStatement>(
      `${this.baseUrl}/balances/${balanceId}/accrual-statement`,
      { asOf },
    );
  }

  /** Annual leave built up and not yet taken, per employee, as at a date (round 5, lane C6). */
  getLeaveOwed(asOf?: string): Promise<LeaveOwedReport> {
    return apiService.get<LeaveOwedReport>(`${this.baseUrl}/balances/owed`, { asOf });
  }

  exportLeaveOwed(asOf?: string): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/balances/owed/export`, { asOf });
  }

  /** The tenant's current leave year, so a screen can open on it (round 5, lane C4). */
  getLeaveYear(): Promise<LeaveYearInfo> {
    return apiService.get<LeaveYearInfo>(`${this.baseUrl}/leave-year`);
  }

  recalculateBalance(data: RecalculateLeaveBalanceRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/balances/recalculate`, data);
  }

  getMandatoryCompliance(year = 0): Promise<MandatoryLeaveCompliance[]> {
    return apiService.get<MandatoryLeaveCompliance[]>(`${this.baseUrl}/mandatory-compliance`, {
      year,
    });
  }

  // ── Adjustments ───────────────────────────────────────────────────────────────

  getAdjustments(
    year = 0,
    employeeId?: string,
    leaveTypeId?: string,
    search?: string,
  ): Promise<LeaveAdjustment[]> {
    return apiService.get<LeaveAdjustment[]>(`${this.baseUrl}/adjustments`, {
      year,
      employeeId,
      leaveTypeId,
      search,
    });
  }

  getAdjustmentById(id: string): Promise<LeaveAdjustment> {
    return apiService.get<LeaveAdjustment>(`${this.baseUrl}/adjustments/${id}`);
  }

  getBalanceAdjustments(balanceId: string): Promise<LeaveAdjustment[]> {
    return apiService.get<LeaveAdjustment[]>(`${this.baseUrl}/balances/${balanceId}/adjustments`);
  }

  /** Standalone: resolves (or creates) the balance from employee + type + year. */
  createAdjustment(data: CreateLeaveAdjustmentRequest): Promise<LeaveAdjustment> {
    return apiService.post<LeaveAdjustment>(`${this.baseUrl}/adjustments`, data);
  }

  updateAdjustment(id: string, data: UpdateLeaveAdjustmentRequest): Promise<LeaveAdjustment> {
    return apiService.put<LeaveAdjustment>(`${this.baseUrl}/adjustments/${id}`, data);
  }

  removeAdjustment(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/adjustments/${id}`);
  }

  // ── Attachments (controlled upload gate) ──────────────────────────────────────

  getAttachments(id: string): Promise<LeaveRequestAttachment[]> {
    return apiService.get<LeaveRequestAttachment[]>(`${this.baseUrl}/${id}/attachments`);
  }

  /**
   * Multipart upload through the scanning gate; rejections arrive as 422 { code, message }.
   *
   * ⚠ `evidenceKind` says WHAT the document is, and the R-15a evidence gate reads it — a leave
   * type requiring excuse duty refuses a submission until a document of that kind is attached.
   * It rides the query string because the controller binds it `[FromQuery]`, the same way the
   * desk's `LeaveAttachmentsPanel` sends it.
   *
   * ⚠ It defaults to `Other` rather than guessing from the leave type. A mis-typed document is
   * worse than an untyped one: the whole point of typing evidence is that `scan.pdf` cannot be
   * checked by its name, and a default that silently claimed a file was a medical certificate
   * would put the gate back where it started.
   */
  uploadAttachment(
    id: string,
    file: File,
    evidenceKind: LeaveEvidenceKind = 'Other',
  ): Promise<LeaveRequestAttachment> {
    return hrDocumentService.upload<LeaveRequestAttachment>(
      `${this.baseUrl}/${id}/attachments?evidenceKind=${encodeURIComponent(evidenceKind)}`,
      file,
    );
  }

  downloadAttachment(attachmentId: string, fileName: string): Promise<void> {
    return hrDocumentService.download(
      `${this.baseUrl}/attachments/${attachmentId}/download`,
      fileName,
    );
  }

  removeAttachment(attachmentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/attachments/${attachmentId}`);
  }
}

/** api/hr/leave-plans — annual leave planning, approved through the workflow engine. */
class LeavePlanService {
  private readonly baseUrl = '/hr/leave-plans';

  /**
   * Is this reliever free over these dates? Same answer the register carries per row as
   * `relieverClashes`, asked before the plan exists. (Finish-plan lane 4.)
   *
   * ⚠ Round 5 lane E1: this used to wrap the query in `{ params: {...} }`. `apiService.get` takes the
   * query object itself, so the request went out as `?params=[object Object]`, the API answered 400
   * "A reliever is required", and the form showed every reliever as free.
   */
  getRelieverClashes(
    relieverId: string,
    startDate: string,
    endDate: string,
    excludePlanId?: string | null,
  ): Promise<LeaveRelieverClash[]> {
    return apiService.get<LeaveRelieverClash[]>(`${this.baseUrl}/reliever-clashes`, {
      relieverId,
      startDate,
      endDate,
      ...(excludePlanId ? { excludePlanId } : {}),
    });
  }

  getByYear(year = 0): Promise<LeavePlan[]> {
    return apiService.get<LeavePlan[]>(this.baseUrl, { year });
  }

  getByEmployee(employeeId: string, year = 0): Promise<LeavePlan[]> {
    return apiService.get<LeavePlan[]>(`${this.baseUrl}/employee/${employeeId}`, { year });
  }

  getById(id: string): Promise<LeavePlan> {
    return apiService.get<LeavePlan>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateLeavePlanRequest): Promise<LeavePlan> {
    return apiService.post<LeavePlan>(this.baseUrl, data);
  }

  update(id: string, data: CreateLeavePlanRequest): Promise<LeavePlan> {
    return apiService.put<LeavePlan>(`${this.baseUrl}/${id}`, data);
  }

  submit(id: string): Promise<LeavePlan> {
    return apiService.patch<LeavePlan>(`${this.baseUrl}/${id}/submit`);
  }

  approve(id: string): Promise<LeavePlan> {
    return apiService.patch<LeavePlan>(`${this.baseUrl}/${id}/approve`);
  }

  /** Reason is posted as a bare JSON string. */
  reject(id: string, reason: string): Promise<LeavePlan> {
    return apiService.patch<LeavePlan>(`${this.baseUrl}/${id}/reject`, reason);
  }

  suggestChanges(id: string, data: SuggestLeavePlanChangesRequest): Promise<LeavePlan> {
    return apiService.patch<LeavePlan>(`${this.baseUrl}/${id}/suggest-changes`, data);
  }

  respondToSuggestion(id: string, data: RespondToLeaveSuggestionRequest): Promise<LeavePlan> {
    return apiService.patch<LeavePlan>(`${this.baseUrl}/${id}/respond-suggestion`, data);
  }

  /**
   * The approver's one edit to a plan — its relievers (round 5 lane E3). Both slots are replaced:
   * send the one being kept as well as the one being changed.
   */
  updateRelievers(id: string, data: UpdateLeavePlanRelieversRequest): Promise<LeavePlan> {
    return apiService.patch<LeavePlan>(`${this.baseUrl}/${id}/relievers`, data);
  }

  /**
   * Cancel a plan. The employee may cancel their own until it is approved; HR may also cancel an
   * approved plan, and must then give a reason (round 5 lane E5).
   */
  cancel(id: string, reason?: string | null): Promise<void> {
    return apiService.patch<void>(`${this.baseUrl}/${id}/cancel`, { reason: reason?.trim() || null });
  }
}

/** api/hr/leave-encashments — converting unused days to cash. */
class LeaveEncashmentService {
  private readonly baseUrl = '/hr/leave-encashments';

  getAll(year = 0, status?: string): Promise<LeaveEncashment[]> {
    return apiService.get<LeaveEncashment[]>(this.baseUrl, { year, status });
  }

  getById(id: string): Promise<LeaveEncashment> {
    return apiService.get<LeaveEncashment>(`${this.baseUrl}/${id}`);
  }

  getByEmployee(employeeId: string, year = 0): Promise<LeaveEncashment[]> {
    return apiService.get<LeaveEncashment[]>(`${this.baseUrl}/employee/${employeeId}`, { year });
  }

  request(data: CreateLeaveEncashmentRequest): Promise<LeaveEncashment> {
    return apiService.post<LeaveEncashment>(this.baseUrl, data);
  }

  approve(id: string): Promise<LeaveEncashment> {
    return apiService.patch<LeaveEncashment>(`${this.baseUrl}/${id}/approve`);
  }

  reject(id: string, reason: string): Promise<LeaveEncashment> {
    return apiService.patch<LeaveEncashment>(`${this.baseUrl}/${id}/reject`, reason);
  }

  /** Records payment after approval — a separate step from the workflow decision. */
  markAsProcessed(id: string, data: ProcessLeaveEncashmentRequest): Promise<LeaveEncashment> {
    return apiService.patch<LeaveEncashment>(`${this.baseUrl}/${id}/process`, data);
  }
}

/** api/hr/leave-year-end — carry-over and forfeiture batch runs. */
class LeaveYearEndService {
  private readonly baseUrl = '/hr/leave-year-end';

  /**
   * ⚠ `dryRun` computes and reports, writing NOTHING.
   *
   * Both these jobs move people's balances in bulk and neither has an undo, so a preview is the
   * difference between catching a misconfigured leave type before the run and catching it in nine
   * hundred balances afterwards (finding L-24). The result carries `isDryRun`, so a preview cannot
   * be mistaken for a run.
   */
  processCarryOver(
    fromYear: number,
    employeeId?: string,
    dryRun = false,
  ): Promise<LeaveYearEndResult> {
    const params = new URLSearchParams({ fromYear: String(fromYear) });
    if (employeeId) params.set('employeeId', employeeId);
    if (dryRun) params.set('dryRun', 'true');
    return apiService.post<LeaveYearEndResult>(`${this.baseUrl}/carry-over?${params.toString()}`);
  }

  /**
   * ⚠ A dry run still demands an employee-linked actor, exactly as the real run does — a
   * preview that succeeds where the real run would fail is a false assurance, not a preview.
   */
  processForfeiture(
    year: number,
    asOf?: string,
    employeeId?: string,
    dryRun = false,
  ): Promise<LeaveYearEndResult> {
    const params = new URLSearchParams({ year: String(year) });
    if (asOf) params.set('asOf', asOf);
    if (employeeId) params.set('employeeId', employeeId);
    if (dryRun) params.set('dryRun', 'true');
    return apiService.post<LeaveYearEndResult>(`${this.baseUrl}/forfeiture?${params.toString()}`);
  }
}

export const leaveService = new LeaveService();
export const leavePlanService = new LeavePlanService();
export const leaveEncashmentService = new LeaveEncashmentService();
export const leaveYearEndService = new LeaveYearEndService();
