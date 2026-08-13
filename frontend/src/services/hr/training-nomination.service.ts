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
