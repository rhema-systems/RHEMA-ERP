import { describe, expect, it } from 'vitest';
import { allocateInvoiceCash, paymentWithholdingChoice, requireGhsWhtScope } from './ap-payment-withholding';

describe('invoice WHT decisions at payment', () => {
  it('does not substitute commercial FX for statutory evidence in any currency leg', () => {
    expect(() => requireGhsWhtScope('GHS', 'GHS', ['GHS'])).not.toThrow();
    expect(() => requireGhsWhtScope('USD', 'GHS', ['GHS'])).toThrow('statutory');
    expect(() => requireGhsWhtScope('GHS', 'GHS', ['USD'])).toThrow('statutory');
    expect(() => requireGhsWhtScope('GHS', 'USD', ['GHS'])).toThrow('statutory');
    expect(() => requireGhsWhtScope('GHS', 'GHS', [''])).toThrow('statutory');
  });
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
  it('uses net supply rather than VAT-inclusive liability', () => {
    expect(allocateInvoiceCash(12000, 11250, 0, 7.5, 10000 / 12000)).toEqual({ cash: 11250, discount: 0, withholding: 750 });
    expect(allocateInvoiceCash(12000, 5625, 0, 7.5, 10000 / 12000)).toEqual({ cash: 5625, discount: 0, withholding: 375 });
    expect(allocateInvoiceCash(11400, 10687.5, 0, 7.5, 9500 / 11400)).toEqual({ cash: 10687.5, discount: 0, withholding: 712.5 });
  });
  it('includes the server-calculated catch-up once without discounting the tax base again', () => {
    expect(allocateInvoiceCash(900, 816, 0, 3, 1, 57)).toEqual({ cash: 816, discount: 0, withholding: 84 });
    expect(allocateInvoiceCash(12000, 11010, 240, 7.5, 10000 / 12000)).toEqual({ cash: 11010, discount: 240, withholding: 750 });
    expect(() => allocateInvoiceCash(100, 10, 0, 7.5, 1, 200)).toThrow('catch-up');
  });
});
