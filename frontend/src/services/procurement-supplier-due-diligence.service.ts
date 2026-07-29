import { apiService } from '@/services/api.service';
import type {
  CreateSupplierDueDiligence,
  SupplierDueDiligence,
  SupplierDueDiligenceCurrentState,
  SupplierDueDiligenceLifecycleRequest,
  SupplierDueDiligencePage,
  SupplierDueDiligenceSearch,
  SupplierDueDiligenceSummary,
  SupplierDueDiligenceSupplierOption,
  SupplierDueDiligenceWorkflowOption,
  UpdateSupplierDueDiligence,
} from '@/types/procurement-supplier-due-diligence';

const root = '/procurement/supplier-due-diligence';

export const procurementSupplierDueDiligenceService = {
  summary: () => apiService.get<SupplierDueDiligenceSummary>(`${root}/summary`),
  search: (request: SupplierDueDiligenceSearch) =>
    apiService.get<SupplierDueDiligencePage>(
      root,
      request as Record<string, unknown>
    ),
  get: (id: string) => apiService.get<SupplierDueDiligence>(`${root}/${id}`),
  current: (businessPartnerId: string) =>
    apiService.get<SupplierDueDiligenceCurrentState>(
      `${root}/suppliers/${businessPartnerId}/current`
    ),
  workflowOptions: () =>
    apiService.get<SupplierDueDiligenceWorkflowOption[]>(
      `${root}/workflow-options`
    ),
  supplierOptions: () =>
    apiService.get<SupplierDueDiligenceSupplierOption[]>(
      `${root}/supplier-options`
    ),
  create: (request: CreateSupplierDueDiligence) =>
    apiService.post<SupplierDueDiligence>(root, request),
  update: (id: string, request: UpdateSupplierDueDiligence) =>
    apiService.put<SupplierDueDiligence>(`${root}/${id}`, request),
  submit: (id: string, request: SupplierDueDiligenceLifecycleRequest) =>
    apiService.post<SupplierDueDiligence>(`${root}/${id}/submit`, request),
  approve: (id: string, request: SupplierDueDiligenceLifecycleRequest) =>
    apiService.post<SupplierDueDiligence>(`${root}/${id}/approve`, request),
  reject: (id: string, request: SupplierDueDiligenceLifecycleRequest) =>
    apiService.post<SupplierDueDiligence>(`${root}/${id}/reject`, request),
  processExpiry: () =>
    apiService.post<{ processed: number }>(`${root}/process-expiry`, {}),
};
