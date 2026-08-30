import { apiService } from '@/services/api.service';
import type { CivilEngineeringInspectionControl, CivilEngineeringInspectionLookups, CivilEngineeringInspectionRevision, CreateCivilEngineeringInspectionControlRequest, ProcessCivilEngineeringInspectionControlRequest } from '@/types/civil-engineering-inspection-control';

const root = '/projects/civil-engineering/inspection-controls';

export const civilEngineeringInspectionControlService = {
  list: (projectId: string) => apiService.get<CivilEngineeringInspectionControl[]>(root, { projectId }),
  lookups: (projectId: string) => apiService.get<CivilEngineeringInspectionLookups>(`${root}/lookups`, { projectId }),
  create: (projectId: string, request: CreateCivilEngineeringInspectionControlRequest) => apiService.post<CivilEngineeringInspectionControl>(`${root}/projects/${projectId}`, request),
  process: (id: string, request: ProcessCivilEngineeringInspectionControlRequest) => apiService.post<CivilEngineeringInspectionControl>(`${root}/${id}/process`, request),
  history: (id: string) => apiService.get<CivilEngineeringInspectionRevision[]>(`${root}/${id}/history`),
};
