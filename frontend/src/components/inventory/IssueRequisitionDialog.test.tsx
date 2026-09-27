import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { IssueRequisitionDialog } from './IssueRequisitionDialog';
import { inventoryRequisitionService as service } from '@/services/inventoryRequisitionService';
import { inventoryManagementService } from '@/services/inventoryManagementService';
import { apiService } from '@/services/api.service';

vi.mock('next/dynamic', () => ({ default: () => (props: { fileData?: Uint8Array }) => <div data-testid="voucher-pdf">{Array.from(props.fileData || []).join(',')}</div> }));
vi.mock('@/services/api.service', () => ({ apiService: { downloadBlob: vi.fn() } }));
vi.mock('@/services/document-management.service', () => ({ documentManagementService: {} }));

const state = vi.hoisted(() => ({ actor: 'receiver', canIssue: false, toast: vi.fn() }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ user: { id: state.actor }, hasPermission: () => state.canIssue }) }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: state.toast }) }));
vi.mock('@/components/inventory/InventoryTrackingExceptionSelect', () => ({
  InventoryTrackingExceptionSelect: ({ value, onValueChange }: { value?: string; onValueChange: (value?: string) => void }) => (
    <select aria-label="Approved tracking exception" value={value || ''} onChange={event => onValueChange(event.target.value || undefined)}>
      <option value="">No approved exception</option>
      <option value="exception-1">Approved FIFO exception</option>
    </select>
  ),
  useAvailableInventoryTrackingExceptions: () => ({ exceptions: [], loading: false, refresh: vi.fn() }),
}));
vi.mock('@/services/inventoryRequisitionService', () => ({
  RequisitionStatusMap: { 3: 'Approved', 6: 'Issued', 7: 'Completed' },
  inventoryRequisitionService: {
    getById: vi.fn(), getIssueVouchers: vi.fn(), getIssueReceivers: vi.fn(),
    getIssueAccountingOptions: vi.fn(), acknowledgeIssueVoucher: vi.fn(), issue: vi.fn(),
  },
}));
vi.mock('@/services/inventoryManagementService', () => ({
  inventoryManagementService: { getWarehouseLocations: vi.fn() },
}));

beforeAll(() => {
  Element.prototype.scrollIntoView = vi.fn();
  Element.prototype.hasPointerCapture = vi.fn().mockReturnValue(false);
  Element.prototype.releasePointerCapture = vi.fn();
});

const voucher = (status: number | string) => ({
  id: 'voucher-1', voucherNumber: 'SIV-TEST', receiverUserId: 'receiver', receiverName: 'Jane Employee',
  issuerName: 'John Manager', status, rowVersion: 'AQID', issuedAtUtc: '2026-09-06T22:22:08Z',
  warehouseName: 'Main Stores', requisitionNumber: 'REQ-TEST',
  movementReasonCode: 'DEPARTMENT_CONSUMPTION', financeJournalEntryId: 'journal-1', lines: [{
    id: 'voucher-line-1', itemCode: 'SKU-001', itemName: 'PVC Pipe', unitOfMeasure: 'EACH',
    quantity: 100, requestedQuantity: 100, receivedQuantity: status === 2 || status === 'Acknowledged' ? 100 : 0,
    outstandingQuantity: status === 2 || status === 'Acknowledged' ? 0 : 100,
  }],
});

beforeEach(() => {
  vi.clearAllMocks();
  state.actor = 'receiver';
  state.canIssue = false;
  vi.mocked(inventoryManagementService.getWarehouseLocations).mockResolvedValue([]);
  vi.mocked(service.getById).mockResolvedValue({
    id: 'req-1', requisitionNumber: 'REQ-TEST', status: 'Issued', requestDate: '2026-09-06',
    departmentName: 'Operations', warehouseName: 'Stores', items: [],
  } as never);
});

const open = () => render(<IssueRequisitionDialog open requisitionId="req-1" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);

describe('Store Issue Voucher PDF', () => {
  it('opens the protected voucher in-page without issuing or acknowledging it again', async () => {
    vi.mocked(service.getIssueVouchers).mockResolvedValue([voucher('Posted')] as never);
    vi.mocked(apiService.downloadBlob).mockResolvedValue({ arrayBuffer: async () => new Uint8Array([37, 80, 68, 70]).buffer } as Blob);
    const popup = vi.spyOn(window, 'open').mockReturnValue(null);
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'PDF' }));
    expect(await screen.findByTestId('voucher-pdf')).toHaveTextContent('37,80,68,70');
    expect(screen.getByRole('dialog', { name: 'SIV-TEST' })).toBeInTheDocument();
    expect(apiService.downloadBlob).toHaveBeenCalledWith('/inventory/requisitions/issue-vouchers/voucher-1/download');
    expect(service.issue).not.toHaveBeenCalled();
    expect(service.acknowledgeIssueVoucher).not.toHaveBeenCalled();
    expect(popup).not.toHaveBeenCalled();
    popup.mockRestore();
  });
  it('keeps the protected PDF error visible rather than silently closing', async () => {
    vi.mocked(service.getIssueVouchers).mockResolvedValue([voucher('Posted')] as never);
    vi.mocked(apiService.downloadBlob).mockRejectedValue(new Error('Voucher PDF is not available to this actor.'));
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'PDF' }));
    expect(await screen.findByText('Voucher PDF is not available to this actor.')).toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: 'SIV-TEST' })).toBeInTheDocument();
    expect(screen.queryByTestId('voucher-pdf')).not.toBeInTheDocument();
    expect(service.issue).not.toHaveBeenCalled();
  });
});

function prepareIssue(locationId?: string, inventoryTrackingExceptionId?: string) {
  state.actor = 'issuer';
  state.canIssue = true;
  vi.mocked(service.getById).mockResolvedValue({
    id: 'req-1', requisitionNumber: 'REQ-TEST', status: 3, requestDate: '2026-09-10',
    requestedById: 'receiver', approvedById: 'approver', rowVersion: 'AQID',
    warehouseId: 'warehouse-1', departmentName: 'Operations', locationId,
    items: [{ id: 'line-1', inventoryItemId: 'pipe', itemCode: 'SKU-001', itemName: 'PVC Pipe 50mm',
      requestedQuantity: 2, approvedQuantity: 2, issuedQuantity: 0, unitOfMeasure: 'EACH', inventoryTrackingExceptionId }],
  } as never);
  vi.mocked(service.getIssueVouchers).mockResolvedValue([]);
  vi.mocked(service.getIssueReceivers).mockResolvedValue([{ userId: 'receiver', displayName: 'Jane Employee' }] as never);
  vi.mocked(service.getIssueAccountingOptions).mockResolvedValue({
    movementReasons: { DEPARTMENT_CONSUMPTION: 'Department consumption' },
    applicableMovementReasonCodes: ['DEPARTMENT_CONSUMPTION'],
    applicableMovementReasonCodesByRequisitionItem: { 'line-1': ['DEPARTMENT_CONSUMPTION'] },
  } as never);
  vi.mocked(inventoryManagementService.getWarehouseLocations).mockResolvedValue([
    { id: 'location-1', warehouseId: 'warehouse-1', locationCode: 'LOC-001', name: 'Main', isActive: true },
    { id: 'location-2', warehouseId: 'warehouse-1', locationCode: 'LOC-002', name: 'Secondary', isActive: true },
    { id: 'inactive', warehouseId: 'warehouse-1', locationCode: 'CLOSED', isActive: false },
    { id: 'elsewhere', warehouseId: 'warehouse-2', locationCode: 'OTHER', isActive: true },
  ] as never);
}

async function chooseIssueLocation(name: string) {
  fireEvent.keyDown(screen.getByRole('combobox', { name: 'Issue location for SKU-001' }), { key: 'ArrowDown' });
  fireEvent.click(await screen.findByRole('option', { name }));
}

describe('issuer selects the actual stock location', () => {
  it('does not offer returned stock as remaining approved quantity', async () => {
    prepareIssue('location-1');
    const source = await service.getById('req-1');
    source.items[0] = { ...source.items[0], issuedQuantity: 1, grossIssuedQuantity: 2,
      returnedQuantity: 1, remainingToIssueQuantity: 0 };
    // Even a stale partially-issued header must not permit another unit.
    source.status = 5;
    vi.mocked(service.getById).mockResolvedValue(source);
    open();
    await screen.findByText('SKU-001');
    const fill = screen.queryByRole('button', { name: 'Fill remaining quantities' });
    if (fill) fireEvent.click(fill);
    expect(screen.queryByRole('button', { name: 'Issue 1 Items' })).not.toBeInTheDocument();
    expect(service.issue).not.toHaveBeenCalled();
  });

  it('requires an explicit location choice when the requester did not specify one', async () => {
    prepareIssue();
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'Fill remaining quantities' }));
    expect(screen.getByRole('combobox', { name: 'Issue location for SKU-001' })).toHaveTextContent('Select issue location');
    fireEvent.click(screen.getByRole('button', { name: 'Issue 2 Items' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Select an active storage location');
    expect(service.issue).not.toHaveBeenCalled();
  });

  it('offers active locations in the approved warehouse and sends the selected location with exactly two units', async () => {
    prepareIssue();
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'Fill remaining quantities' }));
    fireEvent.keyDown(screen.getByRole('combobox', { name: 'Issue location for SKU-001' }), { key: 'ArrowDown' });
    await screen.findByRole('option', { name: 'LOC-001 - Main' });
    expect(screen.queryByRole('option', { name: 'CLOSED' })).not.toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'OTHER' })).not.toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Unbinned/ })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('option', { name: 'LOC-001 - Main' }));
    fireEvent.click(screen.getByRole('button', { name: 'Issue 2 Items' }));
    await waitFor(() => expect(service.issue).toHaveBeenCalledWith('req-1', expect.objectContaining({
      receiverUserId: 'receiver', rowVersion: 'AQID',
      items: [expect.objectContaining({ itemId: 'line-1', issuedQuantity: 2, locationId: 'location-1' })],
    })));
  });

  it('lets the issuer choose a different location without editing the approved requisition', async () => {
    prepareIssue('location-1');
    open();
    await screen.findByRole('button', { name: 'Fill remaining quantities' });
    expect(screen.getByRole('combobox', { name: 'Issue location for SKU-001' })).toHaveTextContent('LOC-001 - Main');
    await chooseIssueLocation('LOC-002 - Secondary');
    fireEvent.click(screen.getByRole('button', { name: 'Fill remaining quantities' }));
    fireEvent.click(screen.getByRole('button', { name: 'Issue 2 Items' }));
    await waitFor(() => expect(service.issue).toHaveBeenCalledWith('req-1', expect.objectContaining({
      items: [expect.objectContaining({ locationId: 'location-2' })],
    })));
  });

  it('retains the selected named location and quantity after a server error', async () => {
    prepareIssue();
    vi.mocked(service.issue).mockRejectedValueOnce({ response: { data: { detail: 'Insufficient stock in this location.', code: 'INV_STOCK_LOW' } } });
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'Fill remaining quantities' }));
    await chooseIssueLocation('LOC-001 - Main');
    fireEvent.click(screen.getByRole('button', { name: 'Issue 2 Items' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Insufficient stock in this location. (INV_STOCK_LOW)');
    expect(screen.getByRole('spinbutton')).toHaveValue(2);
    expect(service.issue).toHaveBeenCalledWith('req-1', expect.objectContaining({
      items: [expect.objectContaining({ issuedQuantity: 2, locationId: 'location-1' })],
    }));
  });
});

async function chooseDefaultLocation(name: string) {
  fireEvent.keyDown(screen.getByRole('combobox', { name: 'Default issue location' }), { key: 'ArrowDown' });
  fireEvent.click(await screen.findByRole('option', { name }));
}

describe('default issue location and quantity helper', () => {
  it('fills blank pending lines without posting, then fills only their outstanding quantities', async () => {
    prepareIssue();
    const detail = await service.getById('req-1');
    vi.mocked(service.getById).mockResolvedValue({ ...detail, items: [
      detail.items[0],
      { ...detail.items[0], id: 'line-2', itemCode: 'SKU-002', approvedQuantity: 5, issuedQuantity: 1 },
      { ...detail.items[0], id: 'line-3', itemCode: 'SKU-003', approvedQuantity: 2, issuedQuantity: 2 },
    ] });
    open();
    await screen.findByRole('button', { name: 'Fill remaining quantities' });
    await chooseDefaultLocation('LOC-001 - Main');
    expect(screen.getByRole('combobox', { name: 'Issue location for SKU-001' })).toHaveTextContent('LOC-001 - Main');
    expect(screen.getByRole('combobox', { name: 'Issue location for SKU-002' })).toHaveTextContent('LOC-001 - Main');
    expect(screen.queryByRole('combobox', { name: 'Issue location for SKU-003' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('spinbutton').map(input => (input as HTMLInputElement).value)).toEqual(['0', '0']);
    fireEvent.click(screen.getByRole('button', { name: 'Fill remaining quantities' }));
    expect(screen.getAllByRole('spinbutton').map(input => (input as HTMLInputElement).value)).toEqual(['2', '4']);
    expect(service.issue).not.toHaveBeenCalled();
  });

  it('preserves requested locations and per-line overrides', async () => {
    prepareIssue('location-1');
    open();
    await screen.findByRole('button', { name: 'Fill remaining quantities' });
    await chooseDefaultLocation('LOC-002 - Secondary');
    expect(screen.getByRole('combobox', { name: 'Issue location for SKU-001' })).toHaveTextContent('LOC-001 - Main');
    await chooseIssueLocation('LOC-002 - Secondary');
    await chooseDefaultLocation('LOC-001 - Main');
    expect(screen.getByRole('combobox', { name: 'Issue location for SKU-001' })).toHaveTextContent('LOC-002 - Secondary');
    expect(service.issue).not.toHaveBeenCalled();
  });

  it('blocks an unavailable requested location until an active default replaces it', async () => {
    prepareIssue('closed-location');
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'Fill remaining quantities' }));
    fireEvent.click(screen.getByRole('button', { name: 'Issue 2 Items' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Select an active storage location');
    expect(service.issue).not.toHaveBeenCalled();
    await chooseDefaultLocation('LOC-001 - Main');
    expect(screen.getByRole('combobox', { name: 'Issue location for SKU-001' })).toHaveTextContent('LOC-001 - Main');
    fireEvent.click(screen.getByRole('button', { name: 'Issue 2 Items' }));
    await waitFor(() => expect(service.issue).toHaveBeenCalledWith('req-1', expect.objectContaining({
      items: [expect.objectContaining({ locationId: 'location-1' })],
    })));
  });

  it('keeps the named line locations when the default is cleared', async () => {
    prepareIssue();
    open();
    await screen.findByRole('button', { name: 'Fill remaining quantities' });
    await chooseDefaultLocation('LOC-001 - Main');
    await chooseDefaultLocation('No default location');
    expect(screen.getByRole('combobox', { name: 'Issue location for SKU-001' })).toHaveTextContent('LOC-001 - Main');
    fireEvent.click(screen.getByRole('button', { name: 'Fill remaining quantities' }));
    fireEvent.click(screen.getByRole('button', { name: 'Issue 2 Items' }));
    await waitFor(() => expect(service.issue).toHaveBeenCalledWith('req-1', expect.objectContaining({
      items: [expect.objectContaining({ issuedQuantity: 2, locationId: 'location-1' })],
    })));
  });

  it('resets the default when reopening the issue form', async () => {
    prepareIssue();
    const view = open();
    await screen.findByRole('button', { name: 'Fill remaining quantities' });
    await chooseDefaultLocation('LOC-001 - Main');
    view.rerender(<IssueRequisitionDialog open={false} requisitionId="req-1" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    view.rerender(<IssueRequisitionDialog open requisitionId="req-1" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    expect(await screen.findByRole('combobox', { name: 'Default issue location' })).toHaveTextContent('No default location');
    expect(screen.getByRole('combobox', { name: 'Issue location for SKU-001' })).toHaveTextContent('Select issue location');
  });
});

describe('advanced tracking options', () => {
  it('hides the exception selector by default while keeping normal tracking fields available', async () => {
    prepareIssue('location-1');
    open();
    const toggle = await screen.findByRole('button', { name: 'Advanced tracking options' });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('combobox', { name: 'Approved tracking exception' })).not.toBeInTheDocument();
    expect(screen.getByPlaceholderText('Lot')).toBeVisible();
    expect(screen.getByPlaceholderText('Batch')).toBeVisible();
    expect(screen.getByPlaceholderText('Serial')).toBeVisible();
    fireEvent.click(toggle);
    expect(await screen.findByRole('combobox', { name: 'Approved tracking exception' })).toBeVisible();
  });

  it('retains a selected exception when collapsed and sends it with the issue', async () => {
    prepareIssue('location-1');
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'Advanced tracking options' }));
    fireEvent.change(await screen.findByRole('combobox', { name: 'Approved tracking exception' }), { target: { value: 'exception-1' } });
    fireEvent.click(screen.getByRole('button', { name: /Advanced tracking options/ }));
    expect(screen.getByText('Exception selected')).toBeVisible();
    expect(screen.queryByRole('combobox', { name: 'Approved tracking exception' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Fill remaining quantities' }));
    fireEvent.click(screen.getByRole('button', { name: 'Issue 2 Items' }));
    await waitFor(() => expect(service.issue).toHaveBeenCalledWith('req-1', expect.objectContaining({
      items: [expect.objectContaining({ inventoryTrackingExceptionId: 'exception-1' })],
    })));
  });

  it('opens existing selected evidence and clears it when the issue location changes', async () => {
    prepareIssue('location-1', 'exception-1');
    open();
    expect(await screen.findByRole('button', { name: /Advanced tracking options/ })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('combobox', { name: 'Approved tracking exception' })).toHaveValue('exception-1');
    await chooseIssueLocation('LOC-002 - Secondary');
    expect(screen.queryByText('Exception selected')).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Approved tracking exception' })).toHaveValue('');
  });
});

describe('requester is the suggested receiver', () => {
  it('shows the requester without requiring the receiver dropdown', async () => {
    prepareIssue('location-1');
    open();
    expect(await screen.findByText('Jane Employee (requester)')).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Receiver' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Fill remaining quantities' }));
    expect(screen.getByRole('button', { name: 'Issue 2 Items' })).toBeEnabled();
    fireEvent.click(screen.getByRole('button', { name: 'Issue 2 Items' }));
    await waitFor(() => expect(service.issue).toHaveBeenCalledWith('req-1', expect.objectContaining({ receiverUserId: 'receiver' })));
  });

  it('allows an explicit alternate receiver and sends that person on the issue', async () => {
    prepareIssue('location-1');
    vi.mocked(service.getIssueReceivers).mockResolvedValue([
      { userId: 'receiver', displayName: 'Jane Employee' },
      { userId: 'collector', displayName: 'Alternate Collector' },
    ] as never);
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'Change receiver' }));
    fireEvent.keyDown(screen.getByRole('combobox', { name: 'Receiver' }), { key: 'ArrowDown' });
    fireEvent.click(await screen.findByRole('option', { name: 'Alternate Collector' }));
    expect(screen.getByText('Alternate Collector')).toBeInTheDocument();
    expect(screen.queryByText('Jane Employee (requester)')).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Receiver' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Fill remaining quantities' }));
    fireEvent.click(screen.getByRole('button', { name: 'Issue 2 Items' }));
    await waitFor(() => expect(service.issue).toHaveBeenCalledWith('req-1', expect.objectContaining({ receiverUserId: 'collector' })));
  });

  it('keeps the requester when cancelling a receiver change', async () => {
    prepareIssue();
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'Change receiver' }));
    fireEvent.click(screen.getByRole('button', { name: 'Cancel change' }));
    expect(screen.getByText('Jane Employee (requester)')).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Receiver' })).not.toBeInTheDocument();
  });

  it('requires another eligible receiver when the requester is unavailable', async () => {
    prepareIssue('location-1');
    vi.mocked(service.getIssueReceivers).mockResolvedValue([{ userId: 'collector', displayName: 'Alternate Collector' }] as never);
    open();
    await screen.findByRole('combobox', { name: 'Receiver' });
    fireEvent.click(screen.getByRole('button', { name: 'Fill remaining quantities' }));
    expect(screen.getByRole('button', { name: 'Issue 2 Items' })).toBeDisabled();
    expect(screen.getByText(/requester is not available as an eligible receiver/)).toBeInTheDocument();
    expect(service.issue).not.toHaveBeenCalled();
  });
});

describe('issued voucher handover', () => {
  it.each(['', '-1', '101', '1.12345', '0'])('rejects missing or invalid actual receipts (%s) without assuming issued quantity', async quantity => {
    vi.mocked(service.getIssueVouchers).mockResolvedValue([voucher(1)] as never);
    open();
    const input = await screen.findByRole('spinbutton', { name: 'Actual quantity received SIV-TEST SKU-001' });
    expect(input).toHaveValue(null);
    fireEvent.change(input, { target: { value: quantity } });
    fireEvent.change(screen.getByPlaceholderText('Receiver handover comment'), { target: { value: 'Delivery checked' } });
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge receipt' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(quantity === '0' ? 'positive received quantity' : 'enter an actual received quantity');
    expect(service.acknowledgeIssueVoucher).not.toHaveBeenCalled();
  });

  it('records 92 then the remaining 8 using refreshed balances and version', async () => {
    const partial = { ...voucher(1), rowVersion: 'BAUG', lines: [{ ...voucher(1).lines[0], receivedQuantity: 92, outstandingQuantity: 8 }] };
    vi.mocked(service.getIssueVouchers).mockResolvedValueOnce([voucher(1)] as never).mockResolvedValueOnce([partial] as never).mockResolvedValueOnce([voucher(2)] as never);
    vi.mocked(service.acknowledgeIssueVoucher).mockResolvedValueOnce(partial as never).mockResolvedValueOnce(voucher(2) as never);
    open();
    fireEvent.change(await screen.findByRole('spinbutton', { name: 'Actual quantity received SIV-TEST SKU-001' }), { target: { value: '92' } });
    fireEvent.change(screen.getByPlaceholderText('Receiver handover comment'), { target: { value: 'Eight still missing' } });
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge receipt' }));
    const remaining = await screen.findByRole('spinbutton', { name: 'Actual quantity received SIV-TEST SKU-001' });
    await waitFor(() => expect(remaining).toHaveAttribute('max', '8'));
    expect(remaining).toHaveValue(null);
    expect(screen.getByText('Partially received')).toBeInTheDocument();
    expect(service.acknowledgeIssueVoucher).toHaveBeenNthCalledWith(1, 'voucher-1', 'AQID', 'Eight still missing', [{ issueVoucherLineId: 'voucher-line-1', receivedQuantity: 92 }], expect.any(String));
    fireEvent.change(remaining, { target: { value: '8' } });
    fireEvent.change(screen.getByPlaceholderText('Receiver handover comment'), { target: { value: 'Remaining eight arrived' } });
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge receipt' }));
    await screen.findByText('Acknowledged');
    expect(service.acknowledgeIssueVoucher).toHaveBeenNthCalledWith(2, 'voucher-1', 'BAUG', 'Remaining eight arrived', [{ issueVoucherLineId: 'voucher-line-1', receivedQuantity: 8 }], expect.any(String));
    expect(screen.queryByRole('button', { name: 'Acknowledge receipt' })).not.toBeInTheDocument();
  });

  it('requires an entry for each outstanding line and permits explicit zero alongside a receipt', async () => {
    const pending = { ...voucher(1), lines: [voucher(1).lines[0], { ...voucher(1).lines[0], id: 'voucher-line-2', itemCode: 'SKU-002' }] };
    vi.mocked(service.getIssueVouchers).mockResolvedValue([pending] as never);
    vi.mocked(service.acknowledgeIssueVoucher).mockResolvedValue(pending as never);
    open();
    fireEvent.change(await screen.findByRole('spinbutton', { name: 'Actual quantity received SIV-TEST SKU-001' }), { target: { value: '92' } });
    fireEvent.change(screen.getByPlaceholderText('Receiver handover comment'), { target: { value: 'Second item missing' } });
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge receipt' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('SKU-002: enter');
    expect(service.acknowledgeIssueVoucher).not.toHaveBeenCalled();
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Actual quantity received SIV-TEST SKU-002' }), { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge receipt' }));
    await waitFor(() => expect(service.acknowledgeIssueVoucher).toHaveBeenCalledWith('voucher-1', 'AQID', 'Second item missing', [
      { issueVoucherLineId: 'voucher-line-1', receivedQuantity: 92 }, { issueVoucherLineId: 'voucher-line-2', receivedQuantity: 0 },
    ], expect.any(String)));
  });

  it('retains actual quantities and the same retry key after an API failure', async () => {
    vi.mocked(service.getIssueVouchers).mockResolvedValue([voucher(1)] as never);
    vi.mocked(service.acknowledgeIssueVoucher).mockRejectedValue({ response: { data: { detail: 'Receipt changed. Refresh before retrying.', code: 'INV_RECEIPT_CONFLICT' } } });
    open();
    const input = await screen.findByRole('spinbutton', { name: 'Actual quantity received SIV-TEST SKU-001' });
    fireEvent.change(input, { target: { value: '92' } });
    fireEvent.change(screen.getByPlaceholderText('Receiver handover comment'), { target: { value: 'Eight missing' } });
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge receipt' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Receipt changed. Refresh before retrying. (INV_RECEIPT_CONFLICT)');
    expect(input).toHaveValue(92);
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge receipt' }));
    await waitFor(() => expect(service.acknowledgeIssueVoucher).toHaveBeenCalledTimes(2));
    expect(vi.mocked(service.acknowledgeIssueVoucher).mock.calls[1]).toEqual(vi.mocked(service.acknowledgeIssueVoucher).mock.calls[0]);
  });

  it('rejects fractional quantities for serial-numbered stock', async () => {
    vi.mocked(service.getIssueVouchers).mockResolvedValue([{ ...voucher(1), lines: [{ ...voucher(1).lines[0], serialNumber: 'SN-001' }] }] as never);
    open();
    fireEvent.change(await screen.findByRole('spinbutton', { name: 'Actual quantity received SIV-TEST SKU-001' }), { target: { value: '0.5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge receipt' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('in whole units');
    expect(service.acknowledgeIssueVoucher).not.toHaveBeenCalled();
  });

  it('identifies historical acknowledgements without inventing actual received quantities', async () => {
    vi.mocked(service.getIssueVouchers).mockResolvedValue([{ ...voucher(2), isLegacyAcknowledgement: true }] as never);
    open();
    expect(await screen.findByText('Historical acknowledgement: actual quantities were not captured per line.')).toBeInTheDocument();
    expect(screen.getByText('Not recorded')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Acknowledge receipt' })).not.toBeInTheDocument();
  });

  it.each([5, 'PartiallyIssued'])('allows receiver handover on a partially issued requisition (%s) without fetching issue setup', async status => {
    vi.mocked(service.getById).mockResolvedValue({
      id: 'req-1', requisitionNumber: 'REQ-TEST', status, requestedById: 'receiver',
      approvedById: 'approver', requestDate: '2026-09-06', items: [],
    } as never);
    vi.mocked(service.getIssueVouchers).mockResolvedValue([voucher('Issued')] as never);
    open();
    expect(await screen.findByRole('button', { name: 'Acknowledge receipt' })).toBeEnabled();
    expect(screen.queryByText('Items to Issue')).not.toBeInTheDocument();
    expect(service.getIssueReceivers).not.toHaveBeenCalled();
    expect(service.getIssueAccountingOptions).not.toHaveBeenCalled();
    expect(inventoryManagementService.getWarehouseLocations).not.toHaveBeenCalled();
  });

  it('still loads issue setup for an independent permitted issuer', async () => {
    state.actor = 'issuer';
    state.canIssue = true;
    vi.mocked(service.getById).mockResolvedValue({
      id: 'req-1', requisitionNumber: 'REQ-TEST', status: 5, requestedById: 'receiver',
      approvedById: 'approver', requestDate: '2026-09-06', items: [],
    } as never);
    vi.mocked(service.getIssueVouchers).mockResolvedValue([]);
    vi.mocked(service.getIssueReceivers).mockResolvedValue([]);
    vi.mocked(service.getIssueAccountingOptions).mockResolvedValue({
      applicableMovementReasonCodes: [], applicableMovementReasonCodesByRequisitionItem: {},
    } as never);
    open();
    await screen.findByText('Items to Issue');
    expect(service.getIssueReceivers).toHaveBeenCalledOnce();
    expect(service.getIssueAccountingOptions).toHaveBeenCalledWith('req-1');
  });

  it.each([1, 'Issued'])('allows only the assigned receiver to acknowledge status %s without issue setup access', async status => {
    vi.mocked(service.getIssueVouchers).mockResolvedValue([voucher(status)] as never);
    open();
    expect(await screen.findByRole('button', { name: 'Acknowledge receipt' })).toBeEnabled();
    expect(screen.getByText('Awaiting receiver')).toBeInTheDocument();
    expect(screen.queryByText('Issue Notes')).not.toBeInTheDocument();
    expect(service.getIssueReceivers).not.toHaveBeenCalled();
    expect(service.getIssueAccountingOptions).not.toHaveBeenCalled();
  });

  it('does not offer the issuer a self-acknowledgement action', async () => {
    state.actor = 'issuer';
    vi.mocked(service.getIssueVouchers).mockResolvedValue([voucher('Issued')] as never);
    open();
    await screen.findByText('SIV-TEST');
    expect(screen.queryByRole('button', { name: 'Acknowledge receipt' })).not.toBeInTheDocument();
    expect(screen.getByText('Awaiting acknowledgement by Jane Employee.')).toBeInTheDocument();
  });

  it.each([2, 'Acknowledged'])('renders acknowledged status %s without a duplicate action', async status => {
    vi.mocked(service.getIssueVouchers).mockResolvedValue([{ ...voucher(status), receiverComment: 'Received in local UAT' }] as never);
    open();
    expect(await screen.findByText('Acknowledged')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Acknowledge receipt' })).not.toBeInTheDocument();
  });

  it('sends the exact voucher, row version and receiver comment then reloads evidence', async () => {
    vi.mocked(service.getIssueVouchers).mockResolvedValueOnce([voucher('Issued')] as never)
      .mockResolvedValueOnce([{ ...voucher('Acknowledged'), receiverComment: 'LOCAL UAT received' }] as never);
    vi.mocked(service.acknowledgeIssueVoucher).mockResolvedValue(voucher('Acknowledged') as never);
    open();
    const button = await screen.findByRole('button', { name: 'Acknowledge receipt' });
    fireEvent.change(screen.getByPlaceholderText('Receiver handover comment'), { target: { value: 'LOCAL UAT received' } });
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Actual quantity received SIV-TEST SKU-001' }), { target: { value: '100' } });
    fireEvent.click(button);
    await waitFor(() => expect(service.acknowledgeIssueVoucher).toHaveBeenCalledWith('voucher-1', 'AQID', 'LOCAL UAT received', [{ issueVoucherLineId: 'voucher-line-1', receivedQuantity: 100 }], expect.any(String)));
    await screen.findByText('Acknowledged');
  });
});
