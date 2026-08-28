import { apiService } from '@/services/api.service';
import type { SubcontractAction } from '@/services/quantity-survey-subcontract.service';

export interface SubcontractChargeEvidence {
  id: string;
  chargeNoticeId: string;
  title: string;
  fileName: string;
  fileSize: number;
  checksumSha256: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
}

export interface SubcontractCharge {
  id: string;
  subcontractId: string;
  projectId: string;
  subcontractorBusinessPartnerId: string;
  appliedValuationId?: string | null;
  appliedValuationNumber?: string | null;
  noticeNumber: string;
  chargeType: 'BackCharge' | 'ContraCharge';
  title: string;
  reason: string;
  noticeDate: string;
  responseDueDate: string;
  proposedAmount: number;
  approvedAmount?: number | null;
  currency: string;
  status: string;
  approvalStatus: string;
  responseStatus: string;
  responseNote?: string | null;
  respondedAt?: string | null;
  workflowInstanceId?: string | null;
  issuedAt?: string | null;
  approvedAt?: string | null;
  appliedAt?: string | null;
  communicationStatus: string;
  communicationRequestedAt?: string | null;
  communicationRequestCount: number;
  rowVersion: string;
  evidence: SubcontractChargeEvidence[];
}

const root = '/quantity-survey/subcontract-charges';
const externalRoot = (projectId: string, subcontractId: string) =>
  `/projects/external/my-projects/${projectId}/subcontracts/${subcontractId}/charge-notices`;

export const quantitySurveySubcontractChargeService = {
  list: (projectId: string, subcontractId: string, external = false) =>
    apiService.get<SubcontractCharge[]>(
      external ? externalRoot(projectId, subcontractId) : root,
      external ? undefined : { projectId, subcontractId }
    ),
  save: (subcontractId: string, request: object) =>
    apiService.put<SubcontractCharge>(`${root}/${subcontractId}`, request),
  uploadEvidence: (
    projectId: string,
    subcontractId: string,
    chargeId: string,
    clientRequestId: string,
    title: string,
    file: File,
    external = false
  ) => {
    const form = new FormData();
    form.append('clientRequestId', clientRequestId);
    form.append('title', title);
    form.append('file', file, file.name);
    return apiService.post<SubcontractChargeEvidence>(
      `${external ? externalRoot(projectId, subcontractId) : root}/${chargeId}/evidence`,
      form
    );
  },
  evidenceContent: (
    projectId: string,
    subcontractId: string,
    chargeId: string,
    evidenceId: string,
    external = false
  ) =>
    apiService.downloadBlob(
      `${external ? externalRoot(projectId, subcontractId) : root}/${chargeId}/evidence/${evidenceId}/content`
    ),
  issue: (id: string, request: SubcontractAction) =>
    apiService.post<SubcontractCharge>(`${root}/${id}/issue`, request),
  submit: (id: string, request: SubcontractAction) =>
    apiService.post<SubcontractCharge>(`${root}/${id}/submit`, request),
  decide: (
    id: string,
    approve: boolean,
    request: SubcontractAction & { approvedAmount: number }
  ) =>
    apiService.post<SubcontractCharge>(
      `${root}/${id}/${approve ? 'approve' : 'reject'}`,
      request
    ),
  respond: (
    projectId: string,
    subcontractId: string,
    id: string,
    request: SubcontractAction & { responseStatus: 'Accepted' | 'Disputed' }
  ) =>
    apiService.post<SubcontractCharge>(
      `${externalRoot(projectId, subcontractId)}/${id}/respond`,
      request
    ),
  retryCommunication: (id: string) =>
    apiService.post<SubcontractCharge>(`${root}/${id}/retry-communication`, {}),
};
