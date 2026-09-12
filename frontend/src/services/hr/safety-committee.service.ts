import { apiService } from '../api.service';
import type {
  SafetyCommittee,
  SafetyCommitteeCreateRequest,
  SafetyCommitteeUpdateRequest,
  SafetyCommitteeMember,
  SafetyCommitteeMemberCreateRequest,
  SafetyCommitteeMemberUpdateRequest,
  SafetyMeeting,
  SafetyMeetingSummary,
  SafetyMeetingCreateRequest,
  SafetyMeetingUpdateRequest,
  SafetyMeetingAttendee,
  SafetyMeetingAttendeeCreateRequest,
  SafetyMeetingActionItem,
  SafetyMeetingActionItemCreateRequest,
  SafetyMeetingActionItemUpdateRequest,
  SafetyMeetingDocument,
  SafetyMeetingDocumentCreateRequest,
} from '@/types/hr/safety-governance';

/**
 * Safety committees, meetings, attendees, action items and meeting documents.
 * Backend route: api/safety/committees. (Committees are a repo extra — they appear
 * in no spec document; kept per the area-10 plan.)
 *
 * Meeting numbers are user-entered and unique per tenant (duplicate → 422); a repeat
 * attendee on the same meeting and a second ACTIVE membership row for the same person
 * are refused the same way. Minutes/decisions are debrief fields recorded via update.
 */
class SafetyCommitteeService {
  private readonly baseUrl = '/safety/committees';

  // ── Committees ─────────────────────────────────────────────────────────────

  getCommittee(id: string): Promise<SafetyCommittee> {
    return apiService.get<SafetyCommittee>(`${this.baseUrl}/${id}`);
  }

  getActiveCommittees(): Promise<SafetyCommittee[]> {
    return apiService.get<SafetyCommittee[]>(`${this.baseUrl}/active`);
  }

  createCommittee(data: SafetyCommitteeCreateRequest): Promise<SafetyCommittee> {
    return apiService.post<SafetyCommittee>(this.baseUrl, data);
  }

  updateCommittee(id: string, data: SafetyCommitteeUpdateRequest): Promise<SafetyCommittee> {
    return apiService.put<SafetyCommittee>(`${this.baseUrl}/${id}`, data);
  }

  removeCommittee(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Members ────────────────────────────────────────────────────────────────

  getMembers(committeeId: string): Promise<SafetyCommitteeMember[]> {
    return apiService.get<SafetyCommitteeMember[]>(`${this.baseUrl}/${committeeId}/members`);
  }

  /** Refused (422) when the employee already has an ACTIVE membership on this committee. */
  addMember(committeeId: string, data: SafetyCommitteeMemberCreateRequest): Promise<SafetyCommitteeMember> {
    return apiService.post<SafetyCommitteeMember>(`${this.baseUrl}/${committeeId}/members`, data);
  }

  updateMember(memberId: string, data: SafetyCommitteeMemberUpdateRequest): Promise<SafetyCommitteeMember> {
    return apiService.put<SafetyCommitteeMember>(`${this.baseUrl}/members/${memberId}`, data);
  }

  removeMember(memberId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/members/${memberId}`);
  }

  // ── Meetings ───────────────────────────────────────────────────────────────

  getMeeting(meetingId: string): Promise<SafetyMeeting> {
    return apiService.get<SafetyMeeting>(`${this.baseUrl}/meetings/${meetingId}`);
  }

  getMeetingsByCommittee(committeeId: string): Promise<SafetyMeetingSummary[]> {
    return apiService.get<SafetyMeetingSummary[]>(`${this.baseUrl}/${committeeId}/meetings`);
  }

  getMeetingsByDateRange(from: string, to: string): Promise<SafetyMeetingSummary[]> {
    return apiService.get<SafetyMeetingSummary[]>(`${this.baseUrl}/meetings/date-range`, { from, to });
  }

  /** Refused (422) when the meeting number is already taken. */
  createMeeting(data: SafetyMeetingCreateRequest): Promise<SafetyMeeting> {
    return apiService.post<SafetyMeeting>(`${this.baseUrl}/meetings`, data);
  }

  updateMeeting(meetingId: string, data: SafetyMeetingUpdateRequest): Promise<SafetyMeeting> {
    return apiService.put<SafetyMeeting>(`${this.baseUrl}/meetings/${meetingId}`, data);
  }

  removeMeeting(meetingId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/meetings/${meetingId}`);
  }

  // ── Attendees ──────────────────────────────────────────────────────────────

  /** Refused (422) when the employee is already recorded on this meeting. */
  addAttendee(meetingId: string, data: SafetyMeetingAttendeeCreateRequest): Promise<SafetyMeetingAttendee> {
    return apiService.post<SafetyMeetingAttendee>(`${this.baseUrl}/meetings/${meetingId}/attendees`, data);
  }

  removeAttendee(attendeeId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/attendees/${attendeeId}`);
  }

  // ── Action items ───────────────────────────────────────────────────────────

  getOpenActionItems(): Promise<SafetyMeetingActionItem[]> {
    return apiService.get<SafetyMeetingActionItem[]>(`${this.baseUrl}/action-items/open`);
  }

  getOverdueActionItems(): Promise<SafetyMeetingActionItem[]> {
    return apiService.get<SafetyMeetingActionItem[]>(`${this.baseUrl}/action-items/overdue`);
  }

  getActionItemsByAssignee(employeeId: string): Promise<SafetyMeetingActionItem[]> {
    return apiService.get<SafetyMeetingActionItem[]>(
      `${this.baseUrl}/action-items/by-assignee/${employeeId}`,
    );
  }

  addActionItem(
    meetingId: string,
    data: SafetyMeetingActionItemCreateRequest,
  ): Promise<SafetyMeetingActionItem> {
    return apiService.post<SafetyMeetingActionItem>(
      `${this.baseUrl}/meetings/${meetingId}/action-items`,
      data,
    );
  }

  updateActionItem(
    actionItemId: string,
    data: SafetyMeetingActionItemUpdateRequest,
  ): Promise<SafetyMeetingActionItem> {
    return apiService.put<SafetyMeetingActionItem>(
      `${this.baseUrl}/action-items/${actionItemId}`,
      data,
    );
  }

  removeActionItem(actionItemId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/action-items/${actionItemId}`);
  }

  // ── Documents ──────────────────────────────────────────────────────────────

  addMeetingDocument(
    meetingId: string,
    data: SafetyMeetingDocumentCreateRequest,
  ): Promise<SafetyMeetingDocument> {
    return apiService.post<SafetyMeetingDocument>(
      `${this.baseUrl}/meetings/${meetingId}/documents`,
      data,
    );
  }

  removeMeetingDocument(documentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/documents/${documentId}`);
  }
}

export const safetyCommitteeService = new SafetyCommitteeService();
export default safetyCommitteeService;
