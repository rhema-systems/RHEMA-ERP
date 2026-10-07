import { describe, expect, it } from 'vitest';
import {
  resolveInvoiceLineTaxSelection,
  shouldBlockApInvoiceSaveForSupplierDefaults,
} from './invoice-tax-selection';

describe('invoice edit tax selection', () => {
  it('persists an explicit no-tax selection as exempt instead of a standard line with a null group', () => {
    expect(resolveInvoiceLineTaxSelection({
      lineTaxGroupId: 'none',
      defaultTaxGroupId: 'default-tax-group',
      taxTreatment: 1,
      isOpeningBalance: false,
    })).toEqual({ taxGroupId: null, taxTreatment: 2 });
  });

  it('persists an inherited no-tax default as exempt', () => {
    expect(resolveInvoiceLineTaxSelection({
      lineTaxGroupId: '',
      defaultTaxGroupId: 'none',
      isOpeningBalance: false,
    })).toEqual({ taxGroupId: null, taxTreatment: 2 });
  });

  it('keeps a selected tax group standard-rated by default', () => {
    expect(resolveInvoiceLineTaxSelection({
      lineTaxGroupId: '',
      defaultTaxGroupId: 'purchase-tax-group',
      isOpeningBalance: false,
    })).toEqual({ taxGroupId: 'purchase-tax-group', taxTreatment: 1 });
  });

  it('returns an exempt line to standard treatment when a tax group is selected', () => {
    expect(resolveInvoiceLineTaxSelection({
      lineTaxGroupId: 'purchase-tax-group',
      taxTreatment: 2,
      isOpeningBalance: false,
    })).toEqual({ taxGroupId: 'purchase-tax-group', taxTreatment: 1 });
  });

  it('retains an explicit governed no-tax treatment', () => {
    expect(resolveInvoiceLineTaxSelection({
      lineTaxGroupId: 'none',
      taxTreatment: 3,
      isOpeningBalance: false,
    })).toEqual({ taxGroupId: null, taxTreatment: 3 });
  });

  it('never silently blocks an AP edit while supplier defaults load', () => {
    expect(shouldBlockApInvoiceSaveForSupplierDefaults({
      isEditMode: true,
      isOpeningBalance: false,
      isLoading: true,
    })).toBe(false);
    expect(shouldBlockApInvoiceSaveForSupplierDefaults({
      isEditMode: false,
      isOpeningBalance: false,
      isLoading: true,
    })).toBe(true);
  });
});
