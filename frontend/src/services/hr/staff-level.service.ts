import { apiService } from '../api.service';
import type {
  StaffLevel,
  StaffLevelListItem,
  CreateStaffLevelRequest,
  UpdateStaffLevelRequest,
} from '@/types/hr/staff-level';

/**
 * CRUD for staff levels (a ranked HR reference lookup). Backend route:
 * api/hr/staff-levels. The controller is tenancy-aware (resolves the tenant
 * server-side); lists come back ordered by rank.
 */
class StaffLevelService {
  private readonly baseUrl = '/hr/staff-levels';

  getAll(): Promise<StaffLevelListItem[]> {
    return apiService.get<StaffLevelListItem[]>(this.baseUrl);
  }

  getActive(): Promise<StaffLevelListItem[]> {
    return apiService.get<StaffLevelListItem[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<StaffLevel> {
    return apiService.get<StaffLevel>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateStaffLevelRequest): Promise<StaffLevel> {
    return apiService.post<StaffLevel>(this.baseUrl, data);
  }

  update(id: string, data: UpdateStaffLevelRequest): Promise<StaffLevel> {
    return apiService.put<StaffLevel>(`${this.baseUrl}/${id}`, data);
  }

  activate(id: string): Promise<StaffLevel> {
    return apiService.patch<StaffLevel>(`${this.baseUrl}/${id}/activate`);
  }

  deactivate(id: string): Promise<StaffLevel> {
    return apiService.patch<StaffLevel>(`${this.baseUrl}/${id}/deactivate`);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const staffLevelService = new StaffLevelService();
