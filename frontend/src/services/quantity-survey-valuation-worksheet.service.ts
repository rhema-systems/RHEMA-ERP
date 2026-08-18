import { apiService } from '@/services/api.service';

const root = '/quantity-survey/valuation-worksheets';
const externalRoot = (projectId: string) =>
  `/projects/external/my-projects/${projectId}/valuation-worksheets`;

export type ValuationEvidenceType =
  | 'ContractorClaim'
  | 'MeasurementSupport'
  | 'SiteRecord'
  | 'ConsultantReview'
  | 'SupportingDocument';

export interface ValuationBoqLookup {
  id: string;
  versionNumber: number;
  label: string;
  lineCount: number;
}
export interface ValuationWorksheetLookups {
  approvedBoqVersions: ValuationBoqLookup[];
  contractors: ValuationPartnerLookup[];
  consultants: ValuationPartnerLookup[];
}
export interface ValuationPartnerLookup {
  id: string;
  label: string;
  description?: string | null;
}
export interface ValuationWorksheetLine {
  id?: string;
  projectBoqVersionLineId: string;
  boqLineKey: string;
  sequence: number;
  label: string;
  description: string;
  unitOfMeasure?: string | null;
  currency: string;
  boqQuantity: number;
  unitRate: number;
  measuredToDateQuantity: number;
  previouslyCertifiedQuantity: number;
  currentClaimedQuantity: number;
  currentCertifiedQuantity: number;
  disputedQuantity: number;
  measuredToDateValue: number;
  previouslyCertifiedValue: number;
  currentClaimedValue: number;
  currentCertifiedValue: number;
  currentPeriodCertifiedValue: number;
  disputedValue: number;
  previousRetentionValue: number;
  retentionToDateValue: number;
  currentRetentionValue: number;
  netCurrentValue: number;
  reviewNote?: string | null;
}
export interface ValuationWorksheet {
  id?: string | null;
  projectId: string;
  projectInterimValuationId: string;
  interimValuationLabel: string;
  projectBoqVersionId: string;
  boqVersionNumber: number;
  status: string;
  approvalStatus: string;
  contractorBusinessPartnerId?: string | null;
  contractorName?: string | null;
  consultantBusinessPartnerId?: string | null;
  consultantName?: string | null;
  workflowInstanceId?: string | null;
  contractorSubmissionRequired: boolean;
  consultantEndorsementRequired: boolean;
  supportingEvidenceRequired: boolean;
  portalIdentityRequired: boolean;
  externalSignatureRequired: boolean;
  contractorAttestationText: string;
  consultantAttestationText: string;
  contractorSubmittedAt?: string | null;
  qsVettedAt?: string | null;
  qsReviewNote?: string | null;
  consultantEndorsedAt?: string | null;
  approvedAt?: string | null;
  rejectionReason?: string | null;
  certificateReady: boolean;
  certificateReadyAt?: string | null;
  retentionPercentage: number;
  measuredToDateValue: number;
  previouslyCertifiedValue: number;
  currentClaimedValue: number;
  currentCertifiedValue: number;
  currentPeriodCertifiedValue: number;
  disputedValue: number;
  retentionToDateValue: number;
  currentRetentionValue: number;
  netCurrentValue: number;
  rowVersion?: string | null;
  lines: ValuationWorksheetLine[];
  evidence: ValuationEvidence[];
}
export interface ValuationEvidence {
  id: string;
  evidenceType: ValuationEvidenceType | number;
  title: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  checksumSha256: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  uploadedByName: string;
  uploadedAt: string;
}
export interface ValuationWorksheetRevision {
  id: string;
  action: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  beforeJson?: string | null;
  afterJson: string;
  createdAt: string;
}

export const quantitySurveyValuationWorksheetService = {
  lookups: (projectId: string) =>
    apiService.get<ValuationWorksheetLookups>(`${root}/lookups`, { projectId }),
  get: (interimValuationId: string, projectBoqVersionId?: string) =>
    apiService.get<ValuationWorksheet>(`${root}/${interimValuationId}`, {
      projectBoqVersionId,
    }),
  save: (
    interimValuationId: string,
    request: {
      clientRequestId: string;
      projectBoqVersionId: string;
      contractorBusinessPartnerId?: string | null;
      consultantBusinessPartnerId?: string | null;
      rowVersion?: string | null;
      retentionPercentage: number;
      lines: Array<{
        projectBoqVersionLineId: string;
        currentClaimedQuantity: number;
        currentCertifiedQuantity: number;
        reviewNote?: string | null;
      }>;
    }
  ) =>
    apiService.put<ValuationWorksheet>(
      `${root}/${interimValuationId}`,
      request
    ),
  vet: (id: string, request: object) =>
    apiService.post<ValuationWorksheet>(
      `${root}/worksheets/${id}/vet`,
      request
    ),
  submitApproval: (id: string, request: object) =>
    apiService.post<ValuationWorksheet>(
      `${root}/worksheets/${id}/submit-approval`,
      request
    ),
  approve: (id: string, request: object) =>
    apiService.post<ValuationWorksheet>(
      `${root}/worksheets/${id}/approve`,
      request
    ),
  reject: (id: string, request: object) =>
    apiService.post<ValuationWorksheet>(
      `${root}/worksheets/${id}/reject`,
      request
    ),
  externalLookups: (projectId: string) =>
    apiService.get<ValuationWorksheetLookups>(
      `${externalRoot(projectId)}/lookups`
    ),
  externalList: (projectId: string) =>
    apiService.get<ValuationWorksheet[]>(externalRoot(projectId)),
  externalGet: (projectId: string, id: string) =>
    apiService.get<ValuationWorksheet>(`${externalRoot(projectId)}/${id}`),
  saveContractorClaim: (projectId: string, id: string, request: object) =>
    apiService.put<ValuationWorksheet>(
      `${externalRoot(projectId)}/${id}/claim`,
      request
    ),
  submitContractorClaim: (projectId: string, id: string, request: object) =>
    apiService.post<ValuationWorksheet>(
      `${externalRoot(projectId)}/${id}/submit`,
      request
    ),
  endorseConsultant: (projectId: string, id: string, request: object) =>
    apiService.post<ValuationWorksheet>(
      `${externalRoot(projectId)}/${id}/endorse`,
      request
    ),
  addEvidence: (
    projectId: string,
    id: string,
    request: {
      clientRequestId: string;
      evidenceType: ValuationEvidenceType;
      title: string;
      file: File;
    },
    external = false
  ) => {
    const form = new FormData();
    form.append('clientRequestId', request.clientRequestId);
    form.append('evidenceType', request.evidenceType);
    form.append('title', request.title);
    form.append('file', request.file, request.file.name);
    return apiService.post<ValuationEvidence>(
      external
        ? `${externalRoot(projectId)}/${id}/evidence`
        : `${root}/worksheets/${id}/evidence`,
      form
    );
  },
  evidenceContent: (
    projectId: string,
    id: string,
    evidenceId: string,
    external = false
  ) =>
    apiService.downloadBlob(
      external
        ? `${externalRoot(projectId)}/${id}/evidence/${evidenceId}/content`
        : `${root}/worksheets/${id}/evidence/${evidenceId}/content`
    ),
  history: (interimValuationId: string) =>
    apiService.get<ValuationWorksheetRevision[]>(
      `${root}/${interimValuationId}/history`
    ),
};
