import { apiService } from '@/services/api.service';
import type {
  EscalateSupplierRiskAlert,
  EvaluateSupplierRisk,
  ResolveSupplierRiskAlert,
  SupplierRiskAlert,
  SupplierRiskAssessment,
  SupplierRiskCurrentState,
  SupplierRiskPage,
  SupplierRiskSearch,
  SupplierRiskSummary,
  SupplierRiskSupplierOption,
  SupplierRiskWorkflowOption,
} from '@/types/procurement-supplier-risk';

const root = '/procurement/supplier-risk';

export const procurementSupplierRiskService = {
  summary: () => apiService.get<SupplierRiskSummary>(`${root}/summary`),
  search: (request: SupplierRiskSearch) =>
    apiService.get<SupplierRiskPage>(root, request as Record<string, unknown>),
  get: (id: string) => apiService.get<SupplierRiskAssessment>(`${root}/${id}`),
  current: (businessPartnerId: string) =>
    apiService.get<SupplierRiskCurrentState>(
      `${root}/suppliers/${businessPartnerId}/current`
    ),
  supplierOptions: () =>
    apiService.get<SupplierRiskSupplierOption[]>(`${root}/supplier-options`),
  workflowOptions: () =>
    apiService.get<SupplierRiskWorkflowOption[]>(`${root}/workflow-options`),
  evaluate: (request: EvaluateSupplierRisk) =>
    apiService.post<SupplierRiskAssessment>(`${root}/assessments`, request),
  escalate: (alertId: string, request: EscalateSupplierRiskAlert) =>
    apiService.post<SupplierRiskAlert>(
      `${root}/alerts/${alertId}/escalate`,
      request
    ),
  resolve: (alertId: string, request: ResolveSupplierRiskAlert) =>
    apiService.post<SupplierRiskAlert>(
      `${root}/alerts/${alertId}/resolve`,
      request
    ),
};
