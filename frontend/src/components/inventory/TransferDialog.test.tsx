import React from 'react';
import { act, fireEvent, render, screen, waitFor, cleanup } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TransferDialog } from './TransferDialog';

const mocks = vi.hoisted(() => ({
  warehouseItems: vi.fn(), locations: vi.fn(), detail: vi.fn(), permission: vi.fn(), submit: vi.fn(),
  close: vi.fn(), toast: vi.fn(), requirements: vi.fn(), visibility: { showTab: true },
}));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ user: { id: 'operator' }, hasPermission: mocks.permission }) }));
vi.mock('@/hooks/useWorkflowSummary', () => ({ useWorkflowSummary: () => ({
  summary: {}, visibility: mocks.visibility, loading: false, refresh: vi.fn(),
}) }));
vi.mock('@/components/workflow/WorkflowApprovalActions', () => ({ WorkflowApprovalActions: ({ onSubmit, canSubmit }: any) =>
  canSubmit ? <button onClick={onSubmit}>Finalize transfer</button> : <span data-testid="approval-actions" /> }));
vi.mock('@/components/workflow/WorkflowRecordTab', () => ({
  WorkflowTabTrigger: () => <span data-testid="workflow-tab">Workflow</span>, WorkflowTabContent: () => null,
}));
vi.mock('@/components/inventory/InventoryTrackingExceptionSelect', () => ({
  InventoryTrackingExceptionSelect: () => null,
  useAvailableInventoryTrackingExceptions: () => ({ exceptions: [], loading: false }),
}));
vi.mock('@/services/document-management.service', () => ({ documentManagementService: { getRecords: vi.fn(async () => []) } }));
vi.mock('@/services/inventoryTrackingControlService', () => ({ inventoryTrackingControlService: { getRequirements: mocks.requirements } }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getWarehouseInventoryItems: mocks.warehouseItems, getWarehouseLocations: mocks.locations,
  getInventoryTransferById: mocks.detail, getTransferDiscrepancyResolutions: vi.fn(async () => ({})),
  submitTransferForApproval: mocks.submit, closeTransfer: mocks.close,
} }));
vi.mock('@/components/ui/select', () => ({
  Select: ({ children, onValueChange, value, disabled }: any) => <select value={value} disabled={disabled} onChange={event => onValueChange(event.target.value)}>{children}</select>,
  SelectTrigger: () => <option value="">Select</option>, SelectValue: () => null,
  SelectContent: ({ children }: any) => <>{children}</>, SelectItem: ({ value, children }: any) => <option value={value}>{children}</option>,
}));

const warehouses: any[] = [{ id: 'warehouse', name: 'Project Demo Warehouse' }];
const saved: any = {
  id: 'transfer', transferNumber: 'TRF-1', status: 'Approved', approvalRequired: false,
  sourceWarehouseId: 'warehouse', destinationWarehouseId: 'warehouse',
  sourceWarehouseName: 'Project Demo Warehouse', destinationWarehouseName: 'Project Demo Warehouse',
  hasOpenDiscrepancy: false, rowVersion: 'AQID', actions: [], discrepancies: [],
  items: [{ id: 'line', inventoryItemId: 'pvc', itemCode: 'SKU-001', itemName: 'PVC Pipe', requestedQuantity: 2,
    shippedQuantity: 0, receivedQuantity: 0, unitCost: 10, totalCost: 20, sourceLocationName: 'LOC-001', destinationLocationName: 'DEFAULT' }],
};
beforeEach(() => {
  vi.clearAllMocks(); mocks.permission.mockReturnValue(true);
  mocks.locations.mockResolvedValue([]); mocks.detail.mockResolvedValue(saved);
  mocks.warehouseItems.mockResolvedValue([{ inventoryItemId: 'pvc', itemCode: 'SKU-001', itemName: 'PVC Pipe', availableStock: 300, unitCost: 10 }]);
  mocks.requirements.mockResolvedValue({ inventoryItemId: 'pvc', requiresSerial: true, requiresLot: true,
    requiresBatch: true, requiresManufactureDate: true, requiresExpiryDate: true });
});
afterEach(cleanup);

describe('transfer dialog', () => {
  it.each(['view', 'edit'] as const)('maximizes and restores Items without reloading or losing input (%s)', async (mode) => {
    mocks.detail.mockResolvedValue({ ...saved, status: 'Draft' });
    render(<TransferDialog open mode={mode} initialTab="items" transfer={saved} warehouses={warehouses} onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    const fullPage = await screen.findByRole('button', { name: 'Full page' });
    if (mode === 'edit') {
      fireEvent.click(screen.getByRole('button', { name: 'Add Item' }));
      fireEvent.change(screen.getByPlaceholderText('Search by item code or name...'), { target: { value: 'PVC' } });
    }
    fireEvent.click(fullPage);
    expect(screen.getByRole('dialog')).toHaveClass('w-[calc(100vw-32px)]', 'h-[calc(100dvh-32px)]');
    expect(screen.getByRole('dialog')).not.toHaveClass('w-[800px]');
    expect(screen.getByRole('button', { name: 'Restore' })).toHaveAttribute('aria-pressed', 'true');
    if (mode === 'edit') {
      expect(screen.getByRole('button', { name: 'Save Changes' })).toBeVisible();
      expect(screen.getByPlaceholderText('Search by item code or name...')).toHaveValue('PVC');
    }
    fireEvent.click(screen.getByRole('button', { name: 'Restore' }));
    expect(screen.getByRole('dialog')).toHaveClass('w-[800px]', 'h-[85vh]');
    expect(screen.getByRole('button', { name: 'Full page' })).toHaveAttribute('aria-pressed', 'false');
    expect(mocks.detail).toHaveBeenCalledTimes(1);
  });

  it('restores fixed width when leaving Items and when reopening the dialog', async () => {
    const props = { mode: 'view' as const, initialTab: 'items' as const, transfer: saved, warehouses, onOpenChange: vi.fn(), onSuccess: vi.fn() };
    const { rerender } = render(<TransferDialog {...props} open />);
    fireEvent.click(await screen.findByRole('button', { name: 'Full page' }));
    fireEvent.mouseDown(screen.getByRole('tab', { name: 'Transfer Details' }), { button: 0, ctrlKey: false });
    expect(screen.getByRole('dialog')).toHaveClass('w-[800px]');
    fireEvent.mouseDown(screen.getByRole('tab', { name: 'Items (1)' }), { button: 0, ctrlKey: false });
    fireEvent.click(screen.getByRole('button', { name: 'Full page' }));
    rerender(<TransferDialog {...props} open={false} />);
    rerender(<TransferDialog {...props} open />);
    expect(await screen.findByRole('button', { name: 'Full page' })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('dialog')).toHaveClass('w-[800px]');
  });

  it('accepts the actual warehouse DTO shape without crashing when selecting the destination', async () => {
    render(<TransferDialog open mode="create" warehouses={warehouses} onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    expect(screen.getByRole('dialog').className).toContain('w-[800px]');
    expect(screen.getByRole('dialog').className).not.toContain('max-w-[1100px]');
    await act(async () => { fireEvent.change(screen.getAllByRole('combobox')[0], { target: { value: 'warehouse' } }); });
    await waitFor(() => expect(mocks.warehouseItems).toHaveBeenCalledWith('warehouse'));
    await act(async () => { fireEvent.change(screen.getAllByRole('combobox')[1], { target: { value: 'warehouse' } }); });
    expect(screen.getByRole('button', { name: 'Create Transfer' })).toBeEnabled();
  });

  it('shows direct Ready to ship and hides approval actions/tabs even if a workflow is later activated', async () => {
    render(<TransferDialog open mode="view" transfer={saved} warehouses={warehouses} currencyCode="GHS" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    expect(await screen.findByText('Ready to ship')).toBeInTheDocument();
    expect(screen.getByRole('dialog').className).toContain('w-[800px]');
    expect(screen.queryByTestId('workflow-tab')).not.toBeInTheDocument();
    expect(screen.queryByTestId('approval-actions')).not.toBeInTheDocument();
    expect(screen.queryByText('Financial Summary')).not.toBeInTheDocument();
    expect(screen.queryByText('Total Value:')).not.toBeInTheDocument();
  });

  it('keeps active approval workflow controls and hides editing for read-only users', async () => {
    mocks.permission.mockReturnValue(false);
    mocks.detail.mockResolvedValue({ ...saved, status: 'Submitted', approvalRequired: true });
    render(<TransferDialog open mode="edit" transfer={saved} warehouses={warehouses} onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    expect(await screen.findByTestId('workflow-tab')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save Changes' })).not.toBeInTheDocument();
  });

  it.each([false, true])('has no manual transfer closure for received transfers (approvalRequired=%s)', async (approvalRequired) => {
    mocks.detail.mockResolvedValue({ ...saved, status: 'Received', approvalRequired });
    const onOpenChange = vi.fn();
    render(<TransferDialog open mode="view" initialTab="controls" transfer={saved} warehouses={warehouses} onOpenChange={onOpenChange} onSuccess={vi.fn()} />);
    expect(await screen.findByRole('tab', { name: 'History' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Close transfer' })).not.toBeInTheDocument();
    expect(screen.queryByText('Transfer closure')).not.toBeInTheDocument();
    expect(screen.queryByText('Immutable transfer action register')).not.toBeInTheDocument();
    expect(screen.queryByText('Receipt discrepancy register')).not.toBeInTheDocument();
    expect(screen.getByRole('dialog').className).toContain('h-[85vh]');
    expect(screen.getByRole('dialog').className).toContain('w-[800px]');
    fireEvent.click(screen.getAllByRole('button', { name: 'Close', exact: true })[0]);
    expect(onOpenChange).toHaveBeenCalledWith(false);
    expect(mocks.close).not.toHaveBeenCalled();
  });

  it('uses central tracking requirements when the warehouse DTO has no tracking flags', async () => {
    mocks.detail.mockResolvedValue({ ...saved, status: 'Draft', approvalRequired: true });
    render(<TransferDialog open mode="edit" initialTab="items" transfer={saved} warehouses={warehouses} onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    const addItemButton = await screen.findByRole('button', { name: 'Add Item' });
    expect(screen.queryByRole('columnheader', { name: 'Unit Cost' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Total' })).not.toBeInTheDocument();
    const edit = screen.getByRole('button', { name: 'Edit' });
    expect(edit).toHaveAttribute('title', 'Edit');
    expect(edit).toHaveTextContent('');
    expect(edit.querySelector('svg')).not.toBeNull();
    await act(async () => { fireEvent.click(addItemButton); });
    const itemPicker = screen.getAllByRole('combobox').find(element => element.querySelector('option[value="pvc"]'));
    expect(itemPicker).toBeDefined();
    await act(async () => { fireEvent.change(itemPicker!, { target: { value: 'pvc' } }); });
    await waitFor(() => expect(mocks.requirements).toHaveBeenCalledWith('pvc'));
    expect(await screen.findByText('Batch Number')).toBeInTheDocument();
    expect(screen.getByText('Serial Number')).toBeInTheDocument();
    expect(screen.getByText('Lot Number')).toBeInTheDocument();
    expect(screen.getByText('Manufacture Date')).toBeInTheDocument();
    expect(screen.getByText('Expiry Date')).toBeInTheDocument();
    expect(screen.queryByText('Unit Cost:')).not.toBeInTheDocument();
  });
});
