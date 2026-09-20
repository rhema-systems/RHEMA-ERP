import React from 'react';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { ReturnRequisitionDialog } from './ReturnRequisitionDialog';
import { inventoryRequisitionService as service } from '@/services/inventoryRequisitionService';
import { documentManagementService } from '@/services/document-management.service';
import { apiService } from '@/services/api.service';

vi.mock('next/dynamic', () => ({ default: () => (props: { fileData?: Uint8Array }) => <div data-testid="voucher-pdf">{Array.from(props.fileData || []).join(',')}</div> }));
vi.mock('@/services/api.service', () => ({ apiService: { downloadBlob: vi.fn() } }));

const state = vi.hoisted(() => ({ actor: 'manager', canApprove: true, canRequest: true, toast: vi.fn() }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({
  user: { id: state.actor }, hasPermission: (permission: string) => permission === 'procurement.inventory.issue' ? state.canRequest : state.canApprove && permission === 'procurement.inventory.adjust.approve',
}) }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: state.toast }) }));
vi.mock('@/components/inventory/InventoryTrackingExceptionSelect', () => ({
  InventoryTrackingExceptionSelect: () => <span>No approved exception</span>,
  useAvailableInventoryTrackingExceptions: () => ({ exceptions: [], loading: false, refresh: vi.fn() }),
}));
vi.mock('@/services/document-management.service', () => ({ documentManagementService: { getRecords: vi.fn() } }));
vi.mock('@/services/inventoryRequisitionService', () => ({
  RequisitionStatusMap: { 6: 'Issued' },
  inventoryRequisitionService: {
    getById: vi.fn(), getReturnReasons: vi.fn(), getReturnVouchers: vi.fn(), returnItems: vi.fn(),
    decideReturnVoucher: vi.fn(), postReturnVoucher: vi.fn(), reverseReturnVoucher: vi.fn(),
  },
}));

const voucher = {
  id: 'return-1', voucherNumber: 'SRV-TEST', requestedById: 'manager', requestedByName: 'John Manager',
  status: 'PendingApproval', reasonCode: 'UNUSED', reason: 'Unused stock', totalValue: 1900,
  lines: [{ id: 'return-line-1', itemCode: 'SKU-001', itemName: 'PVC Pipe', quantity: 1 }], rowVersion: 'AQID',
};
const onOpenChange = vi.fn();
beforeAll(() => { Element.prototype.scrollIntoView = vi.fn(); });
const open = () => render(<ReturnRequisitionDialog open requisitionId="req-1" onOpenChange={onOpenChange} onSuccess={vi.fn()} />);

beforeEach(() => {
  vi.resetAllMocks();
  state.actor = 'manager';
  state.canApprove = true;
  state.canRequest = true;
  vi.mocked(service.getById).mockResolvedValue({
    id: 'req-1', requisitionNumber: 'REQ-TEST', status: 'Issued', requestDate: '2026-09-10',
    requestedById: 'employee', warehouseId: 'warehouse-1', rowVersion: 'AQID',
    items: [{ id: 'line-1', inventoryItemId: 'pipe', itemCode: 'SKU-001', itemName: 'PVC Pipe', issuedQuantity: 2, unitOfMeasure: 'EACH' }],
  } as never);
  vi.mocked(service.getReturnReasons).mockResolvedValue({ UNUSED: 'Unused stock', DEFECTIVE: 'Defective item returned' });
  vi.mocked(service.getReturnVouchers).mockResolvedValue([voucher] as never);
  vi.mocked(documentManagementService.getRecords).mockResolvedValue([]);
  vi.mocked(service.returnItems).mockResolvedValue(voucher as never);
  vi.mocked(service.decideReturnVoucher).mockResolvedValue({ ...voucher, status: 'Approved' } as never);
});

describe('return details and independent approval', () => {
  it('distinguishes unspecified requested location and collapses unused tracking options', async () => {
    open();
    await screen.findByText('SRV-TEST');
    expect(screen.getByText('Requested location:')).toBeInTheDocument();
    expect(screen.getByText('Not specified')).toBeInTheDocument();
    expect(screen.queryByText('Warehouse level')).not.toBeInTheDocument();
    expect(screen.queryByText('No approved exception')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Advanced tracking options' }));
    expect(await screen.findByText('No approved exception')).toBeVisible();
  });
  it('previews the existing return voucher through its protected route without posting', async () => {
    vi.mocked(apiService.downloadBlob).mockResolvedValue({ arrayBuffer: async () => new Uint8Array([37, 80, 68, 70]).buffer } as Blob);
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'Open PDF SRV-TEST' }));
    expect(await screen.findByTestId('voucher-pdf')).toHaveTextContent('37,80,68,70');
    expect(screen.getByRole('dialog', { name: 'SRV-TEST' })).toBeInTheDocument();
    expect(apiService.downloadBlob).toHaveBeenCalledWith('/inventory/requisitions/return-vouchers/return-1/download');
    expect(service.postReturnVoucher).not.toHaveBeenCalled();
    expect(service.returnItems).not.toHaveBeenCalled();
  });
  it('shows protected return PDF failure in the open preview', async () => {
    vi.mocked(apiService.downloadBlob).mockRejectedValue(new Error('Return voucher access denied.'));
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'Open PDF SRV-TEST' }));
    expect(await screen.findByText('Return voucher access denied.')).toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: 'SRV-TEST' })).toBeInTheDocument();
    expect(screen.queryByTestId('voucher-pdf')).not.toBeInTheDocument();
    expect(service.postReturnVoucher).not.toHaveBeenCalled();
  });
  it('allows the authorized posting operator to post a no-approval return without asking for an approver', async () => {
    const ready = { ...voucher, approvalRequired: false, status: 'ReadyToPost' };
    vi.mocked(service.getReturnVouchers).mockResolvedValue([ready] as never);
    vi.mocked(service.postReturnVoucher).mockResolvedValue({ ...ready, status: 'Posted' } as never);
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'Post' }));
    await waitFor(() => expect(service.postReturnVoucher).toHaveBeenCalledWith(ready));
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reject' })).not.toBeInTheDocument();
  });
  it('does not grant posting permission merely because approval is not required', async () => {
    state.canApprove = false;
    vi.mocked(service.getReturnVouchers).mockResolvedValue([{ ...voucher, approvalRequired: false, status: 'ReadyToPost' }] as never);
    open(); await screen.findByText('SRV-TEST');
    expect(screen.queryByRole('button', { name: 'Post' })).not.toBeInTheDocument();
  });
  it('submits a selected reason without typed details or notes', async () => {
    open();
    await screen.findByText('SRV-TEST');
    expect(screen.getByLabelText('Details (optional)')).toHaveValue('');
    fireEvent.change(screen.getByRole('spinbutton'), { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Submit return' }));
    await waitFor(() => expect(service.returnItems).toHaveBeenCalledWith('req-1', expect.objectContaining({
      reasonCode: 'UNUSED', reason: '', rowVersion: 'AQID', items: [expect.objectContaining({ returnedQuantity: 1 })],
    })));
  });

  it.each(['PendingApproval', 'Approved', 'Posted'])('hides independent actions from the return creator in %s', async (status) => {
    vi.mocked(service.getReturnVouchers).mockResolvedValue([{ ...voucher, status }] as never);
    open();
    await screen.findByText('SRV-TEST');
    for (const name of ['Approve', 'Reject', 'Post', 'Reverse'])
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument();
    if (status === 'PendingApproval') expect(screen.getByText('Awaiting independent approval')).toBeInTheDocument();
  });

  it.each(['no-permission', 'unknown-actor', 'unknown-requester', 'same-id-different-case'])('fails closed for %s', async (caseName) => {
    state.actor = caseName === 'unknown-actor' ? '' : caseName === 'same-id-different-case' ? 'MANAGER' : 'approver';
    state.canApprove = caseName !== 'no-permission';
    if (caseName === 'unknown-requester') vi.mocked(service.getReturnVouchers).mockResolvedValue([{ ...voucher, requestedById: '' }] as never);
    open();
    await screen.findByText('SRV-TEST');
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
  });

  it('allows an independent approver to confirm without comments and does not copy draft return details', async () => {
    state.actor = 'approver';
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'Approve' }));
    const dialog = await screen.findByRole('dialog', { name: 'Approve return' });
    expect(within(dialog).getByLabelText('Comments (optional)')).toHaveValue('');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Approve return' }));
    await waitFor(() => expect(service.decideReturnVoucher).toHaveBeenCalledWith(voucher, true, ''));
    expect(await screen.findByRole('button', { name: 'Post' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Submit return' })).not.toBeInTheDocument();
  });

  it('still requires a separate rejection reason', async () => {
    state.actor = 'approver';
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'Reject' }));
    const dialog = await screen.findByRole('dialog', { name: 'Reject return' });
    expect(within(dialog).getByRole('button', { name: 'Reject return' })).toBeDisabled();
    fireEvent.change(within(dialog).getByLabelText('Reason (required)'), { target: { value: 'Incorrect quantity' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Reject return' }));
    await waitFor(() => expect(service.decideReturnVoucher).toHaveBeenCalledWith(voucher, false, 'Incorrect quantity'));
  });

  it('keeps the approval dialog and typed comment on a server rejection', async () => {
    state.actor = 'approver';
    vi.mocked(service.decideReturnVoucher).mockRejectedValue({ response: { data: {
      detail: 'The current actor is not eligible for the active return workflow step.', code: 'INV_RETURN_FORBIDDEN',
    } } });
    open();
    fireEvent.click(await screen.findByRole('button', { name: 'Approve' }));
    const dialog = await screen.findByRole('dialog', { name: 'Approve return' });
    fireEvent.change(within(dialog).getByLabelText('Comments (optional)'), { target: { value: 'Checked quantities' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Approve return' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('not eligible');
    expect(within(dialog).getByLabelText('Comments (optional)')).toHaveValue('Checked quantities');
    expect(onOpenChange).not.toHaveBeenCalled();
  });

  it('still requires evidence for a defective return', async () => {
    open();
    await screen.findByText('SRV-TEST');
    fireEvent.keyDown(screen.getByRole('combobox', { name: 'Return reason' }), { key: 'ArrowDown' });
    fireEvent.click(await screen.findByRole('option', { name: 'Defective item returned' }));
    fireEvent.change(screen.getByRole('spinbutton'), { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Submit return' }));
    expect(service.returnItems).not.toHaveBeenCalled();
    expect(state.toast).toHaveBeenCalledWith(expect.objectContaining({ title: 'Evidence required' }));
  });

  it.each([false, true])('shows a read-only review instead of a new return form to an approver (issue permission: %s)', async (canRequest) => {
    state.actor = 'approver';
    state.canRequest = canRequest;
    open();
    await screen.findByText('SRV-TEST');
    expect(screen.getByText('Requested by: John Manager')).toBeInTheDocument();
    expect(screen.getByText('SKU-001 · PVC Pipe — return quantity: 1')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Submit return' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Return All Issued' })).not.toBeInTheDocument();
    expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Details (optional)')).not.toBeInTheDocument();
    expect(service.returnItems).not.toHaveBeenCalled();
  });
});
