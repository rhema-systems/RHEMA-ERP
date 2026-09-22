// api/talent-pool — recruitment's candidate CRM. ⚠ Distinct from succession's api/talent-pools
// (plural), which serves employee pools; the two controllers share nothing but a name.
//
// Kept out of recruitment-pipeline.service.ts deliberately: that file is 745 lines and already
// carries the candidate CRUD; this is the succession.service.ts precedent of a sibling service.

import { apiService } from '@/services/api.service';
import type {
  AddToTalentPoolPayload,
  BulkTalentPoolPayload,
  CandidateEngagementEvent,
  CandidateSegmentMembership,
  CandidateTalentSegment,
  CandidateTalentSegmentForm,
  CandidateVacancyMatch,
  LogEngagementEventForm,
  RemoveFromTalentPoolPayload,
  TalentPoolAnalytics,
  TalentPoolBulkResult,
  TalentPoolCandidate,
  TalentPoolFilter,
  TalentPoolPagedResult,
  TalentPoolBookInterviewPayload,
  TalentPoolInviteToApplyPayload,
  TalentPoolScreenRequest,
  TalentPoolScreenResult,
  TalentPoolStatus,
  TalentPoolVacancyMatch,
} from '@/types/hr/talent-pool';

class TalentPoolService {
  private readonly baseUrl = '/talent-pool';

  // ── pool candidates ──────────────────────────────────────────────────────

  getCandidates(filter: TalentPoolFilter = {}): Promise<TalentPoolPagedResult> {
    // ⚠ Never send sortBy unset or "LastName" — the server's sort keys are lowercase
    // (fullname|dateadded|lastengaged|reviewdate|experience) and anything else silently falls
    // to the default arm.
    return apiService.get<TalentPoolPagedResult>(`${this.baseUrl}/candidates`, {
      ...filter,
      sortBy: filter.sortBy ?? 'fullname',
    } as Record<string, unknown>);
  }

  getCandidate(candidateId: string): Promise<TalentPoolCandidate> {
    return apiService.get<TalentPoolCandidate>(`${this.baseUrl}/candidates/${candidateId}`);
  }

  addToPool(candidateId: string, payload: AddToTalentPoolPayload): Promise<boolean> {
    return apiService.post<boolean>(`${this.baseUrl}/candidates/${candidateId}/add`, payload);
  }

  removeFromPool(candidateId: string, payload: RemoveFromTalentPoolPayload): Promise<boolean> {
    return apiService.post<boolean>(`${this.baseUrl}/candidates/${candidateId}/remove`, payload);
  }

  updateStatus(candidateId: string, status: TalentPoolStatus): Promise<boolean> {
    return apiService.patch<boolean>(`${this.baseUrl}/candidates/${candidateId}/status`, { status });
  }

  updateReviewDate(candidateId: string, reviewDate: string): Promise<boolean> {
    return apiService.patch<boolean>(`${this.baseUrl}/candidates/${candidateId}/review-date`, { reviewDate });
  }

  bulk(payload: BulkTalentPoolPayload): Promise<TalentPoolBulkResult> {
    return apiService.post<TalentPoolBulkResult>(`${this.baseUrl}/bulk`, payload);
  }

  getAnalytics(): Promise<TalentPoolAnalytics> {
    return apiService.get<TalentPoolAnalytics>(`${this.baseUrl}/analytics`);
  }

  // ── vacancy matching ─────────────────────────────────────────────────────

  matchToVacancy(vacancyId: string, topN = 20): Promise<TalentPoolVacancyMatch[]> {
    return apiService.get<TalentPoolVacancyMatch[]>(`${this.baseUrl}/match/${vacancyId}`, { topN });
  }

  matchCandidateToVacancies(candidateId: string, topN = 10): Promise<CandidateVacancyMatch[]> {
    return apiService.get<CandidateVacancyMatch[]>(
      `${this.baseUrl}/candidates/${candidateId}/match-vacancies`,
      { topN },
    );
  }

  // -- screening by the vacancy's real criteria, and acting on it (round 4, lane B) -------------

  /**
   * Scores pooled candidates against a vacancy's own shortlisting criteria, through the same
   * engine that scores applications.
   *
   * WARNING: POST, though it writes nothing. It carries a filter (and, on the ad-hoc door, a whole
   * criteria set), which is a body rather than a query string.
   *
   * WARNING: refuses with 400 when the vacancy has no criteria. That is the useful answer - the
   * alternative is a full table of dashes reading as "the pool is useless".
   */
  screenAgainstVacancy(vacancyId: string, request: TalentPoolScreenRequest = {}): Promise<TalentPoolScreenResult> {
    return apiService.post<TalentPoolScreenResult>(`${this.baseUrl}/screen/${vacancyId}`, request);
  }

  /** "Who do we have who could do this?", asked before any vacancy exists. */
  screenAdHoc(request: TalentPoolScreenRequest): Promise<TalentPoolScreenResult> {
    return apiService.post<TalentPoolScreenResult>(`${this.baseUrl}/screen`, request);
  }

  /** Opens an application for each candidate and emails them. Partial - read every row's message. */
  inviteToApply(payload: TalentPoolInviteToApplyPayload): Promise<TalentPoolBulkResult> {
    return apiService.post<TalentPoolBulkResult>(`${this.baseUrl}/invite-to-apply`, {
      sendEmail: true,
      ...payload,
    });
  }

  /**
   * Books each candidate into an existing interview session.
   *
   * WARNING: requires an application against that interview's vacancy - invite first. A candidate
   * without one comes back as a skipped row saying so, not as a failure of the whole call.
   */
  bookForInterview(payload: TalentPoolBookInterviewPayload): Promise<TalentPoolBulkResult> {
    return apiService.post<TalentPoolBulkResult>(`${this.baseUrl}/book-interview`, payload);
  }

  // ── segments ─────────────────────────────────────────────────────────────

  getSegments(): Promise<CandidateTalentSegment[]> {
    return apiService.get<CandidateTalentSegment[]>(`${this.baseUrl}/segments`);
  }

  getAllSegments(): Promise<CandidateTalentSegment[]> {
    return apiService.get<CandidateTalentSegment[]>(`${this.baseUrl}/segments/all`);
  }

  createSegment(payload: CandidateTalentSegmentForm): Promise<CandidateTalentSegment> {
    const { isActive: _ignored, ...create } = payload;
    return apiService.post<CandidateTalentSegment>(`${this.baseUrl}/segments`, create);
  }

  updateSegment(segmentId: string, payload: CandidateTalentSegmentForm): Promise<CandidateTalentSegment> {
    return apiService.put<CandidateTalentSegment>(`${this.baseUrl}/segments/${segmentId}`, {
      ...payload,
      isActive: payload.isActive ?? true,
      id: segmentId,
    });
  }

  deleteSegment(segmentId: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/segments/${segmentId}`);
  }

  getCandidateSegments(candidateId: string): Promise<CandidateSegmentMembership[]> {
    return apiService.get<CandidateSegmentMembership[]>(`${this.baseUrl}/candidates/${candidateId}/segments`);
  }

  addToSegment(candidateId: string, segmentId: string, notes?: string | null): Promise<CandidateSegmentMembership> {
    return apiService.post<CandidateSegmentMembership>(`${this.baseUrl}/candidates/${candidateId}/segments`, {
      segmentId,
      notes: notes ?? null,
    });
  }

  removeFromSegment(candidateId: string, segmentId: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/candidates/${candidateId}/segments/${segmentId}`);
  }

  // ── engagement events ────────────────────────────────────────────────────

  getEvents(candidateId: string): Promise<CandidateEngagementEvent[]> {
    return apiService.get<CandidateEngagementEvent[]>(`${this.baseUrl}/candidates/${candidateId}/events`);
  }

  logEvent(candidateId: string, payload: LogEngagementEventForm): Promise<CandidateEngagementEvent> {
    // The server overwrites any jobCandidateId in the body with the route's — route wins.
    return apiService.post<CandidateEngagementEvent>(`${this.baseUrl}/candidates/${candidateId}/events`, {
      ...payload,
      jobCandidateId: candidateId,
    });
  }

  deleteEvent(eventId: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/events/${eventId}`);
  }
}

export const talentPoolService = new TalentPoolService();
