import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringIpcEndorsement,
  CivilEngineeringIpcEndorsementLookups,
  ReviewCivilEngineeringIpcEndorsementRequest,
  SubmitCivilEngineeringIpcEndorsementRequest,
} from '@/types/civil-engineering-ipc-endorsement';

const root = '/projects/civil-engineering/ipc-endorsements';

export const civilEngineeringIpcEndorsementService = {
  list: (projectId: string) => apiService.get<CivilEngineeringIpcEndorsement[]>(root, { projectId }),
  lookups: (projectId: string) => apiService.get<CivilEngineeringIpcEndorsementLookups>(`${root}/lookups`, { projectId }),
  submit: (certificateId: string, request: SubmitCivilEngineeringIpcEndorsementRequest) =>
    apiService.post<CivilEngineeringIpcEndorsement>(`${root}/payment-certificates/${certificateId}/submit`, request),
  review: (endorsementId: string, request: ReviewCivilEngineeringIpcEndorsementRequest) =>
    apiService.post<CivilEngineeringIpcEndorsement>(`${root}/${endorsementId}/review`, request),
};
