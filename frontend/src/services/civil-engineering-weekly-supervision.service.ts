import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringWeeklySupervisionLookups,
  CivilEngineeringWeeklySupervisionReport,
  CreateCivilEngineeringWeeklySupervisionReportRequest,
  ProcessCivilEngineeringWeeklySupervisionReportRequest,
} from '@/types/civil-engineering-weekly-supervision';

const root = '/projects/civil-engineering/weekly-supervision-reports';

export const civilEngineeringWeeklySupervisionService = {
  list: (projectId: string) => apiService.get<CivilEngineeringWeeklySupervisionReport[]>(root, { projectId }),
  lookups: (projectId: string) => apiService.get<CivilEngineeringWeeklySupervisionLookups>(`${root}/lookups`, { projectId }),
  create: (projectId: string, request: CreateCivilEngineeringWeeklySupervisionReportRequest) =>
    apiService.post<CivilEngineeringWeeklySupervisionReport>(`${root}/projects/${projectId}`, request),
  review: (reportId: string, request: ProcessCivilEngineeringWeeklySupervisionReportRequest) =>
    apiService.post<CivilEngineeringWeeklySupervisionReport>(`${root}/${reportId}/review`, request),
  escalateOverdue: (projectId: string, clientRequestId: string) =>
    apiService.post<CivilEngineeringWeeklySupervisionReport[]>(`${root}/projects/${projectId}/escalate-overdue?clientRequestId=${encodeURIComponent(clientRequestId)}`),
};
