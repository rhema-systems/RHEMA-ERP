import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  AppraisalAttachment,
  CalibrationApplyResult,
  CalibrationCriterion,
  CalibrationMatrix,
  CalibrationParticipant,
  CalibrationRatingAdjustment,
  CalibrationSession,
  CompleteCalibrationSession,
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
 * Reads are HR's, and a panellist's for the sessions they sit on — a participant or the
 * facilitator (performance closure P3); anyone else gets 403, and the list reads return only
 * the caller's own sessions. A panellist's own appraisal is left out of every read. Every write
 * needs an HR role.
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

  /**
   * Particulars only — status and the lifecycle stamps are owned by the actions below. The scope
   * (cycle, unit, level) changes only while the session is Pending: 422 once it is open. 422 on a
   * completed or cancelled session.
   */
  update(id: string, data: UpdateCalibrationSession): Promise<CalibrationSession> {
    return apiService.put<CalibrationSession>(`${this.baseUrl}/${id}`, data);
  }

  /** Admin only. 422 once the session is completed; the appraisals it holds are released. */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Lifecycle ────────────────────────────────────────────────────────────────────

  /**
   * Pending → InProgress. Records the caller as facilitator, stamps the start and links every
   * appraisal in scope waiting for calibration to the session, so their computed phase reads
   * "calibration in progress". (There is no separate start any more.)
   */
  open(id: string): Promise<CalibrationSession> {
    return apiService.post<CalibrationSession>(`${this.baseUrl}/${id}/open`);
  }

  /** Closes the room and notifies the panel. Adjustments are refused afterwards. */
  complete(id: string, data: CompleteCalibrationSession = {}): Promise<CalibrationSession> {
    return apiService.post<CalibrationSession>(`${this.baseUrl}/${id}/complete`, data);
  }

  /**
   * Calls off a Pending or InProgress session, with a reason (kept at the head of its meeting
   * notes). The appraisals it holds are released for the next session; nothing of it is applied.
   * 422 on a completed or cancelled session, or with a blank reason.
   */
  cancel(id: string, reason: string): Promise<CalibrationSession> {
    return apiService.post<CalibrationSession>(`${this.baseUrl}/${id}/cancel`, { reason });
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

  /**
   * A new score and rationale. The body's appraisal and criterion must be the adjustment's own —
   * 422 otherwise (an adjustment stays on what it restated; remove it and record a new one).
   */
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

  /** 422 unless the session is in progress — as recording and changing one are. */
  removeAdjustment(sessionId: string, adjustmentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${sessionId}/adjustments/${adjustmentId}`);
  }

  /**
   * Commits the session — irreversible. Writes the agreed ratings onto the appraisals and lifts
   * the calibration gate on everyone in scope at the step, adjusted or not; each grid row's
   * `commitSkipReason` says in advance why one would be left alone. Once per appraisal: run again,
   * it skips what it calibrated and anything whose manager submitted after the panel closed.
   * Only from Completed.
   */
  applyAdjustments(sessionId: string): Promise<CalibrationApplyResult> {
    return apiService.post<CalibrationApplyResult>(`${this.baseUrl}/${sessionId}/apply-adjustments`);
  }

  // ── Grid & attachments ───────────────────────────────────────────────────────────

  /** Every appraisal the session covers, not only the adjusted ones. */
  getMatrix(sessionId: string): Promise<CalibrationMatrix> {
    return apiService.get<CalibrationMatrix>(`${this.baseUrl}/${sessionId}/matrix`);
  }

  /**
   * The appraisal's frozen criteria with the manager's score and any adjustment already made —
   * what a per-criterion calibration needs. A read, so panellists get it, not just HR — for an
   * appraisal in the session's scope only, and never their own (404 otherwise, P3).
   */
  getAppraisalCriteria(sessionId: string, appraisalId: string): Promise<CalibrationCriterion[]> {
    return apiService.get<CalibrationCriterion[]>(
      `${this.baseUrl}/${sessionId}/appraisals/${appraisalId}/criteria`,
    );
  }

  getAttachments(sessionId: string): Promise<AppraisalAttachment[]> {
    return apiService.get<AppraisalAttachment[]>(`${this.baseUrl}/${sessionId}/attachments`);
  }

  /**
   * Goes through the controlled upload gate (scan + DMS registration); throws an
   * `HrDocumentUploadError` when refused. HR only.
   *
   * This was a JSON post carrying a caller-invented `filePath`, which could never have worked —
   * the attachment row's uploader is a required FK the payload had no way to supply.
   */
  uploadAttachment(
    sessionId: string,
    file: File,
    description?: string | null,
  ): Promise<AppraisalAttachment> {
    return hrDocumentService.upload<AppraisalAttachment>(
      `${this.baseUrl}/${sessionId}/attachments`,
      file,
      { description },
    );
  }

  /** Streams the file through the authorizing endpoint — stored paths are not URLs. */
  downloadAttachment(sessionId: string, attachment: AppraisalAttachment): Promise<void> {
    return hrDocumentService.download(
      `${this.baseUrl}/${sessionId}/attachments/${attachment.id}/download`,
      attachment.fileName,
    );
  }

  removeAttachment(sessionId: string, attachmentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${sessionId}/attachments/${attachmentId}`);
  }
}

export const calibrationSessionService = new CalibrationSessionService();
