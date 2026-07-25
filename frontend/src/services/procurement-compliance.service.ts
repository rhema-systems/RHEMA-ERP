import { apiService } from '@/services/api.service';
import type {
  ProcurementComplianceDecision,
  ProcurementComplianceDecisionRequest,
  ProcurementCompliancePolicyOption,
} from '@/types/procurement-compliance';

const root = '/procurement/compliance-decisions';

export const procurementComplianceService = {
  getPolicyOptions: (atUtc?: string) =>
    apiService.get<ProcurementCompliancePolicyOption[]>(`${root}/policy-options`, { atUtc }),
  evaluate: (request: ProcurementComplianceDecisionRequest) =>
    apiService.post<ProcurementComplianceDecision>(`${root}/evaluate`, request),
};
