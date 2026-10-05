import { describe, expect, it } from 'vitest';
import { invoiceTaxTreatment, landedCostTaxReviewPending } from './landed-cost-tax';

describe('landed-cost draft tax review', () => {
  it.each([5, 'PendingReview', '5', undefined, 'Unknown'])('does not mistake %s for a confirmed tax choice', taxTreatment => {
    expect(landedCostTaxReviewPending([{ landedCostItemId: 'cost', taxTreatment }])).toBe(true);
  });
  it.each([2, 3, 4, 'Exempt', 'ZeroRated', 'OutOfScope', 'NonTaxable'])('accepts explicit %s', taxTreatment => {
    expect(landedCostTaxReviewPending([{ landedCostItemId: 'cost', taxTreatment }])).toBe(false);
  });
  it('standard needs a tax group and normal invoices are untouched', () => {
    expect(landedCostTaxReviewPending([{ landedCostItemId: 'cost', taxTreatment: 'Standard' }])).toBe(true);
    expect(landedCostTaxReviewPending([{ landedCostItemId: 'cost', taxTreatment: 'Standard', taxGroupId: 'tax' }])).toBe(false);
    expect(landedCostTaxReviewPending([{ taxTreatment: 'Standard' }])).toBe(false);
    expect(invoiceTaxTreatment('PendingReview')).toBe(5);
    expect(invoiceTaxTreatment(undefined)).toBeUndefined();
    expect(invoiceTaxTreatment(null)).toBeUndefined();
  });
});
