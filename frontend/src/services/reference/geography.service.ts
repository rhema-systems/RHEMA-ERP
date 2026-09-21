import { apiService } from '../api.service';
import type {
  GeoScheme,
  GeoSchemeDetail,
  GeoSchemeRequest,
  GeoLevel,
  GeoLevelRequest,
  GeoArea,
  GeoAreaRequest,
  GeoAreaTreeNode,
  GeoAreaOption,
  GeoAreaResolution,
  GeoAreaAlias,
  GeoAreaAliasRequest,
} from '@/types/reference/geography';

/**
 * Administrative geography — shared reference data. Backend route: `api/reference/geo`.
 *
 * Reads carry no permission (the backend gates them on InternalOnly only), because every module's
 * address form lists regions. Writes need `Reference.Geography.Write`; deletes need
 * `Reference.Geography.Admin`.
 */
class GeographyService {
  private readonly baseUrl = '/reference/geo';

  // ── Schemes ───────────────────────────────────────────────────────────────

  getSchemes(activeOnly = false): Promise<GeoScheme[]> {
    return apiService.get<GeoScheme[]>(`${this.baseUrl}/schemes?activeOnly=${activeOnly}`);
  }

  getScheme(id: string): Promise<GeoSchemeDetail> {
    return apiService.get<GeoSchemeDetail>(`${this.baseUrl}/schemes/${id}`);
  }

  /**
   * The scheme an address form should render for a country.
   *
   * ⚠ Resolves to `null` when the country has no scheme. The endpoint answers 204 for that case
   * deliberately — it is a normal state the form handles by falling back to free text, not an
   * error. An empty body parses to `null`/`''` depending on the client, so both are folded here.
   */
  async getSchemeForCountry(countryId: string): Promise<GeoSchemeDetail | null> {
    const scheme = await apiService.get<GeoSchemeDetail | null | ''>(
      `${this.baseUrl}/schemes/by-country/${countryId}`,
    );
    return scheme ? (scheme as GeoSchemeDetail) : null;
  }

  createScheme(data: GeoSchemeRequest): Promise<GeoScheme> {
    return apiService.post<GeoScheme>(`${this.baseUrl}/schemes`, data);
  }

  updateScheme(id: string, data: GeoSchemeRequest): Promise<GeoScheme> {
    return apiService.put<GeoScheme>(`${this.baseUrl}/schemes/${id}`, { ...data, id });
  }

  removeScheme(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/schemes/${id}`);
  }

  // ── Tiers ─────────────────────────────────────────────────────────────────

  getLevels(schemeId: string, activeOnly = false): Promise<GeoLevel[]> {
    return apiService.get<GeoLevel[]>(
      `${this.baseUrl}/schemes/${schemeId}/levels?activeOnly=${activeOnly}`,
    );
  }

  createLevel(data: GeoLevelRequest): Promise<GeoLevel> {
    return apiService.post<GeoLevel>(`${this.baseUrl}/levels`, data);
  }

  updateLevel(id: string, data: GeoLevelRequest): Promise<GeoLevel> {
    return apiService.put<GeoLevel>(`${this.baseUrl}/levels/${id}`, { ...data, id });
  }

  removeLevel(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/levels/${id}`);
  }

  // ── Areas ─────────────────────────────────────────────────────────────────

  getAreas(
    schemeId: string,
    options: {
      levelId?: string;
      parentId?: string;
      includeHistorical?: boolean;
      search?: string;
    } = {},
  ): Promise<GeoArea[]> {
    const query = new URLSearchParams();
    if (options.levelId) query.set('levelId', options.levelId);
    if (options.parentId) query.set('parentId', options.parentId);
    if (options.includeHistorical) query.set('includeHistorical', 'true');
    if (options.search) query.set('search', options.search);
    const suffix = query.toString() ? `?${query.toString()}` : '';
    return apiService.get<GeoArea[]>(`${this.baseUrl}/schemes/${schemeId}/areas${suffix}`);
  }

  getAreaTree(schemeId: string, includeHistorical = false): Promise<GeoAreaTreeNode[]> {
    return apiService.get<GeoAreaTreeNode[]>(
      `${this.baseUrl}/schemes/${schemeId}/tree?includeHistorical=${includeHistorical}`,
    );
  }

  getArea(id: string): Promise<GeoArea> {
    return apiService.get<GeoArea>(`${this.baseUrl}/areas/${id}`);
  }

  /**
   * The cascade an address form drives. Omit `parentId` for the broadest tier.
   *
   * ⚠ Never returns historical areas — a form must not offer a district that no longer exists.
   * Use {@link resolve} when you need to make sense of an area a stored record already points at.
   */
  getAreaOptions(levelId: string, parentId?: string): Promise<GeoAreaOption[]> {
    const suffix = parentId ? `?parentId=${parentId}` : '';
    return apiService.get<GeoAreaOption[]>(`${this.baseUrl}/levels/${levelId}/options${suffix}`);
  }

  getAncestors(areaId: string): Promise<GeoAreaOption[]> {
    return apiService.get<GeoAreaOption[]>(`${this.baseUrl}/areas/${areaId}/ancestors`);
  }

  resolve(
    name: string,
    options: { schemeId?: string; levelId?: string; parentId?: string } = {},
  ): Promise<GeoAreaResolution[]> {
    const query = new URLSearchParams({ name });
    if (options.schemeId) query.set('schemeId', options.schemeId);
    if (options.levelId) query.set('levelId', options.levelId);
    if (options.parentId) query.set('parentId', options.parentId);
    return apiService.get<GeoAreaResolution[]>(`${this.baseUrl}/resolve?${query.toString()}`);
  }

  createArea(data: GeoAreaRequest): Promise<GeoArea> {
    return apiService.post<GeoArea>(`${this.baseUrl}/areas`, data);
  }

  updateArea(id: string, data: GeoAreaRequest): Promise<GeoArea> {
    return apiService.put<GeoArea>(`${this.baseUrl}/areas/${id}`, { ...data, id });
  }

  removeArea(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/areas/${id}`);
  }

  // ── Alternate names ───────────────────────────────────────────────────────

  getAliases(areaId: string): Promise<GeoAreaAlias[]> {
    return apiService.get<GeoAreaAlias[]>(`${this.baseUrl}/areas/${areaId}/aliases`);
  }

  createAlias(data: GeoAreaAliasRequest): Promise<GeoAreaAlias> {
    return apiService.post<GeoAreaAlias>(`${this.baseUrl}/aliases`, data);
  }

  updateAlias(id: string, data: GeoAreaAliasRequest): Promise<GeoAreaAlias> {
    return apiService.put<GeoAreaAlias>(`${this.baseUrl}/aliases/${id}`, { ...data, id });
  }

  removeAlias(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/aliases/${id}`);
  }
}

export const geographyService = new GeographyService();

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  The anonymous careers reader — round 4, lane A.
//
//  ⚠ These exist because `api/reference/geo` is InternalOnly, and InternalOnly is a BLOCKLIST that
//  excludes ExternalUser, Candidate and ConsultantClient. A signed-in candidate filling in their
//  own address is refused by it — and refused INVISIBLY, because a react-query default of []
//  renders as an empty dropdown. Without this the careers cascade would have read "this country
//  has no scheme" for every candidate on earth, and looked entirely healthy doing it.
//
//  Same shape as the currency picker's public mode: tenant in a header, no auth, rate-limited.
// ═══════════════════════════════════════════════════════════════════════════════════════════════

const API_BASE = process.env.NEXT_PUBLIC_API_URL || '/api';

async function publicGet<T>(path: string, tenantId: string, fallback: T): Promise<T> {
  const res = await fetch(`${API_BASE}/public/catalogue/${path}`, {
    headers: { 'X-Tenant-Id': tenantId },
  });
  // 204 is the documented "this country has no scheme" answer, not a failure.
  if (res.status === 204) return fallback;
  if (!res.ok) throw new Error(`Could not read the geography catalogue (${res.status})`);
  return (await res.json()) as T;
}

/**
 * The three cascade reads, against either the internal route or the anonymous careers one.
 *
 * Pass `publicTenantId` on a candidate-facing page and nothing anywhere else: the internal route
 * carries more (aliases, historical areas, the whole tree) and is the one every HR screen wants.
 */
export function geographyReader(publicTenantId?: string | null) {
  if (!publicTenantId) {
    return {
      getSchemeForCountry: (countryId: string) => geographyService.getSchemeForCountry(countryId),
      getAreaOptions: (levelId: string, parentId?: string) =>
        geographyService.getAreaOptions(levelId, parentId),
      getAncestors: (areaId: string) => geographyService.getAncestors(areaId),
    };
  }

  return {
    getSchemeForCountry: (countryId: string) =>
      publicGet<GeoSchemeDetail | null>(`geo-scheme?countryId=${countryId}`, publicTenantId, null),
    getAreaOptions: (levelId: string, parentId?: string) =>
      publicGet<GeoAreaOption[]>(
        `geo-areas?levelId=${levelId}${parentId ? `&parentId=${parentId}` : ''}`,
        publicTenantId,
        [],
      ),
    getAncestors: (areaId: string) =>
      publicGet<GeoAreaOption[]>(`geo-areas/${areaId}/ancestors`, publicTenantId, []),
  };
}
