import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  rows: [] as any[], permission: true, get: vi.fn(), create: vi.fn(), update: vi.fn(), cancel: vi.fn(),
  submit: vi.fn(), decide: vi.fn(), stage: vi.fn(), complete: vi.fn(), success: vi.fn(), error: vi.fn(),
  warehouseItems: vi.fn(), locations: vi.fn(), records: vi.fn(), record: vi.fn(), accounts: vi.fn(),
}));
vi.mock('sonner', () => ({ toast: { success: mocks.success, error: mocks.error } }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => mocks.permission }) }));
vi.mock('@/components/workflow/WorkflowApprovalHistoryPanel', () => ({
  WorkflowApprovalHistoryPanel: () => <div data-testid="workflow-history">Approval history</div>,
}));
vi.mock('@/services/inventoryDisposalService', () => ({ inventoryDisposalService: {
  getAll: async () => mocks.rows, getById: mocks.get, create: mocks.create, update: mocks.update,
  cancel: mocks.cancel, submit: mocks.submit, decide: mocks.decide, stageExecution: mocks.stage, complete: mocks.complete,
} }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getWarehouses: async () => [{ id: 'warehouse', name: 'Project Demo Warehouse' }],
  getInventoryByWarehouse: mocks.warehouseItems, getWarehouseLocations: mocks.locations,
} }));
vi.mock('@/services/document-management.service', () => ({ documentManagementService: {
  getRecords: mocks.records, getRecord: mocks.record,
} }));
vi.mock('@/services/finance/finance-data.service', () => ({ financeDataService: { getAccounts: mocks.accounts } }));

import Page from './page';

const line = (code = 'SKU-001', quantity = 2) => ({ id: 'line-' + code, inventoryItemId: code === 'SKU-001' ? 'pvc' : 'barcode',
  itemCode: code, itemName: code === 'SKU-001' ? 'PVC Pipe' : 'Barcode Device Kit', locationId: 'bin', locationCode: 'LOC-001',
  quantity, unitOfMeasure: 'EA', unitCost: 100, totalValue: quantity * 100 });
const disposal = (extra: Record<string, unknown> = {}): any => ({
  id: 'disposal', disposalNumber: 'DISP-001', warehouseId: 'warehouse', warehouseName: 'Project Demo Warehouse',
  method: 3, status: 1, reason: 'Damaged stock', identificationDetails: '', requestedByName: 'Manager',
  requestedById: 'manager', requestedAtUtc: '2026-09-12T15:00:00Z', currencyCode: 'GHS', rowVersion: 'AAAA',
  approvalRequired: false, canEdit: true, canCancel: true, canSubmit: true, canApprove: false,
  canStageExecution: false, canComplete: false, totalQuantity: 2, totalValue: 200, proceedsAmount: 0,
  lines: [line()], evidence: [], actions: [], committeeMembers: [], ...extra,
});
const selectTab = (name: RegExp | string) => fireEvent.mouseDown(screen.getByRole('tab', { name }), { button: 0, ctrlKey: false });
async function choose(label: string, option: RegExp | string) {
  fireEvent.click(screen.getByRole('combobox', { name: label }));
  fireEvent.click(await screen.findByRole('option', { name: option }));
}
async function openEdit() {
  render(<Page />);
  fireEvent.click(await screen.findByRole('button', { name: 'Edit DISP-001' }));
  await screen.findByRole('dialog', { name: 'Edit disposal' });
  await waitFor(() => expect(mocks.warehouseItems).toHaveBeenCalledWith('warehouse'));
}

beforeEach(() => {
  vi.clearAllMocks(); mocks.permission = true; mocks.rows = [disposal()];
  mocks.get.mockImplementation(async (id: string) => mocks.rows.find(row => row.id === id));
  mocks.warehouseItems.mockResolvedValue([
    { id: 'pvc', itemCode: 'SKU-001', name: 'PVC Pipe', itemType: 1 },
    { id: 'barcode', itemCode: 'PM-BARCODE', name: 'Barcode Device Kit', itemType: 1 },
    { id: 'asset', itemCode: 'ASSET-001', name: 'Fixed asset', itemType: 2 },
  ]);
  mocks.locations.mockResolvedValue([{ id: 'bin', warehouseId: 'warehouse', locationCode: 'LOC-001', name: 'Main bin', isActive: true, isDefault: true }]);
  mocks.records.mockResolvedValue([]); mocks.accounts.mockResolvedValue([]);
  mocks.create.mockImplementation(async (request: any) => disposal({ ...request, lines: request.lines }));
  mocks.update.mockImplementation(async (value: any, request: any) => disposal({ ...value, ...request, lines: request.lines }));
  mocks.cancel.mockImplementation(async (value: any) => disposal({ ...value, status: 10, canEdit: false, canCancel: false, canSubmit: false }));
  mocks.submit.mockImplementation(async (value: any) => disposal({ ...value, status: 11, canEdit: false, canSubmit: false, canStageExecution: true }));
  mocks.decide.mockResolvedValue(disposal());
  mocks.stage.mockResolvedValue(disposal({ status: 7, canEdit: false, canCancel: false, canSubmit: false, canStageExecution: false, canComplete: true, rowVersion: 'BBBB' }));
  mocks.complete.mockResolvedValue(disposal({ status: 8, canEdit: false, canCancel: false, canSubmit: false, canStageExecution: false, canComplete: false, postedStockValue: 195 }));
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', { configurable: true, value: vi.fn() });
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe('compact inventory disposal page', () => {
  it('uses labelled eye, pencil and cancel icons with no visible action text', async () => {
    render(<Page />);
    for (const action of ['View', 'Edit', 'Cancel']) {
      const button = await screen.findByRole('button', { name: action + ' DISP-001' });
      expect(button).toHaveAttribute('title', action);
      expect(button.textContent).toBe('');
      expect(button.querySelector('svg')).not.toBeNull();
    }
  });

  it('hides the workflow tab and approval actions when approval is inactive', async () => {
    mocks.rows = [disposal({ canApprove: true })];
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'View DISP-001' }));
    await screen.findByRole('dialog', { name: 'DISP-001' });
    expect(screen.queryByRole('tab', { name: /workflow/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Submit for approval' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Continue' })).toBeEnabled();
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
    await waitFor(() => expect(mocks.submit).toHaveBeenCalledWith(expect.objectContaining({ approvalRequired: false, evidence: [] })));
  });

  it('keeps active workflow history and server-authorized approval actions', async () => {
    mocks.rows = [disposal({ approvalRequired: true, status: 5, canEdit: false, canCancel: false, canSubmit: false, canApprove: true })];
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'View DISP-001' }));
    await screen.findByRole('dialog', { name: 'DISP-001' });
    expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled();
    selectTab(/workflow/i);
    expect(screen.getByTestId('workflow-history')).toBeInTheDocument();
  });

  it('requires a supporting document for active-workflow submission only', async () => {
    mocks.rows = [disposal({ approvalRequired: true })];
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'View DISP-001' }));
    await screen.findByRole('dialog', { name: 'DISP-001' });
    expect(screen.getByRole('button', { name: 'Submit for approval' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Submit for approval' })).toHaveAttribute('title', 'Edit the draft and add a supporting document first.');
    expect(mocks.submit).not.toHaveBeenCalled();
  });

  it('respects read-only capability flags even when the actor has inventory access', async () => {
    mocks.permission = false;
    mocks.rows = [disposal({ canEdit: false, canCancel: false, canSubmit: false })];
    render(<Page />);
    expect(await screen.findByRole('button', { name: 'View DISP-001' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Edit DISP-001' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancel DISP-001' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New disposal' })).not.toBeInTheDocument();
  });

  it('creates a draft with searchable item and warehouse controls and the default bin', async () => {
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'New disposal' }));
    await choose('Warehouse', 'Project Demo Warehouse');
    fireEvent.change(screen.getByLabelText('Reason', { exact: true }), { target: { value: 'Damaged packaging' } });
    selectTab(/Items/);
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Item' })).toBeEnabled());
    fireEvent.click(screen.getByRole('combobox', { name: 'Item' }));
    expect(screen.queryByRole('option', { name: /ASSET-001/ })).not.toBeInTheDocument();
    fireEvent.change(screen.getByPlaceholderText('Search item...'), { target: { value: 'Barcode' } });
    fireEvent.click(await screen.findByRole('option', { name: /PM-BARCODE/ }));
    expect(screen.getByRole('combobox', { name: 'Bin' })).toHaveTextContent('LOC-001');
    fireEvent.change(screen.getByLabelText('Quantity', { exact: true }), { target: { value: '3' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(mocks.create).toHaveBeenCalledWith(expect.objectContaining({
      warehouseId: 'warehouse', reason: 'Damaged packaging', evidence: [],
      lines: [expect.objectContaining({ inventoryItemId: 'barcode', locationId: 'bin', quantity: 3 })],
    })));
  });

  it('adds, edits and removes draft lines before saving the retained aggregate', async () => {
    await openEdit(); selectTab(/Items/);
    await choose('Item', /PM-BARCODE/);
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(screen.getByRole('spinbutton', { name: 'Quantity for PM-BARCODE' })).toHaveValue(1);
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Quantity for SKU-001' }), { target: { value: '4' } });
    fireEvent.click(screen.getByRole('button', { name: 'Remove PM-BARCODE' }));
    expect(screen.queryByRole('spinbutton', { name: 'Quantity for PM-BARCODE' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(mocks.update).toHaveBeenCalledWith(expect.objectContaining({ id: 'disposal', rowVersion: 'AAAA' }),
      expect.objectContaining({ lines: [expect.objectContaining({ inventoryItemId: 'pvc', quantity: 4 })] })));
    expect(mocks.create).not.toHaveBeenCalled();
  });

  it('keeps full-page quantities and footer actions while restoring fixed dimensions and equal tabs', async () => {
    await openEdit(); selectTab(/Items/);
    const dialog = screen.getByRole('dialog', { name: 'Edit disposal' });
    expect(dialog).toHaveStyle({ width: '960px', height: 'min(760px, calc(100vh - 48px))' });
    expect(screen.getByRole('tablist')).toHaveStyle({ gridTemplateColumns: 'repeat(3, minmax(0, 1fr))' });
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Quantity for SKU-001' }), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Full page' }));
    expect(dialog).toHaveStyle({ width: 'calc(100vw - 32px)', height: 'calc(100vh - 32px)' });
    expect(screen.getByRole('spinbutton', { name: 'Quantity for SKU-001' })).toHaveValue(5);
    expect(screen.getByRole('button', { name: 'Save draft' })).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: 'Restore' }));
    expect(dialog).toHaveStyle({ width: '960px' });
    fireEvent.click(screen.getByRole('button', { name: 'Full page' }));
    selectTab(/Supporting documents/);
    expect(dialog).toHaveStyle({ width: '960px' });
    selectTab(/Items/);
    expect(screen.getByRole('spinbutton', { name: 'Quantity for SKU-001' })).toHaveValue(5);
  });

  it('keeps cost columns hidden until chosen and searches/paginates the item grid', async () => {
    mocks.rows = [disposal({ lines: Array.from({ length: 30 }, (_, index) => ({ ...line('ROW-' + index), inventoryItemId: 'item-' + index })) })];
    await openEdit(); selectTab(/Items/);
    expect(screen.queryByRole('columnheader', { name: 'Unit cost' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Value' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('spinbutton', { name: /Quantity for/ })).toHaveLength(25);
    const dialog = screen.getByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Next' }));
    expect(screen.getAllByRole('spinbutton', { name: /Quantity for/ })).toHaveLength(5);
    fireEvent.change(screen.getByRole('textbox', { name: 'Search disposal items' }), { target: { value: 'ROW-29' } });
    expect(screen.getAllByRole('spinbutton', { name: /Quantity for/ })).toHaveLength(1);
    fireEvent.click(screen.getByRole('button', { name: 'Columns' }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Cost and value' }));
    expect(screen.getByRole('columnheader', { name: 'Unit cost' })).toBeInTheDocument();
  });

  it('requires a reason in the cancel popup before retaining the cancelled record', async () => {
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'Cancel DISP-001' }));
    const dialog = await screen.findByRole('dialog', { name: 'Cancel disposal' });
    expect(within(dialog).getByRole('button', { name: 'Cancel disposal' })).toBeDisabled();
    fireEvent.change(within(dialog).getByRole('textbox', { name: 'Action reason' }), { target: { value: 'Duplicate count entry' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel disposal' }));
    await waitFor(() => expect(mocks.cancel).toHaveBeenCalledWith(expect.objectContaining({ id: 'disposal' }), 'Duplicate count entry'));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Cancel disposal' })).not.toBeInTheDocument());
    expect(screen.getByRole('dialog', { name: 'DISP-001' })).toBeInTheDocument();
  });

  it('retains edits and the save button after a structured server validation failure', async () => {
    mocks.update.mockRejectedValue({ response: { data: { detail: 'Bin has insufficient stock.', code: 'INV_DISPOSAL_STOCK' } } });
    await openEdit(); selectTab(/Items/);
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Quantity for SKU-001' }), { target: { value: '9' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(mocks.error).toHaveBeenCalledWith('Bin has insufficient stock. (INV_DISPOSAL_STOCK)'));
    expect(screen.getByRole('dialog', { name: 'Edit disposal' })).toBeInTheDocument();
    expect(screen.getByRole('spinbutton', { name: 'Quantity for SKU-001' })).toHaveValue(9);
    expect(screen.getByRole('button', { name: 'Save draft' })).toBeEnabled();
  });

  it('posts a no-workflow write-off without supporting files and displays the actual stock value', async () => {
    mocks.rows = [disposal({ status: 11, canEdit: false, canCancel: false, canSubmit: false, canStageExecution: true })];
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'View DISP-001' }));
    await screen.findByRole('dialog', { name: 'DISP-001' });
    fireEvent.click(screen.getByRole('button', { name: 'Post' }));
    const confirmation = await screen.findByRole('dialog', { name: 'Post disposal' });
    fireEvent.click(within(confirmation).getByRole('button', { name: 'Post' }));
    await waitFor(() => expect(mocks.complete).toHaveBeenCalledWith(expect.objectContaining({ status: 7, rowVersion: 'BBBB' })));
    expect(mocks.stage).toHaveBeenCalledWith(expect.objectContaining({ id: 'disposal' }), expect.objectContaining({ proceedsAmount: 0, evidence: [] }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Post disposal' })).not.toBeInTheDocument());
    expect(screen.getByText('Posted stock value')).toBeInTheDocument();
    expect(screen.getByText(/195\.00/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Post' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
  });

  it('retains a prepared posting after completion fails and retries only completion with its new version', async () => {
    mocks.rows = [disposal({ status: 11, canEdit: false, canCancel: false, canSubmit: false, canStageExecution: true })];
    mocks.complete.mockRejectedValueOnce({ response: { data: { detail: 'Stock is temporarily frozen.', code: 'INV_FROZEN' } } });
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'View DISP-001' }));
    await screen.findByRole('dialog', { name: 'DISP-001' });
    fireEvent.click(screen.getByRole('button', { name: 'Post' }));
    const confirmation = await screen.findByRole('dialog', { name: 'Post disposal' });
    fireEvent.click(within(confirmation).getByRole('button', { name: 'Post' }));
    await waitFor(() => expect(mocks.error).toHaveBeenCalledWith('Stock is temporarily frozen. (INV_FROZEN)'));
    expect(confirmation).toBeInTheDocument();
    await waitFor(() => expect(within(confirmation).getByRole('button', { name: 'Post' })).toBeEnabled());
    fireEvent.click(within(confirmation).getByRole('button', { name: 'Post' }));
    await waitFor(() => expect(mocks.complete).toHaveBeenCalledTimes(2));
    expect(mocks.stage).toHaveBeenCalledTimes(1);
    expect(mocks.complete).toHaveBeenLastCalledWith(expect.objectContaining({ status: 7, rowVersion: 'BBBB', canStageExecution: false }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Post disposal' })).not.toBeInTheDocument());
  });

  it('does not post twice when the stage endpoint atomically returns a completed disposal', async () => {
    mocks.rows = [disposal({ status: 11, canEdit: false, canCancel: false, canSubmit: false, canStageExecution: true })];
    mocks.stage.mockResolvedValue(disposal({ status: 8, canEdit: false, canCancel: false, canSubmit: false,
      canStageExecution: false, canComplete: false, postedStockValue: 195, rowVersion: 'COMPLETED' }));
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'View DISP-001' }));
    await screen.findByRole('dialog', { name: 'DISP-001' });
    fireEvent.click(screen.getByRole('button', { name: 'Post' }));
    const confirmation = await screen.findByRole('dialog', { name: 'Post disposal' });
    fireEvent.click(within(confirmation).getByRole('button', { name: 'Post' }));
    await waitFor(() => expect(mocks.stage).toHaveBeenCalledWith(expect.objectContaining({ id: 'disposal' }),
      expect.objectContaining({ postImmediately: true, evidence: [] })));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Post disposal' })).not.toBeInTheDocument());
    expect(mocks.complete).not.toHaveBeenCalled();
    expect(mocks.success).toHaveBeenCalledWith('Disposal posted.');
    expect(screen.getByText('Posted stock value')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Post' })).not.toBeInTheDocument();
  });
});
