import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringSiteInstructionLookups,
  CivilEngineeringSiteInstructionRouting,
  CreateCivilEngineeringSiteInstructionRequest,
  FollowUpCivilEngineeringSiteInstructionRequest,
  ProcessCivilEngineeringSiteInstructionRoutingRequest,
  ReviewCivilEngineeringSiteInstructionResponseRequest,
  RespondToCivilEngineeringSiteInstructionRequest,
} from '@/types/civil-engineering-site-instruction';

const root = '/projects/civil-engineering/site-instructions';

export const civilEngineeringSiteInstructionService = {
  list: (projectId: string) =>
    apiService.get<CivilEngineeringSiteInstructionRouting[]>(root, {
      projectId,
    }),
  lookups: (projectId: string) =>
    apiService.get<CivilEngineeringSiteInstructionLookups>(`${root}/lookups`, {
      projectId,
    }),
  issue: (
    projectId: string,
    request: CreateCivilEngineeringSiteInstructionRequest
  ) =>
    apiService.post<CivilEngineeringSiteInstructionRouting>(
      `${root}/projects/${projectId}`,
      request
    ),
  processProjectManagerRouting: (
    routingId: string,
    request: ProcessCivilEngineeringSiteInstructionRoutingRequest
  ) =>
    apiService.post<CivilEngineeringSiteInstructionRouting>(
      `${root}/${routingId}/project-manager-routing`,
      request
    ),
  recordContractorResponse: (
    routingId: string,
    request: RespondToCivilEngineeringSiteInstructionRequest
  ) =>
    apiService.post<CivilEngineeringSiteInstructionRouting>(
      `${root}/${routingId}/contractor-response`,
      request
    ),
  reviewContractorResponse: (
    routingId: string,
    request: ReviewCivilEngineeringSiteInstructionResponseRequest
  ) =>
    apiService.post<CivilEngineeringSiteInstructionRouting>(
      `${root}/${routingId}/engineering-review`,
      request
    ),
  recordEngineeringFollowUp: (
    routingId: string,
    request: FollowUpCivilEngineeringSiteInstructionRequest
  ) =>
    apiService.post<CivilEngineeringSiteInstructionRouting>(
      `${root}/${routingId}/engineering-follow-up`,
      request
    ),
};
