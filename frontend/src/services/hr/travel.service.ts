import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  CreateStaffTravelDocument,
  StaffTravelAlertNotification,
  StaffTravelAlertSummary,
  StaffTravelDocument,
  StaffTravelTripHealthRequirement,
  UpdateStaffTravelDocument,
} from '@/types/hr/travel-compliance';
import type { StaffTravelTravellerBookings, StaffTravelTravellerItinerary } from '@/types/hr/travel-bookings';
import type {
  StaffTravelRequest,
  StaffTravelRequestSummary,
  StaffTravelRequestStatus,
  StaffTravelRequestComment,
  StaffTravelRequestAttachment,
  StaffGroupTravel,
  StaffGroupTravelSummary,
  StaffTravelDashboard,
  StaffTravelPolicyPreview,
  StaffTravelSubmitResult,
  StaffTravelApprovalQueueItem,
  StaffTravelApproveResult,
  StaffTravelViewerActions,
  CreateStaffTravelRequest,
  UpdateStaffTravelRequest,
  CreateStaffTravelRequestComment,
  UpdateStaffTravelRequestComment,
  CancelMyStaffTravelRequest,
  CreateStaffGroupTravel,
  UpdateStaffGroupTravel,
  GroupTravelStatus,
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
 *
 * <b>The approver's door</b> (travel final closure, lane 2): `getById`, `getComments`,
 * `getAttachments` and the download, `approve`, `reject`, `returnForRevision`, `getMyApprovals` and
 * `getViewerActions` (and, since lane 5, `getDestinationAlerts`) also answer the traveller's line manager and whoever the request waits for — no
 * travel permission needed. Everything else here stays the desk's.
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

  /** Travel requests waiting for the caller's decision — the engine's, then the line rule's (lane 2). */
  getMyApprovals(params: { pageNumber?: number; pageSize?: number } = {}) {
    return apiService.get<PagedResult<StaffTravelApprovalQueueItem>>(`${this.baseUrl}/my-approvals`, params);
  }

  /** What the caller may decide on this request, at which stage, and as whom (lane 2). */
  getViewerActions(id: string) {
    return apiService.get<StaffTravelViewerActions>(`${this.baseUrl}/${id}/viewer-actions`);
  }

  /**
   * The destination alerts in force over the trip, most severe first (lane 5, T-45) — on the approver's door, so
   * whoever decides or books the trip sees a Critical or Emergency alert. A warning; a block is TDC's question.
   */
  getDestinationAlerts(id: string) {
    return apiService.get<StaffTravelAlertSummary[]>(`${this.baseUrl}/${id}/destination-alerts`);
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

  /**
   * The approved policy a trip for this traveller would be checked against, and its limits — the
   * same resolution the server applies at submission (finding T-16).
   */
  getPolicyPreview(params: {
    employeeId: string;
    departure: string;
    originCountryId?: string;
    destinationCountryId?: string;
  }) {
    return apiService.get<StaffTravelPolicyPreview>(`${this.baseUrl}/policy-preview`, params);
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

  /**
   * Everything that must hold is checked first and refused with a 422 that says what (lane 1). The
   * one override is the desk's: a trip whose departure has passed goes with the reason it is late,
   * kept on the request as an internal note in the submitter's name. The answer carries warnings —
   * approved leave over the same days — that did not stop it.
   */
  submit(id: string, lateSubmissionReason?: string) {
    return apiService.post<StaffTravelSubmitResult>(
      `${this.baseUrl}/${id}/submit`, lateSubmissionReason ? { lateSubmissionReason } : {});
  }

  /**
   * Approves the stage the request is on; the approver is the token, never sent. The budget is the last
   * stage's to set (lane 2) — sent at an earlier stage it is refused; left out at the last, the estimate
   * is approved. The answer says whether the trip is approved or gone on to the next stage.
   */
  approve(id: string, approvedBudget?: number, notes?: string) {
    return apiService.post<StaffTravelApproveResult>(`${this.baseUrl}/${id}/approve`, {
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

  /**
   * Only from Approved or InProgress, and not before the trip starts — completion records that the
   * trip happened.
   */
  complete(id: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/complete`, {});
  }

  /**
   * An approver sends a submitted request back with what to change; it returns to the requester to
   * edit and resubmit (lane 1, D-6). The approve gate applies, and never the traveller.
   */
  returnForRevision(id: string, reason: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/return`, { reason });
  }

  /** An approved trip goes back for revision and is approved again; its bookings stay (lane 1, D-9). */
  requestChange(id: string, reason: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/request-change`, { reason });
  }

  /**
   * Back to Draft from approval — the traveller's or whoever raised it. With a workflow instance only
   * the login that submitted it can recall it; the server says so otherwise.
   */
  recall(id: string, reason?: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/recall`, reason ? { reason } : {});
  }

  /** A completed trip, once every claim is paid or rejected and every advance settled (lane 1, D-6). */
  close(id: string) {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/close`, {});
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

  getGroupsByStatus(status: GroupTravelStatus) {
    return apiService.get<StaffGroupTravelSummary[]>(`${this.baseUrl}/groups/status/${status}`);
  }

  createGroup(payload: CreateStaffGroupTravel) {
    return apiService.post<StaffGroupTravel>(`${this.baseUrl}/groups`, payload);
  }

  /**
   * No status (lane 1, slice 1c): `openGroup`, `closeGroup` and `cancelGroup` move it. A new
   * destination or new dates are given to the travellers whose trips are still drafts or returned
   * for revision; submitted and approved trips keep their own.
   */
  updateGroup(payload: UpdateStaffGroupTravel) {
    return apiService.put<StaffGroupTravel>(`${this.baseUrl}/groups/${payload.id}`, payload);
  }

  /** From Planning, or reopens a closed group, so travellers can be added. */
  openGroup(id: string) {
    return apiService.post<StaffGroupTravel>(`${this.baseUrl}/groups/${id}/open`, {});
  }

  /** No new travellers; the trips already on it carry on. */
  closeGroup(id: string) {
    return apiService.post<StaffGroupTravel>(`${this.baseUrl}/groups/${id}/close`, {});
  }

  /** Refused while any traveller's trip is still going ahead — the server names them. */
  cancelGroup(id: string) {
    return apiService.post<StaffGroupTravel>(`${this.baseUrl}/groups/${id}/cancel`, {});
  }

  /**
   * `HR.Travel.Admin`. The travellers come off the group first (lane 1) and their trips carry on as
   * ordinary ones — they used to keep the deleted group's id. Deleting means "no longer organised as
   * a group", not "nobody is going".
   */
  deleteGroup(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/groups/${id}`);
  }

  /**
   * Raises one draft request per employee, skipping anyone already holding a place. Refused when the
   * group is not taking travellers or has no room for them all.
   */
  addGroupParticipants(groupId: string, employeeIds: string[], template: Record<string, unknown>) {
    return apiService.post<StaffGroupTravel>(`${this.baseUrl}/groups/${groupId}/participants`, {
      groupTravelId: groupId, employeeIds, ...template,
    });
  }

  /**
   * Puts an existing draft (or returned) request on the group; it takes the group's destination and
   * dates (lane 1 — there was no way to do this from any screen).
   */
  linkGroupRequest(groupId: string, requestId: string) {
    return apiService.post<StaffGroupTravel>(`${this.baseUrl}/groups/${groupId}/requests/${requestId}`, {});
  }

  /**
   * `HR.Travel.Admin`. Takes the person off the group without cancelling their trip — their
   * request survives on its own, which is why the screen says so before confirming.
   */
  removeGroupParticipant(groupId: string, requestId: string) {
    return apiService.delete<void>(
      `${this.baseUrl}/groups/${groupId}/participants/${requestId}`);
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

  /** The traveller, the initiator and the initiator's role are all taken from the token here. */
  createMine(payload: Omit<CreateStaffTravelRequest, 'employeeId' | 'initiatedByRole'>) {
    return apiService.post<StaffTravelRequest>(`${this.meUrl}/requests`, payload);
  }

  updateMine(payload: UpdateStaffTravelRequest) {
    return apiService.put<StaffTravelRequest>(`${this.meUrl}/requests/${payload.id}`, payload);
  }

  /** A past departure is refused here — that trip is the travel desk's to submit, with a reason. */
  submitMine(id: string) {
    return apiService.post<StaffTravelSubmitResult>(`${this.meUrl}/requests/${id}/submit`, {});
  }

  /** The approved policy that covers the caller for a trip on these dates — their own, only. */
  getMyPolicyPreview(params: { departure: string; originCountryId?: string; destinationCountryId?: string }) {
    return apiService.get<StaffTravelPolicyPreview>(`${this.meUrl}/policy-preview`, params);
  }

  cancelMine(id: string, payload: CancelMyStaffTravelRequest) {
    return apiService.post<void>(`${this.meUrl}/requests/${id}/cancel`, payload);
  }

  /** Take your own request back from approval to change it. */
  recallMine(id: string, reason?: string) {
    return apiService.post<void>(`${this.meUrl}/requests/${id}/recall`, reason ? { reason } : {});
  }

  /** Ask for a change to your own approved trip — it comes back to you and is approved again. */
  requestChangeMine(id: string, reason: string) {
    return apiService.post<void>(`${this.meUrl}/requests/${id}/request-change`, { reason });
  }

  // ── My destination alerts ──────────────────────────────────────────────────
  //
  // ⚠ These exist because the desk-side acknowledge was unreachable by anyone. It sits on
  // Travel.Write, which `HrStaffGrants` gives to HR staff and never to the `Employee` role, while
  // the service refuses anyone but the employee the alert was addressed to — so a traveller could
  // not pass the gate and an HR officer who did was refused by the service. Same shape as the
  // discipline authority gate, and the same answer the assets acknowledgement already uses: a
  // token-scoped route that takes no employee id from anywhere.

  getMyAlerts() {
    return apiService.get<StaffTravelAlertNotification[]>(`${this.meUrl}/alert-notifications`);
  }

  getMyUnacknowledgedAlerts() {
    return apiService.get<StaffTravelAlertNotification[]>(
      `${this.meUrl}/alert-notifications/unacknowledged`);
  }

  /** Confirms the caller has read a destination alert. Nobody can do this on their behalf. */
  acknowledgeMyAlert(id: string) {
    return apiService.post<void>(`${this.meUrl}/alert-notifications/${id}/acknowledge`, {});
  }

  // ── My trip: what the desk arranged (lane 7, slice 7c1) ────────────────────
  //
  // Each answers 404 for a trip that is not the caller's.

  /** The itinerary in force (the one the desk finalised), or none and whether one is being drafted. */
  getMyItinerary(requestId: string) {
    return apiService.get<StaffTravelTravellerItinerary>(`${this.meUrl}/requests/${requestId}/itinerary`);
  }

  /** Every booking on the trip in full — a flight's segments are its times. */
  getMyBookings(requestId: string) {
    return apiService.get<StaffTravelTravellerBookings>(`${this.meUrl}/requests/${requestId}/bookings`);
  }

  getMyHealthRequirements(requestId: string) {
    return apiService.get<StaffTravelTripHealthRequirement[]>(
      `${this.meUrl}/requests/${requestId}/health-requirements`);
  }

  /** Every alert in force for the destination over the trip, with its text — not only the ones sent to the caller. */
  getMyDestinationAlerts(requestId: string) {
    return apiService.get<StaffTravelAlertSummary[]>(`${this.meUrl}/requests/${requestId}/destination-alerts`);
  }

  /** E1: confirms the caller has read their trip's risk assessment. A Critical trip's ticket waits for it (D-37). */
  acknowledgeMyRiskAssessment(id: string) {
    return apiService.post<void>(`${this.meUrl}/risk-assessments/${id}/acknowledge`, {});
  }

  // ── My travel documents (lane 7, slice 7c1, E2) ────────────────────────────
  //
  // Whose they are is the token's. The list masks numbers to the last four; a document's own read shows its owner the
  // full number. An edit takes the desk's verification off; a verified document is not deleted.

  getMyDocuments() {
    return apiService.get<StaffTravelDocument[]>(`${this.meUrl}/travel-documents`);
  }

  getMyDocument(id: string) {
    return apiService.get<StaffTravelDocument>(`${this.meUrl}/travel-documents/${id}`);
  }

  createMyDocument(payload: Omit<CreateStaffTravelDocument, 'employeeId'>) {
    return apiService.post<StaffTravelDocument>(`${this.meUrl}/travel-documents`, payload);
  }

  updateMyDocument(payload: Omit<UpdateStaffTravelDocument, 'employeeId'>) {
    return apiService.put<StaffTravelDocument>(`${this.meUrl}/travel-documents/${payload.id}`, payload);
  }

  deleteMyDocument(id: string) {
    return apiService.delete<void>(`${this.meUrl}/travel-documents/${id}`);
  }
}

export const travelService = new TravelService();
