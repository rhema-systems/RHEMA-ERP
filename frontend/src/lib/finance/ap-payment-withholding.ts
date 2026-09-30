import type { OutstandingVendorInvoice } from '@/types/ap';

type InvoiceChoice = Pick<OutstandingVendorInvoice, 'invoiceId' | 'applySupplierWithholdingDefaults' |
  'withholdingTaxId' | 'withholdingTaxRate' | 'withholdingTaxRateOverride' | 'withholdingTaxAccountId'>;

export function invoiceWithholdingChoice(invoice: InvoiceChoice) {
  if (invoice.applySupplierWithholdingDefaults === false) return { taxId: undefined, rate: 0, accountId: undefined };
  if (invoice.withholdingTaxId) return {
    taxId: invoice.withholdingTaxId,
    rate: Number(invoice.withholdingTaxRateOverride ?? invoice.withholdingTaxRate ?? 0),
    accountId: invoice.withholdingTaxAccountId ?? undefined,
  };
  if (invoice.applySupplierWithholdingDefaults === true) throw new Error('Review the invoice WHT configuration before payment.');
  return null;
}

export function paymentWithholdingChoice(invoices: InvoiceChoice[]) {
  const choices = invoices.map(invoiceWithholdingChoice);
  const decided = choices.filter((choice) => choice !== null);
  if (!decided.length) return null;
  const first = decided[0];
  if (!first) return null;
  if (decided.some((choice) => choice?.taxId !== first.taxId || choice?.rate !== first.rate) ||
      (decided.length !== invoices.length && first.taxId)) {
    throw new Error('Selected invoices have different WHT decisions or rates. Record separate payments for each WHT choice and rate.');
  }
  return first;
}

const round = (value: number) => Math.round((value + Number.EPSILON) * 100) / 100;

/** Commercial FX approval is not Ghana statutory conversion authority (Act915, s21). */
export function requireGhsWhtScope(paymentCurrency: string, functionalCurrency: string, invoiceCurrencies: string[]) {
  if ([paymentCurrency, functionalCurrency, ...invoiceCurrencies].some(currency => currency.trim().toUpperCase() !== 'GHS')) {
    throw new Error('Foreign-currency WHT requires Finance-approved statutory GHS conversion evidence. An approved payment exchange rate alone is not sufficient.');
  }
}

/** Payment amount is bank cash. WHT also settles AP, including a partial payment. */
export function allocateInvoiceCash(balance: number, availableCash: number, discount: number, rate: number,
  netSupplyFraction = 1, catchUp = 0) {
  if (![balance, availableCash, discount, rate, netSupplyFraction, catchUp].every(Number.isFinite) ||
      balance < 0 || availableCash < 0 || discount < 0 || rate < 0 || rate > 100 ||
      netSupplyFraction < 0 || netSupplyFraction > 1 || catchUp < 0) {
    throw new Error('Review the invoice tax-base and withholding evidence before allocation.');
  }
  const ratio = rate * netSupplyFraction / 100;
  const wht = (gross: number) => round(round(gross * netSupplyFraction) * rate / 100) + catchUp;
  const fullWht = wht(balance);
  if (fullWht > balance) throw new Error('WHT catch-up exceeds this invoice balance. Finance must review recovery before payment.');
  const fullDiscount = Math.min(discount, balance - fullWht);
  const fullCash = round(balance - fullDiscount - fullWht);
  if (availableCash >= fullCash) return { cash: fullCash, discount: fullDiscount, withholding: fullWht };
  const cash = round(availableCash);
  const gross = ratio < 1 ? Math.min(balance, round((cash + catchUp) / (1 - ratio))) : balance;
  return { cash, discount: 0, withholding: wht(gross) };
}
