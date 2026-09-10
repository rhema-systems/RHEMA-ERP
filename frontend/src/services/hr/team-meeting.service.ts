import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type { TeamTaskDetail } from '@/types/hr/team-activity';
import type {
  AcknowledgeTeamReviewRequest,
  CreateTeamMeetingDecisionRequest,
  CreateTeamMeetingRequest,
  CreateTeamReviewRequest,
  HoldTeamMeetingRequest,
  RaiseTaskFromDecisionRequest,
  TeamDashboard,
  TeamMeeting,
  TeamMeetingDecision,
  TeamMeetingDetail,
  TeamReminderDispatch,
  TeamReminderRun,
  TeamReminderRunResult,
  TeamReview,
  TeamReviewDetail,
  TeamReviewLine,
  UpdateTeamMeetingDecisionRequest,
  UpdateTeamMeetingRequest,
  UpdateTeamReviewRequest,
  UpsertTeamReviewLineRequest,
} from '@/types/hr/team-meeting';

/**
 * A team's minute book, its reviews and its dashboard. Backend: `api/hr/teams/**`.
 *
 * ⚠ **Who may write is decided by the SERVER against the team record.** HR acts on any team, the
 * lead or deputy on their own. A refusal comes back 403 with a sentence; a rule refusal comes back
 * 422 with the rule's own words.
 */
class TeamMeetingService {
  private readonly baseUrl = '/hr/teams';
  private readonly docsUrl = '/hr/team-documents';

  /** How the team is doing, in ONE read — the tiles have to agree with each other. */
  getDashboard(teamId: string) {
    return apiService.get<TeamDashboard>(`${this.baseUrl}/${teamId}/dashboard`);
  }

  // ── Meetings ──────────────────────────────────────────────────────────────

  getMeetings(teamId: string) {
    return apiService.get<TeamMeeting[]>(`${this.baseUrl}/${teamId}/meetings`);
  }

  getMeeting(id: string) {
    return apiService.get<TeamMeetingDetail>(`${this.baseUrl}/meetings/${id}`);
  }

  createMeeting(teamId: string, payload: CreateTeamMeetingRequest) {
    return apiService.post<TeamMeetingDetail>(`${this.baseUrl}/${teamId}/meetings`, payload);
  }

  /** ⚠ `attendeeMemberIds` REPLACES the invitee list — omitting somebody removes them. */
  updateMeeting(id: string, payload: UpdateTeamMeetingRequest) {
    return apiService.put<TeamMeetingDetail>(`${this.baseUrl}/meetings/${id}`, payload);
  }

  /** ⚠ Attendance here is NOT a replace set — anyone omitted keeps what was recorded. */
  holdMeeting(id: string, payload: HoldTeamMeetingRequest) {
    return apiService.post<TeamMeetingDetail>(`${this.baseUrl}/meetings/${id}/hold`, payload);
  }

  cancelMeeting(id: string, reason: string) {
    return apiService.post<TeamMeetingDetail>(`${this.baseUrl}/meetings/${id}/cancel`, { reason });
  }

  deleteMeeting(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/meetings/${id}`);
  }

  uploadMinutes(meetingId: string, file: File) {
    return hrDocumentService.upload<unknown>(`${this.docsUrl}/meetings/${meetingId}/minutes`, file);
  }

  minutesUrl(meetingId: string) {
    return `${this.docsUrl}/meetings/${meetingId}/minutes`;
  }

  // ── Decisions ─────────────────────────────────────────────────────────────

  addDecision(meetingId: string, payload: CreateTeamMeetingDecisionRequest) {
    return apiService.post<TeamMeetingDecision>(
      `${this.baseUrl}/meetings/${meetingId}/decisions`, payload);
  }

  updateDecision(id: string, payload: UpdateTeamMeetingDecisionRequest) {
    return apiService.put<TeamMeetingDecision>(`${this.baseUrl}/decisions/${id}`, payload);
  }

  deleteDecision(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/decisions/${id}`);
  }

  /**
   * Turns a decision into a task — the whole reason the minute book is in the system.
   *
   * ⚠ The assignee and the due date come from the DECISION, not from this payload.
   */
  raiseTaskFromDecision(decisionId: string, payload: RaiseTaskFromDecisionRequest) {
    return apiService.post<TeamTaskDetail>(
      `${this.baseUrl}/decisions/${decisionId}/raise-task`, payload);
  }

  // ── Reviews ───────────────────────────────────────────────────────────────

  getReviews(teamId: string) {
    return apiService.get<TeamReview[]>(`${this.baseUrl}/${teamId}/reviews`);
  }

  getReview(id: string) {
    return apiService.get<TeamReviewDetail>(`${this.baseUrl}/reviews/${id}`);
  }

  createReview(teamId: string, payload: CreateTeamReviewRequest) {
    return apiService.post<TeamReviewDetail>(`${this.baseUrl}/${teamId}/reviews`, payload);
  }

  /** Edits a DRAFT. A submitted review is a record and cannot be changed. */
  updateReview(id: string, payload: UpdateTeamReviewRequest) {
    return apiService.put<TeamReviewDetail>(`${this.baseUrl}/reviews/${id}`, payload);
  }

  upsertReviewLine(reviewId: string, payload: UpsertTeamReviewLineRequest) {
    return apiService.put<TeamReviewLine>(`${this.baseUrl}/reviews/${reviewId}/lines`, payload);
  }

  deleteReviewLine(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/review-lines/${id}`);
  }

  submitReview(id: string) {
    return apiService.post<TeamReviewDetail>(`${this.baseUrl}/reviews/${id}/submit`, {});
  }

  /** The lead acknowledges — says it was read, not that they agreed. */
  acknowledgeReview(id: string, payload: AcknowledgeTeamReviewRequest = {}) {
    return apiService.post<TeamReviewDetail>(`${this.baseUrl}/reviews/${id}/acknowledge`, payload);
  }

  deleteReview(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/reviews/${id}`);
  }

  // ── The sweep ─────────────────────────────────────────────────────────────
  //
  // ⚠ HR-gated, unlike everything above: a tenant-wide sweep is an administrative act.

  runReminderSweep() {
    return apiService.post<TeamReminderRunResult>(`${this.baseUrl}/reminders/run`, {});
  }

  /** How anyone answers "did the sweep fire last night?" — the question two dead sweeps failed. */
  getReminderRuns(take = 20) {
    return apiService.get<TeamReminderRun[]>(`${this.baseUrl}/reminders/runs?take=${take}`);
  }

  getReminderDispatches(runId: string) {
    return apiService.get<TeamReminderDispatch[]>(
      `${this.baseUrl}/reminders/runs/${runId}/dispatches`);
  }
}

export const teamMeetingService = new TeamMeetingService();
