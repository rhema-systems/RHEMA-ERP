import axios from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { inventoryManagementService } from './inventoryManagementService';

vi.mock('axios', () => ({
  default: {
    get: vi.fn(),
    put: vi.fn(),
    post: vi.fn(),
  },
}));

const mockedAxios = vi.mocked(axios, true);

describe('inventory item identifier client', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.setItem('authToken', 'test-token');
  });

  it('uses the tenant-safe resolver and passes the identifier as a query parameter', async () => {
    mockedAxios.get.mockResolvedValueOnce({ data: { identifierKind: 'PrimaryBarcode' } });

    await inventoryManagementService.resolveItemIdentifier('ABC-123');

    expect(mockedAxios.get).toHaveBeenCalledWith('/api/inventory/item-identifiers/resolve', {
      params: { identifier: 'ABC-123' },
      headers: { Authorization: 'Bearer test-token', 'Content-Type': 'application/json' },
    });
  });

  it('updates item and unit identifiers only through the dedicated control API', async () => {
    mockedAxios.put.mockResolvedValue({ data: {} });

    await inventoryManagementService.updateItemIdentifiers('item-1', { barcode: 'PRIMARY' });
    await inventoryManagementService.updateItemUnitIdentifier('item-1', 'unit-1', {
      unitOfMeasureId: 'unit-1',
      conversionToBase: 12,
      isBaseUnit: false,
      isPurchaseUnit: true,
      isSalesUnit: true,
      isStockingUnit: true,
      barcode: 'BOX-12',
    });

    expect(mockedAxios.put).toHaveBeenNthCalledWith(
      1,
      '/api/inventory/item-identifiers/items/item-1',
      { barcode: 'PRIMARY' },
      { headers: { Authorization: 'Bearer test-token', 'Content-Type': 'application/json' } },
    );
    expect(mockedAxios.put).toHaveBeenNthCalledWith(
      2,
      '/api/inventory/item-identifiers/items/item-1/units/unit-1',
      expect.objectContaining({ barcode: 'BOX-12', conversionToBase: 12 }),
      { headers: { Authorization: 'Bearer test-token', 'Content-Type': 'application/json' } },
    );
  });

  it('requests a blob export and sends import content as multipart form data', async () => {
    const exportBlob = new Blob(['csv'], { type: 'text/csv' });
    mockedAxios.get.mockResolvedValueOnce({ data: exportBlob });
    mockedAxios.post.mockResolvedValueOnce({ data: { totalRows: 1, updatedItems: 1, updatedUnits: 0, errors: [] } });
    const file = new File(['ItemCode'], 'identifiers.csv', { type: 'text/csv' });

    expect(await inventoryManagementService.exportItemIdentifiers()).toBe(exportBlob);
    await inventoryManagementService.importItemIdentifiers(file);

    expect(mockedAxios.get).toHaveBeenCalledWith('/api/inventory/item-identifiers/export', {
      headers: { Authorization: 'Bearer test-token', 'Content-Type': 'application/json' },
      responseType: 'blob',
    });
    expect(mockedAxios.post).toHaveBeenCalledWith(
      '/api/inventory/item-identifiers/import',
      expect.any(FormData),
      { headers: { Authorization: 'Bearer test-token', 'Content-Type': 'application/json' } },
    );
  });
});
