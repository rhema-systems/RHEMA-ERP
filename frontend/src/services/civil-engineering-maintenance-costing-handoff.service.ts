import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringMaintenanceCostingHandoff,
  CivilEngineeringMaintenanceCostingHandoffActionRequest,
  CivilEngineeringMaintenanceCostingHandoffLookups,
  CreateCivilEngineeringMaintenanceCostingHandoffRequest,
} from '@/types/civil-engineering-maintenance-costing-handoff';

const root = '/projects/civil-engineering/maintenance-costing-handoffs';

export const civilEngineeringMaintenanceCostingHandoffService = {
  lookups: () => apiService.get<CivilEngineeringMaintenanceCostingHandoffLookups>(`${root}/lookups`),
  list: () => apiService.get<CivilEngineeringMaintenanceCostingHandoff[]>(root),
  create: (request: CreateCivilEngineeringMaintenanceCostingHandoffRequest) => apiService.post<CivilEngineeringMaintenanceCostingHandoff>(root, request),
  act: (handoffId: string, request: CivilEngineeringMaintenanceCostingHandoffActionRequest) => apiService.post<CivilEngineeringMaintenanceCostingHandoff>(`${root}/${handoffId}/actions`, request),
};
