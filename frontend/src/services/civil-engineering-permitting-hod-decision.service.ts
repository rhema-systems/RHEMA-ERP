import { apiService } from '@/services/api.service';
import type { CivilEngineeringPermittingHodDecisionQueueItem, DecideCivilEngineeringPermittingReviewRequest } from '@/types/civil-engineering-permitting-hod-decision';

const root = '/projects/civil-engineering/permitting-engineering-reviews';

export const civilEngineeringPermittingHodDecisionService = {
  pending: () => apiService.get<CivilEngineeringPermittingHodDecisionQueueItem[]>(`${root}/pending-hod-decisions`),
  decide: (engineeringReviewId: string, request: DecideCivilEngineeringPermittingReviewRequest) => apiService.post<CivilEngineeringPermittingHodDecisionQueueItem>(`${root}/${engineeringReviewId}/hod-decision`, request),
};
