import { apiService } from '@/services/api.service';
import type { ProcurementMethodType } from '@/types/procurement-policy';
import type {
  CreateProcurementSourcingCase,
  ProcurementSourcingCase,
  ProcurementSourcingCasePage,
  ProcurementSourcingCaseReadiness,
  ProcurementSourcingCaseSourceOption,
  ProcurementSourcingCaseSummary,
} from '@/types/procurement-sourcing-case';

const root = '/procurement/sourcing-cases';

export const procurementSourcingCaseService = {
  summary: () =>
    apiService.get<ProcurementSourcingCaseSummary>(`${root}/summary`),
  search: (query?: Record<string, unknown>) =>
    apiService.get<ProcurementSourcingCasePage>(root, query),
  sourceOptions: () =>
    apiService.get<ProcurementSourcingCaseSourceOption[]>(
      `${root}/source-options`
    ),
  get: (id: string) => apiService.get<ProcurementSourcingCase>(`${root}/${id}`),
  readiness: (
    requisitionId: string,
    method?: ProcurementMethodType,
    overrideReason?: string
  ) =>
    apiService.get<ProcurementSourcingCaseReadiness>(
      `${root}/readiness/${requisitionId}`,
      method || overrideReason ? { method, overrideReason } : undefined
    ),
  create: (request: CreateProcurementSourcingCase) =>
    apiService.post<ProcurementSourcingCase>(root, request),
  close: (id: string, rowVersion: string, reason: string) =>
    apiService.post<ProcurementSourcingCase>(`${root}/${id}/close`, {
      rowVersion,
      reason,
    }),
  cancel: (id: string, rowVersion: string, reason: string) =>
    apiService.post<ProcurementSourcingCase>(`${root}/${id}/cancel`, {
      rowVersion,
      reason,
    }),
};
