import type { SupplierOnboardingPayment } from '@/types/procurement-supplier-onboarding-token';

export const getVerifiableSupplierOnboardingPayment = (
  payments: SupplierOnboardingPayment[],
  applicationTokenDeliveryRecoveryRequired: boolean
) =>
  payments.find(
    (payment) =>
      payment.status === 'Pending' ||
      payment.status === 'Posted' ||
      (payment.status === 'Reconciled' &&
        applicationTokenDeliveryRecoveryRequired)
  );

export const isSupplierOnboardingDeliveryRecovery = (
  payment: SupplierOnboardingPayment | undefined,
  applicationTokenDeliveryRecoveryRequired: boolean
) =>
  payment?.status === 'Reconciled' &&
  applicationTokenDeliveryRecoveryRequired;
