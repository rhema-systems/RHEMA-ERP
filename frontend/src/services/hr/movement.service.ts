import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  StaffMovement,
  StaffMovementSummary,
  StaffMovementStatus,
  StaffMovementType,
  StaffMovementAttachment,
  StaffMovementAttachmentType,
  StaffMovementChecklistItem,
  StaffMovementStatusHistory,
  StaffMovementApprovalLevel,
  StaffMovementDashboard,
  CreateStaffMovementRequest,
  UpdateStaffMovementRequest,
} from '@/types/hr/movements';

/**
 * Staff movements. Backend route: api/staff-movements.
 *
 * HR-gated per action, with four exceptions that belong to the employee rather than to HR:
 * `getMine`, `getMyChecklistItems`, reading a movement you are the subject of, and `respond`.
 * The server enforces those — `respond` is refused for anyone but the subject, HR included,
 * because acceptance is the employee's own testimony.
 */
class MovementService {
  private readonly baseUrl = '/staff-movements';

  // ── Queries ────────────────────────────────────────────────────────────────

  getPaged(params: {
    pageNumber?: number;
    pageSize?: number;
    status?: StaffMovementStatus;
    type?: StaffMovementType;
    isPendingApproval?: boolean;
  } = {}): Promise<PagedResult<StaffMovementSummary>> {
    return apiService.get<PagedResult<StaffMovementSummary>>(this.baseUrl, params);
  }

  getAll(): Promise<StaffMovementSummary[]> {
    return apiService.get<StaffMovementSummary[]>(`${this.baseUrl}/all`);
  }

  getById(id: string): Promise<StaffMovement> {
    return apiService.get<StaffMovement>(`${this.baseUrl}/${id}`);
  }

  getByNumber(movementNumber: string): Promise<StaffMovement | null> {
    return apiService.get<StaffMovement | null>(
      `${this.baseUrl}/number/${encodeURIComponent(movementNumber)}`,
    );
  }

  getByEmployee(employeeId: string): Promise<StaffMovementSummary[]> {
    return apiService.get<StaffMovementSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  /** The caller's own movements — the token supplies the employee. */
  getMine(): Promise<StaffMovementSummary[]> {
    return apiService.get<StaffMovementSummary[]>(`${this.baseUrl}/employee/me`);
  }

  getLatestForEmployee(employeeId: string): Promise<StaffMovementSummary[]> {
    return apiService.get<StaffMovementSummary[]>(`${this.baseUrl}/employee/${employeeId}/latest`);
  }

  getByStatus(status: StaffMovementStatus): Promise<StaffMovementSummary[]> {
    return apiService.get<StaffMovementSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getByType(type: StaffMovementType, from?: string, to?: string): Promise<StaffMovementSummary[]> {
    return apiService.get<StaffMovementSummary[]>(`${this.baseUrl}/type/${type}`, { from, to });
  }

  getPendingApproval(): Promise<StaffMovementSummary[]> {
    return apiService.get<StaffMovementSummary[]>(`${this.baseUrl}/pending-approval`);
  }

  getPendingEmployeeAcceptance(): Promise<StaffMovementSummary[]> {
    return apiService.get<StaffMovementSummary[]>(`${this.baseUrl}/pending-employee-acceptance`);
  }

  getPendingHandover(): Promise<StaffMovementSummary[]> {
    return apiService.get<StaffMovementSummary[]>(`${this.baseUrl}/pending-handover`);
  }

  getActiveTemporary(): Promise<StaffMovementSummary[]> {
    return apiService.get<StaffMovementSummary[]>(`${this.baseUrl}/temporary/active`);
  }

  getExpiringTemporary(daysAhead = 30): Promise<StaffMovementSummary[]> {
    return apiService.get<StaffMovementSummary[]>(`${this.baseUrl}/temporary/expiring`, { daysAhead });
  }

  getByDateRange(from: string, to: string): Promise<StaffMovementSummary[]> {
    return apiService.get<StaffMovementSummary[]>(`${this.baseUrl}/date-range`, { from, to });
  }

  getDashboard(filterYear?: number): Promise<StaffMovementDashboard> {
    return apiService.get<StaffMovementDashboard>(`${this.baseUrl}/dashboard`, { filterYear });
  }

  // ── CRUD ───────────────────────────────────────────────────────────────────

  create(request: CreateStaffMovementRequest): Promise<StaffMovement> {
    return apiService.post<StaffMovement>(this.baseUrl, request);
  }

  update(id: string, request: UpdateStaffMovementRequest): Promise<StaffMovement> {
    return apiService.put<StaffMovement>(`${this.baseUrl}/${id}`, request);
  }

  /** Refused (422) once the movement is authorised or implemented — it is a record of a decision. */
  delete(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Workflow ───────────────────────────────────────────────────────────────

  submit(id: string, submissionNotes?: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/submit`, { movementId: id, submissionNotes });
  }

  /** Refused until every approval level is cleared — and a movement with no levels cannot be authorised at all. */
  authorize(id: string, comments?: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/authorize`, { movementId: id, comments });
  }

  /** The subject's own acceptance or refusal. Refused for anyone else, HR included. */
  respond(id: string, accepted: boolean, comments?: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/respond`, { movementId: id, accepted, comments });
  }

  completeHandover(id: string, handoverNotes?: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/handover`, { movementId: id, handoverNotes });
  }

  reject(id: string, rejectionReason: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/reject`, { movementId: id, rejectionReason });
  }

  cancel(id: string, cancellationReason: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/cancel`, { movementId: id, cancellationReason });
  }

  processReturn(id: string, actualReturnDate: string, notes?: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/return`, {
      movementId: id,
      actualReturnDate,
      notes,
    });
  }

  implement(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/implement`, {});
  }

  // ── Approval levels ────────────────────────────────────────────────────────

  getApprovalLevels(id: string): Promise<StaffMovementApprovalLevel[]> {
    return apiService.get<StaffMovementApprovalLevel[]>(`${this.baseUrl}/${id}/approval-levels`);
  }

  addApprovalLevel(
    id: string,
    request: { level: number; roleName: string; approverId: string },
  ): Promise<StaffMovementApprovalLevel> {
    return apiService.post<StaffMovementApprovalLevel>(`${this.baseUrl}/${id}/approval-levels`, {
      movementId: id,
      ...request,
    });
  }

  /** Only the named approver or their delegate may action a level — not HR. */
  actionApprovalLevel(
    approvalLevelId: string,
    decision: 'Approved' | 'Rejected',
    comments?: string,
  ): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/approval-levels/${approvalLevelId}/action`, {
      approvalLevelId,
      decision,
      comments,
    });
  }

  /** Only the assigned approver may delegate their own level. */
  delegateApprovalLevel(
    approvalLevelId: string,
    delegatedToId: string,
    delegationReason?: string,
  ): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/approval-levels/${approvalLevelId}/delegate`, {
      approvalLevelId,
      delegatedToId,
      delegationReason,
    });
  }

  // ── Status history ─────────────────────────────────────────────────────────

  getStatusHistory(id: string): Promise<StaffMovementStatusHistory[]> {
    return apiService.get<StaffMovementStatusHistory[]>(`${this.baseUrl}/${id}/status-history`);
  }

  // ── Attachments ────────────────────────────────────────────────────────────

  getAttachments(id: string): Promise<StaffMovementAttachment[]> {
    return apiService.get<StaffMovementAttachment[]>(`${this.baseUrl}/${id}/attachments`);
  }

  /**
   * Uploads through the controlled gate (scan + central DMS registration). There is no way to
   * attach a file by path — the server refuses a caller-supplied location outright.
   */
  uploadAttachment(
    id: string,
    file: File,
    attachmentType: StaffMovementAttachmentType,
    description?: string,
  ): Promise<StaffMovementAttachment> {
    return hrDocumentService.upload<StaffMovementAttachment>(
      `${this.baseUrl}/${id}/attachments/upload`,
      file,
      { attachmentType, description },
    );
  }

  /** Streams the file with the bearer token attached; stored paths are not URLs. */
  downloadAttachment(attachmentId: string, fileName: string): Promise<void> {
    return hrDocumentService.download(
      `${this.baseUrl}/attachments/${attachmentId}/download`,
      fileName,
    );
  }

  deleteAttachment(attachmentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/attachments/${attachmentId}`);
  }

  // ── Checklist ──────────────────────────────────────────────────────────────

  getChecklistItems(id: string): Promise<StaffMovementChecklistItem[]> {
    return apiService.get<StaffMovementChecklistItem[]>(`${this.baseUrl}/${id}/checklist`);
  }

  getOverdueChecklistItems(): Promise<StaffMovementChecklistItem[]> {
    return apiService.get<StaffMovementChecklistItem[]>(`${this.baseUrl}/checklist/overdue`);
  }

  /** The caller's own outstanding tasks — the token supplies the employee. */
  getMyChecklistItems(): Promise<StaffMovementChecklistItem[]> {
    return apiService.get<StaffMovementChecklistItem[]>(`${this.baseUrl}/checklist/mine`);
  }

  addChecklistItem(
    id: string,
    request: {
      taskDescription: string;
      category: string;
      isRequired: boolean;
      responsiblePersonId?: string | null;
      dueDate?: string | null;
      displayOrder: number;
    },
  ): Promise<StaffMovementChecklistItem> {
    return apiService.post<StaffMovementChecklistItem>(`${this.baseUrl}/${id}/checklist`, {
      movementId: id,
      ...request,
    });
  }

  /** Only the responsible person may complete their own task; HR may close one out. */
  completeChecklistItem(itemId: string, completionNotes?: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/checklist/${itemId}/complete`, {
      itemId,
      completionNotes,
    });
  }

  deleteChecklistItem(itemId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/checklist/${itemId}`);
  }
}

export const movementService = new MovementService();
