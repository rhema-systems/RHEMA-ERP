export type ReceiptAllocationDisposition =
  | 'customer-advance'
  | 'fully-allocated'
  | 'partially-allocated'
  | 'over-allocated';

const roundMoney = (value: number): number =>
  Math.round((value + Number.EPSILON) * 100) / 100;

export const getReceiptAllocationDisposition = (
  receiptAmount: number,
  allocatedReceiptCash: number,
  hasSettlementComponents: boolean
): ReceiptAllocationDisposition => {
  if (!hasSettlementComponents) return 'customer-advance';

  const difference = roundMoney(receiptAmount - allocatedReceiptCash);
  if (Math.abs(difference) <= 0.01) return 'fully-allocated';
  return difference > 0 ? 'partially-allocated' : 'over-allocated';
};

/**
 * Provides a visible configured-rate reference without pretending that Finance can derive the
 * customer's legal certificate amount from an invoice balance alone. The entered certificate
 * amount remains authoritative and any variance is explained in the receipt notes.
 */
export const calculateWithholdingBalanceReference = (
  outstandingInvoiceAmount: number,
  configuredRatePercent: number
): number =>
  roundMoney(
    (Math.max(outstandingInvoiceAmount, 0) *
      Math.max(configuredRatePercent, 0)) /
      100
  );

export const hasMaterialWithholdingVariance = (
  enteredAmount: number,
  referenceAmount: number
): boolean =>
  enteredAmount > 0 &&
  Math.abs(roundMoney(enteredAmount - referenceAmount)) > 0.01;
