import { apiService } from '@/services/api.service';
import type {
  ProcurementSodBypassAudit,
  ProcurementSodCoverage,
  ProcurementSodGuardDecision,
  ProcurementSodGuardRequest,
  ProcurementSodProvisionResult,
} from '@/types/procurement-sod';

const root = '/procurement/sod-controls';

export const procurementSodService = {
  coverage: (atUtc?: string) => apiService.get<ProcurementSodCoverage>(`${root}/coverage`, { atUtc }),
  applyRequired: (policySetId: string, reason: string) =>
    apiService.post<ProcurementSodProvisionResult>(`${root}/policies/${policySetId}/apply-required`, { reason }),
  check: (request: ProcurementSodGuardRequest) =>
    apiService.post<ProcurementSodGuardDecision>(`${root}/check`, request),
  enforce: (request: ProcurementSodGuardRequest) =>
    apiService.post<ProcurementSodGuardDecision>(`${root}/enforce`, request),
  blockedAttempts: (take = 50) =>
    apiService.get<ProcurementSodBypassAudit[]>(`${root}/blocked-attempts`, { take }),
};
