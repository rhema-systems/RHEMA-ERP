import { describe, expect, it } from 'vitest';

import type { OpeningStockItemOption, OpeningStockWarehouseOption } from './opening-balance-governance';
import { buildInventoryOpeningTemplateCsv, parseInventoryOpeningImportFile } from './inventory-opening-import';

const items: OpeningStockItemOption[] = [
  { id: 'item-1', itemCode: 'CHAIR', name: 'Chair', unitOfMeasure: 'EA', isSerialTracked: false, isLotTracked: false, isBatchTracked: false },
  { id: 'item-2', itemCode: 'LAPTOP', name: 'Laptop', unitOfMeasure: 'EA', isSerialTracked: true, isLotTracked: false, isBatchTracked: false },
];
const warehouse: OpeningStockWarehouseOption = {
  id: 'warehouse-1', code: 'MAIN', name: 'Main', locations: [{ id: 'location-1', code: 'A1', name: 'A1' }],
};
const file = (content: string, name = 'opening.csv') => {
  const bytes = new TextEncoder().encode(content);
  return { name, arrayBuffer: async () => bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength) } as File;
};

describe('inventory opening import', () => {
  it('builds a reusable CSV template', () => {
    expect(buildInventoryOpeningTemplateCsv('CHAIR', 'A1')).toContain('"itemCode","locationCode","quantity","unitCost"');
    expect(buildInventoryOpeningTemplateCsv('CHAIR', 'A1')).toContain('"CHAIR","A1"');
    expect(buildInventoryOpeningTemplateCsv('CHAIR', 'A1')).toContain('"1.00"');
  });

  it('resolves eligible item and location codes and totals valid rows', async () => {
    const result = await parseInventoryOpeningImportFile(file([
      'itemCode,locationCode,quantity,unitCost,notes',
      'CHAIR,A1,25,12.50,Counted',
    ].join('\n')), items, warehouse);
    expect(result.errors).toEqual([]);
    expect(result.totalValue).toBe(312.5);
    expect(result.rows[0]).toEqual(expect.objectContaining({ inventoryItemId: 'item-1', locationId: 'location-1', quantity: '25', unitCost: '12.5' }));
  });

  it('returns row-level master-data, tracking, and duplicate errors without accepting invalid rows', async () => {
    const result = await parseInventoryOpeningImportFile(file([
      'itemCode,locationCode,quantity,unitCost,serialNumber',
      'LAPTOP,A1,2,1000,',
      'UNKNOWN,A1,1,10,',
      'CHAIR,A1,1,10,',
      'CHAIR,A1,1,10,',
    ].join('\n')), items, warehouse);
    expect(result.errors).toContain('Row 2: serialNumber is required for serial-tracked item LAPTOP.');
    expect(result.errors).toContain('Row 2: serial-tracked item LAPTOP must have quantity 1 per row.');
    expect(result.errors).toContain("Row 3: itemCode 'UNKNOWN' is not an eligible active stock item.");
    expect(result.errors).toContain('Row 5: duplicate item/location/tracking identity in this file.');
    expect(result.rows).toHaveLength(1);
  });

  it('preflights one thousand rows without rendering them', async () => {
    const rows = Array.from({ length: 1000 }, (_, index) => `CHAIR,A1,1,${index + 1},,,BATCH-${index + 1},`);
    const result = await parseInventoryOpeningImportFile(file([
      'itemCode,locationCode,quantity,unitCost,serialNumber,lotNumber,batchNumber,notes',
      ...rows,
    ].join('\n')), items, warehouse);
    expect(result.errors).toEqual([]);
    expect(result.rows).toHaveLength(1000);
    expect(result.totalValue).toBe(500500);
  });
});
