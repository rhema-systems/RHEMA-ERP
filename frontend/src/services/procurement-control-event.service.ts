import { apiService } from '@/services/api.service';
import type {
  ProcurementControlEvent,
  ProcurementControlEventIntegrity,
  ProcurementControlEventPage,
  ProcurementControlEventSearch,
  ProcurementControlEventSummary,
} from '@/types/procurement-control-event';

const root = '/procurement/control-events';

export const procurementControlEventService = {
  summary: () =>
    apiService.get<ProcurementControlEventSummary>(`${root}/summary`),
  search: (request: ProcurementControlEventSearch) =>
    apiService.get<ProcurementControlEventPage>(
      root,
      request as Record<string, unknown>
    ),
  get: (id: string) => apiService.get<ProcurementControlEvent>(`${root}/${id}`),
  correlation: (correlationId: string) =>
    apiService.get<ProcurementControlEvent[]>(
      `${root}/correlations/${encodeURIComponent(correlationId)}`
    ),
  verifyIntegrity: (take = 1000) =>
    apiService.post<ProcurementControlEventIntegrity>(
      `${root}/integrity/verify?take=${take}`
    ),
};
