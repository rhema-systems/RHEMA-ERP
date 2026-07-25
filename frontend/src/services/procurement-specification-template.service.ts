import { apiService } from '@/services/api.service';
import type {
  CloneProcurementSpecificationTemplateRequest,
  ProcurementSpecificationTemplate,
  ProcurementSpecificationTemplateLifecycleRequest,
  ProcurementSpecificationTemplatePage,
  ProcurementSpecificationTemplateSearch,
  ProcurementSpecificationTemplateSummary,
  ProcurementSpecificationWorkflowOption,
  SaveProcurementSpecificationTemplate,
} from '@/types/procurement-specification-template';

const root = '/procurement/specification-templates';

export const procurementSpecificationTemplateService = {
  summary: () =>
    apiService.get<ProcurementSpecificationTemplateSummary>(`${root}/summary`),
  workflowOptions: () =>
    apiService.get<ProcurementSpecificationWorkflowOption[]>(
      `${root}/workflow-options`
    ),
  search: (request: ProcurementSpecificationTemplateSearch) =>
    apiService.get<ProcurementSpecificationTemplatePage>(
      root,
      request as Record<string, unknown>
    ),
  get: (id: string) =>
    apiService.get<ProcurementSpecificationTemplate>(`${root}/${id}`),
  create: (request: SaveProcurementSpecificationTemplate) =>
    apiService.post<ProcurementSpecificationTemplate>(root, request),
  update: (id: string, request: SaveProcurementSpecificationTemplate) =>
    apiService.put<ProcurementSpecificationTemplate>(`${root}/${id}`, request),
  submit: (
    id: string,
    request: ProcurementSpecificationTemplateLifecycleRequest
  ) =>
    apiService.post<ProcurementSpecificationTemplate>(
      `${root}/${id}/submit`,
      request
    ),
  publish: (
    id: string,
    request: ProcurementSpecificationTemplateLifecycleRequest
  ) =>
    apiService.post<ProcurementSpecificationTemplate>(
      `${root}/${id}/publish`,
      request
    ),
  reject: (
    id: string,
    request: ProcurementSpecificationTemplateLifecycleRequest
  ) =>
    apiService.post<ProcurementSpecificationTemplate>(
      `${root}/${id}/reject`,
      request
    ),
  clone: (id: string, request: CloneProcurementSpecificationTemplateRequest) =>
    apiService.post<ProcurementSpecificationTemplate>(
      `${root}/${id}/clone`,
      request
    ),
  retire: (
    id: string,
    request: ProcurementSpecificationTemplateLifecycleRequest
  ) =>
    apiService.post<ProcurementSpecificationTemplate>(
      `${root}/${id}/retire`,
      request
    ),
  deleteDraft: (
    id: string,
    request: ProcurementSpecificationTemplateLifecycleRequest
  ) => apiService.post<void>(`${root}/${id}/delete-draft`, request),
};
