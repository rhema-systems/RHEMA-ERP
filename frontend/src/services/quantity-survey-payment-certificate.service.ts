import { apiService } from '@/services/api.service';

const root = '/quantity-survey/payment-certificates';

export interface PaymentCertificateValuationLookup {
  worksheetId: string;
  interimValuationId: string;
  contractId?: string | null;
  label: string;
  currency: string;
  certifiedToDateAmount: number;
  previousCertificateAmount: number;
  currentGrossAmount: number;
  currentRetentionAmount: number;
}

export interface PaymentCertificateAdvanceLookup {
  agreementId: string;
  contractId: string;
  label: string;
  currency: string;
  recoveryPercentage: number;
  remainingBalance: number;
}

export interface PaymentCertificateMaterialLookup {
  reconciliationId: string;
  worksheetId: string;
  contractId: string;
  label: string;
  currency: string;
  materialOnSiteAmount: number;
  materialOffSiteAmount: number;
  tdcSuppliedDeductionAmount: number;
}

export interface PaymentCertificateLookups {
  eligibleValuations: PaymentCertificateValuationLookup[];
  eligibleAdvanceRecoveries: PaymentCertificateAdvanceLookup[];
  approvedMaterialReconciliations: PaymentCertificateMaterialLookup[];
}

export interface QuantitySurveyPaymentCertificate {
  id: string;
  projectId: string;
  projectInterimValuationId: string;
  quantitySurveyValuationWorksheetId: string;
  previousPaymentCertificateId?: string | null;
  advanceRecoveryAgreementId?: string | null;
  materialReconciliationId?: string | null;
  vendorInvoiceId?: string | null;
  vendorInvoiceNumber?: string | null;
  certificateNumber?: string | null;
  title: string;
  status: string;
  approvalStatus: string;
  issueDate: string;
  paymentDueDate?: string | null;
  currency: string;
  certifiedToDateAmount: number;
  previouslyCertifiedAmount: number;
  grossCertifiedAmount: number;
  retentionHeldAmount: number;
  retentionReleasedAmount: number;
  advanceRecoveryAmount: number;
  materialDeductionAmount: number;
  materialOnSiteAmount: number;
  materialOffSiteAmount: number;
  otherDeductionsAmount: number;
  taxAmount: number;
  netCertifiedAmount: number;
  taxHandling: string;
  notes?: string | null;
  workflowInstanceId?: string | null;
  apHandoffStatus: string;
  paymentStatus: string;
  financeInvoiceAmount?: number | null;
  financePaidAmount?: number | null;
  financeBalanceAmount?: number | null;
  financeInvoiceJournalEntryId?: string | null;
  financePostingStatus: string;
  reconciliationStatus: string;
  apHandoffFailure?: string | null;
  paymentStatusUpdatedAt?: string | null;
  documentGenerated: boolean;
  generatedAt?: string | null;
  rowVersion: string;
}

export interface PaymentCertificateRevision {
  id: string;
  action: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  createdAt: string;
  beforeJson?: string | null;
  afterJson: string;
}

export interface PaymentCertificateMutation {
  clientRequestId: string;
  rowVersion: string;
  reason?: string | null;
}

export const quantitySurveyPaymentCertificateService = {
  lookups: (projectId: string) =>
    apiService.get<PaymentCertificateLookups>(`${root}/lookups`, { projectId }),
  list: (projectId: string) =>
    apiService.get<QuantitySurveyPaymentCertificate[]>(root, { projectId }),
  get: (id: string) =>
    apiService.get<QuantitySurveyPaymentCertificate>(`${root}/${id}`),
  generate: (
    projectId: string,
    request: {
      clientRequestId: string;
      valuationWorksheetId: string;
      advanceRecoveryAgreementId?: string | null;
      materialReconciliationId?: string | null;
      paymentDueDate?: string | null;
      advanceRecoveryAmount: number;
      otherDeductionsAmount: number;
      notes?: string | null;
    }
  ) =>
    apiService.post<QuantitySurveyPaymentCertificate>(
      `${root}?projectId=${encodeURIComponent(projectId)}`,
      request
    ),
  update: (
    id: string,
    request: {
      clientRequestId: string;
      rowVersion: string;
      paymentDueDate?: string | null;
      materialReconciliationId?: string | null;
      advanceRecoveryAmount: number;
      otherDeductionsAmount: number;
      notes?: string | null;
    }
  ) =>
    apiService.put<QuantitySurveyPaymentCertificate>(`${root}/${id}`, request),
  submit: (id: string, request: PaymentCertificateMutation) =>
    apiService.post<QuantitySurveyPaymentCertificate>(
      `${root}/${id}/submit`,
      request
    ),
  approve: (id: string, request: PaymentCertificateMutation) =>
    apiService.post<QuantitySurveyPaymentCertificate>(
      `${root}/${id}/approve`,
      request
    ),
  reject: (id: string, request: PaymentCertificateMutation) =>
    apiService.post<QuantitySurveyPaymentCertificate>(
      `${root}/${id}/reject`,
      request
    ),
  handoffToAp: (id: string, request: PaymentCertificateMutation) =>
    apiService.post<QuantitySurveyPaymentCertificate>(
      `${root}/${id}/handoff-ap`,
      request
    ),
  refreshPayment: (id: string) =>
    apiService.post<QuantitySurveyPaymentCertificate>(
      `${root}/${id}/refresh-payment`,
      {}
    ),
  document: (id: string) => apiService.downloadBlob(`${root}/${id}/document`),
  history: (id: string) =>
    apiService.get<PaymentCertificateRevision[]>(`${root}/${id}/history`),
};
