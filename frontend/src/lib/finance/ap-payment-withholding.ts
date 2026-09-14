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
  const first = decided[0]!;
  if (decided.some((choice) => choice?.taxId !== first.taxId || choice?.rate !== first.rate) ||
      (decided.length !== invoices.length && first.taxId)) {
    throw new Error('Selected invoices have different WHT decisions or rates. Record separate payments for each WHT choice and rate.');
  }
  return first;
}

const round = (value: number) => Math.round((value + Number.EPSILON) * 100) / 100;

/** Payment amount is bank cash. WHT also settles AP, including a partial payment. */
export function allocateInvoiceCash(balance: number, availableCash: number, discount: number, rate: number) {
  const ratio = Math.max(0, Math.min(rate, 100)) / 100;
  const fullDiscount = ratio === 1 ? 0 : Math.min(discount, balance * (1 - ratio));
  const fullWht = round(balance * ratio);
  const fullCash = round(Math.max(balance - fullDiscount - fullWht, 0));
  if (availableCash >= fullCash) return { cash: fullCash, discount: fullDiscount, withholding: fullWht };
  const cash = round(Math.max(availableCash, 0));
  const gross = ratio < 1 ? Math.min(balance, round(cash / (1 - ratio))) : balance;
  return { cash, discount: 0, withholding: round(Math.max(gross - cash, 0)) };
}
