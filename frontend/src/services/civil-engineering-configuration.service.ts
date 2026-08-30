import { apiService } from '@/services/api.service';
import type {
  CreateCivilProfileRequest,
  LinkCivilEvidenceRequest,
  CivilDecision,
  CivilDecisionActionRequest,
  CivilDecisionSchema,
  CivilEvidence,
  CivilLifecycleRequest,
  CivilLookups,
  CivilPagedResult,
  CivilProfile,
  CivilProfileStatus,
  CivilProfileSummary,
  CivilRevision,
  CivilValidationResult,
  SaveCivilDecisionRequest,
  UpdateCivilProfileRequest,
} from '@/types/civil-engineering-configuration';

const root = '/civil-engineering/configuration-profiles';

export const civilEngineeringConfigurationService = {
  schemas: () => apiService.get<CivilDecisionSchema[]>(`${root}/schemas`),
  lookups: () => apiService.get<CivilLookups>(`${root}/lookups`),
  list: (query?: {
    search?: string;
    status?: CivilProfileStatus;
    page?: number;
    pageSize?: number;
  }) => apiService.get<CivilPagedResult<CivilProfileSummary>>(root, query),
  get: (id: string) => apiService.get<CivilProfile>(`${root}/${id}`),
  effective: (atUtc?: string) =>
    apiService.get<CivilProfile>(`${root}/effective`, { atUtc }),
  create: (request: CreateCivilProfileRequest) =>
    apiService.post<CivilProfile>(root, request),
  update: (id: string, request: UpdateCivilProfileRequest) =>
    apiService.put<CivilProfile>(`${root}/${id}`, request),
  saveDecision: (id: string, key: string, request: SaveCivilDecisionRequest) =>
    apiService.put<CivilDecision>(`${root}/${id}/decisions/${key}`, request),
  submitDecision: (
    id: string,
    key: string,
    request: CivilDecisionActionRequest
  ) =>
    apiService.post<CivilDecision>(
      `${root}/${id}/decisions/${key}/submit`,
      request
    ),
  approveDecision: (
    id: string,
    key: string,
    request: CivilDecisionActionRequest
  ) =>
    apiService.post<CivilDecision>(
      `${root}/${id}/decisions/${key}/approve`,
      request
    ),
  rejectDecision: (
    id: string,
    key: string,
    request: CivilDecisionActionRequest
  ) =>
    apiService.post<CivilDecision>(
      `${root}/${id}/decisions/${key}/reject`,
      request
    ),
  linkEvidence: (id: string, key: string, request: LinkCivilEvidenceRequest) =>
    apiService.post<CivilEvidence>(
      `${root}/${id}/decisions/${key}/evidence`,
      request
    ),
  unlinkEvidence: (
    id: string,
    key: string,
    evidenceId: string,
    rowVersion: string,
    reason?: string
  ) =>
    apiService.delete<void>(
      `${root}/${id}/decisions/${key}/evidence/${evidenceId}?rowVersion=${encodeURIComponent(rowVersion)}${reason ? `&reason=${encodeURIComponent(reason)}` : ''}`
    ),
  validate: (id: string) =>
    apiService.post<CivilValidationResult>(`${root}/${id}/validate`, {}),
  publish: (id: string, request: CivilLifecycleRequest) =>
    apiService.post<CivilProfile>(`${root}/${id}/publish`, request),
  retire: (id: string, request: CivilLifecycleRequest) =>
    apiService.post<CivilProfile>(`${root}/${id}/retire`, request),
  clone: (id: string, effectiveFrom?: string, changeSummary?: string) =>
    apiService.post<CivilProfile>(`${root}/${id}/clone-draft`, {
      effectiveFrom,
      changeSummary,
    }),
  deleteDraft: (id: string, request: CivilLifecycleRequest) =>
    apiService.request<void>(`${root}/${id}`, {
      method: 'DELETE',
      body: JSON.stringify(request),
    }),
  history: (id: string) =>
    apiService.get<CivilRevision[]>(`${root}/${id}/history`),
};
