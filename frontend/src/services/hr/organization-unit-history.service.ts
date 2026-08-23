import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  OrganizationUnitHistoryEntry,
  OrganizationUnitHistoryQuery,
} from '@/types/hr/organization';

/**
 * The organisation-unit change log. Backend route: `api/OrganizationUnitHistory`.
 *
 * **Read-only, and deliberately so.** There is no create, no update and no delete: rows are written
 * by `OrganizationUnitService` as a side effect of a restructure or a change of head, because an
 * audit trail somebody can author by hand is not one. That also means nothing here can be undone —
 * a wrong entry is corrected by making the correcting change, not by editing the log.
 *
 * ⚠ Gated on SuperAdmin / TenantAdmin / HR. The log names the employees who have led each unit, so
 * it is org-structure information about identifiable people, not the public noticeboard the
 * organogram's unit view is.
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

  // ⚠ `getLatestForUnit` and an `active` variant were deleted in slice 12 with the two endpoints
  // behind them. Since the log became effective-dated per SERIES, the newest row in a series is
  // always the open one, so the two endpoints returned the same row as each other — and one row
  // cannot state an arrangement that is up to two rows (a unit's reporting line and its leadership
  // move independently). Ask {@link getByUnit} and read both series.
}

export const organizationUnitHistoryService = new OrganizationUnitHistoryService();
