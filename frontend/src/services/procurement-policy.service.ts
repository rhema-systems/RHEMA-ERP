import { apiService } from '@/services/api.service';
import type {
  CreateProcurementPolicySetRequest,
  ProcurementPolicyLifecycleRequest,
  ProcurementPolicyLifecycleStatus,
  ProcurementPolicyPagedResult,
  ProcurementPolicyRevision,
  ProcurementPolicyRule,
  ProcurementPolicyRuleKind,
  ProcurementPolicySet,
  ProcurementPolicyValidationResult,
  SaveProcurementPolicyRuleRequest,
  UpdateProcurementPolicySetRequest,
} from '@/types/procurement-policy';

const root = '/procurement/policy-sets';

export const procurementPolicyService = {
  list: (query?: { search?: string; status?: ProcurementPolicyLifecycleStatus; page?: number; pageSize?: number }) =>
    apiService.get<ProcurementPolicyPagedResult>(root, query),
  get: (id: string) => apiService.get<ProcurementPolicySet>(`${root}/${id}`),
  getEffective: (code: string, atUtc?: string) => apiService.get<ProcurementPolicySet>(`${root}/effective`, { code, atUtc }),
  create: (request: CreateProcurementPolicySetRequest) => apiService.post<ProcurementPolicySet>(root, request),
  update: (id: string, request: UpdateProcurementPolicySetRequest) => apiService.put<ProcurementPolicySet>(`${root}/${id}`, request),
  createRule: (id: string, request: SaveProcurementPolicyRuleRequest) => apiService.post<ProcurementPolicyRule>(`${root}/${id}/rules`, request),
  updateRule: (id: string, ruleId: string, request: SaveProcurementPolicyRuleRequest) => apiService.put<ProcurementPolicyRule>(`${root}/${id}/rules/${ruleId}`, request),
  deleteRule: (id: string, kind: ProcurementPolicyRuleKind, ruleId: string, request: ProcurementPolicyLifecycleRequest) =>
    apiService.request<void>(`${root}/${id}/rules/${kind}/${ruleId}`, { method: 'DELETE', body: JSON.stringify(request) }),
  validate: (id: string) => apiService.post<ProcurementPolicyValidationResult>(`${root}/${id}/validate`, {}),
  publish: (id: string, request: ProcurementPolicyLifecycleRequest) => apiService.post<ProcurementPolicySet>(`${root}/${id}/publish`, request),
  retire: (id: string, request: ProcurementPolicyLifecycleRequest) => apiService.post<ProcurementPolicySet>(`${root}/${id}/retire`, request),
  cloneDraft: (id: string, changeSummary?: string) => apiService.post<ProcurementPolicySet>(`${root}/${id}/clone-draft`, { changeSummary }),
  deleteDraft: (id: string, request: ProcurementPolicyLifecycleRequest) =>
    apiService.request<void>(`${root}/${id}`, { method: 'DELETE', body: JSON.stringify(request) }),
  history: (id: string) => apiService.get<ProcurementPolicyRevision[]>(`${root}/${id}/history`),
};
