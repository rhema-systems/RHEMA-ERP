import { apiService } from '@/services/api.service';

export type ContractClaimType = 'ExtensionOfTime' | 'LossAndExpense' | 'Variation' | 'Daywork' | 'AdditionalWork' | 'Other';
export type ClaimDisputeStatus = 'None' | 'Open' | 'ResolvedAccepted' | 'ResolvedRejected';
export type ClaimSettlementStatus = 'NotApplicable' | 'Pending' | 'PartiallySettled' | 'Settled';
export interface ClaimContract { id: string; number: string; title: string; contractorId: string; contractor: string; contractSum: number; currency: string; }
export interface ClaimVariation { id: string; reference: string; title: string; type: string; amount: number; }
export interface ClaimExtension { id: string; reference: string; title: string; daysRequested?: number | null; daysApproved?: number | null; status: string; }
export interface ClaimBoq { id: string; versionNumber: string; status: string; }
export interface ClaimEvidence { id: string; title: string; fileName: string; contentType: string; fileSize: number; checksumSha256: string; centralDocumentRecordId: string; centralDocumentVersionId: string; }
export interface ContractClaim {
  id: string; projectId: string; contractId: string; contractorBusinessPartnerId: string;
  approvedBoqVersionId?: string | null; variationOrderId?: string | null; extensionOfTimeId?: string | null;
  claimNumber: string; claimType: ContractClaimType; title: string; basis: string; status: string; approvalStatus: string;
  contractNumber: string; contractorName: string; currency: string; claimedAmount: number; qsAssessedAmount?: number | null;
  approvedAmount?: number | null; rejectedAmount?: number | null; qsReviewNote?: string | null;
  disputeStatus: ClaimDisputeStatus; disputeReason?: string | null; disputeResolution?: string | null;
  settlementStatus: ClaimSettlementStatus; settledAmount: number; settlementReference?: string | null; settlementDate?: string | null;
  workflowInstanceId?: string | null; rejectionReason?: string | null; rowVersion: string; evidence: ClaimEvidence[];
}
export interface ContractClaimWorkspace { contracts: ClaimContract[]; variations: ClaimVariation[]; extensionsOfTime: ClaimExtension[]; approvedBoqVersions: ClaimBoq[]; claims: ContractClaim[]; }
export interface ClaimAction { clientRequestId: string; rowVersion: string; reason: string; }

const root = '/quantity-survey/contract-claims';
const externalRoot = (projectId: string) => `/projects/external/my-projects/${projectId}/contract-claims`;
export const quantitySurveyContractClaimService = {
  workspace: (projectId: string, external = false) => apiService.get<ContractClaimWorkspace>(external ? externalRoot(projectId) : root, external ? undefined : { projectId }),
  get: (projectId: string, id: string, external = false) => apiService.get<ContractClaim>(external ? `${externalRoot(projectId)}/${id}` : `${root}/${id}`),
  save: (projectId: string, request: object, external = false) => apiService.put<ContractClaim>(external ? externalRoot(projectId) : `${root}?projectId=${encodeURIComponent(projectId)}`, request),
  submit: (projectId: string, id: string, request: ClaimAction, external = false) => apiService.post<ContractClaim>(external ? `${externalRoot(projectId)}/${id}/submit` : `${root}/${id}/submit`, request),
  saveExternal: (projectId: string, request: object) => apiService.put<ContractClaim>(externalRoot(projectId), request),
  uploadEvidence: (projectId: string, id: string, clientRequestId: string, title: string, file: File, external = false) => {
    const form = new FormData(); form.append('clientRequestId', clientRequestId); form.append('title', title); form.append('file', file, file.name);
    return apiService.post<ClaimEvidence>(external ? `${externalRoot(projectId)}/${id}/evidence` : `${root}/${id}/evidence`, form);
  },
  evidenceContent: (projectId: string, id: string, evidenceId: string, external = false) => apiService.downloadBlob(external ? `${externalRoot(projectId)}/${id}/evidence/${evidenceId}/content` : `${root}/${id}/evidence/${evidenceId}/content`),
  submitExternal: (projectId: string, id: string, request: ClaimAction) => apiService.post<ContractClaim>(`${externalRoot(projectId)}/${id}/submit`, request),
  disputeExternal: (projectId: string, id: string, request: ClaimAction) => apiService.post<ContractClaim>(`${externalRoot(projectId)}/${id}/dispute`, request),
  vet: (id: string, request: ClaimAction & { assessedAmount: number }) => apiService.post<ContractClaim>(`${root}/${id}/vet`, request),
  submitApproval: (id: string, request: ClaimAction) => apiService.post<ContractClaim>(`${root}/${id}/submit-approval`, request),
  approve: (id: string, request: ClaimAction) => apiService.post<ContractClaim>(`${root}/${id}/approve`, request),
  reject: (id: string, request: ClaimAction) => apiService.post<ContractClaim>(`${root}/${id}/reject`, request),
  resolveDispute: (id: string, accepted: boolean, request: ClaimAction) => apiService.post<ContractClaim>(`${root}/${id}/dispute/${accepted ? 'accept' : 'reject'}`, request),
  settle: (id: string, request: ClaimAction & { amount: number; settlementReference: string; settlementDate: string }) => apiService.post<ContractClaim>(`${root}/${id}/settle`, request),
};
