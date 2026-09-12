import { apiService } from '../api.service';
import type {
  MentoringProgram,
  MentoringProgramSummary,
  MentoringProgramCreate,
  MentoringProgramUpdate,
  MentoringPair,
  MentoringPairSummary,
  MentoringPairCreate,
  MentoringPairUpdate,
  MentoringSession,
  MentoringSessionCreate,
  MentoringSessionUpdate,
} from '@/types/hr/mentoring';

/**
 * Mentoring programmes, pairs and session logs. Backend route: api/mentoring.
 *
 * Pair and session reads are restricted server-side to the mentor, the mentee, the programme
 * coordinator and HR — anyone else gets a 403 carrying the reason. Within that, the private note and
 * rating fields are filtered to their author, so two people looking at the same session legitimately
 * see different objects.
 */
class MentoringService {
  private readonly baseUrl = '/mentoring';

  // ── Programmes ──────────────────────────────────────────────────────────────
  getAllPrograms(): Promise<MentoringProgramSummary[]> {
    return apiService.get<MentoringProgramSummary[]>(`${this.baseUrl}/programs`);
  }

  getActivePrograms(): Promise<MentoringProgramSummary[]> {
    return apiService.get<MentoringProgramSummary[]>(`${this.baseUrl}/programs/active`);
  }

  getProgramById(id: string): Promise<MentoringProgram> {
    return apiService.get<MentoringProgram>(`${this.baseUrl}/programs/${id}`);
  }

  createProgram(data: MentoringProgramCreate): Promise<MentoringProgram> {
    return apiService.post<MentoringProgram>(`${this.baseUrl}/programs`, data);
  }

  updateProgram(id: string, data: MentoringProgramUpdate): Promise<MentoringProgram> {
    return apiService.put<MentoringProgram>(`${this.baseUrl}/programs/${id}`, { id, ...data });
  }

  deleteProgram(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/programs/${id}`);
  }

  // ── Pairs ───────────────────────────────────────────────────────────────────
  getPairById(id: string): Promise<MentoringPair> {
    return apiService.get<MentoringPair>(`${this.baseUrl}/pairs/${id}`);
  }

  /** The caller's own pairs, on both sides — token-derived, no employee id on the wire. */
  getMyPairs(): Promise<MentoringPairSummary[]> {
    return apiService.get<MentoringPairSummary[]>(`${this.baseUrl}/pairs/mine`);
  }

  getActivePairs(): Promise<MentoringPairSummary[]> {
    return apiService.get<MentoringPairSummary[]>(`${this.baseUrl}/pairs/active`);
  }

  getPairsForProgram(programId: string): Promise<MentoringPairSummary[]> {
    return apiService.get<MentoringPairSummary[]>(`${this.baseUrl}/programs/${programId}/pairs`);
  }

  getPairsForEmployee(employeeId: string): Promise<MentoringPairSummary[]> {
    return apiService.get<MentoringPairSummary[]>(`${this.baseUrl}/pairs/employee/${employeeId}`);
  }

  createPair(data: MentoringPairCreate): Promise<MentoringPair> {
    return apiService.post<MentoringPair>(`${this.baseUrl}/pairs`, data);
  }

  /**
   * ⚠ Send back the ratings you were given, including nulls. The server preserves the absent party's
   * rating rather than letting your null overwrite it — but never present a null rating as an empty
   * input the user can "fill in", because it is somebody else's score, not a missing one.
   */
  updatePair(id: string, data: MentoringPairUpdate): Promise<MentoringPair> {
    return apiService.put<MentoringPair>(`${this.baseUrl}/pairs/${id}`, { id, ...data });
  }

  /** Returns the closed pair, so the caller can render the new status without refetching. */
  closePair(id: string, closureNotes: string): Promise<MentoringPair> {
    return apiService.post<MentoringPair>(`${this.baseUrl}/pairs/${id}/close`, { closureNotes });
  }

  // ── Sessions ────────────────────────────────────────────────────────────────
  getSessionsForPair(pairId: string): Promise<MentoringSession[]> {
    return apiService.get<MentoringSession[]>(`${this.baseUrl}/pairs/${pairId}/sessions`);
  }

  getSessionById(id: string): Promise<MentoringSession> {
    return apiService.get<MentoringSession>(`${this.baseUrl}/sessions/${id}`);
  }

  logSession(data: MentoringSessionCreate): Promise<MentoringSession> {
    return apiService.post<MentoringSession>(`${this.baseUrl}/sessions`, data);
  }

  updateSession(id: string, data: MentoringSessionUpdate): Promise<MentoringSession> {
    return apiService.put<MentoringSession>(`${this.baseUrl}/sessions/${id}`, { id, ...data });
  }

  deleteSession(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/sessions/${id}`);
  }
}

export const mentoringService = new MentoringService();
