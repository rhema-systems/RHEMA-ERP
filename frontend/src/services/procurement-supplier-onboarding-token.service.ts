import { apiService } from '@/services/api.service';
import type {
  SupplierOnboardingRegistrationOption,
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

// These operations commit activation before waiting for bounded SMTP delivery.
// Give the server time to return its delivery result; never automatically repeat
// a mutation when its outcome is uncertain.
async function postWithTokenDelivery<T>(path: string, body: unknown): Promise<T> {
  const controller = new AbortController();
  const deadline = setTimeout(() => controller.abort(), 90_000);
  try {
    return await apiService.post<T>(path, body, controller.signal);
  } catch (error) {
    if (controller.signal.aborted) {
      throw new Error(
        'Token delivery request timed out. Refresh the token and payment status before retrying; the payment may already be posted.'
      );
    }
    throw error;
  } finally {
    clearTimeout(deadline);
  }
}

export const procurementSupplierOnboardingTokenService = {
  summary: () =>
    apiService.get<SupplierOnboardingTokenSummary>(`${root}/summary`),
  issueOptions: () =>
    apiService.get<SupplierOnboardingRegistrationOption[]>(
      `${root}/issue-options`
    ),
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
    postWithTokenDelivery<SupplierOnboardingTokenIssueResult>(root, {
      registrationId,
    }),
  reissue: (id: string, reason: string, rowVersion: string) =>
    postWithTokenDelivery<SupplierOnboardingTokenIssueResult>(
      `${root}/${id}/reissue`,
      {
        reason,
        rowVersion,
      }
    ),
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
    notes: string | undefined,
    rowVersion: string
  ) =>
    postWithTokenDelivery<SupplierOnboardingToken>(
      `${root}/${tokenId}/payments/${paymentId}/reconcile`,
      { reconciliationReference, ...(notes ? { notes } : {}), rowVersion }
    ),
  requestExemption: (id: string, request: SupplierOnboardingExemptionRequest) =>
    apiService.post<SupplierOnboardingToken>(
      `${root}/${id}/exemptions`,
      request
    ),
  decideExemption: (
    tokenId: string,
    exemptionId: string,
    request: SupplierOnboardingExemptionDecision
  ) =>
    postWithTokenDelivery<SupplierOnboardingToken>(
      `${root}/${tokenId}/exemptions/${exemptionId}/decision`,
      request
    ),
};
