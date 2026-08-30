import { apiService } from '@/services/api.service';
import type { CivilEngineeringDevelopmentApprovalFile, CivilEngineeringDevelopmentApprovalLookups, CreateCivilEngineeringDevelopmentApprovalFileRequest, RecordCivilEngineeringSiteInspectionRequest } from '@/types/civil-engineering-development-approval-file';

const root = '/projects/civil-engineering/development-approval-files';

export const civilEngineeringDevelopmentApprovalFileService = {
  lookups: () => apiService.get<CivilEngineeringDevelopmentApprovalLookups>(`${root}/lookups`),
  list: () => apiService.get<CivilEngineeringDevelopmentApprovalFile[]>(root),
  create: (request: CreateCivilEngineeringDevelopmentApprovalFileRequest) => apiService.post<CivilEngineeringDevelopmentApprovalFile>(root, request),
  recordSiteInspection: (fileId: string, request: RecordCivilEngineeringSiteInspectionRequest) => apiService.post<CivilEngineeringDevelopmentApprovalFile>(`${root}/${fileId}/site-inspection`, request),
};
