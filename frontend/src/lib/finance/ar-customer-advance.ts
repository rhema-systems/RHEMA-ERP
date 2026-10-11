import type { CustomerPayment } from '@/types/ar';

type AdvanceCandidate = Pick<
  CustomerPayment,
  | 'businessPartnerId'
  | 'isCreditNote'
  | 'isCustomerAdvance'
  | 'status'
  | 'unallocatedAmount'
>;

export function isEligibleCustomerAdvance(
  payment: AdvanceCandidate,
  businessPartnerId: string
): boolean {
  return (
    payment.businessPartnerId === businessPartnerId &&
    payment.status === 'Posted' &&
    payment.isCustomerAdvance &&
    !payment.isCreditNote &&
    Number(payment.unallocatedAmount) > 0
  );
}

export function buildCustomerAdvanceApplicationUrl(
  businessPartnerId: string,
  paymentId: string,
  invoiceId?: string | null
): string {
  const params = new URLSearchParams({
    businessPartnerId,
    paymentId,
    mode: 'apply-account',
  });
  if (invoiceId) params.set('invoiceId', invoiceId);
  return `/finance/ar/receipts/new?${params.toString()}`;
}
