import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  ChangeUnitHeadRequest,
  OrganizationUnit,
  OrganizationUnitSummary,
  CreateOrganizationUnitRequest,
  MoveUnitRequest,
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

  /**
   * Reparents a unit and records the move on the change log. Answers a bare `true`.
   *
   * ⚠ Idempotent by design: moving a unit to the parent it already has returns true and records
   * NOTHING. Nine "moved from A to A" rows sit on the live log from before that guard existed, and
   * a change log has no delete, so they are there for good.
   */
  move(unitId: string, data: MoveUnitRequest): Promise<boolean> {
    return apiService.post<boolean>(`${this.baseUrl}/${unitId}/move`, data);
  }

  /**
   * Appoints or replaces the unit's head and records it on the change log. Answers a bare `true`.
   *
   * ⚠ The server refuses to clear the head of a level that requires one, and reappointing the
   * sitting head is a no-op that records nothing — the same guard as {@link move}.
   */
  changeHead(unitId: string, data: ChangeUnitHeadRequest): Promise<boolean> {
    return apiService.post<boolean>(`${this.baseUrl}/${unitId}/change-head`, data);
  }
}

export const organizationUnitService = new OrganizationUnitService();
