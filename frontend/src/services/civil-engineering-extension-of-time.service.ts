import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringExtensionOfTimeControl,
  CivilEngineeringExtensionOfTimeLookups,
  CivilEngineeringExtensionOfTimeRevision,
  CreateCivilEngineeringExtensionOfTimeRequest,
  ReviewCivilEngineeringExtensionOfTimeRequest,
} from '@/types/civil-engineering-extension-of-time';

const root = '/projects/civil-engineering/extension-of-time-controls';

export const civilEngineeringExtensionOfTimeService = {
  list: (projectId: string) => apiService.get<CivilEngineeringExtensionOfTimeControl[]>(root, { projectId }),
  lookups: (projectId: string) => apiService.get<CivilEngineeringExtensionOfTimeLookups>(`${root}/lookups`, { projectId }),
  create: (projectId: string, request: CreateCivilEngineeringExtensionOfTimeRequest) =>
    apiService.post<CivilEngineeringExtensionOfTimeControl>(`${root}/projects/${projectId}`, request),
  review: (controlId: string, request: ReviewCivilEngineeringExtensionOfTimeRequest) =>
    apiService.post<CivilEngineeringExtensionOfTimeControl>(`${root}/${controlId}/review`, request),
  history: (controlId: string) => apiService.get<CivilEngineeringExtensionOfTimeRevision[]>(`${root}/${controlId}/history`),
};
