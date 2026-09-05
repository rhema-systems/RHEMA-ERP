import { apiService } from '../api.service';
import type {
  BulkCompletionResult,
  TrainingCompletion,
  RecordTrainingCompletionRequest,
  VerifyTrainingCompletionRequest,
  TrainingCompletionStatus,
} from '@/types/hr/training-delivery';

/**
 * Completion records — the outcome of a nomination once the training has run.
 * Backend route: api/training-completions.
 *
 * Verifying a passed completion also writes the programme's target skills onto the employee's
 * profile, so verification is a real action rather than a rubber stamp.
 */
class TrainingCompletionService {
  private readonly baseUrl = '/training-completions';

  getById(id: string): Promise<TrainingCompletion> {
    return apiService.get<TrainingCompletion>(`${this.baseUrl}/${id}`);
  }

  getByNomination(nominationId: string): Promise<TrainingCompletion | null> {
    return apiService.get<TrainingCompletion | null>(`${this.baseUrl}/nomination/${nominationId}`);
  }

  /** The caller's own completion records — token-derived. */
  getMine(): Promise<TrainingCompletion[]> {
    return apiService.get<TrainingCompletion[]>(`${this.baseUrl}/mine`);
  }

  getByEmployee(employeeId: string): Promise<TrainingCompletion[]> {
    return apiService.get<TrainingCompletion[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getBySchedule(scheduleId: string): Promise<TrainingCompletion[]> {
    return apiService.get<TrainingCompletion[]>(`${this.baseUrl}/schedule/${scheduleId}`);
  }

  getPendingVerification(): Promise<TrainingCompletion[]> {
    return apiService.get<TrainingCompletion[]>(`${this.baseUrl}/pending-verification`);
  }

  /**
   * Record completion for a whole schedule at once.
   *
   * ⚠ The server checks only for a completion that already exists — not that the nomination
   * belongs to this schedule, nor that it was ever confirmed. The caller is the constraint; see
   * `BulkCompletionPanel`.
   */
  bulkRecord(data: {
    scheduleId: string;
    items: {
      nominationId: string;
      employeeId: string;
      completionDate: string;
      status: string;
      finalScore?: number | null;
      isPassed: boolean;
    }[];
  }): Promise<BulkCompletionResult> {
    return apiService.post<BulkCompletionResult>(`${this.baseUrl}/bulk`, data);
  }

  record(data: RecordTrainingCompletionRequest): Promise<TrainingCompletion> {
    return apiService.post<TrainingCompletion>(this.baseUrl, data);
  }

  update(id: string, data: Partial<RecordTrainingCompletionRequest>): Promise<TrainingCompletion> {
    return apiService.put<TrainingCompletion>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  verify(id: string, data: Omit<VerifyTrainingCompletionRequest, 'completionId'>): Promise<TrainingCompletion> {
    return apiService.post<TrainingCompletion>(`${this.baseUrl}/${id}/verify`, { completionId: id, ...data });
  }
}

export const trainingCompletionService = new TrainingCompletionService();
export type { TrainingCompletionStatus };
