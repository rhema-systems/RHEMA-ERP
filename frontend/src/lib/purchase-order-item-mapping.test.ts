import { describe, expect, it } from 'vitest';
import { getPurchaseOrderItemMappingError } from './purchase-order-item-mapping';
import { purchaseOrderLineType, requiresPurchaseOrderStock } from './purchase-order-line-types';

describe('purchase order inventory mapping', () => {
  it.each([null, undefined, '', ' ', '00000000-0000-0000-0000-000000000000'])(
    'allows a descriptive line without a catalogue selection: %s', inventoryItemId => {
      expect(getPurchaseOrderItemMappingError([{ inventoryItemId, itemDescription: 'Ad hoc goods or services', unitOfMeasure: 'EACH' }])).toBeNull();
    }
  );
  it('identifies the first unmapped line without changing source identity or prices', () => {
    const items = [{ inventoryItemId: '23d374bf-f2db-43db-a295-6049d14b3b94', itemDescription: 'Pipe', unitOfMeasure: 'EACH' }, { inventoryItemId: null }];
    expect(getPurchaseOrderItemMappingError(items)).toContain('Line 2:');
    expect(items[1].inventoryItemId).toBeNull();
    expect(getPurchaseOrderItemMappingError(items.slice(0, 1))).toBeNull();
  });
  it.each([1, 4, 'StockItem', 'FixedAsset'] as const)('requires mapping at stock receipt for %s', value => {
    expect(requiresPurchaseOrderStock(value)).toBe(true);
  });
  it.each([2, 3, 'Service', 'NonStock'] as const)('does not create inventory for %s', value => {
    expect(requiresPurchaseOrderStock(value)).toBe(false);
  });
  it('preserves legacy stock behaviour and serializes enum names consistently', () => {
    expect(purchaseOrderLineType(undefined)).toBe(1);
    expect(purchaseOrderLineType('Service')).toBe(2);
  });
});
