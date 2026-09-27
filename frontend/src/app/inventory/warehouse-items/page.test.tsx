import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import WarehouseItemsPage from './page';

const mocks = vi.hoisted(() => ({ capability: vi.fn(), items: vi.fn(), warehouses: vi.fn(), assignments: vi.fn(), assign: vi.fn(), toast: vi.fn() }));
vi.mock('@/services/procurement-access-control.service', () => ({ procurementAccessControlService: { checkCapability: mocks.capability } }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getInventoryItems: mocks.items, getWarehouses: mocks.warehouses, getWarehouseItems: mocks.assignments, assignItemsToWarehouses: mocks.assign,
} }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/hooks/useInventoryCostCurrency', () => ({ useInventoryCostCurrency: () => 'GHS' }));
vi.mock('@/components/inventory/InventoryCostValue', () => ({ InventoryCostValue: () => null }));
vi.mock('@/components/ui/select', () => ({
  Select: ({ children, value, onValueChange }: any) => <select value={value} onChange={event => onValueChange(event.target.value)}>{children}</select>,
  SelectTrigger: () => null, SelectValue: () => null, SelectContent: ({ children }: any) => <>{children}</>,
  SelectItem: ({ children, value }: any) => <option value={value}>{children}</option>,
}));

beforeEach(() => {
  vi.clearAllMocks();
  mocks.capability.mockResolvedValue({ allowed: true });
  mocks.warehouses.mockResolvedValue([{ id: 'one', name: 'Warehouse One', code: 'ONE', isActive: true }, { id: 'two', name: 'Warehouse Two', code: 'TWO', isActive: true }]);
  mocks.items.mockResolvedValue([
    { id: 'a', itemCode: 'A', name: 'Alpha', isActive: true },
    { id: 'b', itemCode: 'B', name: 'Beta', isActive: true },
    { id: 'inactive', itemCode: 'Z', name: 'Inactive', isActive: false },
  ]);
  mocks.assignments.mockResolvedValue([{ id: 'existing', warehouseId: 'one', warehouseName: 'Warehouse One', inventoryItemId: 'a', itemCode: 'A', itemName: 'Alpha', currentStock: 0, availableStock: 0, allocatedStock: 0, reorderLevel: 0 }]);
  mocks.assign.mockResolvedValue({ created: 1, skipped: 0, errors: [] });
});
afterEach(cleanup);

async function openAssignment() {
  render(<WarehouseItemsPage />);
  fireEvent.click(await screen.findByRole('button', { name: 'Assign Items to Warehouses' }));
  return within(screen.getByRole('dialog'));
}

describe('warehouse assignment selection and capability', () => {
  it('selects only filtered eligible items, preserves other manual selections, and clears all', async () => {
    const dialog = await openAssignment();
    fireEvent.click(dialog.getByLabelText('Warehouse Two (TWO)'));
    expect(dialog.queryByText('Z - Inactive')).not.toBeInTheDocument();
    fireEvent.click(dialog.getByLabelText('B - Beta'));
    fireEvent.change(dialog.getByPlaceholderText('Search items...'), { target: { value: 'Alpha' } });
    fireEvent.click(dialog.getByRole('button', { name: 'Select all filtered (1)' }));
    expect(dialog.getByText(/2 selected/)).toBeInTheDocument();
    fireEvent.click(dialog.getByLabelText('A - Alpha'));
    expect(dialog.getByText(/1 selected.*eligible in filter/)).toBeInTheDocument();
    fireEvent.click(dialog.getByRole('button', { name: 'Clear all' }));
    expect(dialog.getByRole('button', { name: 'Assign 0 Item(s) to 1 Warehouse(s)' })).toBeDisabled();
  });

  it('includes partially assigned items across mixed targets and submits both targets', async () => {
    const dialog = await openAssignment();
    fireEvent.click(dialog.getByLabelText('Warehouse One (ONE)'));
    expect(dialog.queryByText('A - Alpha')).not.toBeInTheDocument();
    fireEvent.click(dialog.getByLabelText('Warehouse Two (TWO)'));
    fireEvent.click(dialog.getByRole('button', { name: 'Select all filtered (2)' }));
    fireEvent.click(dialog.getByRole('button', { name: 'Assign 2 Item(s) to 2 Warehouse(s)' }));
    await waitFor(() => expect(mocks.assign).toHaveBeenCalledWith(expect.objectContaining({ inventoryItemIds: ['a', 'b'], warehouseIds: ['one', 'two'] })));
  });

  it('prunes selections that become ineligible after changing target warehouses', async () => {
    const dialog = await openAssignment();
    fireEvent.click(dialog.getByLabelText('Warehouse One (ONE)'));
    fireEvent.click(dialog.getByLabelText('Warehouse Two (TWO)'));
    fireEvent.click(dialog.getByRole('button', { name: 'Select all filtered (2)' }));
    fireEvent.click(dialog.getByLabelText('Warehouse Two (TWO)'));
    expect(dialog.queryByText('A - Alpha')).not.toBeInTheDocument();
    fireEvent.click(dialog.getByRole('button', { name: 'Assign 1 Item(s) to 1 Warehouse(s)' }));
    await waitFor(() => expect(mocks.assign).toHaveBeenCalledWith(expect.objectContaining({ inventoryItemIds: ['b'], warehouseIds: ['one'] })));
  });

  it('uses the same server capability and hides every mutation for a denied actor', async () => {
    mocks.capability.mockResolvedValue({ allowed: false, code: 'ACCESS_PERMISSION_DENIED', message: 'Your Security role does not grant assignment access.' });
    render(<WarehouseItemsPage />);
    expect(await screen.findByRole('status')).toHaveTextContent('ACCESS_PERMISSION_DENIED');
    expect(screen.queryByRole('button', { name: 'Assign Items to Warehouses' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Edit Alpha|Remove Alpha/ })).not.toBeInTheDocument();
    expect(mocks.capability).toHaveBeenCalledWith({ permissionCode: 'procurement.inventory.master-data.manage', sourceType: 'WarehouseItemAssignment', sourceReference: 'warehouse-item-assignment' });
    expect(mocks.assign).not.toHaveBeenCalled();
  });

  it('fails closed when access verification fails but retains readable assignments', async () => {
    mocks.capability.mockRejectedValue({ detail: 'Access service is unavailable', code: 'ACCESS_UNAVAILABLE' });
    render(<WarehouseItemsPage />);
    expect(await screen.findByRole('status')).toHaveTextContent('Access service is unavailable (ACCESS_UNAVAILABLE)');
    expect(screen.getByText('Alpha')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Assign Items to Warehouses' })).not.toBeInTheDocument();
  });

  it('retains selections after a server denial and displays its ProblemDetails', async () => {
    mocks.assign.mockRejectedValue({ isAxiosError: true, response: { status: 403, data: { detail: 'Assignment permission was revoked', extensions: { code: 'ACCESS_PERMISSION_DENIED' } } } });
    const dialog = await openAssignment();
    fireEvent.click(dialog.getByLabelText('Warehouse Two (TWO)'));
    fireEvent.click(dialog.getByLabelText('A - Alpha'));
    fireEvent.click(dialog.getByRole('button', { name: 'Assign 1 Item(s) to 1 Warehouse(s)' }));
    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ description: 'Assignment permission was revoked (ACCESS_PERMISSION_DENIED)' })));
    expect(dialog.getByLabelText('A - Alpha')).toBeChecked();
    expect(dialog.getByRole('button', { name: 'Assign 1 Item(s) to 1 Warehouse(s)' })).toBeEnabled();
  });
});
