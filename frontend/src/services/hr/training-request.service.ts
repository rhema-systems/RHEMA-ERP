import { apiService } from '../api.service';
import type {
  TrainingRequest,
  TrainingRequestSummary,
  TrainingRequestCreate,
  TrainingRequestUpdate,
  ApproveTrainingRequestRequest,
  RejectTrainingRequestRequest,
  TrainingRequestStatus,
} from '@/types/hr/training-delivery';

/**
 * Employee-raised requests for training that may not be in the catalog yet.
 * Backend route: api/training-requests.
 *
 * A request is free text; approving it can optionally link it to a catalog programme, which is how
 * an ad-hoc ask becomes something schedulable.
 */
class TrainingRequestService {
  private readonly baseUrl = '/training-requests';

  getAll(): Promise<TrainingRequestSummary[]> {
    return apiService.get<TrainingRequestSummary[]>(this.baseUrl);
  }

  getById(id: string): Promise<TrainingRequest> {
    return apiService.get<TrainingRequest>(`${this.baseUrl}/${id}`);
  }

  /** The caller's own requests — token-derived. */
  getMine(): Promise<TrainingRequestSummary[]> {
    return apiService.get<TrainingRequestSummary[]>(`${this.baseUrl}/mine`);
  }

  getByEmployee(employeeId: string): Promise<TrainingRequestSummary[]> {
    return apiService.get<TrainingRequestSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getByStatus(status: TrainingRequestStatus): Promise<TrainingRequestSummary[]> {
    return apiService.get<TrainingRequestSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getPendingApproval(): Promise<TrainingRequestSummary[]> {
    return apiService.get<TrainingRequestSummary[]>(`${this.baseUrl}/pending-approval`);
  }

  create(data: TrainingRequestCreate): Promise<TrainingRequest> {
    return apiService.post<TrainingRequest>(this.baseUrl, data);
  }

  update(id: string, data: Omit<TrainingRequestUpdate, 'id'>): Promise<TrainingRequest> {
    return apiService.put<TrainingRequest>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  submit(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/submit`, {});
  }

  approve(id: string, data: Omit<ApproveTrainingRequestRequest, 'requestId'>): Promise<TrainingRequest> {
    return apiService.post<TrainingRequest>(`${this.baseUrl}/${id}/approve`, { requestId: id, ...data });
  }

  reject(id: string, data: Omit<RejectTrainingRequestRequest, 'requestId'>): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/reject`, { requestId: id, ...data });
  }
}

export const trainingRequestService = new TrainingRequestService();
