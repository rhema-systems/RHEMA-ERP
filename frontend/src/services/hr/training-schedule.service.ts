import { apiService } from '../api.service';
import type {
  TrainingSchedule,
  TrainingScheduleSummary,
  TrainingScheduleRequest,
  TrainingSession,
  TrainingSessionRequest,
  TrainerAvailabilityCheck,
  ScheduleStatus,
} from '@/types/hr/training-delivery';

/**
 * Training schedules — the calendar of delivered runs of a catalog programme — plus their session
 * breakdown and the trainer conflict check. Backend route: api/training-schedules.
 */
class TrainingScheduleService {
  private readonly baseUrl = '/training-schedules';

  getAll(): Promise<TrainingScheduleSummary[]> {
    return apiService.get<TrainingScheduleSummary[]>(this.baseUrl);
  }

  getById(id: string): Promise<TrainingSchedule> {
    return apiService.get<TrainingSchedule>(`${this.baseUrl}/${id}`);
  }

  getByProgram(programId: string): Promise<TrainingScheduleSummary[]> {
    return apiService.get<TrainingScheduleSummary[]>(`${this.baseUrl}/program/${programId}`);
  }

  getByTrainer(trainerId: string): Promise<TrainingScheduleSummary[]> {
    return apiService.get<TrainingScheduleSummary[]>(`${this.baseUrl}/trainer/${trainerId}`);
  }

  getByStatus(status: ScheduleStatus): Promise<TrainingScheduleSummary[]> {
    return apiService.get<TrainingScheduleSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getUpcoming(daysAhead = 90): Promise<TrainingScheduleSummary[]> {
    return apiService.get<TrainingScheduleSummary[]>(`${this.baseUrl}/upcoming`, { daysAhead });
  }

  getOpenForRegistration(): Promise<TrainingScheduleSummary[]> {
    return apiService.get<TrainingScheduleSummary[]>(`${this.baseUrl}/open-for-registration`);
  }

  /** Overlapping schedules + blocked availability windows for a trainer over a date range. */
  checkTrainerAvailability(
    trainerId: string,
    from: string,
    to: string,
    excludeScheduleId?: string,
  ): Promise<TrainerAvailabilityCheck> {
    return apiService.get<TrainerAvailabilityCheck>(`${this.baseUrl}/trainer/${trainerId}/availability-check`, {
      from,
      to,
      ...(excludeScheduleId ? { excludeScheduleId } : {}),
    });
  }

  create(data: TrainingScheduleRequest): Promise<TrainingSchedule> {
    return apiService.post<TrainingSchedule>(this.baseUrl, data);
  }

  update(id: string, data: TrainingScheduleRequest): Promise<TrainingSchedule> {
    return apiService.put<TrainingSchedule>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  approve(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/approve`, { scheduleId: id });
  }

  /** The canceller comes from the token — there is no cancelledById on the payload. */
  cancel(id: string, cancellationReason: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/cancel`, { scheduleId: id, cancellationReason });
  }

  /** Completing a schedule also credits the trainer's delivered sessions and hours. */
  complete(id: string, completionDate: string, completionNotes?: string | null): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/complete`, {
      scheduleId: id,
      completionDate,
      completionNotes: completionNotes || null,
    });
  }

  // ── Sessions ────────────────────────────────────────────────────────────────
  getSessions(scheduleId: string): Promise<TrainingSession[]> {
    return apiService.get<TrainingSession[]>(`${this.baseUrl}/${scheduleId}/sessions`);
  }

  addSession(scheduleId: string, data: Omit<TrainingSessionRequest, 'scheduleId'>): Promise<TrainingSession> {
    return apiService.post<TrainingSession>(`${this.baseUrl}/${scheduleId}/sessions`, { ...data, scheduleId });
  }

  updateSession(sessionId: string, data: Omit<TrainingSessionRequest, 'scheduleId'>): Promise<TrainingSession> {
    return apiService.put<TrainingSession>(`${this.baseUrl}/sessions/${sessionId}`, { id: sessionId, ...data });
  }

  removeSession(sessionId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/sessions/${sessionId}`);
  }
}

export const trainingScheduleService = new TrainingScheduleService();
