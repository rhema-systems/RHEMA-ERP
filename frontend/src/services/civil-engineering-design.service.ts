import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringDesignCase,
  CivilEngineeringCommercialReadiness,
  CivilEngineeringDocument,
  CivilEngineeringDocumentLookups,
  CivilEngineeringDesignInputRequest,
  CivilEngineeringDesignLookups,
  CivilEngineeringDesignRevision,
  CivilEngineeringDesignTransitionRequest,
  CivilEngineeringPlanningGisLookups,
  CivilEngineeringPlanningGisValidation,
  CivilEngineeringPlanningGisValidationStatus,
  CreateCivilEngineeringPlanningGisValidationRequest,
  CivilEngineeringReconnaissanceReport,
  CreateCivilEngineeringReconnaissanceRequest,
  CreateCivilEngineeringDesignInputRequest,
  CreateCivilEngineeringDesignCaseRequest,
  CreateCivilEngineeringDocumentRequest,
  ReviewCivilEngineeringDesignInputRequest,
  SubmitCivilEngineeringDesignInputResponseRequest,
  UpdateCivilEngineeringReconnaissanceRequest,
} from '@/types/civil-engineering-design';

const root = '/projects/civil-engineering/design-cases';

export const civilEngineeringDesignService = {
  list: (projectId: string) =>
    apiService.get<CivilEngineeringDesignCase[]>(root, { projectId }),
  lookups: (projectId: string) =>
    apiService.get<CivilEngineeringDesignLookups>(`${root}/lookups`, {
      projectId,
    }),
  get: (id: string) =>
    apiService.get<CivilEngineeringDesignCase>(`${root}/${id}`),
  commercialReadiness: (id: string) =>
    apiService.get<CivilEngineeringCommercialReadiness>(
      `${root}/${id}/commercial-readiness`
    ),
  create: (request: CreateCivilEngineeringDesignCaseRequest) =>
    apiService.post<CivilEngineeringDesignCase>(root, request),
  transition: (id: string, request: CivilEngineeringDesignTransitionRequest) =>
    apiService.post<CivilEngineeringDesignCase>(
      `${root}/${id}/transition`,
      request
    ),
  decide: (id: string, request: CivilEngineeringDesignTransitionRequest) =>
    apiService.post<CivilEngineeringDesignCase>(
      `${root}/${id}/decision`,
      request
    ),
  history: (id: string) =>
    apiService.get<CivilEngineeringDesignRevision[]>(`${root}/${id}/history`),
  listReconnaissance: (designCaseId: string) =>
    apiService.get<CivilEngineeringReconnaissanceReport[]>(
      `${root}/${designCaseId}/reconnaissance`
    ),
  createReconnaissance: (
    designCaseId: string,
    request: CreateCivilEngineeringReconnaissanceRequest
  ) =>
    apiService.post<CivilEngineeringReconnaissanceReport>(
      `${root}/${designCaseId}/reconnaissance`,
      request
    ),
  updateReconnaissance: (
    designCaseId: string,
    reportId: string,
    request: UpdateCivilEngineeringReconnaissanceRequest
  ) =>
    apiService.put<CivilEngineeringReconnaissanceReport>(
      `${root}/${designCaseId}/reconnaissance/${reportId}`,
      request
    ),
  completeReconnaissance: (
    designCaseId: string,
    reportId: string,
    request: { clientRequestId: string; rowVersion: string; reason: string }
  ) =>
    apiService.post<CivilEngineeringReconnaissanceReport>(
      `${root}/${designCaseId}/reconnaissance/${reportId}/complete`,
      request
    ),
  listInformationRequests: (designCaseId: string) =>
    apiService.get<CivilEngineeringDesignInputRequest[]>(
      `${root}/${designCaseId}/information-requests`
    ),
  listAssignedInformationRequests: () =>
    apiService.get<CivilEngineeringDesignInputRequest[]>(
      `${root}/information-requests/assigned`
    ),
  createInformationRequest: (
    designCaseId: string,
    request: CreateCivilEngineeringDesignInputRequest
  ) =>
    apiService.post<CivilEngineeringDesignInputRequest>(
      `${root}/${designCaseId}/information-requests`,
      request
    ),
  submitInformationResponse: (
    requestId: string,
    request: SubmitCivilEngineeringDesignInputResponseRequest
  ) =>
    apiService.post<CivilEngineeringDesignInputRequest>(
      `${root}/information-requests/${requestId}/response`,
      request
    ),
  reviewInformationResponse: (
    requestId: string,
    request: ReviewCivilEngineeringDesignInputRequest
  ) =>
    apiService.post<CivilEngineeringDesignInputRequest>(
      `${root}/information-requests/${requestId}/review`,
      request
    ),
  documentLookups: (designCaseId: string) =>
    apiService.get<CivilEngineeringDocumentLookups>(
      `${root}/${designCaseId}/documents/lookups`
    ),
  listDocuments: (designCaseId: string) =>
    apiService.get<CivilEngineeringDocument[]>(
      `${root}/${designCaseId}/documents`
    ),
  createDocument: (
    designCaseId: string,
    request: CreateCivilEngineeringDocumentRequest
  ) =>
    apiService.post<CivilEngineeringDocument>(
      `${root}/${designCaseId}/documents`,
      request
    ),
  submitDocument: (
    documentId: string,
    request: { clientRequestId: string; rowVersion: string }
  ) =>
    apiService.post<CivilEngineeringDocument>(
      `${root}/documents/${documentId}/submit`,
      request
    ),
  reviewDocument: (
    documentId: string,
    request: {
      clientRequestId: string;
      rowVersion: string;
      approve: boolean;
      reason: string;
    }
  ) =>
    apiService.post<CivilEngineeringDocument>(
      `${root}/documents/${documentId}/decision`,
      request
    ),
  planningGisLookups: (designCaseId: string) =>
    apiService.get<CivilEngineeringPlanningGisLookups>(
      `${root}/${designCaseId}/planning-gis/lookups`
    ),
  listPlanningGis: (designCaseId: string) =>
    apiService.get<CivilEngineeringPlanningGisValidation[]>(
      `${root}/${designCaseId}/planning-gis`
    ),
  createPlanningGis: (
    designCaseId: string,
    request: CreateCivilEngineeringPlanningGisValidationRequest
  ) =>
    apiService.post<CivilEngineeringPlanningGisValidation>(
      `${root}/${designCaseId}/planning-gis`,
      request
    ),
  submitPlanningGis: (
    validationId: string,
    request: { clientRequestId: string; rowVersion: string }
  ) =>
    apiService.post<CivilEngineeringPlanningGisValidation>(
      `${root}/planning-gis/${validationId}/submit`,
      request
    ),
  decidePlanningGis: (
    validationId: string,
    request: {
      clientRequestId: string;
      rowVersion: string;
      outcome: Extract<
        CivilEngineeringPlanningGisValidationStatus,
        'Approved' | 'Rejected'
      >;
    }
  ) =>
    apiService.post<CivilEngineeringPlanningGisValidation>(
      `${root}/planning-gis/${validationId}/decision`,
      request
    ),
};
