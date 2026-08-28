import { apiService } from '@/services/api.service';

export interface SubcontractContractLookup {
  id: string;
  number: string;
  title: string;
  contractValue: number;
  currency: string;
  retentionPercentage: number;
  paymentTermId: string;
}
export interface SubcontractPartnerLookup {
  id: string;
  code: string;
  name: string;
}
export interface SubcontractPaymentTermLookup {
  id: string;
  code: string;
  name: string;
}
export interface SubcontractEvidence {
  id: string;
  valuationId?: string | null;
  evidenceType: string;
  title: string;
  fileName: string;
  fileSize: number;
  checksumSha256: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
}
export interface SubcontractValuation {
  id: string;
  subcontractId: string;
  valuationNumber: string;
  valuationDate: string;
  periodEndDate: string;
  claimedToDateAmount: number;
  assessedToDateAmount?: number | null;
  previouslyCertifiedAmount: number;
  currentCertifiedAmount: number;
  retentionHeldAmount: number;
  retentionReleasedAmount: number;
  approvedBackChargeAmount: number;
  approvedContraChargeAmount: number;
  taxAmount: number;
  netCertifiedAmount: number;
  isFinal: boolean;
  status: string;
  approvalStatus: string;
  submissionNote?: string | null;
  assessmentNote?: string | null;
  workflowInstanceId?: string | null;
  paymentCertificateId?: string | null;
  certificateNumber?: string | null;
  vendorInvoiceId?: string | null;
  paymentStatus: string;
  apHandoffStatus: string;
  rowVersion: string;
  evidence: SubcontractEvidence[];
}
export interface Subcontract {
  id: string;
  projectId: string;
  contractId: string;
  contractNumber: string;
  subcontractorBusinessPartnerId: string;
  subcontractorName: string;
  paymentTermId: string;
  paymentTermName: string;
  subcontractNumber: string;
  title: string;
  scope: string;
  subcontractValue: number;
  currency: string;
  retentionPercentage: number;
  startDate: string;
  endDate?: string | null;
  status: string;
  approvalStatus: string;
  workflowInstanceId?: string | null;
  closedAt?: string | null;
  closureNote?: string | null;
  certifiedToDateAmount: number;
  paidToDateAmount: number;
  retentionBalance: number;
  outstandingBalance: number;
  rowVersion: string;
  evidence: SubcontractEvidence[];
  valuations: SubcontractValuation[];
}
export interface SubcontractWorkspace {
  contracts: SubcontractContractLookup[];
  subcontractors: SubcontractPartnerLookup[];
  paymentTerms: SubcontractPaymentTermLookup[];
  subcontracts: Subcontract[];
  externalBusinessPartnerId?: string | null;
}
export interface SubcontractAction {
  clientRequestId: string;
  rowVersion: string;
  reason: string;
}

const root = '/quantity-survey/subcontracts';
const externalRoot = (projectId: string) =>
  `/projects/external/my-projects/${projectId}/subcontracts`;
export const quantitySurveySubcontractService = {
  workspace: (projectId: string, external = false) =>
    apiService.get<SubcontractWorkspace>(
      external ? externalRoot(projectId) : root,
      external ? undefined : { projectId }
    ),
  save: (projectId: string, request: object) =>
    apiService.put<Subcontract>(
      `${root}?projectId=${encodeURIComponent(projectId)}`,
      request
    ),
  submitSubcontract: (id: string, request: SubcontractAction) =>
    apiService.post<Subcontract>(`${root}/${id}/submit`, request),
  decideSubcontract: (
    id: string,
    approve: boolean,
    request: SubcontractAction
  ) =>
    apiService.post<Subcontract>(
      `${root}/${id}/${approve ? 'approve' : 'reject'}`,
      request
    ),
  saveValuation: (
    projectId: string,
    subcontractId: string,
    request: object,
    external = false
  ) =>
    apiService.put<SubcontractValuation>(
      external
        ? `${externalRoot(projectId)}/${subcontractId}/valuations`
        : `${root}/${subcontractId}/valuations`,
      request
    ),
  uploadEvidence: (
    projectId: string,
    subcontractId: string,
    valuationId: string | null,
    clientRequestId: string,
    title: string,
    file: File,
    external = false
  ) => {
    const form = new FormData();
    form.append('clientRequestId', clientRequestId);
    form.append('title', title);
    form.append('file', file, file.name);
    if (!external && valuationId) form.append('valuationId', valuationId);
    return apiService.post<SubcontractEvidence>(
      external && valuationId
        ? `${externalRoot(projectId)}/${subcontractId}/valuations/${valuationId}/evidence`
        : `${root}/${subcontractId}/evidence`,
      form
    );
  },
  evidenceContent: (
    projectId: string,
    subcontractId: string,
    evidenceId: string,
    external = false
  ) =>
    apiService.downloadBlob(
      external
        ? `${externalRoot(projectId)}/${subcontractId}/evidence/${evidenceId}/content`
        : `${root}/${subcontractId}/evidence/${evidenceId}/content`
    ),
  submitValuation: (
    projectId: string,
    subcontractId: string,
    valuationId: string,
    request: SubcontractAction,
    external = false
  ) =>
    apiService.post<SubcontractValuation>(
      external
        ? `${externalRoot(projectId)}/${subcontractId}/valuations/${valuationId}/submit`
        : `${root}/valuations/${valuationId}/submit`,
      request
    ),
  assess: (
    id: string,
    request: SubcontractAction & {
      assessedToDateAmount: number;
      retentionReleasedAmount: number;
      chargeNoticeIds: string[];
    }
  ) =>
    apiService.post<SubcontractValuation>(
      `${root}/valuations/${id}/assess`,
      request
    ),
  decideValuation: (id: string, approve: boolean, request: SubcontractAction) =>
    apiService.post<SubcontractValuation>(
      `${root}/valuations/${id}/${approve ? 'approve' : 'reject'}`,
      request
    ),
  handoff: (id: string, request: SubcontractAction) =>
    apiService.post<SubcontractValuation>(
      `${root}/valuations/${id}/handoff-ap`,
      request
    ),
  refreshPayment: (id: string) =>
    apiService.post<SubcontractValuation>(
      `${root}/valuations/${id}/refresh-payment`,
      {}
    ),
  close: (id: string, request: SubcontractAction) =>
    apiService.post<Subcontract>(`${root}/${id}/close`, request),
};
