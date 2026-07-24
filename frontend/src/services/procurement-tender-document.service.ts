import { apiService } from '@/services/api.service';
import type {
  AcknowledgeProcurementTenderDocumentChangeRequest,
  BindProcurementTenderDocumentRequest,
  CloneProcurementTenderDocumentTemplateRequest,
  CreateProcurementTenderDocumentChangeRequest,
  DecideProcurementTenderDocumentChangeRequest,
  IssueProcurementTenderDocumentRegisterRequest,
  ProcurementTenderDocumentChange,
  ProcurementTenderDocumentAcknowledgement,
  ProcurementTenderDocumentIssuance,
  ProcurementTenderDocumentLifecycleRequest,
  ProcurementTenderDocumentPolicyOption,
  ProcurementTenderDocumentReadiness,
  ProcurementTenderDocumentRegister,
  ProcurementTenderDocumentSearch,
  ProcurementTenderDocumentSourceType,
  ProcurementTenderDocumentTemplate,
  ProcurementTenderDocumentTemplatePage,
  ProcurementTenderDocumentTemplateSummary,
  ProcurementTenderDocumentWorkflowOption,
  SaveProcurementTenderDocumentTemplate,
} from '@/types/procurement-tender-document';

const templateRoot = '/procurement/tender-document-templates';
const registerRoot = '/procurement/tender-document-register';

export const procurementTenderDocumentService = {
  templateSummary: () =>
    apiService.get<ProcurementTenderDocumentTemplateSummary>(
      `${templateRoot}/summary`
    ),
  workflowOptions: () =>
    apiService.get<ProcurementTenderDocumentWorkflowOption[]>(
      `${templateRoot}/workflow-options`
    ),
  policyOptions: () =>
    apiService.get<ProcurementTenderDocumentPolicyOption[]>(
      `${templateRoot}/policy-options`
    ),
  searchTemplates: (request: ProcurementTenderDocumentSearch) =>
    apiService.get<ProcurementTenderDocumentTemplatePage>(
      templateRoot,
      request as Record<string, unknown>
    ),
  getTemplate: (id: string) =>
    apiService.get<ProcurementTenderDocumentTemplate>(`${templateRoot}/${id}`),
  createTemplate: (request: SaveProcurementTenderDocumentTemplate) =>
    apiService.post<ProcurementTenderDocumentTemplate>(templateRoot, request),
  updateTemplate: (
    id: string,
    request: SaveProcurementTenderDocumentTemplate
  ) =>
    apiService.put<ProcurementTenderDocumentTemplate>(
      `${templateRoot}/${id}`,
      request
    ),
  submitTemplate: (
    id: string,
    request: ProcurementTenderDocumentLifecycleRequest
  ) =>
    apiService.post<ProcurementTenderDocumentTemplate>(
      `${templateRoot}/${id}/submit`,
      request
    ),
  publishTemplate: (
    id: string,
    request: ProcurementTenderDocumentLifecycleRequest
  ) =>
    apiService.post<ProcurementTenderDocumentTemplate>(
      `${templateRoot}/${id}/publish`,
      request
    ),
  rejectTemplate: (
    id: string,
    request: ProcurementTenderDocumentLifecycleRequest
  ) =>
    apiService.post<ProcurementTenderDocumentTemplate>(
      `${templateRoot}/${id}/reject`,
      request
    ),
  cloneTemplate: (
    id: string,
    request: CloneProcurementTenderDocumentTemplateRequest
  ) =>
    apiService.post<ProcurementTenderDocumentTemplate>(
      `${templateRoot}/${id}/clone`,
      request
    ),
  retireTemplate: (
    id: string,
    request: ProcurementTenderDocumentLifecycleRequest
  ) =>
    apiService.post<ProcurementTenderDocumentTemplate>(
      `${templateRoot}/${id}/retire`,
      request
    ),
  deleteDraft: (
    id: string,
    request: ProcurementTenderDocumentLifecycleRequest
  ) => apiService.post<void>(`${templateRoot}/${id}/delete-draft`, request),

  readiness: (
    sourceType: ProcurementTenderDocumentSourceType,
    sourceId: string
  ) =>
    apiService.get<ProcurementTenderDocumentReadiness>(
      `${registerRoot}/readiness`,
      { sourceType, sourceId }
    ),
  getRegister: (
    sourceType: ProcurementTenderDocumentSourceType,
    sourceId: string
  ) =>
    apiService.get<ProcurementTenderDocumentRegister>(
      registerRoot,
      { sourceType, sourceId }
    ),
  bind: (request: BindProcurementTenderDocumentRequest) =>
    apiService.post<ProcurementTenderDocumentRegister>(
      `${registerRoot}/bind`,
      request
    ),
  issue: (request: IssueProcurementTenderDocumentRegisterRequest) =>
    apiService.post<ProcurementTenderDocumentIssuance>(
      `${registerRoot}/issue`,
      request
    ),
  createChange: (request: CreateProcurementTenderDocumentChangeRequest) =>
    apiService.post<ProcurementTenderDocumentChange>(
      `${registerRoot}/changes`,
      request
    ),
  decideChange: (
    changeId: string,
    request: DecideProcurementTenderDocumentChangeRequest
  ) =>
    apiService.post<ProcurementTenderDocumentChange>(
      `${registerRoot}/changes/${changeId}/decision`,
      request
    ),
  acknowledge: (request: AcknowledgeProcurementTenderDocumentChangeRequest) =>
    apiService.post<ProcurementTenderDocumentAcknowledgement>(
      `${registerRoot}/acknowledgements`,
      request
    ),
};
