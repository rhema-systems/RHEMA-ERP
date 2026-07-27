import { apiService } from '@/services/api.service';
import type {
  CalculateSupplierPerformance,
  SupplierPerformanceCurrentState,
  SupplierPerformancePage,
  SupplierPerformanceScorecard,
  SupplierPerformanceSearch,
  SupplierPerformanceSummary,
  SupplierPerformanceSupplierOption,
} from '@/types/procurement-supplier-performance';

const root = '/procurement/supplier-performance-scorecards';

export const procurementSupplierPerformanceService = {
  summary: () => apiService.get<SupplierPerformanceSummary>(`${root}/summary`),
  search: (request: SupplierPerformanceSearch) =>
    apiService.get<SupplierPerformancePage>(
      root,
      request as Record<string, unknown>
    ),
  get: (id: string) =>
    apiService.get<SupplierPerformanceScorecard>(`${root}/${id}`),
  current: (businessPartnerId: string) =>
    apiService.get<SupplierPerformanceCurrentState>(
      `${root}/suppliers/${businessPartnerId}/current`
    ),
  supplierOptions: () =>
    apiService.get<SupplierPerformanceSupplierOption[]>(
      `${root}/supplier-options`
    ),
  calculate: (request: CalculateSupplierPerformance) =>
    apiService.post<SupplierPerformanceScorecard>(
      `${root}/calculate`,
      request
    ),
};
