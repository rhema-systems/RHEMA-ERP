import { apiService } from '../api.service';
import type {
  SheStopWorkOrder,
  SheStopWorkOrderCreateRequest,
  SheStopWorkStatus,
} from '@/types/hr/safety-audits';

/**
 * Stop-work authority (FR-SHE-200). Backend route: api/safety/stop-work.
 *
 * Raising and "mine" are OPEN to every authenticated employee — stop-work
 * authority is not gatekept; non-HR raisers always raise as themselves (the
 * server takes the raiser from the token). The register and the lifecycle
 * (route → resolve → clear, cancel for false alarms) are HR-only; clearing an
 * order that is not Resolved is refused (422) — work resumes only once the
 * danger is dealt with. Orders are testimony: there is no edit endpoint.
 */
class SafetyStopWorkService {
  private readonly baseUrl = '/safety/stop-work';

  /** Open to every employee. */
  raise(data: SheStopWorkOrderCreateRequest): Promise<SheStopWorkOrder> {
    return apiService.post<SheStopWorkOrder>(this.baseUrl, data);
  }

  /** Open to every employee — own raised orders only. */
  getMine(): Promise<SheStopWorkOrder[]> {
    return apiService.get<SheStopWorkOrder[]>(`${this.baseUrl}/mine`);
  }

  getAll(status?: SheStopWorkStatus): Promise<SheStopWorkOrder[]> {
    return apiService.get<SheStopWorkOrder[]>(
      status ? `${this.baseUrl}?status=${status}` : this.baseUrl,
    );
  }

  getById(id: string): Promise<SheStopWorkOrder> {
    return apiService.get<SheStopWorkOrder>(`${this.baseUrl}/${id}`);
  }

  route(id: string, routedToId: string): Promise<SheStopWorkOrder> {
    return apiService.post<SheStopWorkOrder>(`${this.baseUrl}/${id}/route`, {
      orderId: id,
      routedToId,
    });
  }

  resolve(id: string, resolutionDescription: string, resolvedById: string): Promise<SheStopWorkOrder> {
    return apiService.post<SheStopWorkOrder>(`${this.baseUrl}/${id}/resolve`, {
      orderId: id,
      resolutionDescription,
      resolvedById,
    });
  }

  clear(id: string, clearedById: string, clearanceNotes?: string | null): Promise<SheStopWorkOrder> {
    return apiService.post<SheStopWorkOrder>(`${this.baseUrl}/${id}/clear`, {
      orderId: id,
      clearedById,
      clearanceNotes: clearanceNotes ?? null,
    });
  }

  cancel(id: string, reason: string): Promise<SheStopWorkOrder> {
    return apiService.post<SheStopWorkOrder>(`${this.baseUrl}/${id}/cancel`, {
      orderId: id,
      reason,
    });
  }
}

export const safetyStopWorkService = new SafetyStopWorkService();
export default safetyStopWorkService;
