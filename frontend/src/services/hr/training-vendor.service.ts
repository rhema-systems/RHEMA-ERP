import { apiService } from '../api.service';
import type {
  TrainingVendor,
  TrainingVendorSummary,
  TrainingVendorCreateRequest,
  TrainingVendorUpdateRequest,
} from '@/types/hr/training';
import type { PagedResult } from '@/types/hr/common';

/**
 * CRUD for training vendors (external providers). Backend route: api/training-vendors.
 */
class TrainingVendorService {
  private readonly baseUrl = '/training-vendors';

  getAll(): Promise<TrainingVendorSummary[]> {
    return apiService.get<TrainingVendorSummary[]>(this.baseUrl);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<TrainingVendorSummary>> {
    return apiService.get<PagedResult<TrainingVendorSummary>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getActive(): Promise<TrainingVendorSummary[]> {
    return apiService.get<TrainingVendorSummary[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<TrainingVendor> {
    return apiService.get<TrainingVendor>(`${this.baseUrl}/${id}`);
  }

  create(data: TrainingVendorCreateRequest): Promise<TrainingVendor> {
    return apiService.post<TrainingVendor>(this.baseUrl, data);
  }

  update(id: string, data: TrainingVendorUpdateRequest): Promise<TrainingVendor> {
    return apiService.put<TrainingVendor>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  blacklist(id: string, blacklistReason: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/blacklist`, { vendorId: id, blacklistReason });
  }

  unblacklist(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/unblacklist`);
  }
}

export const trainingVendorService = new TrainingVendorService();
