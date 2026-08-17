import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  StaffTravelRequest,
  StaffTravelRequestSummary,
  StaffTravelRequestStatus,
  StaffTravelRequestComment,
  StaffTravelRequestAttachment,
  StaffGroupTravel,
  StaffGroupTravelSummary,
  StaffTravelDashboard,
  CreateStaffTravelRequest,
  UpdateStaffTravelRequest,
  CreateStaffTravelRequestComment,
  UpdateStaffTravelRequestComment,
  CancelMyStaffTravelRequest,
  CreateStaffGroupTravel,
} from '@/types/hr/travel';

/**
 * Staff travel requests. Backend route: api/staff-travel/requests.
 *
 * <b>Two surfaces, deliberately separate.</b> Everything on this class except the `my*` methods is
 * the travel desk's and is gated on `HR.Travel.*` — the register, the dashboard, other people's
 * trips. The `my*` methods hit `api/staff-travel/me`, which any authenticated employee may use and
 * which takes no employee id anywhere: the traveller is the token. Do not "simplify" a screen by
 * pointing it at the desk endpoint with the user's own id — that endpoint answers 403 for a plain
 * employee, which is the point.
 *
 * ⚠ Someone else's request on the self-service surface is a 404, not a 403, so the surface cannot
 * be used to enumerate travel-request ids. Treat 404 there as "not yours", not as "deleted".
 */
class TravelService {
  private readonly baseUrl = '/staff-travel/requests';
  private readonly meUrl = '/staff-travel/me';

  // ── Desk queries (HR.Travel.Read) ──────────────────────────────────────────

  getPaged(params: { pageNumber?: number; pageSize?: number } = {}) {
    return apiService.get<PagedResult<StaffTravelRequestSummary>>(this.baseUrl, params);
  }

  getAll() {
    return apiService.get<StaffTravelRequestSummary[]>(`${this.baseUrl}/all`);
  }

  getById(id: string) {
    return apiService.get<StaffTravelRequest>(`${this.baseUrl}/${id}`);
  }

  getByNumber(requestNumber: string) {
    return apiService.get<StaffTravelRequest | null>(`${this.baseUrl}/number/${requestNumber}`);
  }

  getByEmployee(employeeId: string) {
    return apiService.get<StaffTravelRequestSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getByStatus(status: StaffTravelRequestStatus) {
    return apiService.get<StaffTravelRequestSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getByDateRange(start: string, end: string) {
    return apiService.get<StaffTravelRequestSummary[]>(`${this.baseUrl}/date-range`, { start, end });
  }

  getPendingApproval() {
    return apiService.get<StaffTravelRequestSummary[]>(`${this.baseUrl}/pending-approval`);
  }

  getUpcoming(daysAhead = 30) {
    return apiService.get<StaffTravelRequestSummary[]>(`${this.baseUrl}/upcoming`, { daysAhead });
  }

  getChildren(parentRequestId: string) {
    return apiService.get<StaffTravelRequestSummary[]>(`${this.baseUrl}/${parentRequestId}/children`);
  }

  getDashboard(upcomingDays = 30) {
    return apiService.get<StaffTravelDashboard>(`${this.baseUrl}/dashboard`, { upcomingDays });
  }

  // ── Desk writes (HR.Travel.Write; delete is Admin) ─────────────────────────

  create(payload: CreateStaffTravelRequest) {
    return apiService.post<StaffTravelRequest>(this.baseUrl, payload);
  }

  update(payload: UpdateStaffTravelRequest) {
    return apiService.put<StaffTravelRequest>(`${this.baseUrl}/${payload.id}`, payload);
  }

  /** Admin-gated. Only a Draft request can be deleted — cancel anything further along. */
  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Lifecycle ──────────────────────────────────────────────────────────────
  //
  // These start and drive the WORKFLOW. A screen must never set a status itself: submit, refetch,
  // and let the workflow record say where the request now is.

  submit(id: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/submit`, { requestId: id });
  }

  /** The approver is resolved from the token against the published definition, never sent. */
  approve(id: string, approvedBudget?: number, notes?: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/approve`, {
      requestId: id, approvedBudget, notes,
    });
  }

  /** The reason is a query parameter on this endpoint, not a body field. */
  reject(id: string, reason?: string) {
    const query = reason ? `?reason=${encodeURIComponent(reason)}` : '';
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/reject${query}`, {});
  }

  cancel(id: string, cancellationReason: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/cancel`, {
      requestId: id, cancellationReason,
    });
  }

  /** Only from Approved or InProgress — completion records that the trip happened. */
  complete(id: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/complete`, {});
  }

  // ── Comments ───────────────────────────────────────────────────────────────

  getComments(requestId: string) {
    return apiService.get<StaffTravelRequestComment[]>(`${this.baseUrl}/${requestId}/comments`);
  }

  addComment(requestId: string, payload: CreateStaffTravelRequestComment) {
    return apiService.post<StaffTravelRequestComment>(
      `${this.baseUrl}/${requestId}/comments`, payload);
  }

  updateComment(payload: UpdateStaffTravelRequestComment) {
    return apiService.put<StaffTravelRequestComment>(
      `${this.baseUrl}/comments/${payload.id}`, payload);
  }

  deleteComment(commentId: string) {
    return apiService.delete<void>(`${this.baseUrl}/comments/${commentId}`);
  }

  // ── Attachments ────────────────────────────────────────────────────────────

  getAttachments(requestId: string) {
    return apiService.get<StaffTravelRequestAttachment[]>(`${this.baseUrl}/${requestId}/attachments`);
  }

  /**
   * Multipart, through the controlled-upload gate: the file is scanned, registered in the DMS and
   * stored outside the web root. There is deliberately no way to attach a path — a caller-supplied
   * FileUrl was a path-injection sink, and a travel attachment is typically a passport scan.
   */
  uploadAttachment(requestId: string, file: File, attachmentType: string, description?: string) {
    return hrDocumentService.upload<StaffTravelRequestAttachment>(
      `${this.baseUrl}/${requestId}/attachments`, file, { attachmentType, description });
  }

  /** Streams the stored bytes back. Never build a link from `fileUrl` — it is empty by design. */
  downloadAttachmentUrl(attachmentId: string) {
    return `${this.baseUrl}/attachments/${attachmentId}/download`;
  }

  deleteAttachment(attachmentId: string) {
    return apiService.delete<void>(`${this.baseUrl}/attachments/${attachmentId}`);
  }

  // ── Group travel ───────────────────────────────────────────────────────────

  getGroups() {
    return apiService.get<StaffGroupTravelSummary[]>(`${this.baseUrl}/groups`);
  }

  getGroupById(id: string) {
    return apiService.get<StaffGroupTravel>(`${this.baseUrl}/groups/${id}`);
  }

  createGroup(payload: CreateStaffGroupTravel) {
    return apiService.post<StaffGroupTravel>(`${this.baseUrl}/groups`, payload);
  }

  /** Raises one request per employee, skipping anyone already in the group. */
  addGroupParticipants(groupId: string, employeeIds: string[], template: Record<string, unknown>) {
    return apiService.post<StaffGroupTravel>(`${this.baseUrl}/groups/${groupId}/participants`, {
      groupTravelId: groupId, employeeIds, ...template,
    });
  }

  // ── Self-service (api/staff-travel/me) ─────────────────────────────────────
  //
  // No employee id anywhere: the traveller is the token.

  getMine() {
    return apiService.get<StaffTravelRequestSummary[]>(`${this.meUrl}/requests`);
  }

  getMineById(id: string) {
    return apiService.get<StaffTravelRequest>(`${this.meUrl}/requests/${id}`);
  }

  /** `employeeId` / `initiatedById` are overwritten server-side from the token. */
  createMine(payload: Omit<CreateStaffTravelRequest, 'employeeId' | 'initiatedById' | 'initiatedByRole'>) {
    return apiService.post<StaffTravelRequest>(`${this.meUrl}/requests`, payload);
  }

  updateMine(payload: UpdateStaffTravelRequest) {
    return apiService.put<StaffTravelRequest>(`${this.meUrl}/requests/${payload.id}`, payload);
  }

  submitMine(id: string) {
    return apiService.post<void>(`${this.meUrl}/requests/${id}/submit`, {});
  }

  cancelMine(id: string, payload: CancelMyStaffTravelRequest) {
    return apiService.post<void>(`${this.meUrl}/requests/${id}/cancel`, payload);
  }
}

export const travelService = new TravelService();
