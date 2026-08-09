import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type { AppraisalAttachment } from '@/types/hr/calibration';
import type { GoalProgressEntry } from '@/types/hr/goals';
import type {
  AppraisalReviewEvent,
  CompleteReviewEvent,
  CreateAppraisalReviewEvent,
  FinalizeFullInterimAppraisal,
  FullInterimAppraisalContext,
  RecordReviewProgressEntry,
  ReviewEventType,
  SubmitReviewEvent,
  UpdateAppraisalReviewEvent,
} from '@/types/hr/interim-reviews';

/**
 * api/AppraisalReviewEvents — the quarterly / mid-year checkpoints inside an appraisal cycle.
 *
 * Every read and write is gated to HR, the appraisee, or the appraisee's line manager, so nothing
 * here takes an actor id: `submit` is the appraisee's, `complete` and `finalizeFullAppraisal` are
 * the manager's, and `create` / `delete` are HR's. Passing someone else's id is not possible
 * because the server no longer accepts one.
 *
 * ⚠ `getByCycle` is HR-only (it spans the tenant). A manager builds their team view from
 * `getByAppraisal` per report.
 */
class InterimReviewService {
  private readonly baseUrl = '/AppraisalReviewEvents';

  getById(id: string): Promise<AppraisalReviewEvent> {
    return apiService.get<AppraisalReviewEvent>(`${this.baseUrl}/${id}`);
  }

  getByAppraisal(appraisalId: string): Promise<AppraisalReviewEvent[]> {
    return apiService.get<AppraisalReviewEvent[]>(`${this.baseUrl}/by-appraisal/${appraisalId}`);
  }

  /** The signed-in employee's own checkpoints. No employee id needed — it comes from the token. */
  getMine(cycleId?: string): Promise<AppraisalReviewEvent[]> {
    return apiService.get<AppraisalReviewEvent[]>(
      `${this.baseUrl}/mine`,
      cycleId ? { cycleId } : undefined,
    );
  }

  /** Checkpoints for the signed-in manager's direct reports — their review queue. */
  getTeam(cycleId?: string): Promise<AppraisalReviewEvent[]> {
    return apiService.get<AppraisalReviewEvent[]>(
      `${this.baseUrl}/team`,
      cycleId ? { cycleId } : undefined,
    );
  }

  /** HR's org-wide view of a cycle's checkpoints. 403 for anyone else. */
  getByCycle(cycleId: string): Promise<AppraisalReviewEvent[]> {
    return apiService.get<AppraisalReviewEvent[]>(`${this.baseUrl}/by-cycle/${cycleId}`);
  }

  getByType(appraisalId: string, type: ReviewEventType): Promise<AppraisalReviewEvent[]> {
    return apiService.get<AppraisalReviewEvent[]>(
      `${this.baseUrl}/by-appraisal/${appraisalId}/type/${type}`,
    );
  }

  /** HR only. The cycle's own generation covers every frequency except `Custom`. */
  create(data: CreateAppraisalReviewEvent): Promise<AppraisalReviewEvent> {
    return apiService.post<AppraisalReviewEvent>(this.baseUrl, data);
  }

  /** Reschedule or amend. 422 once the event is completed. */
  update(id: string, data: UpdateAppraisalReviewEvent): Promise<AppraisalReviewEvent> {
    return apiService.put<AppraisalReviewEvent>(`${this.baseUrl}/${id}`, data);
  }

  /** HR only. */
  delete(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /**
   * The appraisee's self-assessment. 422 when `requireGoalProgressUpdateAtReview` is on and any
   * live goal still has no progress entry against this event — the message names how many.
   */
  submit(eventId: string, data: SubmitReviewEvent): Promise<AppraisalReviewEvent> {
    return apiService.post<AppraisalReviewEvent>(`${this.baseUrl}/${eventId}/submit`, data);
  }

  /**
   * The manager closes a light-touch review. 422 when `requireMidYearSelfAssessment` is on and the
   * employee has not submitted, or when goal progress is required and missing.
   */
  complete(eventId: string, data: CompleteReviewEvent): Promise<AppraisalReviewEvent> {
    return apiService.post<AppraisalReviewEvent>(`${this.baseUrl}/${eventId}/complete`, data);
  }

  // ── Full interim appraisal ─────────────────────────────────────────────────

  /** The period's goals with their weights, current progress, and any score already recorded. */
  getFullAppraisalContext(eventId: string): Promise<FullInterimAppraisalContext> {
    return apiService.get<FullInterimAppraisalContext>(
      `${this.baseUrl}/${eventId}/full-appraisal-context`,
    );
  }

  /**
   * Scores the period's goals and closes the event with a weighted `overallPeriodScore`.
   *
   * ⚠ 422 when the event is not configured as a full appraisal (`isFullAppraisal` false) or is
   * already completed; 404 when a goal id does not belong to this review's employee and cycle.
   * Scoring also moves each goal's own percent and status.
   */
  finalizeFullAppraisal(
    eventId: string,
    data: FinalizeFullInterimAppraisal,
  ): Promise<AppraisalReviewEvent> {
    return apiService.post<AppraisalReviewEvent>(
      `${this.baseUrl}/${eventId}/finalize-full-appraisal`,
      data,
    );
  }

  // ── Progress entries ───────────────────────────────────────────────────────

  getProgressEntries(eventId: string): Promise<GoalProgressEntry[]> {
    return apiService.get<GoalProgressEntry[]>(`${this.baseUrl}/${eventId}/progress`);
  }

  /** Records progress against one goal *and* moves that goal. 422 once the event is completed. */
  recordProgressEntry(
    eventId: string,
    data: RecordReviewProgressEntry,
  ): Promise<GoalProgressEntry> {
    return apiService.post<GoalProgressEntry>(`${this.baseUrl}/${eventId}/progress`, data);
  }

  // ── Attachments ────────────────────────────────────────────────────────────

  getAttachments(eventId: string): Promise<AppraisalAttachment[]> {
    return apiService.get<AppraisalAttachment[]>(`${this.baseUrl}/${eventId}/attachments`);
  }

  /** Goes through the controlled upload gate; throws an `HrDocumentUploadError` when refused. */
  uploadAttachment(
    eventId: string,
    file: File,
    description?: string | null,
  ): Promise<AppraisalAttachment> {
    return hrDocumentService.upload<AppraisalAttachment>(
      `${this.baseUrl}/${eventId}/attachments`,
      file,
      { description },
    );
  }

  /** Streams the file through the authorizing endpoint — stored paths are not URLs. */
  downloadAttachment(eventId: string, attachment: AppraisalAttachment): Promise<void> {
    return hrDocumentService.download(
      `${this.baseUrl}/${eventId}/attachments/${attachment.id}/download`,
      attachment.fileName,
    );
  }

  /** The uploader, the manager running the review, or HR. */
  deleteAttachment(eventId: string, attachmentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${eventId}/attachments/${attachmentId}`);
  }
}

export const interimReviewService = new InterimReviewService();
