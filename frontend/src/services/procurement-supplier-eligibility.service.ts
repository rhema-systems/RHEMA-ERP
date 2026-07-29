import { apiService } from '@/services/api.service';
import type {
  SupplierEligibilityBoundaryOption,
  SupplierEligibilityEvaluationRequest,
  SupplierEligibilityResult,
} from '@/types/procurement-supplier-eligibility';

const root = '/procurement/supplier-validation';

export const procurementSupplierEligibilityService = {
  boundaries: () =>
    apiService.get<SupplierEligibilityBoundaryOption[]>(`${root}/boundaries`),
  evaluate: (request: SupplierEligibilityEvaluationRequest) =>
    apiService.post<SupplierEligibilityResult>(`${root}/eligibility`, request),
};
