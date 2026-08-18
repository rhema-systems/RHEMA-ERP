import { apiService } from '@/services/api.service';

const root = '/quantity-survey/final-accounts';

export interface FinalAccountContractLookup {
  id: string;
  contractNumber: string;
  title: string;
  contractor: string;
  currency: string;
  contractValue: number;
}

export interface FinalAccountLine {
  category: string;
  label: string;
  sourceId?: string | null;
  sourceReference?: string | null;
  amount: number;
  effect: string;
  status: string;
}

export interface FinalAccount {
  id: string;
  projectId: string;
  contractId: string;
  contractNumber: string;
  contractTitle: string;
  contractor: string;
  status: string;
  approvalStatus: string;
  settlementDate?: string | null;
  approvedBoqVersionId?: string | null;
  approvedBoqVersionNumber?: number | null;
  approvedBoqValue: number;
  originalContractValue: number;
  approvedVariationAmount: number;
  approvedClaimAmount: number;
  approvedEscalationAmount: number;
  grossFinalAccountValue: number;
  advanceRecoveryAmount: number;
  materialDeductionAmount: number;
  otherDeductionAmount: number;
  totalDeductionAmount: number;
  finalAccountValue: number;
  certifiedToDate: number;
  retentionHeldAmount: number;
  retentionReleasedAmount: number;
  retentionOutstandingAmount: number;
  paidToDateAmount: number;
  finalPaymentAmount: number;
  currency: string;
  notes?: string | null;
  workflowInstanceId?: string | null;
  rejectionReason?: string | null;
  closureReason?: string | null;
  hasPendingCommercialRecords: boolean;
  canClose: boolean;
  closureBlockers: string[];
  lines: FinalAccountLine[];
  rowVersion: string;
}

export interface FinalAccountWorkspace {
  finalAccount?: FinalAccount | null;
  contracts: FinalAccountContractLookup[];
}

export interface FinalAccountRevision {
  id: string;
  action: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  reason?: string | null;
  createdAt: string;
}

interface FinalAccountAction {
  clientRequestId: string;
  rowVersion: string;
  reason: string;
}

export const quantitySurveyFinalAccountService = {
  workspace: (projectId: string) =>
    apiService.get<FinalAccountWorkspace>(root, { projectId }),
  prepare: (
    projectId: string,
    request: {
      clientRequestId: string;
      contractId: string;
      settlementDate: string;
      rowVersion?: string;
      reason: string;
      notes?: string;
    }
  ) =>
    apiService.post<FinalAccount>(
      `${root}?projectId=${encodeURIComponent(projectId)}`,
      request
    ),
  submit: (id: string, request: FinalAccountAction) =>
    apiService.post<FinalAccount>(`${root}/${id}/submit`, request),
  approve: (id: string, request: FinalAccountAction) =>
    apiService.post<FinalAccount>(`${root}/${id}/approve`, request),
  reject: (id: string, request: FinalAccountAction) =>
    apiService.post<FinalAccount>(`${root}/${id}/reject`, request),
  close: (id: string, request: FinalAccountAction) =>
    apiService.post<FinalAccount>(`${root}/${id}/close`, request),
  history: (id: string) =>
    apiService.get<FinalAccountRevision[]>(`${root}/${id}/history`),
};
