import { apiService } from '../api.service';
import type { OrganogramDimension, OrganogramResponse } from '@/types/hr/organogram';

/**
 * Organogram projections. Backend route: `api/Organogram/{dimension}`.
 *
 * Read-only. Every call returns the same envelope — a flat node list the client turns into a tree.
 *
 * ⚠ The gate is **split**, and a caller has to know it. `units`, `positions` and `locations`
 * describe the company and are open to any authenticated user. `people` is the personnel register
 * — 6,286 rows carrying everyone's work email in one unpaged call — and is gated to
 * SuperAdmin / TenantAdmin / HR. Anyone else gets a 403 on that dimension alone, which is a normal
 * outcome the screen renders rather than an error it reports.
 *
 * ⚠ `locations` **requires** a structure id and answers 400 without one. There is no default:
 * fetch `api/LocationStructure` and let the user pick (or take `isDefault`).
 */
class OrganogramService {
  private readonly baseUrl = '/Organogram';

  units(): Promise<OrganogramResponse> {
    return apiService.get<OrganogramResponse>(`${this.baseUrl}/units`);
  }

  positions(): Promise<OrganogramResponse> {
    return apiService.get<OrganogramResponse>(`${this.baseUrl}/positions`);
  }

  people(): Promise<OrganogramResponse> {
    return apiService.get<OrganogramResponse>(`${this.baseUrl}/people`);
  }

  locations(structureId: string): Promise<OrganogramResponse> {
    return apiService.get<OrganogramResponse>(`${this.baseUrl}/locations`, { structureId });
  }

  teams(): Promise<OrganogramResponse> {
    return apiService.get<OrganogramResponse>(`${this.baseUrl}/teams`);
  }

  /** Dispatches by dimension. `locations` is the only one that needs the second argument. */
  byDimension(dimension: OrganogramDimension, structureId?: string): Promise<OrganogramResponse> {
    switch (dimension) {
      case 'units':
        return this.units();
      case 'positions':
        return this.positions();
      case 'people':
        return this.people();
      case 'teams':
        return this.teams();
      case 'locations':
        if (!structureId) {
          return Promise.reject(new Error('A location structure must be chosen first.'));
        }
        return this.locations(structureId);
    }
  }
}

export const organogramService = new OrganogramService();
