import { describe, expect, it } from 'vitest';
import type { ProcurementPurchaseOrderSourceLineDto } from '@/services/purchasingService';
import type { ItemUnitOfMeasureDto } from '@/services/inventoryManagementService';
import {
  createApprovedPurchaseOrderItems,
  findApprovedPurchaseOrderLine,
  retainApprovedPurchaseOrderTerms,
  supportsApprovedPurchaseOrderUnit,
} from './purchase-order-approved-lines';

const approved: ProcurementPurchaseOrderSourceLineDto = {
  sourceLineId: 'bid-pvc', itemCode: '', description: 'PVC Pipe 50mm',
  quantity: 20, unitOfMeasure: 'EACH', unitPrice: 1900, lineTotal: 38000,
};
const ea: ItemUnitOfMeasureDto = {
  id: 'item-ea', unitOfMeasureId: 'ea-id', unitCode: 'EA', unitName: 'Each',
  conversionFactor: 1, isBaseUnit: true, isPurchaseUnit: true, isSalesUnit: true,
};

describe('approved purchase-order line mapping', () => {
  it('loads the contract commercial lines without guessing a stock mapping', () => {
    expect(createApprovedPurchaseOrderItems([approved], '2026-10-01')).toEqual([
      expect.objectContaining({
        approvedSourceLineId: 'bid-pvc', inventoryItemId: '',
        itemDescription: 'PVC Pipe 50mm', orderedQuantity: 20,
        unitOfMeasure: 'EACH', unitPrice: 1900, expectedDeliveryDate: '2026-10-01',
      }),
    ]);
  });

  it('retains EACH when mapping stock returns only EA and a catalogue price', () => {
    const row = createApprovedPurchaseOrderItems([approved])[0];
    const result = retainApprovedPurchaseOrderTerms({
      ...row, inventoryItemId: 'pvc-stock', warehouseId: 'main-store',
      itemDescription: 'Catalogue pipe description', unitOfMeasure: 'EA',
      itemUnitOfMeasureId: 'item-ea', unitPrice: 42, priceListLineId: 'price-list',
    }, findApprovedPurchaseOrderLine([approved], row), [ea]);
    expect(result).toMatchObject({
      inventoryItemId: 'pvc-stock', warehouseId: 'main-store',
      itemDescription: 'PVC Pipe 50mm', unitOfMeasure: 'EACH', unitPrice: 1900,
      orderedQuantity: 20, itemUnitOfMeasureId: undefined, priceListLineId: undefined,
    });
  });

  it('retains the approved device description instead of the seeded catalogue text', () => {
    const device = { ...approved, sourceLineId: 'bid-device', description: 'Barcode Device Kit', unitOfMeasure: 'EA', unitPrice: 700 };
    const row = createApprovedPurchaseOrderItems([device])[0];
    expect(retainApprovedPurchaseOrderTerms({ ...row, itemDescription: 'Seeded device kit for project material and procurement flows.' }, device, [ea]))
      .toMatchObject({ itemDescription: 'Barcode Device Kit', unitOfMeasure: 'EA', unitPrice: 700, itemUnitOfMeasureId: 'item-ea' });
  });

  it('allows a partial contract quantity without replacing delivery or warehouse choices', () => {
    const row = { ...createApprovedPurchaseOrderItems([approved])[0], orderedQuantity: 5, warehouseId: 'store', expectedDeliveryDate: '2026-10-02' };
    expect(retainApprovedPurchaseOrderTerms(row, approved)).toMatchObject({ orderedQuantity: 5, warehouseId: 'store', expectedDeliveryDate: '2026-10-02' });
  });

  it('recovers an existing manually entered row by its unique approved description', () => {
    expect(findApprovedPurchaseOrderLine([approved], { itemDescription: ' pvc  pipe 50mm ' })).toBe(approved);
  });

  it('does not guess from an unknown or ambiguous description', () => {
    expect(findApprovedPurchaseOrderLine([approved], { itemDescription: 'Different pipe' })).toBeUndefined();
    expect(findApprovedPurchaseOrderLine([approved, { ...approved, sourceLineId: 'other' }], { itemDescription: approved.description })).toBeUndefined();
  });

  it('does not carry a source-line id into a different contract', () => {
    expect(findApprovedPurchaseOrderLine([{ ...approved, sourceLineId: 'other' }], createApprovedPurchaseOrderItems([approved])[0])).toBeUndefined();
  });

  it('does not change unrelated manual rows', () => {
    const row = createApprovedPurchaseOrderItems([approved])[0];
    expect(retainApprovedPurchaseOrderTerms(row, undefined, [ea])).toBe(row);
  });

  it('does not attach a synthetic schedule-unit id as a real item-unit FK', () => {
    const unit = { ...ea, unitCode: 'EACH', unitOfMeasureId: '00000000-0000-0000-0000-000000000000' };
    expect(retainApprovedPurchaseOrderTerms(createApprovedPurchaseOrderItems([approved])[0], approved, [unit]).itemUnitOfMeasureId).toBeUndefined();
  });

  it('supports the exact inventory base unit even when the dropdown schedule lists EA', () => {
    expect(supportsApprovedPurchaseOrderUnit(approved, 'EACH', [ea])).toBe(true);
  });

  it('does not assume that different unit codes or packaging are equivalent', () => {
    expect(supportsApprovedPurchaseOrderUnit(approved, 'BOX', [ea])).toBe(false);
    expect(supportsApprovedPurchaseOrderUnit(approved, 'EA', [ea])).toBe(false);
    expect(supportsApprovedPurchaseOrderUnit(approved, 'BOX', [{ ...ea, unitCode: 'EACH' }])).toBe(true);
  });
});
