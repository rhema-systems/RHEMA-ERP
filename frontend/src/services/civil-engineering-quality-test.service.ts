import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringQualityTestLookups,
  CivilEngineeringQualityTestReport,
  CreateCivilEngineeringQualityTestReportRequest,
  ProcessCivilEngineeringQualityTestReportRequest,
} from '@/types/civil-engineering-quality-test';

const root = '/projects/civil-engineering/quality-tests';

export const civilEngineeringQualityTestService = {
  list: (projectId: string) => apiService.get<CivilEngineeringQualityTestReport[]>(root, { projectId }),
  lookups: (projectId: string) => apiService.get<CivilEngineeringQualityTestLookups>(`${root}/lookups`, { projectId }),
  create: (projectId: string, request: CreateCivilEngineeringQualityTestReportRequest) =>
    apiService.post<CivilEngineeringQualityTestReport>(`${root}/projects/${projectId}`, request),
  review: (reportId: string, request: ProcessCivilEngineeringQualityTestReportRequest) =>
    apiService.post<CivilEngineeringQualityTestReport>(`${root}/${reportId}/review`, request),
};
