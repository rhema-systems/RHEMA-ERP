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

  /**
   * Positions in a unit and, by default, in every unit above it — the reports-to option source
   * (round 2, C1). The server walks the parent chain, so this works on seeded units with no path.
   */
  getByOrganizationUnit(organizationUnitId: string, includeAncestors = true): Promise<EmployeePosition[]> {
    return apiService.get<EmployeePosition[]>(
      `${this.baseUrl}/organization-unit/${organizationUnitId}`,
      { includeAncestors },
    );
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
