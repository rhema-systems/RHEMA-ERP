import { apiService } from '@/services/api.service';
import type {
  CreateProcurementConfigurationProfileRequest,
  LinkProcurementConfigurationEvidenceRequest,
  ProcurementConfigurationEvidenceLink,
  ProcurementConfigurationLifecycleRequest,
  ProcurementConfigurationPagedResult,
  ProcurementConfigurationProfile,
  ProcurementConfigurationProfileStatus,
  ProcurementConfigurationProfileSummary,
  ProcurementConfigurationRevision,
  ProcurementConfigurationValidationResult,
  ProcurementDecisionSchema,
  SaveProcurementConfigurationDecisionRequest,
  UpdateProcurementConfigurationProfileRequest,
} from '@/types/procurement-configuration';

const root = '/procurement/configuration-profiles';

export const procurementConfigurationService = {
  getSchemas: () => apiService.get<ProcurementDecisionSchema[]>(`${root}/schemas`),

  list: (query?: {
    search?: string;
    status?: ProcurementConfigurationProfileStatus;
    page?: number;
    pageSize?: number;
  }) => apiService.get<ProcurementConfigurationPagedResult<ProcurementConfigurationProfileSummary>>(root, query),

  get: (id: string) => apiService.get<ProcurementConfigurationProfile>(`${root}/${id}`),

  getEffective: (profileCode = 'TDC-PROCUREMENT', atUtc?: string) =>
    apiService.get<ProcurementConfigurationProfile>(`${root}/effective`, { profileCode, atUtc }),

  create: (request: CreateProcurementConfigurationProfileRequest) =>
    apiService.post<ProcurementConfigurationProfile>(root, request),

  update: (id: string, request: UpdateProcurementConfigurationProfileRequest) =>
    apiService.put<ProcurementConfigurationProfile>(`${root}/${id}`, request),

  saveDecision: (id: string, decisionKey: string, request: SaveProcurementConfigurationDecisionRequest) =>
    apiService.put<ProcurementConfigurationProfile>(`${root}/${id}/decisions/${decisionKey}`, request),

  validate: (id: string) =>
    apiService.post<ProcurementConfigurationValidationResult>(`${root}/${id}/validate`, {}),

  publish: (id: string, request: ProcurementConfigurationLifecycleRequest) =>
    apiService.post<ProcurementConfigurationProfile>(`${root}/${id}/publish`, request),

  retire: (id: string, request: ProcurementConfigurationLifecycleRequest) =>
    apiService.post<ProcurementConfigurationProfile>(`${root}/${id}/retire`, request),

  withdrawDecision: (id: string, decisionKey: string, request: ProcurementConfigurationLifecycleRequest) =>
    apiService.post<ProcurementConfigurationProfile>(`${root}/${id}/decisions/${decisionKey}/withdraw`, request),

  cloneDraft: (id: string, changeSummary?: string) =>
    apiService.post<ProcurementConfigurationProfile>(`${root}/${id}/clone-draft`, { changeSummary }),

  deleteDraft: (id: string, request: ProcurementConfigurationLifecycleRequest) =>
    apiService.request<void>(`${root}/${id}`, { method: 'DELETE', body: JSON.stringify(request) }),

  history: (id: string) => apiService.get<ProcurementConfigurationRevision[]>(`${root}/${id}/history`),

  linkEvidence: (id: string, decisionKey: string, request: LinkProcurementConfigurationEvidenceRequest) =>
    apiService.post<ProcurementConfigurationEvidenceLink>(`${root}/${id}/decisions/${decisionKey}/evidence`, request),

  unlinkEvidence: (
    id: string,
    decisionKey: string,
    evidenceId: string,
    decisionRowVersion: string,
    reason?: string,
  ) => apiService.delete<void>(
    `${root}/${id}/decisions/${decisionKey}/evidence/${evidenceId}` +
      `?decisionRowVersion=${encodeURIComponent(decisionRowVersion)}` +
      (reason ? `&reason=${encodeURIComponent(reason)}` : ''),
  ),
};
