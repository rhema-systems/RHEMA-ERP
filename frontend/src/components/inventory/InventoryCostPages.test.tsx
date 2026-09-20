import React from 'react';
import { render, screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import InventoryItemsPage from '@/app/inventory/items/page';
import WarehouseItemsPage from '@/app/inventory/warehouse-items/page';

const mocks = vi.hoisted(() => ({ getInventoryItems: vi.fn(), getWarehouseItems: vi.fn() }));
vi.mock('@/hooks/useInventoryCostCurrency', () => ({ useInventoryCostCurrency: () => 'GHS' }));
vi.mock('@/hooks/useFieldLabels', () => ({ useInventoryItemLabels: () => ({ getLabel: (name: string) => name }) }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  ...mocks,
  getUnitsOfMeasure: async () => [], getUomSchedules: async () => [], getActiveInventoryCategories: async () => [],
  getWarehouses: async () => [{ id: 'warehouse', name: 'Project Demo Warehouse', isActive: true }],
} }));

describe('inventory item-wide cost page wiring', () => {
  beforeEach(() => {
    mocks.getInventoryItems.mockResolvedValue([{ id: 'item', itemCode: 'SKU-001', name: 'PVC Pipe 50mm',
      averageCost: 1907.09, standardCost: 123, currentCost: 456, currentStock: 40, availableStock: 40,
      reorderLevel: 0, itemType: 1, status: 1, isActive: true }]);
    mocks.getWarehouseItems.mockResolvedValue([{ id: 'assignment', inventoryItemId: 'item', itemCode: 'SKU-001',
      itemName: 'PVC Pipe 50mm', warehouseId: 'warehouse', warehouseName: 'Project Demo Warehouse', itemType: 'StockItem',
      currentStock: 40, availableStock: 40, allocatedStock: 0, reorderLevel: 0, averageCost: 1918.85,
      itemAverageCost: 1907.09 }]);
  });
  it('catalogue labels the stored item average separately from standard cost', async () => {
    render(<InventoryItemsPage />);
    expect(await screen.findByText(/1,907\.09/)).toHaveTextContent('GHS');
    expect(screen.getByText(/Item-wide average cost:/)).toBeInTheDocument();
    expect(screen.getByText(/123\.00/)).toHaveTextContent('GHS');
    expect(screen.queryByText(/1,918\.85/)).not.toBeInTheDocument();
  });
  it('warehouse list uses separate response fields for warehouse and item-wide costs', async () => {
    render(<WarehouseItemsPage />);
    const row = (await screen.findByText(/1,907\.09/)).closest('tr')!;
    const cells = within(row).getAllByRole('cell');
    expect(cells).toHaveLength(12);
    expect(cells[8]).toHaveTextContent('1,918.85');
    expect(cells[9]).toHaveTextContent('1,907.09');
    expect(screen.getByRole('columnheader', { name: 'Warehouse avg. cost' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Item-wide avg. cost' })).toBeInTheDocument();
  });
});
