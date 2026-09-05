import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringPermittingEngineeringReview,
  CivilEngineeringPermittingEngineeringReviewLookups,
  SubmitCivilEngineeringPermittingEngineeringReviewRequest,
} from '@/types/civil-engineering-permitting-engineering-review';

const root = (fileId: string) => `/projects/civil-engineering/development-approval-files/${fileId}/engineering-reviews`;

export const civilEngineeringPermittingEngineeringReviewService = {
  lookups: (fileId: string) => apiService.get<CivilEngineeringPermittingEngineeringReviewLookups>(`${root(fileId)}/lookups`),
  list: (fileId: string) => apiService.get<CivilEngineeringPermittingEngineeringReview[]>(root(fileId)),
  submit: (fileId: string, request: SubmitCivilEngineeringPermittingEngineeringReviewRequest) => apiService.post<CivilEngineeringPermittingEngineeringReview>(root(fileId), request),
};
