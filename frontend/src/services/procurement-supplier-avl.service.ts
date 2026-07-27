import { apiService } from '@/services/api.service';
import type {
  AddSupplierAvlEntry,
  CreateSupplierAvl,
  SupplierAvlCurrentState,
  SupplierAvlEntryLifecycleRequest,
  SupplierAvlLifecycleRequest,
  SupplierAvlPage,
  SupplierAvlRegister,
  SupplierAvlSearch,
  SupplierAvlSummary,
  SupplierAvlSupplierOption,
  SupplierAvlWorkflowOption,
  UpdateSupplierAvl,
} from '@/types/procurement-supplier-avl';

const root = '/procurement/supplier-avl';

export const procurementSupplierAvlService = {
  summary: () => apiService.get<SupplierAvlSummary>(`${root}/summary`),
  search: (request: SupplierAvlSearch) =>
    apiService.get<SupplierAvlPage>(root, request as Record<string, unknown>),
  get: (id: string) => apiService.get<SupplierAvlRegister>(`${root}/${id}`),
  current: (businessPartnerId: string) =>
    apiService.get<SupplierAvlCurrentState>(
      `${root}/suppliers/${businessPartnerId}/current`
    ),
  workflowOptions: () =>
    apiService.get<SupplierAvlWorkflowOption[]>(`${root}/workflow-options`),
  supplierOptions: () =>
    apiService.get<SupplierAvlSupplierOption[]>(`${root}/supplier-options`),
  create: (request: CreateSupplierAvl) =>
    apiService.post<SupplierAvlRegister>(root, request),
  update: (id: string, request: UpdateSupplierAvl) =>
    apiService.put<SupplierAvlRegister>(`${root}/${id}`, request),
  addEntry: (id: string, request: AddSupplierAvlEntry) =>
    apiService.post<SupplierAvlRegister>(`${root}/${id}/entries`, request),
  removeEntry: (id: string, entryId: string, rowVersion: string) =>
    apiService.delete<SupplierAvlRegister>(
      `${root}/${id}/entries/${entryId}?rowVersion=${encodeURIComponent(rowVersion)}`
    ),
  submit: (id: string, request: SupplierAvlLifecycleRequest) =>
    apiService.post<SupplierAvlRegister>(`${root}/${id}/submit`, request),
  approve: (id: string, request: SupplierAvlLifecycleRequest) =>
    apiService.post<SupplierAvlRegister>(`${root}/${id}/approve`, request),
  reject: (id: string, request: SupplierAvlLifecycleRequest) =>
    apiService.post<SupplierAvlRegister>(`${root}/${id}/reject`, request),
  publish: (id: string, request: SupplierAvlLifecycleRequest) =>
    apiService.post<SupplierAvlRegister>(`${root}/${id}/publish`, request),
  suspendEntry: (
    id: string,
    entryId: string,
    request: SupplierAvlEntryLifecycleRequest
  ) =>
    apiService.post<SupplierAvlRegister>(
      `${root}/${id}/entries/${entryId}/suspend`,
      request
    ),
  reinstateEntry: (
    id: string,
    entryId: string,
    request: SupplierAvlEntryLifecycleRequest
  ) =>
    apiService.post<SupplierAvlRegister>(
      `${root}/${id}/entries/${entryId}/reinstate`,
      request
    ),
  processExpiry: () =>
    apiService.post<{ processed: number }>(`${root}/process-expiry`, {}),
};
