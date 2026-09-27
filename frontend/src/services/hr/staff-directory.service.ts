import { apiService } from '../api.service';
import { organogramService } from './organogram.service';
import type { OrganogramNode } from '@/types/hr/organogram';

/**
 * Area 25 slice 13a — the staff directory and "my team".
 *
 * Shapes measured against the live API (`probe-slice13a.mjs`), not inferred from the endpoint
 * names — a type written from a route is fiction that type-checks.
 *
 * ⚠ **This is a LEAN projection on purpose.** The employee summary at
 * `POST api/hr/employees/paged` carries gender, employment type, hire date, years of service and
 * expatriate status — and since master's global search (2026-09-27) it needs `HR.Employee.Read`.
 * The probe asserts none of those columns appear on a directory row; if one ever does, the fix is
 * on the server, not a `delete` in the client.
 *
 * ⚠ Work contact only. `businessNumber` and `extension` are office numbers; the personal mobile
 * the employee maintains themselves (slice 12a) is deliberately not served. Measured on DEFAULT
 * 2026-08-27: every employee has an email, **none** has a business number, extension or photo —
 * so those fields render empty on this tenant until TDC loads them. Design for the empty case.
 */

export interface StaffDirectoryEntry {
  id: string;
  employeeNumber: string;
  fullName: string;
  displayName: string;
  title?: string | null;
  positionTitle: string;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  organizationLevelName?: string | null;
  locationId?: string | null;
  locationName?: string | null;
  emailAddress: string;
  businessNumber?: string | null;
  extension?: string | null;
  picturePath?: string | null;
  /** Set by the server on the caller's own row, so the list marks it rather than hiding it. */
  isSelf: boolean;
}

export interface StaffDirectoryProfile extends StaffDirectoryEntry {
  managerId?: string | null;
  managerName?: string | null;
  managerPositionTitle?: string | null;
  /** Root-first, e.g. ["Board of Directors", …, "Financial Accounts"]. Empty when unplaced. */
  unitPath: string[];
  directReports: StaffDirectoryEntry[];
}

export interface MyTeam {
  managerId?: string | null;
  managerName?: string | null;
  managerPositionTitle?: string | null;
  managerEmailAddress?: string | null;
  directReports: StaffDirectoryEntry[];
  managesAnyone: boolean;
}

export interface DirectoryPage {
  items: StaffDirectoryEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface DirectoryQuery {
  search?: string;
  organizationUnitId?: string;
  locationId?: string;
  page?: number;
  pageSize?: number;
}

/**
 * The organisation tree the browse panel draws comes from `api/Organogram/units` via the existing
 * {@link organogramService}, NOT from a second unit read of our own. That endpoint is already open
 * to any authenticated user, returns the whole tenant tree in one ~17 KB call (48 nodes on
 * DEFAULT), and carries `totalEmployeeCount` — the subtree rollup, which is the number a browse
 * panel wants beside a unit name. Its `employeeCount` (direct members) is 0 for most branches on
 * this tenant, which is precisely why the browse walks descendants server-side.
 *
 * ⚠ `Organogram/people` is deliberately NOT used and answers 403 for a plain employee: it is the
 * personnel register wearing a chart's clothes. This directory is the employee-facing answer to
 * the same question, with a projection that can be shown to everyone.
 */

/** The directory pages server-side — 8,007 people is ~4.1 MB unpaged, so never fetch it all. */
export const DIRECTORY_PAGE_SIZE = 25;

class StaffDirectoryService {
  private readonly baseUrl = '/employee-portal';

  /**
   * Search and browse. `organizationUnitId` reaches the unit AND everything beneath it.
   *
   * ⚠ Never pass an all-zeros guid: the server answers 400 rather than silently dropping the
   * filter. Omit the key instead.
   */
  search(query: DirectoryQuery = {}): Promise<DirectoryPage> {
    const qs = new URLSearchParams();
    if (query.search?.trim()) qs.set('search', query.search.trim());
    if (query.organizationUnitId) qs.set('organizationUnitId', query.organizationUnitId);
    if (query.locationId) qs.set('locationId', query.locationId);
    qs.set('page', String(query.page ?? 1));
    qs.set('pageSize', String(query.pageSize ?? DIRECTORY_PAGE_SIZE));
    return apiService.get<DirectoryPage>(`${this.baseUrl}/directory?${qs.toString()}`);
  }

  /**
   * The shared `EmployeePicker`'s search: the same lean card, `take` at a time (server caps at 25).
   *
   * Unlike {@link search} it needs no employee link — `admin` fills HR forms too — and it is a
   * search, never a browse: under two characters the server returns an empty page.
   */
  lookup(search: string, take = 10): Promise<DirectoryPage> {
    const qs = new URLSearchParams({ search: search.trim(), take: String(take) });
    return apiService.get<DirectoryPage>(`${this.baseUrl}/directory/lookup?${qs.toString()}`);
  }

  /** One colleague's card. 404 when they are not on strength — a miss, not a refusal. */
  getProfile(id: string): Promise<StaffDirectoryProfile> {
    return apiService.get<StaffDirectoryProfile>(`${this.baseUrl}/directory/${id}`);
  }

  /** Who the caller reports to, and who reports to them. Honestly empty for most people. */
  getMyTeam(): Promise<MyTeam> {
    return apiService.get<MyTeam>(`${this.baseUrl}/my-team`);
  }

  /** The organisation tree for the browse panel — see the note above on why it is reused. */
  async getUnitTree(): Promise<OrganogramNode[]> {
    const res = await organogramService.units();
    return res?.nodes ?? [];
  }
}

export const staffDirectoryService = new StaffDirectoryService();
