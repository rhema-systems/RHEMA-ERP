import { apiService } from '@/services/api.service';
import type {
  CloneSupplierEvidencePackRequest,
  SaveSupplierEvidencePack,
  SupplierEvidencePack,
  SupplierEvidencePackLifecycleRequest,
  SupplierEvidencePackOption,
  SupplierEvidencePackPage,
  SupplierEvidencePackSearch,
  SupplierEvidencePackSummary,
  SupplierEvidenceReadiness,
} from '@/types/procurement-supplier-evidence-pack';

const root = '/procurement/supplier-evidence-packs';

export const procurementSupplierEvidencePackService = {
  summary: () => apiService.get<SupplierEvidencePackSummary>(`${root}/summary`),
  search: (request: SupplierEvidencePackSearch) =>
    apiService.get<SupplierEvidencePackPage>(
      root,
      request as Record<string, unknown>
    ),
  get: (id: string) => apiService.get<SupplierEvidencePack>(`${root}/${id}`),
  workflowOptions: () =>
    apiService.get<SupplierEvidencePackOption[]>(`${root}/workflow-options`),
  configurationProfileOptions: () =>
    apiService.get<SupplierEvidencePackOption[]>(
      `${root}/configuration-profile-options`
    ),
  create: (request: SaveSupplierEvidencePack) =>
    apiService.post<SupplierEvidencePack>(root, request),
  update: (id: string, request: SaveSupplierEvidencePack) =>
    apiService.put<SupplierEvidencePack>(`${root}/${id}`, request),
  submit: (id: string, request: SupplierEvidencePackLifecycleRequest) =>
    apiService.post<SupplierEvidencePack>(`${root}/${id}/submit`, request),
  publish: (id: string, request: SupplierEvidencePackLifecycleRequest) =>
    apiService.post<SupplierEvidencePack>(`${root}/${id}/publish`, request),
  reject: (id: string, request: SupplierEvidencePackLifecycleRequest) =>
    apiService.post<SupplierEvidencePack>(`${root}/${id}/reject`, request),
  retire: (id: string, request: SupplierEvidencePackLifecycleRequest) =>
    apiService.post<SupplierEvidencePack>(`${root}/${id}/retire`, request),
  clone: (id: string, request: CloneSupplierEvidencePackRequest) =>
    apiService.post<SupplierEvidencePack>(`${root}/${id}/clone`, request),
  deleteDraft: (id: string, request: SupplierEvidencePackLifecycleRequest) =>
    apiService.post<void>(`${root}/${id}/delete-draft`, request),
  registrationReadiness: (registrationId: string) =>
    apiService.get<SupplierEvidenceReadiness>(
      `${root}/registrations/${registrationId}/readiness`
    ),
};
