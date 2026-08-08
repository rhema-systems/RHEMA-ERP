import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  OrganizationLevel,
  CreateOrganizationLevelRequest,
  UpdateOrganizationLevelRequest,
} from '@/types/hr/organization';

/**
 * CRUD for organization levels (the configurable tiers of the org hierarchy,
 * e.g. Division / Department / Unit). Backend route: api/OrganizationLevel.
 * NOTE: the paged endpoint takes pageNumber/pageSize and returns the HR
 * PagedResult shape (page/hasPrevious/hasNext).
 */
class OrganizationLevelService {
  private readonly baseUrl = '/OrganizationLevel';

  getAll(): Promise<OrganizationLevel[]> {
    return apiService.get<OrganizationLevel[]>(this.baseUrl);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<OrganizationLevel>> {
    const params = new URLSearchParams({
      pageNumber: pageNumber.toString(),
      pageSize: pageSize.toString(),
    });
    return apiService.get<PagedResult<OrganizationLevel>>(`${this.baseUrl}/paged?${params.toString()}`);
  }

  getByStructure(structureId: string): Promise<OrganizationLevel[]> {
    return apiService.get<OrganizationLevel[]>(`${this.baseUrl}/structure/${structureId}`);
  }

  getById(id: string): Promise<OrganizationLevel> {
    return apiService.get<OrganizationLevel>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateOrganizationLevelRequest): Promise<OrganizationLevel> {
    return apiService.post<OrganizationLevel>(this.baseUrl, data);
  }

  update(id: string, data: UpdateOrganizationLevelRequest): Promise<OrganizationLevel> {
    return apiService.put<OrganizationLevel>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }
}

export const organizationLevelService = new OrganizationLevelService();
