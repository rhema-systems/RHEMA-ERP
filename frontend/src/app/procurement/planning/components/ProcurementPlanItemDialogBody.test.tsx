import { fireEvent, render, screen } from '@testing-library/react';
import React, { useMemo, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';

import type { BusinessPartnerDto } from '@/services/businessPartnerService';
import type {
  CreateProcurementPlanItemDto,
  CreateProcurementPlanItemSupplierDto,
  InventoryItemDto,
} from '@/services/procurementPlanningService';

import { ProcurementPlanItemDialogBody } from './ProcurementPlanItemDialogBody';

const inventoryItem: InventoryItemDto = {
  id: 'inventory-item-1',
  itemCode: 'TOOL-SCAN-001',
  name: 'Heavy Duty Diagnostic Scanner',
  description: 'Vehicle diagnostic scanner',
  unitOfMeasure: 'EA',
  currentStock: 2,
  availableStock: 2,
  standardCost: 5595,
  averageCost: 5595,
  isSerialTracked: true,
  isLotTracked: false,
  itemType: 0,
  status: 1,
  categoryName: 'Tools',
};

const initialForm: CreateProcurementPlanItemDto = {
  itemDescription: '',
  estimatedQuantity: 1,
  unitOfMeasure: 'EA',
  estimatedUnitPrice: 0,
};

const supplier: BusinessPartnerDto = {
  id: 'supplier-1',
  partnerCode: 'SUP260003',
  partnerType: 'Supplier',
  partnerName: 'Pat Wages Enterprise',
  status: 'Active',
  isPreferred: false,
  isBlacklisted: false,
  createdAt: '2026-08-01T00:00:00Z',
};

function ProductSelectorHarness() {
  const [form, setForm] = useState(initialForm);
  const [searchTerm, setSearchTerm] = useState('TOOL');
  const [selectedItem, setSelectedItem] = useState<InventoryItemDto | null>(null);
  const inventoryResults = useMemo(() => {
    const query = searchTerm.trim().toLowerCase();
    return [inventoryItem].filter(
      (item) =>
        item.itemCode.toLowerCase().includes(query) ||
        item.name.toLowerCase().includes(query) ||
        item.categoryName.toLowerCase().includes(query),
    );
  }, [searchTerm]);

  return (
    <ProcurementPlanItemDialogBody
      currency="GHS"
      form={form}
      setForm={setForm}
      inventorySearchTerm={searchTerm}
      onInventorySearchTermChange={setSearchTerm}
      inventoryResults={inventoryResults}
      loadingInventory={false}
      selectedInventoryItem={selectedItem}
      onSelectInventoryItem={(item) => {
        setSelectedItem(item);
        setForm((current) => ({
          ...current,
          inventoryItemId: item.id,
          itemDescription: item.name,
          unitOfMeasure: item.unitOfMeasure,
          estimatedUnitPrice: item.standardCost,
        }));
      }}
      unitOfMeasureOptions={[{ value: 'EA', label: 'EA - Each' }]}
      loadingUnitsOfMeasure={false}
      marketAnalyses={[]}
      loadingMarketAnalyses={false}
      onMarketAnalysisSelect={vi.fn()}
      budgetAllocations={[]}
      loadingBudgetAllocations={false}
      suppliers={[]}
      supplierSearchTerm=""
      onSupplierSearchTermChange={vi.fn()}
      filteredSuppliers={[]}
      loadingSuppliers={false}
      selectedItemSuppliers={[]}
      onAddSupplier={vi.fn()}
      onUpdateSupplier={vi.fn()}
      onRemoveSupplier={vi.fn()}
    />
  );
}

function SupplierSelectorHarness() {
  const [form, setForm] = useState(initialForm);
  const [searchTerm, setSearchTerm] = useState('pat');
  const [selectedSuppliers, setSelectedSuppliers] = useState<CreateProcurementPlanItemSupplierDto[]>([]);
  const filteredSuppliers = [supplier].filter(
    (entry) =>
      entry.partnerName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      entry.partnerCode.toLowerCase().includes(searchTerm.toLowerCase()),
  );

  return (
    <ProcurementPlanItemDialogBody
      currency="GHS"
      form={form}
      setForm={setForm}
      inventorySearchTerm=""
      onInventorySearchTermChange={vi.fn()}
      inventoryResults={[]}
      loadingInventory={false}
      selectedInventoryItem={null}
      onSelectInventoryItem={vi.fn()}
      unitOfMeasureOptions={[{ value: 'EA', label: 'EA - Each' }]}
      loadingUnitsOfMeasure={false}
      marketAnalyses={[]}
      loadingMarketAnalyses={false}
      onMarketAnalysisSelect={vi.fn()}
      budgetAllocations={[]}
      loadingBudgetAllocations={false}
      suppliers={[supplier]}
      supplierSearchTerm={searchTerm}
      onSupplierSearchTermChange={setSearchTerm}
      filteredSuppliers={filteredSuppliers}
      loadingSuppliers={false}
      selectedItemSuppliers={selectedSuppliers}
      onAddSupplier={(entry) =>
        setSelectedSuppliers((current) => [
          ...current,
          { supplierId: entry.id, isPreferred: current.length === 0 },
        ])
      }
      onUpdateSupplier={vi.fn()}
      onRemoveSupplier={vi.fn()}
    />
  );
}

describe('ProcurementPlanItemDialogBody product selector', () => {
  it('closes the result overlay after selecting a searched inventory item', () => {
    render(<ProductSelectorHarness />);

    fireEvent.mouseDown(screen.getByRole('button', { name: /Heavy Duty Diagnostic Scanner/i }));

    expect(screen.getByLabelText('Product Search')).toHaveValue(
      'TOOL-SCAN-001 - Heavy Duty Diagnostic Scanner',
    );
    expect(screen.getByLabelText('Item Description *')).toHaveValue('Heavy Duty Diagnostic Scanner');
    expect(screen.queryByText('No items found')).not.toBeInTheDocument();
  });

  it('closes the supplier result overlay after adding a searched supplier', () => {
    render(<SupplierSelectorHarness />);

    fireEvent.mouseDown(screen.getByRole('button', { name: /Pat Wages Enterprise/i }));

    expect(screen.getByPlaceholderText('Search suppliers')).toHaveValue('');
    expect(screen.queryByRole('button', { name: /Pat Wages Enterprise/i })).not.toBeInTheDocument();
    expect(screen.getByText('Pat Wages Enterprise')).toBeInTheDocument();
  });

  it('does not ask the planner to choose a sourcing method', () => {
    render(<ProductSelectorHarness />);

    expect(document.getElementById('procurementMethod')).toBeNull();
    expect(screen.queryByText('Method')).not.toBeInTheDocument();
  });
});
