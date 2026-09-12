import { apiService } from '../api.service';
import type {
  SheCorrectiveActionFilters,
  SheUnifiedCorrectiveAction,
  SheUnifiedCorrectiveActionSummary,
} from '@/types/hr/safety-kpi';

/**
 * The unified corrective-action tracker (FR-SHE-245) — a read-only union view over
 * the four live corrective-action stores (incident, inspection, equipment, committee).
 * Backend route: api/safety/corrective-actions. HR-only. Writes stay on each silo's
 * own screens; rows link back to their parent record via parentPath.
 */
class SafetyCorrectiveActionsService {
  private readonly baseUrl = '/safety/corrective-actions';

  getAll(filters: SheCorrectiveActionFilters = {}): Promise<SheUnifiedCorrectiveAction[]> {
    const params = new URLSearchParams();
    if (filters.source) params.set('source', filters.source);
    if (filters.status) params.set('status', filters.status);
    if (filters.assignedToId) params.set('assignedToId', filters.assignedToId);
    if (filters.overdueOnly) params.set('overdueOnly', 'true');
    if (filters.dueFrom) params.set('dueFrom', filters.dueFrom);
    if (filters.dueTo) params.set('dueTo', filters.dueTo);
    const query = params.toString();
    return apiService.get<SheUnifiedCorrectiveAction[]>(query ? `${this.baseUrl}?${query}` : this.baseUrl);
  }

  getSummary(): Promise<SheUnifiedCorrectiveActionSummary> {
    return apiService.get<SheUnifiedCorrectiveActionSummary>(`${this.baseUrl}/summary`);
  }
}

export const safetyCorrectiveActionsService = new SafetyCorrectiveActionsService();
export default safetyCorrectiveActionsService;
