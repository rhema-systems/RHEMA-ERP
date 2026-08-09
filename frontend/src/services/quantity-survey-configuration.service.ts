import { apiService } from '@/services/api.service';
import type {
  CreateQsProfileRequest,
  LinkQsEvidenceRequest,
  QsDecision,
  QsDecisionActionRequest,
  QsDecisionSchema,
  QsEvidence,
  QsLifecycleRequest,
  QsLookups,
  QsPagedResult,
  QsProfile,
  QsProfileStatus,
  QsProfileSummary,
  QsRevision,
  QsValidationResult,
  SaveQsDecisionRequest,
  UpdateQsProfileRequest,
} from '@/types/quantity-survey-configuration';

const root = '/quantity-survey/configuration-profiles';

export const quantitySurveyConfigurationService = {
  schemas: () => apiService.get<QsDecisionSchema[]>(`${root}/schemas`),
  lookups: () => apiService.get<QsLookups>(`${root}/lookups`),
  list: (query?: {
    search?: string;
    status?: QsProfileStatus;
    page?: number;
    pageSize?: number;
  }) => apiService.get<QsPagedResult<QsProfileSummary>>(root, query),
  get: (id: string) => apiService.get<QsProfile>(`${root}/${id}`),
  effective: (atUtc?: string) =>
    apiService.get<QsProfile>(`${root}/effective`, { atUtc }),
  create: (request: CreateQsProfileRequest) =>
    apiService.post<QsProfile>(root, request),
  update: (id: string, request: UpdateQsProfileRequest) =>
    apiService.put<QsProfile>(`${root}/${id}`, request),
  saveDecision: (id: string, key: string, request: SaveQsDecisionRequest) =>
    apiService.put<QsDecision>(`${root}/${id}/decisions/${key}`, request),
  submitDecision: (id: string, key: string, request: QsDecisionActionRequest) =>
    apiService.post<QsDecision>(
      `${root}/${id}/decisions/${key}/submit`,
      request
    ),
  approveDecision: (
    id: string,
    key: string,
    request: QsDecisionActionRequest
  ) =>
    apiService.post<QsDecision>(
      `${root}/${id}/decisions/${key}/approve`,
      request
    ),
  rejectDecision: (id: string, key: string, request: QsDecisionActionRequest) =>
    apiService.post<QsDecision>(
      `${root}/${id}/decisions/${key}/reject`,
      request
    ),
  linkEvidence: (id: string, key: string, request: LinkQsEvidenceRequest) =>
    apiService.post<QsEvidence>(
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
    apiService.post<QsValidationResult>(`${root}/${id}/validate`, {}),
  publish: (id: string, request: QsLifecycleRequest) =>
    apiService.post<QsProfile>(`${root}/${id}/publish`, request),
  retire: (id: string, request: QsLifecycleRequest) =>
    apiService.post<QsProfile>(`${root}/${id}/retire`, request),
  clone: (id: string, effectiveFrom?: string, changeSummary?: string) =>
    apiService.post<QsProfile>(`${root}/${id}/clone-draft`, {
      effectiveFrom,
      changeSummary,
    }),
  deleteDraft: (id: string, request: QsLifecycleRequest) =>
    apiService.request<void>(`${root}/${id}`, {
      method: 'DELETE',
      body: JSON.stringify(request),
    }),
  history: (id: string) =>
    apiService.get<QsRevision[]>(`${root}/${id}/history`),
};
