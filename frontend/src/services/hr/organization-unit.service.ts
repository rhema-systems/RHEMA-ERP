import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  OrganizationUnit,
  OrganizationUnitSummary,
  CreateOrganizationUnitRequest,
  UpdateOrganizationUnitRequest,
} from '@/types/hr/organization';

/**
 * CRUD for organization units (the actual nodes of the org hierarchy, e.g.
 * "Finance Division", "IT Department"). Backend route: api/OrganizationUnit.
 * Paged endpoint takes pageNumber/pageSize and returns the HR PagedResult shape.
 */
class OrganizationUnitService {
  private readonly baseUrl = '/OrganizationUnit';

  getAll(): Promise<OrganizationUnit[]> {
    return apiService.get<OrganizationUnit[]>(this.baseUrl);
  }

  getSummary(): Promise<OrganizationUnitSummary[]> {
    return apiService.get<OrganizationUnitSummary[]>(`${this.baseUrl}/summary`);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<OrganizationUnit>> {
    const params = new URLSearchParams({
      pageNumber: pageNumber.toString(),
      pageSize: pageSize.toString(),
    });
    return apiService.get<PagedResult<OrganizationUnit>>(`${this.baseUrl}/paged?${params.toString()}`);
  }

  getByLevel(levelId: string): Promise<OrganizationUnit[]> {
    return apiService.get<OrganizationUnit[]>(`${this.baseUrl}/level/${levelId}`);
  }

  getById(id: string): Promise<OrganizationUnit> {
    return apiService.get<OrganizationUnit>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateOrganizationUnitRequest): Promise<OrganizationUnit> {
    return apiService.post<OrganizationUnit>(this.baseUrl, data);
  }

  update(id: string, data: UpdateOrganizationUnitRequest): Promise<OrganizationUnit> {
    return apiService.put<OrganizationUnit>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }
}

export const organizationUnitService = new OrganizationUnitService();
