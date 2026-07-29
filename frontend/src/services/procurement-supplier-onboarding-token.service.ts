import { apiService } from '@/services/api.service';
import type {
  RecordSupplierOnboardingPayment,
  SupplierOnboardingExemptionDecision,
  SupplierOnboardingExemptionRequest,
  SupplierOnboardingPaymentMethodOption,
  SupplierOnboardingToken,
  SupplierOnboardingTokenIssueResult,
  SupplierOnboardingTokenPage,
  SupplierOnboardingTokenSearch,
  SupplierOnboardingTokenSummary,
} from '@/types/procurement-supplier-onboarding-token';

const root = '/procurement/supplier-onboarding-tokens';

export const procurementSupplierOnboardingTokenService = {
  summary: () => apiService.get<SupplierOnboardingTokenSummary>(`${root}/summary`),
  search: (request: SupplierOnboardingTokenSearch) =>
    apiService.get<SupplierOnboardingTokenPage>(
      root,
      request as Record<string, unknown>
    ),
  get: (id: string) => apiService.get<SupplierOnboardingToken>(`${root}/${id}`),
  getForRegistration: (registrationId: string) =>
    apiService.get<SupplierOnboardingToken | undefined>(
      `${root}/registrations/${registrationId}`
    ),
  issue: (registrationId: string) =>
    apiService.post<SupplierOnboardingTokenIssueResult>(root, { registrationId }),
  reissue: (id: string, reason: string, rowVersion: string) =>
    apiService.post<SupplierOnboardingTokenIssueResult>(`${root}/${id}/reissue`, {
      reason,
      rowVersion,
    }),
  paymentMethods: (id: string) =>
    apiService.get<SupplierOnboardingPaymentMethodOption[]>(
      `${root}/${id}/payment-methods`
    ),
  recordPayment: (id: string, request: RecordSupplierOnboardingPayment) =>
    apiService.post<SupplierOnboardingTokenIssueResult>(
      `${root}/${id}/payments`,
      request
    ),
  reconcile: (
    tokenId: string,
    paymentId: string,
    reconciliationReference: string,
    notes: string,
    rowVersion: string
  ) =>
    apiService.post<SupplierOnboardingToken>(
      `${root}/${tokenId}/payments/${paymentId}/reconcile`,
      { reconciliationReference, notes, rowVersion }
    ),
  requestExemption: (
    id: string,
    request: SupplierOnboardingExemptionRequest
  ) =>
    apiService.post<SupplierOnboardingToken>(
      `${root}/${id}/exemptions`,
      request
    ),
  decideExemption: (
    tokenId: string,
    exemptionId: string,
    request: SupplierOnboardingExemptionDecision
  ) =>
    apiService.post<SupplierOnboardingToken>(
      `${root}/${tokenId}/exemptions/${exemptionId}/decision`,
      request
    ),
};
