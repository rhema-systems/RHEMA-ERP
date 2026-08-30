import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringMaintenanceAssessment,
  CivilEngineeringMaintenanceAssessmentLookups,
  CivilEngineeringMaintenanceAssessmentTransitionRequest,
  StartCivilEngineeringMaintenanceAssessmentRequest,
} from '@/types/civil-engineering-maintenance-assessment';

const root = '/projects/civil-engineering/maintenance-assessments';

export const civilEngineeringMaintenanceAssessmentService = {
  lookups: () => apiService.get<CivilEngineeringMaintenanceAssessmentLookups>(`${root}/lookups`),
  list: () => apiService.get<CivilEngineeringMaintenanceAssessment[]>(root),
  start: (intakeId: string, request: StartCivilEngineeringMaintenanceAssessmentRequest) =>
    apiService.post<CivilEngineeringMaintenanceAssessment>(`${root}/intakes/${intakeId}`, request),
  transition: (assessmentId: string, request: CivilEngineeringMaintenanceAssessmentTransitionRequest) =>
    apiService.post<CivilEngineeringMaintenanceAssessment>(`${root}/${assessmentId}/transition`, request),
};
