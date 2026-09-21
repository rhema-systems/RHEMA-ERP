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

  /**
   * The movements the caller can approve right now — the engine decides what is in it. An approver
   * is normally a line manager, and the register answers 403 for them, so this is their queue.
   */
  getAwaitingMyApproval(): Promise<StaffMovementSummary[]> {
    return apiService.get<StaffMovementSummary[]>(`${this.baseUrl}/awaiting-my-approval`);
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

  /** Refused unless every gate has been cleared: acceptance, handover and required checklist tasks. */
  implementMovement(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/implement`, {});
  }

  // ── Workflow ───────────────────────────────────────────────────────────────

  /**
   * Hands the movement to the workflow engine.
   *
   * ⚠ This used to say "Inoperable until a StaffMovement definition is published for the tenant".
   * **It was not inoperable — it auto-approved** (corrected 2026-09-15): with no definition the
   * engine returned `Approved`, the adapter mapped it to `Approved`, and the service recorded the
   * submitter as the authoriser of their own move. With no definition published, submitting now
   * lands the movement at `Submitted`; a `HR.Movements.Admin` holder who did not request it then
   * approves or rejects it. The second half of the old sentence still holds and is the point of
   * the engine: when a definition *is* published, approval authority comes from the definition and
   * not from a role.
   */
  submit(id: string, submissionNotes?: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/submit`, { movementId: id, submissionNotes });
  }

  /**
   * Approves the current workflow step. Refused unless the engine has the caller assigned to it —
   * which is usually a line manager or head of department, not HR.
   */
  approve(id: string, comments?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/approve`, { movementId: id, comments });
  }

  /** Withdraws a submitted movement from approval, back to the requester as a draft. */
  recall(id: string, comments?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/recall`, { movementId: id, comments });
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

  // ── Approval levels ────────────────────────────────────────────────────────

  getApprovalLevels(id: string): Promise<StaffMovementApprovalLevel[]> {
    return apiService.get<StaffMovementApprovalLevel[]>(`${this.baseUrl}/${id}/approval-levels`);
  }

  // The add / action / delegate calls are gone — the workflow engine owns approval now. The read
  // above stays so a chain recorded by an earlier build is still visible on the movement.

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
