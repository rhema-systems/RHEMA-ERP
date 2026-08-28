import { apiService } from '@/services/api.service';

const root = '/quantity-survey/advance-recoveries';

export interface AdvancePaymentLookup {
  vendorPaymentId: string;
  contractId: string;
  contractNumber: string;
  contractorName: string;
  paymentNumber: string;
  paymentDate: string;
  currency: string;
  originalAmount: number;
  financeAllocatedAmount: number;
  financeAvailableAmount: number;
}

export interface AdvanceRecoveryLedgerEntry {
  paymentCertificateId: string;
  certificateNumber: string;
  issueDate: string;
  status: string;
  recoveryAmount: number;
  vendorInvoiceId?: string | null;
  financeAppliedAmount: number;
  runningRecoveryBalance: number;
}

export interface AdvanceRecoveryAgreement {
  id: string;
  projectId: string;
  contractId: string;
  vendorPaymentId: string;
  recoveryNumber: string;
  status: string;
  approvalStatus: string;
  contractNumber: string;
  contractorName: string;
  paymentNumber: string;
  paymentDate: string;
  currency: string;
  originalAdvanceAmount: number;
  recoveryPercentage: number;
  approvedRecoveryAmount: number;
  pendingRecoveryAmount: number;
  remainingRecoveryBalance: number;
  financeAllocatedAmount: number;
  financeAvailableAmount: number;
  financeAppliedToQsCertificates: number;
  reconciliationDifference: number;
  preparedById: string;
  approvedById?: string | null;
  preparedAt: string;
  approvedAt?: string | null;
  rejectionReason?: string | null;
  rowVersion: string;
  ledger: AdvanceRecoveryLedgerEntry[];
}

export interface AdvanceRecoveryWorkspace {
  eligibleAdvances: AdvancePaymentLookup[];
  agreements: AdvanceRecoveryAgreement[];
}

export interface AdvanceRecoveryRevision {
  id: string;
  action: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  reason?: string | null;
  createdAt: string;
}

export const quantitySurveyAdvanceRecoveryService = {
  workspace: (projectId: string) =>
    apiService.get<AdvanceRecoveryWorkspace>(root, { projectId }),
  create: (
    projectId: string,
    request: {
      clientRequestId: string;
      contractId: string;
      vendorPaymentId: string;
      recoveryPercentage: number;
      reason: string;
    }
  ) =>
    apiService.post<AdvanceRecoveryAgreement>(
      `${root}?projectId=${encodeURIComponent(projectId)}`,
      request
    ),
  submit: (id: string, request: AdvanceRecoveryAction) =>
    apiService.post<AdvanceRecoveryAgreement>(`${root}/${id}/submit`, request),
  approve: (id: string, request: AdvanceRecoveryAction) =>
    apiService.post<AdvanceRecoveryAgreement>(`${root}/${id}/approve`, request),
  reject: (id: string, request: AdvanceRecoveryAction) =>
    apiService.post<AdvanceRecoveryAgreement>(`${root}/${id}/reject`, request),
  history: (id: string) =>
    apiService.get<AdvanceRecoveryRevision[]>(`${root}/${id}/history`),
};

interface AdvanceRecoveryAction {
  clientRequestId: string;
  rowVersion: string;
  reason: string;
}
