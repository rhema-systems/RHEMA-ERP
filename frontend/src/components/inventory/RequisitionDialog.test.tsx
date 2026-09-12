import React from 'react';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RequisitionDialog } from './RequisitionDialog';
import { inventoryRequisitionService as service } from '@/services/inventoryRequisitionService';

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/components/workflow/WorkflowApprovalActions', () => ({ WorkflowApprovalActions: () => null }));
vi.mock('@/components/workflow/WorkflowApprovalHistoryPanel', () => ({
  WorkflowApprovalHistoryPanel: () => <div>No workflow history found for this record.</div>,
}));
vi.mock('@/services/inventoryRequisitionService', () => ({
  RequisitionStatusMap: { 1: 'Draft' },
  RequisitionTypeMap: { 1: 'Department Requisition' },
  inventoryRequisitionService: { getById: vi.fn(), getDepartments: vi.fn(), update: vi.fn(), submit: vi.fn() },
}));
vi.mock('@/services/inventoryManagementService', () => ({
  inventoryManagementService: {
    getWarehouseLocations: vi.fn().mockResolvedValue([]),
    getWarehouseInventoryItems: vi.fn().mockResolvedValue([]),
  },
}));

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(service.getDepartments).mockResolvedValue([]);
  vi.mocked(service.getById).mockResolvedValue({
    id: 'req-1', requisitionNumber: 'REQ-TEST', status: 1, requisitionType: 1,
    priority: 'Normal', items: [],
  } as never);
});

function open(mode: 'create' | 'edit' | 'view' = 'edit') {
  return render(<RequisitionDialog open mode={mode} requisitionId="req-1" warehouses={[]}
    onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
}

function selectTab(name: string | RegExp) {
  fireEvent.mouseDown(screen.getByRole('tab', { name }), { button: 0, ctrlKey: false });
}

function expectFixedFrame() {
  const dialog = screen.getByRole('dialog');
  expect(dialog).toHaveClass('flex', 'flex-col', 'h-[680px]', 'max-h-[90dvh]', 'overflow-hidden');
  expect(dialog).toHaveClass('max-w-6xl');
  expect(screen.getByRole('tablist')).toHaveClass('shrink-0');
  expect(screen.getByRole('tablist').parentElement).toHaveClass('flex', 'min-h-0', 'flex-1', 'flex-col');
  expect(screen.getByRole('tabpanel')).toHaveClass('min-h-0', 'flex-1', 'overflow-y-auto');
  const footer = screen.getAllByRole('button', { name: /^(Cancel|Close)$/ })[0].parentElement?.parentElement;
  expect(footer).toHaveClass('shrink-0');
  expect(screen.getByRole('tabpanel')).not.toContainElement(footer!);
}

describe('requisition dialog tab layout', () => {
  it('shows the original issue separately from posted returns and the net balance', async () => {
    vi.mocked(service.getById).mockResolvedValue({
      id: 'req-1', requisitionNumber: 'REQ-TEST', status: 6, requisitionType: 1,
      priority: 'Normal', items: [{ id: 'line-1', itemCode: 'SKU-001', itemName: 'PVC Pipe',
        requestedQuantity: 2, approvedQuantity: 2, issuedQuantity: 1,
        grossIssuedQuantity: 2, returnedQuantity: 1, netIssuedQuantity: 1,
        locationId: 'location-1', locationName: 'LOC-001 - Main',
        unitOfMeasure: 'EACH', unitCost: 1900, totalCost: 3800 }],
    } as never);
    open('view');
    await screen.findByRole('tab', { name: 'Items (1)' });
    selectTab('Items (1)');
    const headers = screen.getAllByRole('columnheader').map(cell => cell.textContent);
    const row = (await screen.findByText('SKU-001')).closest('tr')!;
    const cells = within(row).getAllByRole('cell');
    expect(cells[0]).toHaveClass('min-w-[140px]');
    expect(screen.getByRole('table')).toHaveClass('min-w-[1080px]');
    expect(cells[headers.indexOf('Location')]).toHaveTextContent('LOC-001 - Main');
    expect(cells[headers.indexOf('Returned')]).toHaveClass('bg-red-50', 'font-semibold', 'text-red-800');
    expect(screen.queryByText('Warehouse level')).not.toBeInTheDocument();
    for (const [name, value] of [['Requested', '2'], ['Approved', '2'], ['Issued', '2'], ['Returned', '1'], ['Net issued', '1']]) {
      expect(cells[headers.indexOf(name)]).toHaveTextContent(value);
    }
    expectFixedFrame();
  });

  it('does not invent a warehouse-level location for an issued legacy line', async () => {
    vi.mocked(service.getById).mockResolvedValue({ id: 'req-1', status: 6, items: [
      { id: 'line-1', itemCode: 'SKU-001', issuedQuantity: 2, approvedQuantity: 2, requestedQuantity: 2,
        unitCost: 1900, totalCost: 3800 },
    ] } as never);
    open('view');
    await screen.findByRole('tab', { name: 'Items (1)' });
    selectTab('Items (1)');
    expect(await screen.findByText('Not recorded')).toBeInTheDocument();
    expect(screen.queryByText('Warehouse level')).not.toBeInTheDocument();
    const headers = screen.getAllByRole('columnheader').map(cell => cell.textContent);
    const row = screen.getByText('SKU-001').closest('tr')!;
    const returnedCell = within(row).getAllByRole('cell')[headers.indexOf('Returned')];
    expect(returnedCell).toHaveTextContent('0');
    expect(returnedCell).not.toHaveClass('bg-red-50', 'text-red-800');
  });

  it.each(['edit', 'view'] as const)('keeps all three %s panels inside one fixed-height frame', async mode => {
    open(mode);
    await screen.findByRole('tab', { name: 'Workflow' });
    for (const name of ['Details', 'Items (0)', 'Workflow', 'Details']) {
      selectTab(name);
      await waitFor(() => expect(screen.getByRole('tab', { name })).toHaveAttribute('aria-selected', 'true'));
      expectFixedFrame();
    }
    expect(service.update).not.toHaveBeenCalled();
    expect(service.submit).not.toHaveBeenCalled();
  });

  it('keeps the expanded add-item form inside the scrollable panel without moving Save', async () => {
    open();
    await screen.findByRole('tab', { name: 'Items (0)' });
    selectTab('Items (0)');
    fireEvent.click(await screen.findByRole('button', { name: 'Add Item' }));
    expect(screen.getByRole('tabpanel')).toContainElement(screen.getByRole('combobox', { name: 'Inventory item' }));
    expectFixedFrame();
    expect(screen.getByRole('button', { name: 'Save' })).toBeVisible();
  });

  it('uses the same bounded height for the two-tab new requisition dialog', async () => {
    open('create');
    await waitFor(() => expect(service.getDepartments).toHaveBeenCalled());
    expect(screen.queryByRole('tab', { name: 'Workflow' })).not.toBeInTheDocument();
    expectFixedFrame();
    selectTab('Items (0)');
    expectFixedFrame();
  });
});
