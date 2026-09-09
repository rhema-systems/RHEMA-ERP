import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  CreateOrganizationUnitHistoryRequest,
  OrganizationUnitHistoryEntry,
  OrganizationUnitHistoryQuery,
  UpdateOrganizationUnitHistoryRequest,
} from '@/types/hr/organization';

/**
 * The organisation-unit change log. Backend route: `api/OrganizationUnitHistory`.
 *
 * Rows are written by `OrganizationUnitService` as a side effect of a creation, a restructure or a
 * change of head. Two admin-tier writes exist beside those since demo feedback round 2 (O-3b):
 * {@link createManual} records an entry by hand (it moves nothing and appoints nobody, so it
 * classifies as `Other` and must give a reason), and {@link update} corrects a row's dates, reason
 * and notes. Neither can rewrite WHAT a row says changed, and there is still no delete — an audit
 * trail somebody can author freely is not one.
 *
 * ⚠ Reads are gated on HR.Employee.Read; the two writes on HR.Employee.Admin. The log names the
 * employees who have led each unit, so it is org-structure information about identifiable people,
 * not the public noticeboard the organogram's unit view is.
 */
class OrganizationUnitHistoryService {
  private readonly baseUrl = '/OrganizationUnitHistory';

  /**
   * The register. Every filter is optional; an unrecognised `changeType` is refused with a 400
   * rather than ignored, which is why the query type is a union and not a string.
   */
  getPaged(query: OrganizationUnitHistoryQuery = {}): Promise<PagedResult<OrganizationUnitHistoryEntry>> {
    const { pageNumber = 1, pageSize = 20, unitId, startDate, endDate, changeType } = query;
    return apiService.get<PagedResult<OrganizationUnitHistoryEntry>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
      ...(unitId ? { unitId } : {}),
      ...(startDate ? { startDate } : {}),
      ...(endDate ? { endDate } : {}),
      ...(changeType ? { changeType } : {}),
    });
  }

  /** Everything recorded against one unit, newest first — the unit's own change-log tab. */
  getByUnit(unitId: string): Promise<OrganizationUnitHistoryEntry[]> {
    return apiService.get<OrganizationUnitHistoryEntry[]>(`${this.baseUrl}/unit/${unitId}`);
  }

  getById(id: string): Promise<OrganizationUnitHistoryEntry> {
    return apiService.get<OrganizationUnitHistoryEntry>(`${this.baseUrl}/${id}`);
  }

  /**
   * ⚠ Returns a bare array with no paging envelope, unlike {@link getPaged}. The register uses the
   * paged endpoint's own date filters instead — this is kept for callers that genuinely want the
   * whole range at once.
   */
  getByDateRange(startDate: string, endDate: string): Promise<OrganizationUnitHistoryEntry[]> {
    return apiService.get<OrganizationUnitHistoryEntry[]>(`${this.baseUrl}/date-range`, {
      startDate,
      endDate,
    });
  }

  /** Records an entry by hand. Admin-tier; the server refuses one without a reason. */
  createManual(data: CreateOrganizationUnitHistoryRequest): Promise<OrganizationUnitHistoryEntry> {
    return apiService.post<OrganizationUnitHistoryEntry>(this.baseUrl, data);
  }

  /**
   * Corrects a row's dates, reason and notes. Admin-tier.
   *
   * ⚠ The neighbouring row in the same series is not re-derived: moving this row's start does not
   * move the previous row's end. Correct both when both are wrong.
   */
  update(id: string, data: UpdateOrganizationUnitHistoryRequest): Promise<OrganizationUnitHistoryEntry> {
    return apiService.put<OrganizationUnitHistoryEntry>(`${this.baseUrl}/${id}`, data);
  }

  // ⚠ `getLatestForUnit` and an `active` variant were deleted in slice 12 with the two endpoints
  // behind them. Since the log became effective-dated per SERIES, the newest row in a series is
  // always the open one, so the two endpoints returned the same row as each other — and one row
  // cannot state an arrangement that is up to two rows (a unit's reporting line and its leadership
  // move independently). Ask {@link getByUnit} and read both series.
}

export const organizationUnitHistoryService = new OrganizationUnitHistoryService();
