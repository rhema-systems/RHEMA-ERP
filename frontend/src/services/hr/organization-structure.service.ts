import { apiService } from '../api.service';
import type {
  OrganizationStructure,
  OrganizationStructureSummary,
  CreateOrganizationStructureRequest,
  UpdateOrganizationStructureRequest,
} from '@/types/hr/organization';

/**
 * CRUD for organization structures — the top of the org hierarchy
 * (Structure -> Levels -> Units); levels/units are created against a structure.
 * Backend route: api/OrganizationStructure.
 */
class OrganizationStructureService {
  private readonly baseUrl = '/OrganizationStructure';

  getAll(): Promise<OrganizationStructure[]> {
    return apiService.get<OrganizationStructure[]>(this.baseUrl);
  }

  getSummary(): Promise<OrganizationStructureSummary[]> {
    return apiService.get<OrganizationStructureSummary[]>(`${this.baseUrl}/summary`);
  }

  getDefault(): Promise<OrganizationStructure> {
    return apiService.get<OrganizationStructure>(`${this.baseUrl}/default`);
  }

  getById(id: string): Promise<OrganizationStructure> {
    return apiService.get<OrganizationStructure>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateOrganizationStructureRequest): Promise<OrganizationStructure> {
    return apiService.post<OrganizationStructure>(this.baseUrl, data);
  }

  update(id: string, data: UpdateOrganizationStructureRequest): Promise<OrganizationStructure> {
    return apiService.put<OrganizationStructure>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }

  setDefault(id: string): Promise<boolean> {
    return apiService.patch<boolean>(`${this.baseUrl}/${id}/set-default`);
  }
}

export const organizationStructureService = new OrganizationStructureService();
