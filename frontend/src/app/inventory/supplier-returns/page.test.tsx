import React from 'react';
import { fireEvent, render, screen, waitFor, cleanup } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  rows: [] as any[], sources: [] as any[], locations: vi.fn(), canIssue: true, toast: vi.fn(), submit: vi.fn(), ship: vi.fn(), get: vi.fn(), update: vi.fn(),
}));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: (permission: string) => permission === 'procurement.inventory.issue' && mocks.canIssue }) }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/services/inventoryWarehouseService', () => ({ inventoryWarehouseService: { getWarehouseLocations: mocks.locations } }));
vi.mock('@/services/supplierReturnService', () => ({
  supplierReturnReasons: ['Quality', 'Damage', 'Excess', 'Wrong', 'Other'],
  supplierReturnService: {
    getAll: async () => mocks.rows, getSourceGrns: async () => mocks.sources, submit: mocks.submit,
    ship: mocks.ship, get: mocks.get, update: mocks.update, reject: vi.fn(), cancel: vi.fn(), create: vi.fn(),
  },
}));
import Page from './page';

describe('Inventory supplier-return actions', () => {
  it('uses the stored Finance completion on reload without exposing Finance data to a stores user', async () => {
    Object.assign(mocks.rows[0], { status: 'Shipped', financeResolutionCompleted: true, supplierDebitNoteId: 'credit-1', canSubmit: false, canCancel: false });
    mocks.get.mockResolvedValue({ ...mocks.rows[0], warehouseId: 'warehouse-1', items: [] });
    render(<Page />);
    expect(await screen.findByText('Credit applied')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'View SRT-1' }));
    expect(await screen.findByText('Supplier credit resolved')).toBeInTheDocument();
    expect(screen.queryByText(/Finance can link/)).not.toBeInTheDocument();
    expect(screen.queryByText('Finance resolution pending')).not.toBeInTheDocument();
  });
  beforeEach(() => {
    vi.clearAllMocks(); mocks.canIssue = true; mocks.sources = [];
    mocks.locations.mockReset(); mocks.locations.mockResolvedValue([]); mocks.get.mockReset();
    mocks.rows = [{ id: 'return-1', returnNumber: 'SRT-1', status: 'Draft', supplierName: 'Supplier',
      grnNumber: 'GRN-1', totalQuantity: 1, totalValue: 100, returnReason: 'Excess',
      approvalRequired: false, canSubmit: true, canCancel: true, canApprove: false, canDispatch: false }];
    mocks.submit.mockResolvedValue(undefined); mocks.ship.mockResolvedValue(undefined);
  });
  afterEach(cleanup);

  it('uses Complete for the server-confirmed inactive draft and keeps it editable', async () => {
    render(<Page />);
    fireEvent.click(await screen.findByRole('button', { name: 'Complete' }));
    await waitFor(() => expect(mocks.submit).toHaveBeenCalledWith('return-1'));
    expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Edit' })).toHaveAttribute('title', 'Edit');
    expect(screen.getByRole('button', { name: 'Edit' }).querySelector('svg')).not.toBeNull();
    expect(screen.getByRole('button', { name: 'Edit' })).toHaveTextContent('');
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
  });
  it('keeps active draft submission wording', async () => {
    mocks.rows[0].approvalRequired = true;
    render(<Page />);
    expect(await screen.findByRole('button', { name: 'Submit' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Complete' })).not.toBeInTheDocument();
  });
  it('does not show approval or create actions to the read-only actor', async () => {
    mocks.canIssue = false; Object.assign(mocks.rows[0], { status: 'Submitted', canSubmit: false, canCancel: false, canApprove: false });
    render(<Page />); await screen.findByText('SRT-1');
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New supplier return' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'View SRT-1' })).toBeInTheDocument();
  });
  it('dispatches directly ready stock without claiming a human approval or automatic Finance document', async () => {
    Object.assign(mocks.rows[0], { status: 'ReadyToDispatch', canSubmit: false, canDispatch: true });
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'Dispatch' }));
    expect(screen.getByText('Ready to dispatch')).toBeInTheDocument();
    expect(screen.queryByText(/independent approver cannot dispatch/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Dispatch stock' }));
    await waitFor(() => expect(mocks.ship).toHaveBeenCalledWith('return-1', undefined));
    expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ title: expect.stringContaining('Finance resolution is pending') }));
  });
  it('retains the dispatch dialog and typed tracking reference when stock validation fails', async () => {
    Object.assign(mocks.rows[0], { status: 'ReadyToDispatch', canSubmit: false, canDispatch: true });
    mocks.ship.mockRejectedValue({ response: { data: { detail: 'Insufficient bin stock', code: 'INV_STOCK' } } });
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'Dispatch' }));
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'TRACK-1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Dispatch stock' }));
    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ description: 'Insufficient bin stock (INV_STOCK)' })));
    expect(screen.getByRole('textbox')).toHaveValue('TRACK-1');
    expect(screen.getByRole('button', { name: 'Dispatch stock' })).toBeInTheDocument();
  });
  it('labels dispatched stock as Finance pending without a second dispatch action', async () => {
    Object.assign(mocks.rows[0], { status: 'Shipped', canSubmit: false, canCancel: false });
    render(<Page />);
    expect(await screen.findByText('Finance resolution pending')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Dispatch' })).not.toBeInTheDocument();
  });

  it('distinguishes an acknowledged supplier response from financial completion', async () => {
    Object.assign(mocks.rows[0], { status: 'Acknowledged', canSubmit: false, canCancel: false });
    render(<Page />);
    expect(await screen.findByText('Supplier response recorded · Finance pending')).toBeInTheDocument();
    expect(screen.queryByText('Finance resolution pending')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Dispatch' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Post|Refund|Create credit/i })).not.toBeInTheDocument();
  });

  it('shows the stored credit reference and amount without claiming an AP credit or journal exists', async () => {
    Object.assign(mocks.rows[0], { status: 'Acknowledged', canSubmit: false, canCancel: false });
    mocks.get.mockResolvedValue({ ...mocks.rows[0], warehouseId: 'warehouse-1', items: [], creditNoteNumber: 'SCN-2026-12', creditNoteAmount: 695 });
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'View SRT-1' }));
    expect(await screen.findByRole('region', { name: 'Supplier resolution' })).toBeInTheDocument();
    expect(screen.getByText('Recorded credit reference')).toBeInTheDocument();
    expect(screen.getByText('SCN-2026-12')).toBeInTheDocument();
    expect(screen.getByText('695.00')).toBeInTheDocument();
    expect(screen.getByText('A recorded supplier reference alone does not settle the original invoice.')).toBeInTheDocument();
    expect(mocks.ship).not.toHaveBeenCalled();
    expect(mocks.update).not.toHaveBeenCalled();
  });

  it('keeps a dispatched return awaiting supplier response instead of inventing a credit', async () => {
    Object.assign(mocks.rows[0], { status: 'Shipped', canSubmit: false, canCancel: false });
    mocks.get.mockResolvedValue({ ...mocks.rows[0], warehouseId: 'warehouse-1', items: [], creditNoteNumber: null, creditNoteAmount: 0 });
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'View SRT-1' }));
    expect(await screen.findByText('Awaiting supplier response')).toBeInTheDocument();
    expect(screen.queryByText('Recorded credit reference')).not.toBeInTheDocument();
    expect(screen.queryByText('Recorded credit amount')).not.toBeInTheDocument();
    expect(screen.getByText('Dispatch does not create a supplier credit automatically.')).toBeInTheDocument();
  });

  const returnedLine = () => ({ id: 'line-1', inventoryItemId: 'item-1', locationId: 'bin-1',
    itemCode: 'SKU-1', itemName: 'Test item', returnQuantity: 1, unitOfMeasure: 'EA', unitCost: 700, totalCost: 700 });

  it('resolves the source bin code and name within the return warehouse without changing GRN value', async () => {
    mocks.get.mockResolvedValue({ ...mocks.rows[0], warehouseId: 'warehouse-1', items: [returnedLine()] });
    mocks.locations.mockResolvedValue([{ id: 'bin-1', warehouseId: 'warehouse-1', locationCode: 'LOC-001', name: 'Main' }]);
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'View SRT-1' }));
    expect(await screen.findByRole('cell', { name: 'LOC-001 — Main' })).toBeInTheDocument();
    expect(mocks.locations).toHaveBeenCalledWith('warehouse-1');
    expect(screen.getByRole('cell', { name: '700.00' })).toBeInTheDocument();
    expect(screen.queryByText('bin-1')).not.toBeInTheDocument();
  });

  it('does not display a matching bin from another warehouse', async () => {
    mocks.get.mockResolvedValue({ ...mocks.rows[0], warehouseId: 'warehouse-1', items: [returnedLine()] });
    mocks.locations.mockResolvedValue([{ id: 'bin-1', warehouseId: 'warehouse-2', locationCode: 'WRONG', name: 'Other' }]);
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'View SRT-1' }));
    expect(await screen.findByRole('cell', { name: 'Bin name unavailable' })).toBeInTheDocument();
    expect(screen.queryByText('WRONG — Other')).not.toBeInTheDocument();
  });

  it('keeps return details available after a lookup error without showing an ID or a false dispatch blocker', async () => {
    mocks.get.mockResolvedValue({ ...mocks.rows[0], warehouseId: 'warehouse-1', status: 'Shipped', items: [returnedLine()] });
    mocks.locations.mockRejectedValue(new Error('Lookup unavailable'));
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'View SRT-1' }));
    expect(await screen.findByRole('cell', { name: 'Bin name unavailable' })).toBeInTheDocument();
    expect(screen.queryByText('Missing — dispatch blocked')).not.toBeInTheDocument();
    expect(screen.queryByText('bin-1')).not.toBeInTheDocument();
  });

  it('clearly labels the original receipt unit cost in the editable draft', async () => {
    mocks.sources = [{ id: 'grn-1', supplierId: 'supplier-1', warehouseId: 'warehouse-1', grnNumber: 'GRN-1',
      items: [{ id: 'grn-line-1', acceptedQuantity: 20, unitCost: 700, itemCode: 'SKU-1', unitOfMeasure: 'EA' }] }];
    mocks.get.mockResolvedValue({ ...mocks.rows[0], goodsReceiptNoteId: 'grn-1', items: [{ ...returnedLine(), grnItemId: 'grn-line-1' }] });
    render(<Page />); fireEvent.click(await screen.findByRole('button', { name: 'Edit' }));
    expect(await screen.findByRole('columnheader', { name: 'GRN unit cost' })).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'System cost' })).not.toBeInTheDocument();
    expect(screen.getByRole('spinbutton')).toHaveValue(1);
  });
});
