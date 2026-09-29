import { describe, expect, it } from 'vitest';
import {
  isEligibleManualArRevenueAccount,
  taxGroupForNewArInvoiceLine,
} from './ar-invoice-entry';

describe('manual AR invoice entry governance', () => {
  it('snapshots the selected header tax group onto new lines only', () => {
    expect(taxGroupForNewArInvoiceLine('vat-standard', false)).toBe('vat-standard');
    expect(taxGroupForNewArInvoiceLine('none', false)).toBe('none');
    expect(taxGroupForNewArInvoiceLine(undefined, false)).toBe('none');
    expect(taxGroupForNewArInvoiceLine('vat-standard', true)).toBe('none');
  });

  it('allows only active direct-posting revenue accounts on manual invoice lines', () => {
    expect(isEligibleManualArRevenueAccount({
      status: 'Active', accountType: 'Revenue', allowDirectPosting: true, isControlAccount: false,
    })).toBe(true);
    expect(isEligibleManualArRevenueAccount({
      status: 'Active', accountType: 'Asset', allowDirectPosting: true, isControlAccount: false,
    })).toBe(false);
    expect(isEligibleManualArRevenueAccount({
      status: 'Active', accountType: 'Revenue', allowDirectPosting: true, isControlAccount: true,
    })).toBe(false);
    expect(isEligibleManualArRevenueAccount({
      status: 'Inactive', accountType: 'Revenue', allowDirectPosting: true, isControlAccount: false,
    })).toBe(false);
  });
});
