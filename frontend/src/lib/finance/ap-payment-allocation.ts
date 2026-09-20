export type ApPaymentAllocationDisposition =
  | 'supplier-advance'
  | 'fully-allocated'
  | 'partially-allocated'
  | 'over-allocated';

export interface ApPaymentAllocationRow {
  invoiceCash?: number;
  paymentCurrencyCash?: number;
  discount?: number;
  withholdingTax?: number;
  otherAdjustments?: number[];
}

export interface ApPaymentAllocationState {
  disposition: ApPaymentAllocationDisposition;
  hasSettlementComponents: boolean;
  allocatedPaymentCurrencyCash: number;
  remainingPaymentCurrencyCash: number;
}

export interface ApPaymentSubmitGate {
  allocationState: ApPaymentAllocationState;
  isAdvanceApplication?: boolean;
  hasPaymentReadinessFailure?: boolean;
  hasFxEvidenceFailure?: boolean;
  hasIncompleteCurrencyPair?: boolean;
  isBusy?: boolean;
}

// VendorPaymentService currently normalizes payment totals and allocated payment-currency
// cash to two decimals with MidpointRounding.AwayFromZero. Keep this explicit until the API
// exposes a different per-payment-currency settlement precision contract.
export const AP_PAYMENT_CURRENCY_DECIMAL_PLACES = 2;

const toMinorUnits = (
  value: number,
  decimalPlaces = AP_PAYMENT_CURRENCY_DECIMAL_PLACES
): number => {
  const finiteValue = Number.isFinite(value) ? value : 0;
  const scale = 10 ** decimalPlaces;
  const midpointCorrection =
    Number.EPSILON * Math.max(1, Math.abs(finiteValue));
  return (
    Math.sign(finiteValue) *
    Math.round((Math.abs(finiteValue) + midpointCorrection) * scale)
  );
};

const fromMinorUnits = (
  value: number,
  decimalPlaces = AP_PAYMENT_CURRENCY_DECIMAL_PLACES
): number => value / 10 ** decimalPlaces;

const hasEnteredValue = (value?: number): boolean =>
  Number.isFinite(value) && Number(value) !== 0;

export const hasApPaymentSettlementComponents = (
  rows: ApPaymentAllocationRow[]
): boolean =>
  rows.some(
    row =>
      hasEnteredValue(row.invoiceCash) ||
      hasEnteredValue(row.paymentCurrencyCash) ||
      hasEnteredValue(row.discount) ||
      hasEnteredValue(row.withholdingTax) ||
      row.otherAdjustments?.some(hasEnteredValue) === true
  );

export const getApPaymentAllocationState = (
  paymentAmount: number,
  rows: ApPaymentAllocationRow[],
  decimalPlaces = AP_PAYMENT_CURRENCY_DECIMAL_PLACES
): ApPaymentAllocationState => {
  const hasSettlementComponents = hasApPaymentSettlementComponents(rows);
  const paymentMinorUnits = toMinorUnits(paymentAmount, decimalPlaces);
  // Match VendorPaymentService: sum payment-currency cash first, then round the total.
  const allocatedPaymentCurrencyMinorUnits = toMinorUnits(
    rows.reduce(
      (total, row) => total + (Number(row.paymentCurrencyCash) || 0),
      0
    ),
    decimalPlaces
  );
  const remainingMinorUnits =
    paymentMinorUnits - allocatedPaymentCurrencyMinorUnits;

  let disposition: ApPaymentAllocationDisposition;
  if (!hasSettlementComponents) {
    disposition = 'supplier-advance';
  } else if (remainingMinorUnits === 0) {
    disposition = 'fully-allocated';
  } else if (remainingMinorUnits > 0) {
    disposition = 'partially-allocated';
  } else {
    disposition = 'over-allocated';
  }

  return {
    disposition,
    hasSettlementComponents,
    allocatedPaymentCurrencyCash: fromMinorUnits(
      allocatedPaymentCurrencyMinorUnits,
      decimalPlaces
    ),
    remainingPaymentCurrencyCash: fromMinorUnits(
      remainingMinorUnits,
      decimalPlaces
    ),
  };
};

export const canSubmitApPayment = ({
  allocationState,
  isAdvanceApplication = false,
  hasPaymentReadinessFailure = false,
  hasFxEvidenceFailure = false,
  hasIncompleteCurrencyPair = false,
  isBusy = false,
}: ApPaymentSubmitGate): boolean => {
  if (
    isBusy ||
    hasPaymentReadinessFailure ||
    hasFxEvidenceFailure ||
    hasIncompleteCurrencyPair
  ) {
    return false;
  }

  if (isAdvanceApplication) {
    return allocationState.hasSettlementComponents;
  }

  return (
    allocationState.disposition === 'supplier-advance' ||
    allocationState.disposition === 'fully-allocated'
  );
};
