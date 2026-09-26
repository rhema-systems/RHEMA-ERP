import { apiService } from '../api.service';
import type {
  CreateSalaryChangeRequest,
  SalaryChangeRequest,
  SalaryChangeRequestStatus,
  UpdateSalaryChangeRequest,
} from '@/types/hr/salary-change-request';

/**
 * Salary change requests (round 3, lane S) — `api/hr/salary-change-requests`. Approve, reject and
 * recall go through the engine; the service applies the change when the outcome is Approved, so a
 * screen refetches and never sets a status itself.
 */
class SalaryChangeRequestService {
  private readonly baseUrl = '/hr/salary-change-requests';

  list(employeeId?: string, status?: SalaryChangeRequestStatus): Promise<SalaryChangeRequest[]> {
    const params = new URLSearchParams();
    if (employeeId) params.set('employeeId', employeeId);
    if (status) params.set('status', status);
    const qs = params.toString();
    return apiService.get<SalaryChangeRequest[]>(`${this.baseUrl}${qs ? `?${qs}` : ''}`);
  }

  getById(id: string): Promise<SalaryChangeRequest> {
    return apiService.get<SalaryChangeRequest>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateSalaryChangeRequest): Promise<SalaryChangeRequest> {
    return apiService.post<SalaryChangeRequest>(this.baseUrl, data);
  }

  update(id: string, data: UpdateSalaryChangeRequest): Promise<SalaryChangeRequest> {
    return apiService.put<SalaryChangeRequest>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  submit(id: string): Promise<SalaryChangeRequest> {
    return apiService.post<SalaryChangeRequest>(`${this.baseUrl}/${id}/submit`, {});
  }

  approve(id: string): Promise<SalaryChangeRequest> {
    return apiService.post<SalaryChangeRequest>(`${this.baseUrl}/${id}/approve`, {});
  }

  reject(id: string, reason?: string | null): Promise<SalaryChangeRequest> {
    return apiService.post<SalaryChangeRequest>(`${this.baseUrl}/${id}/reject`, { reason: reason ?? null });
  }

  recall(id: string): Promise<SalaryChangeRequest> {
    return apiService.post<SalaryChangeRequest>(`${this.baseUrl}/${id}/recall`, {});
  }

  retryApply(id: string): Promise<SalaryChangeRequest> {
    return apiService.post<SalaryChangeRequest>(`${this.baseUrl}/${id}/retry-apply`, {});
  }
}

export const salaryChangeRequestService = new SalaryChangeRequestService();
