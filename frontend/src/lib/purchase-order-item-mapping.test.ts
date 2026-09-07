import { describe, expect, it } from 'vitest';
import { getPurchaseOrderItemMappingError } from './purchase-order-item-mapping';

describe('purchase order inventory mapping', () => {
  it.each([null, undefined, '', ' ', '00000000-0000-0000-0000-000000000000'])(
    'explains missing mapping %s before submitting the DTO', inventoryItemId => {
      expect(getPurchaseOrderItemMappingError([{ inventoryItemId }])).toContain('Line 1: select the matching saved inventory item');
    }
  );
  it('identifies the first unmapped line without changing source identity or prices', () => {
    const items = [{ inventoryItemId: '23d374bf-f2db-43db-a295-6049d14b3b94' }, { inventoryItemId: null }];
    expect(getPurchaseOrderItemMappingError(items)).toContain('Line 2:');
    expect(items[1].inventoryItemId).toBeNull();
    expect(getPurchaseOrderItemMappingError(items.slice(0, 1))).toBeNull();
  });
});
