import { describe, expect, it } from 'vitest';
import { planApSupplierDefaults, type ApSupplierDefaultValues } from './ap-supplier-defaults';
import type { PurchaseOrderSupplierDefaultsDto } from '@/services/purchasingService';

const defaults: PurchaseOrderSupplierDefaultsDto = { businessPartnerId: 'bp', paymentTermId: 'net30', postingDefaults: {
  cashAccountSource: 'Chequebook', subjectToWithholdingDeduction: true, withholdingTaxRate: 7.5,
  defaultApAccountId: 'ap-control', defaultExpenseAccountId: 'purchases', defaultTaxGroupId: 'vat',
} };
const values = (): ApSupplierDefaultValues => ({ paymentTermId: '', apAccountId: '', expenseAccountId: '', taxGroupId: 'none', lineItems: [
  { sourceLineId: 'expense', lineItemType: 'Expense', glAccountId: '', taxGroupId: 'none' },
  { sourceLineId: 'stock', lineItemType: 'Inventory', glAccountId: '', taxGroupId: 'none' },
] });
const taxes = new Set(['vat']);

describe('visible AP supplier-default assignments', () => {
  it('routes typed supplier charges while preserving explicit line accounts', () => {
    const current = values();
    current.lineItems = [
      { sourceLineId: 'freight', lineItemType: 'Freight', glAccountId: 'old-purchases' },
      { sourceLineId: 'misc', lineItemType: 'Miscellaneous' },
      { sourceLineId: 'interest', lineItemType: 'FinanceCharge', glAccountId: 'manual-account' },
    ];
    const source = { ...defaults, postingDefaults: { ...defaults.postingDefaults,
      defaultFreightAccountId: 'freight-expense', defaultMiscellaneousAccountId: 'misc-expense',
      defaultFinanceChargesAccountId: 'interest-expense' } };
    const plan = planApSupplierDefaults(current, source, new Set(['interest:glAccountId']), taxes);
    expect(plan.assignments).toContainEqual({ field: 'lineItems.0.glAccountId', value: 'freight-expense' });
    expect(plan.assignments).toContainEqual({ field: 'lineItems.1.glAccountId', value: 'misc-expense' });
    expect(plan.assignments).not.toContainEqual(expect.objectContaining({ field: 'lineItems.2.glAccountId' }));
    current.lineItems[0].lineItemType = 'Expense';
    expect(planApSupplierDefaults(current, source, new Set(), taxes).assignments)
      .toContainEqual({ field: 'lineItems.0.glAccountId', value: 'purchases' });
  });

  it('fills visible header accounts/payment/tax and applicable lines, but never invents a WHT tax ID', () => {
    const plan = planApSupplierDefaults(values(), defaults, new Set(), taxes);
    expect(plan.assignments).toContainEqual({ field: 'paymentTermId', value: 'net30' });
    expect(plan.assignments).toContainEqual({ field: 'apAccountId', value: 'ap-control' });
    expect(plan.assignments).toContainEqual({ field: 'expenseAccountId', value: 'purchases' });
    expect(plan.assignments).toContainEqual({ field: 'lineItems.0.glAccountId', value: 'purchases' });
    expect(plan.assignments).not.toContainEqual(expect.objectContaining({ field: 'lineItems.1.glAccountId' }));
    expect(plan.assignments).toContainEqual({ field: 'lineItems.1.taxGroupId', value: 'vat' });
    expect(plan.assignments.some(item => item.field.startsWith('withholding'))).toBe(false);
    expect(plan.allowServerDefaults).toBe(true);
  });

  it('preserves explicit header No Tax and prevents server fallback introducing tax after save', () => {
    const plan = planApSupplierDefaults(values(), defaults, new Set(['taxGroupId']), taxes);
    expect(plan.assignments.some(item => item.field.endsWith('taxGroupId'))).toBe(false);
    expect(plan.allowServerDefaults).toBe(false);
  });

  it('preserves line No Tax while allowing other untouched lines to use the schedule', () => {
    const plan = planApSupplierDefaults(values(), defaults, new Set(['expense:taxGroupId']), taxes);
    expect(plan.assignments).not.toContainEqual(expect.objectContaining({ field: 'lineItems.0.taxGroupId' }));
    expect(plan.assignments).toContainEqual({ field: 'lineItems.1.taxGroupId', value: 'vat' });
    expect(plan.allowServerDefaults).toBe(false);
  });

  it('never substitutes an unavailable tax schedule with an unpreviewed server calculation', () => {
    const plan = planApSupplierDefaults(values(), defaults, new Set(), new Set());
    expect(plan.taxUnavailable).toBe(true);
    expect(plan.assignments.some(item => item.field.endsWith('taxGroupId'))).toBe(false);
    expect(plan.allowServerDefaults).toBe(false);
  });

  it('keeps explicit payment terms and GL overrides, including an explicitly cleared AP account', () => {
    const edited = new Set(['paymentTermId', 'apAccountId', 'expense:glAccountId']);
    const plan = planApSupplierDefaults(values(), defaults, edited, taxes);
    expect(plan.assignments.some(item => ['paymentTermId', 'apAccountId', 'lineItems.0.glAccountId'].includes(item.field))).toBe(false);
    expect(plan.allowServerDefaults).toBe(false);
  });

  it('applies a manually chosen default purchases account only to untouched expense lines', () => {
    const current = values();
    current.expenseAccountId = 'other-expense';
    const plan = planApSupplierDefaults(current, defaults, new Set(['expenseAccountId']), taxes);
    expect(plan.assignments).toContainEqual({ field: 'lineItems.0.glAccountId', value: 'other-expense' });
    expect(plan.assignments).not.toContainEqual(expect.objectContaining({ field: 'expenseAccountId' }));
  });

  it('keeps nonstandard tax treatments and already applied values unchanged', () => {
    const current = values();
    current.lineItems[0].taxTreatment = 3;
    current.apAccountId = 'ap-control';
    const plan = planApSupplierDefaults(current, defaults, new Set(), taxes);
    expect(plan.assignments).not.toContainEqual(expect.objectContaining({ field: 'lineItems.0.taxGroupId' }));
    expect(plan.assignments).not.toContainEqual(expect.objectContaining({ field: 'apAccountId' }));
  });
});
