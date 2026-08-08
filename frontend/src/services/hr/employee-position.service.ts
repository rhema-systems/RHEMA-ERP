import { apiService } from '../api.service';
import type {
  EmployeePosition,
  CreateEmployeePositionRequest,
  UpdateEmployeePositionRequest,
} from '@/types/hr/position';

/**
 * CRUD for employee (job) positions. Backend route: api/EmployeePositions.
 * NOTE: there is no paged endpoint — the list uses GET / (all). Both
 * OrganizationUnitId and OrganizationLevelId are required on create/update.
 */
class EmployeePositionService {
  private readonly baseUrl = '/EmployeePositions';

  getAll(): Promise<EmployeePosition[]> {
    return apiService.get<EmployeePosition[]>(this.baseUrl);
  }

  getActive(): Promise<EmployeePosition[]> {
    return apiService.get<EmployeePosition[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<EmployeePosition> {
    return apiService.get<EmployeePosition>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateEmployeePositionRequest): Promise<EmployeePosition> {
    return apiService.post<EmployeePosition>(this.baseUrl, data);
  }

  update(id: string, data: UpdateEmployeePositionRequest): Promise<EmployeePosition> {
    return apiService.put<EmployeePosition>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const employeePositionService = new EmployeePositionService();
