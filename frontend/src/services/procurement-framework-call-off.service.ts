import { apiService } from '@/services/api.service';
import type {
  CreateFrameworkCallOff,
  FrameworkCallOff,
  FrameworkCallOffDecisionRequest,
  FrameworkCallOffLifecycleRequest,
  FrameworkCallOffOptions,
  FrameworkCallOffPage,
  FrameworkCallOffSearch,
  FrameworkCallOffSummary,
} from '@/types/procurement-framework-call-off';

const root = '/procurement/framework-call-offs';

export const procurementFrameworkCallOffService = {
  summary: () => apiService.get<FrameworkCallOffSummary>(`${root}/summary`),
  search: (request: FrameworkCallOffSearch) =>
    apiService.get<FrameworkCallOffPage>(
      root,
      request as Record<string, unknown>
    ),
  get: (id: string) => apiService.get<FrameworkCallOff>(`${root}/${id}`),
  options: () => apiService.get<FrameworkCallOffOptions>(`${root}/options`),
  create: (request: CreateFrameworkCallOff) =>
    apiService.post<FrameworkCallOff>(root, request),
  submit: (id: string, request: FrameworkCallOffLifecycleRequest) =>
    apiService.post<FrameworkCallOff>(`${root}/${id}/submit`, request),
  decide: (id: string, request: FrameworkCallOffDecisionRequest) =>
    apiService.post<FrameworkCallOff>(`${root}/${id}/decision`, request),
  issue: (id: string, request: FrameworkCallOffLifecycleRequest) =>
    apiService.post<FrameworkCallOff>(`${root}/${id}/issue`, request),
  cancel: (id: string, request: FrameworkCallOffLifecycleRequest) =>
    apiService.post<FrameworkCallOff>(`${root}/${id}/cancel`, request),
  processExpiryAlerts: () =>
    apiService.post<{ alerted: number }>(`${root}/process-expiry-alerts`, {}),
};
