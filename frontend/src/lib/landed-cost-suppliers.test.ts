import { describe, expect, it } from 'vitest';
import { groupLandedCostsBySupplier } from './landed-cost-suppliers';

describe('landed cost supplier grouping', () => {
  const freight = { supplierId: 'A', supplierName: 'Carrier', currency: 'GHS', amount: 310 };
  it('sums charges for the same supplier and currency', () => {
    expect(groupLandedCostsBySupplier([freight, { ...freight, amount: 50 }]))
      .toEqual([expect.objectContaining({ supplierId: 'a', amount: 360, count: 2 })]);
  });
  it('never merges suppliers with identical names', () => {
    expect(groupLandedCostsBySupplier([freight, { ...freight, supplierId: 'B', amount: 50 }])).toHaveLength(2);
  });
  it('keeps currencies and different bill references separate', () => {
    expect(groupLandedCostsBySupplier([freight, { ...freight, currency: 'USD' },
      { ...freight, referenceNumber: 'BILL-1' }, { ...freight, referenceNumber: 'BILL-2' }])).toHaveLength(4);
  });
  it('keeps linked invoices apart from unlinked cost references', () => {
    expect(groupLandedCostsBySupplier([{ ...freight, referenceNumber: 'INV-1' },
      { ...freight, invoiceNumber: 'INV-1' }])).toHaveLength(2);
  });
  it('shows missing suppliers explicitly without assigning the goods supplier', () => {
    expect(groupLandedCostsBySupplier([{ currency: 'GHS', amount: 50 }]))
      .toEqual([expect.objectContaining({ supplierId: undefined, supplierName: 'Supplier not selected', amount: 50 })]);
  });
  it('normalizes supplier IDs and currencies and rounds currency totals', () => {
    expect(groupLandedCostsBySupplier([{ ...freight, amount: .1 }, { ...freight, supplierId: 'a', currency: 'ghs', amount: .2 }]))
      .toEqual([expect.objectContaining({ currency: 'GHS', amount: .3, count: 2 })]);
  });
});
