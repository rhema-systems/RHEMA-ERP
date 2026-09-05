import { apiService } from '../api.service';
import type {
  OrientationSession,
  OrientationSessionSummary,
  OrientationSessionCreateRequest,
  OrientationSessionUpdateRequest,
  OrientationSessionStatus,
  ChangeOrientationSessionStatusRequest,
  OrientationSessionFacilitator,
  OrientationSessionFacilitatorCreateRequest,
  OrientationSessionFacilitatorUpdateRequest,
  OrientationAttendanceRecord,
  MarkOrientationAttendanceRequest,
} from '@/types/hr/orientation';

/**
 * Session scheduling, facilitators and the attendance register.
 * Backend route: api/orientation-sessions.
 *
 * HR-only except `getById`, which stays open so an enrolled participant can see their session's
 * time, venue and joining link.
 */
class OrientationSessionService {
  private readonly baseUrl = '/orientation-sessions';

  // ── Sessions ──────────────────────────────────────────────────────────────

  /** Open to any authenticated user. */
  getById(id: string): Promise<OrientationSession> {
    return apiService.get<OrientationSession>(`${this.baseUrl}/${id}`);
  }

  getByCode(sessionCode: string): Promise<OrientationSession | null> {
    return apiService.get<OrientationSession | null>(
      `${this.baseUrl}/code/${encodeURIComponent(sessionCode)}`,
    );
  }

  getByProgram(programId: string): Promise<OrientationSessionSummary[]> {
    return apiService.get<OrientationSessionSummary[]>(`${this.baseUrl}/program/${programId}`);
  }

  getByStatus(status: OrientationSessionStatus): Promise<OrientationSessionSummary[]> {
    return apiService.get<OrientationSessionSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getUpcoming(daysAhead = 30): Promise<OrientationSessionSummary[]> {
    return apiService.get<OrientationSessionSummary[]>(`${this.baseUrl}/upcoming`, { daysAhead });
  }

  getOpenForEnrollment(): Promise<OrientationSessionSummary[]> {
    return apiService.get<OrientationSessionSummary[]>(`${this.baseUrl}/open-for-enrollment`);
  }

  create(data: OrientationSessionCreateRequest): Promise<OrientationSession> {
    return apiService.post<OrientationSession>(this.baseUrl, data);
  }

  update(id: string, data: OrientationSessionUpdateRequest): Promise<OrientationSession> {
    return apiService.put<OrientationSession>(`${this.baseUrl}/${id}`, data);
  }

  /** Moving to InProgress/Completed stamps actualStartAt/actualEndAt server-side if unset. */
  changeStatus(id: string, data: ChangeOrientationSessionStatusRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/status`, data);
  }

  /** Refused with 422 while the session has seat-occupying enrollments — cancel it instead. */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Facilitators ──────────────────────────────────────────────────────────

  getFacilitators(sessionId: string): Promise<OrientationSessionFacilitator[]> {
    return apiService.get<OrientationSessionFacilitator[]>(
      `${this.baseUrl}/${sessionId}/facilitators`,
    );
  }

  /** Requires either an employeeId or an external facilitator name — 422 otherwise. */
  addFacilitator(
    sessionId: string,
    data: OrientationSessionFacilitatorCreateRequest,
  ): Promise<OrientationSessionFacilitator> {
    return apiService.post<OrientationSessionFacilitator>(
      `${this.baseUrl}/${sessionId}/facilitators`,
      data,
    );
  }

  updateFacilitator(
    facilitatorId: string,
    data: OrientationSessionFacilitatorUpdateRequest,
  ): Promise<OrientationSessionFacilitator> {
    return apiService.put<OrientationSessionFacilitator>(
      `${this.baseUrl}/facilitators/${facilitatorId}`,
      data,
    );
  }

  removeFacilitator(facilitatorId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/facilitators/${facilitatorId}`);
  }

  // ── Attendance ────────────────────────────────────────────────────────────

  getAttendanceForSession(sessionId: string): Promise<OrientationAttendanceRecord[]> {
    return apiService.get<OrientationAttendanceRecord[]>(`${this.baseUrl}/${sessionId}/attendance`);
  }

  getAttendanceForEnrollment(enrollmentId: string): Promise<OrientationAttendanceRecord[]> {
    return apiService.get<OrientationAttendanceRecord[]>(
      `${this.baseUrl}/enrollments/${enrollmentId}/attendance`,
    );
  }

  /**
   * Upserts one register day for a set of enrollments and returns the saved rows for that day.
   * Every entry must belong to this session — a mismatch is refused with 422 rather than silently
   * marking attendance against another session's enrollment. `attendedMinutes` is derived from
   * check-in/check-out when both are supplied.
   */
  markAttendance(
    sessionId: string,
    data: MarkOrientationAttendanceRequest,
  ): Promise<OrientationAttendanceRecord[]> {
    return apiService.post<OrientationAttendanceRecord[]>(
      `${this.baseUrl}/${sessionId}/attendance`,
      data,
    );
  }
}

export const orientationSessionService = new OrientationSessionService();
export default orientationSessionService;
