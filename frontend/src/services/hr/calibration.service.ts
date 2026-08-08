import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  AppraisalAttachment,
  CalibrationApplyResult,
  CalibrationMatrix,
  CalibrationParticipant,
  CalibrationRatingAdjustment,
  CalibrationSession,
  CompleteCalibrationSession,
  CreateAppraisalAttachment,
  CreateCalibrationParticipant,
  CreateCalibrationRatingAdjustment,
  CreateCalibrationSession,
  UpdateCalibrationRatingAdjustment,
  UpdateCalibrationSession,
} from '@/types/hr/calibration';

/**
 * api/CalibrationSessions — the panel that reconciles ratings before HR sign-off.
 *
 * **The actor is never sent.** Opening, completing, adjusting and committing all take the acting
 * employee from the token. The ported routes carried it as a path segment; those are gone.
 *
 * **Business rules answer 422** with `{ message }` — wrong lifecycle state, an appraisal outside
 * the session's scope, a session covering nobody. `apiService` surfaces `.message`, so those are
 * safe to show verbatim. 404 means not found *or* not yours.
 *
 * Reads work for any authenticated user; every write needs an HR role.
 */
class CalibrationSessionService {
  private readonly baseUrl = '/CalibrationSessions';

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<CalibrationSession>> {
    return apiService.get<PagedResult<CalibrationSession>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<CalibrationSession> {
    return apiService.get<CalibrationSession>(`${this.baseUrl}/${id}`);
  }

  getByCycle(cycleId: string): Promise<CalibrationSession[]> {
    return apiService.get<CalibrationSession[]>(`${this.baseUrl}/by-cycle/${cycleId}`);
  }

  create(data: CreateCalibrationSession): Promise<CalibrationSession> {
    return apiService.post<CalibrationSession>(this.baseUrl, data);
  }

  /** Particulars only — status and the lifecycle stamps are owned by the actions below. */
  update(id: string, data: UpdateCalibrationSession): Promise<CalibrationSession> {
    return apiService.put<CalibrationSession>(`${this.baseUrl}/${id}`, data);
  }

  /** 422 once the session is completed. */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Lifecycle ────────────────────────────────────────────────────────────────────

  /**
   * Pending → InProgress. Records the caller as facilitator and links every appraisal in scope
   * to the session, so their computed phase reads "calibration in progress".
   */
  open(id: string): Promise<CalibrationSession> {
    return apiService.post<CalibrationSession>(`${this.baseUrl}/${id}/open`);
  }

  /** Stamps the session as having convened. Only from InProgress. */
  start(id: string): Promise<CalibrationSession> {
    return apiService.post<CalibrationSession>(`${this.baseUrl}/${id}/start`);
  }

  /** Closes the room and notifies the panel. Adjustments are refused afterwards. */
  complete(id: string, data: CompleteCalibrationSession = {}): Promise<CalibrationSession> {
    return apiService.post<CalibrationSession>(`${this.baseUrl}/${id}/complete`, data);
  }

  // ── Participants ─────────────────────────────────────────────────────────────────

  getParticipants(sessionId: string): Promise<CalibrationParticipant[]> {
    return apiService.get<CalibrationParticipant[]>(`${this.baseUrl}/${sessionId}/participants`);
  }

  /** 422 if the employee is already on the panel. */
  addParticipant(
    sessionId: string,
    data: CreateCalibrationParticipant,
  ): Promise<CalibrationParticipant> {
    return apiService.post<CalibrationParticipant>(
      `${this.baseUrl}/${sessionId}/participants`,
      data,
    );
  }

  removeParticipant(sessionId: string, participantId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${sessionId}/participants/${participantId}`);
  }

  recordAttendance(sessionId: string, participantId: string, attended: boolean): Promise<void> {
    return apiService.patch<void>(
      `${this.baseUrl}/${sessionId}/participants/${participantId}/attendance`,
      { attended },
    );
  }

  // ── Rating adjustments ───────────────────────────────────────────────────────────

  getAdjustments(sessionId: string): Promise<CalibrationRatingAdjustment[]> {
    return apiService.get<CalibrationRatingAdjustment[]>(`${this.baseUrl}/${sessionId}/adjustments`);
  }

  getAdjustmentsForAppraisal(
    sessionId: string,
    appraisalId: string,
  ): Promise<CalibrationRatingAdjustment[]> {
    return apiService.get<CalibrationRatingAdjustment[]>(
      `${this.baseUrl}/${sessionId}/adjustments/appraisal/${appraisalId}`,
    );
  }

  /**
   * Records a panel decision. Omit `templateItemId` to restate the overall score; supply one to
   * move a single criterion. 422 while the session is Pending, Completed or Cancelled, and 422
   * if the appraisal is outside the session's scope.
   */
  addAdjustment(
    sessionId: string,
    data: CreateCalibrationRatingAdjustment,
  ): Promise<CalibrationRatingAdjustment> {
    return apiService.post<CalibrationRatingAdjustment>(
      `${this.baseUrl}/${sessionId}/adjustments`,
      data,
    );
  }

  updateAdjustment(
    sessionId: string,
    adjustmentId: string,
    data: UpdateCalibrationRatingAdjustment,
  ): Promise<CalibrationRatingAdjustment> {
    return apiService.put<CalibrationRatingAdjustment>(
      `${this.baseUrl}/${sessionId}/adjustments/${adjustmentId}`,
      data,
    );
  }

  removeAdjustment(sessionId: string, adjustmentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${sessionId}/adjustments/${adjustmentId}`);
  }

  /**
   * Commits the session — irreversible. Writes the agreed ratings onto the appraisals and lifts
   * the calibration gate on everyone in scope, adjusted or not. Only from Completed.
   */
  applyAdjustments(sessionId: string): Promise<CalibrationApplyResult> {
    return apiService.post<CalibrationApplyResult>(`${this.baseUrl}/${sessionId}/apply-adjustments`);
  }

  // ── Grid & attachments ───────────────────────────────────────────────────────────

  /** Every appraisal the session covers, not only the adjusted ones. */
  getMatrix(sessionId: string): Promise<CalibrationMatrix> {
    return apiService.get<CalibrationMatrix>(`${this.baseUrl}/${sessionId}/matrix`);
  }

  getAttachments(sessionId: string): Promise<AppraisalAttachment[]> {
    return apiService.get<AppraisalAttachment[]>(`${this.baseUrl}/${sessionId}/attachments`);
  }

  addAttachment(sessionId: string, data: CreateAppraisalAttachment): Promise<AppraisalAttachment> {
    return apiService.post<AppraisalAttachment>(`${this.baseUrl}/${sessionId}/attachments`, data);
  }

  removeAttachment(sessionId: string, attachmentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${sessionId}/attachments/${attachmentId}`);
  }
}

export const calibrationSessionService = new CalibrationSessionService();
