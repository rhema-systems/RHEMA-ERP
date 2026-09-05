import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringMaintenanceIntake,
  CivilEngineeringMaintenanceIntakeLookups,
  CreateCivilEngineeringMaintenanceIntakeRequest,
} from '@/types/civil-engineering-maintenance-intake';

const root = '/projects/civil-engineering/maintenance-intakes';

export const civilEngineeringMaintenanceIntakeService = {
  lookups: () => apiService.get<CivilEngineeringMaintenanceIntakeLookups>(`${root}/lookups`),
  list: () => apiService.get<CivilEngineeringMaintenanceIntake[]>(root),
  create: (request: CreateCivilEngineeringMaintenanceIntakeRequest) =>
    apiService.post<CivilEngineeringMaintenanceIntake>(root, request),
};
