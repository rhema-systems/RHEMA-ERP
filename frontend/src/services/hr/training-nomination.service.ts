import { apiService } from '../api.service';
import type {
  TrainingNomination,
  TrainingNominationSummary,
  TrainingNominationRequest,
  BulkNominationRequest,
  BulkNominationResult,
  ApproveNominationRequest,
  RejectNominationRequest,
  NominationStatus,
  TrainingAttendance,
  MarkAttendanceRequest,
  AttendanceEntry,
  TrainingFeedback,
  SubmitTrainingFeedbackRequest,
  TrainingFollowUpAssessment,
  SubmitFollowUpAssessmentRequest,
} from '@/types/hr/training-delivery';

/**
 * Nominations (who attends a scheduled run) and the delivery records hung off the same controller:
 * attendance, feedback and follow-up assessments. Backend route: api/training-nominations.
 *
 * Nomination approval runs on the generic workflow engine, so submit/approve move the record through
 * whatever definition is published for the TrainingNomination entity type.
 */
class TrainingNominationService {
  private readonly baseUrl = '/training-nominations';

  getById(id: string): Promise<TrainingNomination> {
    return apiService.get<TrainingNomination>(`${this.baseUrl}/${id}`);
  }

  getBySchedule(scheduleId: string): Promise<TrainingNominationSummary[]> {
    return apiService.get<TrainingNominationSummary[]>(`${this.baseUrl}/schedule/${scheduleId}`);
  }

  /** The caller's own nominations — token-derived, so a self-service screen needs no employee id. */
  /**
   * The feedback this employee has given, newest first.
   *
   * ⚠ Added in slice 14. Until then the ONLY feedback read was
   * `schedule/{id}/feedback` — HR's aggregate over everybody on a course, which answers 403 for a
   * trainee. So feedback could be filed and never seen again, and a form had no way to know it
   * had already been answered. That was the actual blocker on this form from slice 6, not the
   * screen work.
   */
  getMyFeedback(): Promise<TrainingFeedback[]> {
    return apiService.get<TrainingFeedback[]>(`${this.baseUrl}/feedback/mine`);
  }

  getMine(): Promise<TrainingNominationSummary[]> {
    return apiService.get<TrainingNominationSummary[]>(`${this.baseUrl}/mine`);
  }

  getByEmployee(employeeId: string): Promise<TrainingNominationSummary[]> {
    return apiService.get<TrainingNominationSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getByStatus(status: NominationStatus): Promise<TrainingNominationSummary[]> {
    return apiService.get<TrainingNominationSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getPendingSupervisor(): Promise<TrainingNominationSummary[]> {
    return apiService.get<TrainingNominationSummary[]>(`${this.baseUrl}/pending-supervisor`);
  }

  getPendingHr(): Promise<TrainingNominationSummary[]> {
    return apiService.get<TrainingNominationSummary[]>(`${this.baseUrl}/pending-hr`);
  }

  create(data: TrainingNominationRequest): Promise<TrainingNomination> {
    return apiService.post<TrainingNomination>(this.baseUrl, data);
  }

  /** Skipped employees come back with a per-row reason — surface them rather than only a count. */
  bulkCreate(data: BulkNominationRequest): Promise<BulkNominationResult> {
    return apiService.post<BulkNominationResult>(`${this.baseUrl}/bulk`, data);
  }

  update(id: string, data: TrainingNominationRequest): Promise<TrainingNomination> {
    return apiService.put<TrainingNomination>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  submit(id: string): Promise<TrainingNomination> {
    return apiService.post<TrainingNomination>(`${this.baseUrl}/${id}/submit`, {});
  }

  approve(id: string, data: Omit<ApproveNominationRequest, 'nominationId'>): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/approve`, { nominationId: id, ...data });
  }

  reject(id: string, data: Omit<RejectNominationRequest, 'nominationId'>): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/reject`, { nominationId: id, ...data });
  }

  withdraw(id: string, reason?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/withdraw`, { nominationId: id, reason: reason || null });
  }

  // ── Attendance ──────────────────────────────────────────────────────────────
  getAttendance(scheduleId: string): Promise<TrainingAttendance[]> {
    return apiService.get<TrainingAttendance[]>(`${this.baseUrl}/schedule/${scheduleId}/attendance`);
  }

  getAttendanceByDate(scheduleId: string, date: string): Promise<TrainingAttendance[]> {
    return apiService.get<TrainingAttendance[]>(`${this.baseUrl}/schedule/${scheduleId}/attendance/by-date`, { date });
  }

  markAttendance(data: MarkAttendanceRequest): Promise<TrainingAttendance> {
    return apiService.post<TrainingAttendance>(`${this.baseUrl}/attendance`, data);
  }

  /**
   * Marks a whole register in one call. The entries are narrower than a single mark — one date
   * applies to all of them, and the marker comes from the token — so this takes AttendanceEntry,
   * not MarkAttendanceRequest.
   */
  bulkMarkAttendance(
    scheduleId: string,
    attendanceDate: string,
    entries: AttendanceEntry[],
  ): Promise<TrainingAttendance[]> {
    return apiService.post<TrainingAttendance[]>(`${this.baseUrl}/attendance/bulk`, {
      scheduleId,
      attendanceDate,
      entries,
    });
  }

  // ── Feedback ────────────────────────────────────────────────────────────────
  getFeedback(scheduleId: string): Promise<TrainingFeedback[]> {
    return apiService.get<TrainingFeedback[]>(`${this.baseUrl}/schedule/${scheduleId}/feedback`);
  }

  /** trainerKnowledgeRating is what feeds the trainer's running average rating. */
  /**
   * ⚠ ONE PER COURSE, enforced server-side since area 25 slice 14 — a second attempt is a 422
   * saying so. That guard matters more than it looks: the trainer's rating average is credited on
   * EVERY submission, so before it one attendee could move a trainer's score by submitting twice.
   * Call `getMyFeedback()` first and do not offer the form for a course already answered.
   *
   * `employeeId` must be the caller's own unless they hold `HR.Training.Write` — W3 made feedback
   * the trainee's own voice, where it had previously been filable as anyone.
   */
  submitFeedback(data: SubmitTrainingFeedbackRequest): Promise<TrainingFeedback> {
    return apiService.post<TrainingFeedback>(`${this.baseUrl}/feedback`, data);
  }

  // ── Follow-up assessments ───────────────────────────────────────────────────
  getFollowUps(scheduleId: string): Promise<TrainingFollowUpAssessment[]> {
    return apiService.get<TrainingFollowUpAssessment[]>(`${this.baseUrl}/schedule/${scheduleId}/follow-up`);
  }

  submitFollowUp(data: SubmitFollowUpAssessmentRequest): Promise<TrainingFollowUpAssessment> {
    return apiService.post<TrainingFollowUpAssessment>(`${this.baseUrl}/follow-up`, data);
  }

  submitManagerObservation(assessmentId: string, managerObservationNotes: string): Promise<TrainingFollowUpAssessment> {
    return apiService.post<TrainingFollowUpAssessment>(`${this.baseUrl}/follow-up/${assessmentId}/manager-observation`, {
      assessmentId,
      managerObservationNotes,
    });
  }
}

export const trainingNominationService = new TrainingNominationService();
