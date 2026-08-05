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
  LeaveBalanceDetail,
  RecalculateLeaveBalanceRequest,
  LeaveAdjustment,
  CreateLeaveAdjustmentRequest,
  UpdateLeaveAdjustmentRequest,
  MandatoryLeaveCompliance,
  LeaveRequestAttachment,
  LeavePlan,
  CreateLeavePlanRequest,
  SuggestLeavePlanChangesRequest,
  RespondToLeaveSuggestionRequest,
  LeaveEncashment,
  CreateLeaveEncashmentRequest,
  ProcessLeaveEncashmentRequest,
  LeaveYearEndResult,
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

  getEmployeeHistory(
    employeeId: string,
    year = 0,
    pageNumber = 1,
    pageSize = 20,
  ): Promise<PagedResult<LeaveRequest>> {
    return apiService.get<PagedResult<LeaveRequest>>(
      `${this.baseUrl}/employee/${employeeId}/history`,
      { year, pageNumber, pageSize },
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

  close(id: string, data: CloseLeaveRequest): Promise<LeaveRequest> {
    return apiService.put<LeaveRequest>(`${this.baseUrl}/${id}/close`, data);
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

  /** Multipart upload through the scanning gate; rejections arrive as 422 { code, message }. */
  uploadAttachment(id: string, file: File): Promise<LeaveRequestAttachment> {
    return hrDocumentService.upload<LeaveRequestAttachment>(
      `${this.baseUrl}/${id}/attachments`,
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

  cancel(id: string): Promise<void> {
    return apiService.patch<void>(`${this.baseUrl}/${id}/cancel`);
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

  processCarryOver(fromYear: number, employeeId?: string): Promise<LeaveYearEndResult> {
    const params = new URLSearchParams({ fromYear: String(fromYear) });
    if (employeeId) params.set('employeeId', employeeId);
    return apiService.post<LeaveYearEndResult>(`${this.baseUrl}/carry-over?${params.toString()}`);
  }

  processForfeiture(year: number, asOf?: string, employeeId?: string): Promise<LeaveYearEndResult> {
    const params = new URLSearchParams({ year: String(year) });
    if (asOf) params.set('asOf', asOf);
    if (employeeId) params.set('employeeId', employeeId);
    return apiService.post<LeaveYearEndResult>(`${this.baseUrl}/forfeiture?${params.toString()}`);
  }
}

export const leaveService = new LeaveService();
export const leavePlanService = new LeavePlanService();
export const leaveEncashmentService = new LeaveEncashmentService();
export const leaveYearEndService = new LeaveYearEndService();
