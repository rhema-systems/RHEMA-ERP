import { apiService } from '@/services/api.service';
import type { CivilEngineeringMaintenanceCompletionControl, CivilEngineeringMaintenanceCompletionLookups, CreateCivilEngineeringMaintenanceCompletionControlRequest, ProcessCivilEngineeringMaintenanceCompletionControlRequest } from '@/types/civil-engineering-maintenance-completion-control';

const root = '/projects/civil-engineering/maintenance-completion-controls';

export const civilEngineeringMaintenanceCompletionControlService = {
  lookups: () => apiService.get<CivilEngineeringMaintenanceCompletionLookups>(`${root}/lookups`),
  list: () => apiService.get<CivilEngineeringMaintenanceCompletionControl[]>(root),
  create: (request: CreateCivilEngineeringMaintenanceCompletionControlRequest) => apiService.post<CivilEngineeringMaintenanceCompletionControl>(root, request),
  process: (completionControlId: string, request: ProcessCivilEngineeringMaintenanceCompletionControlRequest) => apiService.post<CivilEngineeringMaintenanceCompletionControl>(`${root}/${completionControlId}/process`, request),
};
