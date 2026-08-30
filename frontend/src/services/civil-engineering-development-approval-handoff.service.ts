import { apiService } from '@/services/api.service';
import type { CivilEngineeringDevelopmentApprovalHandoff, CivilEngineeringDevelopmentApprovalHandoffLookups, CreateCivilEngineeringDevelopmentApprovalHandoffRequest } from '@/types/civil-engineering-development-approval-handoff';

const root = (fileId: string) => `/projects/civil-engineering/development-approval-files/${fileId}/handoffs`;

export const civilEngineeringDevelopmentApprovalHandoffService = {
  lookups: (fileId: string) => apiService.get<CivilEngineeringDevelopmentApprovalHandoffLookups>(`${root(fileId)}/lookups`),
  list: (fileId: string) => apiService.get<CivilEngineeringDevelopmentApprovalHandoff[]>(root(fileId)),
  create: (fileId: string, request: CreateCivilEngineeringDevelopmentApprovalHandoffRequest) => apiService.post<CivilEngineeringDevelopmentApprovalHandoff>(root(fileId), request),
};
