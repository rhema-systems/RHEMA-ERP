import { apiService } from '../api.service';
import type {
  SheHazard,
  SheHazardSummary,
  SheHazardCreateRequest,
  SheHazardUpdateRequest,
  SheHazardControl,
  SheHazardControlCreateRequest,
  SheHazardControlUpdateRequest,
  SheHazardCorrectiveAction,
  SheHazardCorrectiveActionCreateRequest,
  SheHazardCategory,
  SheHazardStatus,
  SheHazardRiskLevel,
} from '@/types/hr/safety-hazards';

/**
 * The living hazard register with hierarchy-of-controls and corrective-action links.
 * Backend route: api/safety/hazards.
 *
 * `create` is the ONE action open to every authenticated employee (hazard reporting must not be
 * gatekept) — the reporter is stamped from the token. Everything else answers 403 for non-HR.
 * Risk scores and levels are computed server-side from the four 1–5 factors.
 */
class SafetyHazardService {
  private readonly baseUrl = '/safety/hazards';

  // ── Queries ────────────────────────────────────────────────────────────────

  getAll(activeOnly = false): Promise<SheHazardSummary[]> {
    return apiService.get<SheHazardSummary[]>(this.baseUrl, activeOnly ? { activeOnly } : undefined);
  }

  getById(id: string): Promise<SheHazard> {
    return apiService.get<SheHazard>(`${this.baseUrl}/${id}`);
  }

  getByCode(code: string): Promise<SheHazard | null> {
    return apiService.get<SheHazard | null>(`${this.baseUrl}/code/${encodeURIComponent(code)}`);
  }

  getByStatus(status: SheHazardStatus): Promise<SheHazardSummary[]> {
    return apiService.get<SheHazardSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getByCategory(category: SheHazardCategory): Promise<SheHazardSummary[]> {
    return apiService.get<SheHazardSummary[]>(`${this.baseUrl}/category/${category}`);
  }

  getByResidualRiskLevel(level: SheHazardRiskLevel): Promise<SheHazardSummary[]> {
    return apiService.get<SheHazardSummary[]>(`${this.baseUrl}/risk-level/${level}`);
  }

  getByLocation(locationId: string): Promise<SheHazardSummary[]> {
    return apiService.get<SheHazardSummary[]>(`${this.baseUrl}/location/${locationId}`);
  }

  getByOwner(ownerId: string): Promise<SheHazardSummary[]> {
    return apiService.get<SheHazardSummary[]>(`${this.baseUrl}/owner/${ownerId}`);
  }

  getHighResidualRisk(minimumScore = 12): Promise<SheHazardSummary[]> {
    return apiService.get<SheHazardSummary[]>(`${this.baseUrl}/high-risk`, { minimumScore });
  }

  getDueForReview(daysAhead = 30): Promise<SheHazardSummary[]> {
    return apiService.get<SheHazardSummary[]>(`${this.baseUrl}/due-for-review`, { daysAhead });
  }

  // ── CRUD ───────────────────────────────────────────────────────────────────

  /** Open to every authenticated employee — the reporting surface. */
  create(data: SheHazardCreateRequest): Promise<SheHazard> {
    return apiService.post<SheHazard>(this.baseUrl, data);
  }

  update(id: string, data: SheHazardUpdateRequest): Promise<SheHazard> {
    return apiService.put<SheHazard>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Controls (hierarchy of controls) ───────────────────────────────────────

  addControl(hazardId: string, data: SheHazardControlCreateRequest): Promise<SheHazardControl> {
    return apiService.post<SheHazardControl>(`${this.baseUrl}/${hazardId}/controls`, data);
  }

  updateControl(controlId: string, data: SheHazardControlUpdateRequest): Promise<SheHazardControl> {
    return apiService.put<SheHazardControl>(`${this.baseUrl}/controls/${controlId}`, data);
  }

  removeControl(controlId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/controls/${controlId}`);
  }

  // ── Corrective-action links ────────────────────────────────────────────────

  addCorrectiveAction(
    hazardId: string,
    data: SheHazardCorrectiveActionCreateRequest,
  ): Promise<SheHazardCorrectiveAction> {
    return apiService.post<SheHazardCorrectiveAction>(
      `${this.baseUrl}/${hazardId}/corrective-actions`,
      data,
    );
  }

  removeCorrectiveAction(correctiveActionId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/corrective-actions/${correctiveActionId}`);
  }
}

export const safetyHazardService = new SafetyHazardService();
export default safetyHazardService;
