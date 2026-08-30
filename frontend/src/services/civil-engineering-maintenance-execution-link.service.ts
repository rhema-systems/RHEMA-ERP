import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringMaintenanceExecutionLink,
  CivilEngineeringMaintenanceExecutionLookups,
  CreateCivilEngineeringMaintenanceExecutionLinkRequest,
  RefreshCivilEngineeringMaintenanceExecutionLinkRequest,
} from '@/types/civil-engineering-maintenance-execution-link';

const root = '/projects/civil-engineering/maintenance-execution-links';

export const civilEngineeringMaintenanceExecutionLinkService = {
  lookups: () => apiService.get<CivilEngineeringMaintenanceExecutionLookups>(`${root}/lookups`),
  list: () => apiService.get<CivilEngineeringMaintenanceExecutionLink[]>(root),
  create: (request: CreateCivilEngineeringMaintenanceExecutionLinkRequest) => apiService.post<CivilEngineeringMaintenanceExecutionLink>(root, request),
  refresh: (executionLinkId: string, request: RefreshCivilEngineeringMaintenanceExecutionLinkRequest) => apiService.post<CivilEngineeringMaintenanceExecutionLink>(`${root}/${executionLinkId}/refresh`, request),
};
