import { describe, expect, it } from 'vitest';
import { allocateInvoiceCash, paymentWithholdingChoice } from './ap-payment-withholding';

describe('invoice WHT decisions at payment', () => {
  it('preserves No without consulting supplier or tax defaults', () => {
    expect(paymentWithholdingChoice([{ invoiceId: '1', applySupplierWithholdingDefaults: false }])).toEqual({ taxId: undefined, rate: 0, accountId: undefined });
  });
  it('keeps the transaction rate, including zero', () => {
    expect(paymentWithholdingChoice([{ invoiceId: '1', applySupplierWithholdingDefaults: true, withholdingTaxId: 'tax', withholdingTaxRate: 10, withholdingTaxRateOverride: 0 }])?.rate).toBe(0);
    expect(paymentWithholdingChoice([{ invoiceId: '1', withholdingTaxId: 'tax', withholdingTaxRate: 5 }])?.rate).toBe(5);
  });
  it('requires separate payments for different choices or rates', () => {
    expect(() => paymentWithholdingChoice([{ invoiceId: '1', applySupplierWithholdingDefaults: false }, { invoiceId: '2', withholdingTaxId: 'tax', withholdingTaxRate: 5 }])).toThrow('separate payments');
    expect(() => paymentWithholdingChoice([{ invoiceId: '1', withholdingTaxId: 'tax', withholdingTaxRate: 5 }, { invoiceId: '2', withholdingTaxId: 'tax', withholdingTaxRate: 10 }])).toThrow('separate payments');
  });
  it('retains the legacy payment-configured path without a saved invoice decision', () => {
    expect(paymentWithholdingChoice([{ invoiceId: '1' }])).toBeNull();
  });
  it('deducts WHT for partial and full settlement without double counting', () => {
    expect(allocateInvoiceCash(100, 45, 0, 10)).toEqual({ cash: 45, discount: 0, withholding: 5 });
    expect(allocateInvoiceCash(100, 90, 0, 10)).toEqual({ cash: 90, discount: 0, withholding: 10 });
    expect(allocateInvoiceCash(100, 85, 5, 10)).toEqual({ cash: 85, discount: 5, withholding: 10 });
    expect(allocateInvoiceCash(100, 100, 0, 0)).toEqual({ cash: 100, discount: 0, withholding: 0 });
  });
});
