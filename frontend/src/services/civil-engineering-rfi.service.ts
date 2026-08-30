import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringRfiLookups,
  CivilEngineeringRfiRouting,
  ProcessCivilEngineeringRfiResponseRequest,
  SubmitCivilEngineeringRfiResponseRequest,
} from '@/types/civil-engineering-rfi';

const root = '/projects/civil-engineering/rfis';

export const civilEngineeringRfiService = {
  list: (projectId: string) =>
    apiService.get<CivilEngineeringRfiRouting[]>(root, { projectId }),
  lookups: (projectId: string) =>
    apiService.get<CivilEngineeringRfiLookups>(`${root}/lookups`, { projectId }),
  submitProjectEngineerResponse: (
    routingId: string,
    request: SubmitCivilEngineeringRfiResponseRequest
  ) =>
    apiService.post<CivilEngineeringRfiRouting>(
      `${root}/${routingId}/project-engineer-response`,
      request
    ),
  processProjectManagerResponse: (
    routingId: string,
    request: ProcessCivilEngineeringRfiResponseRequest
  ) =>
    apiService.post<CivilEngineeringRfiRouting>(
      `${root}/${routingId}/project-manager-response`,
      request
    ),
};
