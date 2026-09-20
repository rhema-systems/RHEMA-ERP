import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ShipTransferDialog } from './ShipTransferDialog';
import { ReceiveTransferDialog } from './ReceiveTransferDialog';
import { MovementDetailDialog } from './MovementDetailDialog';
import InventoryTransfersPage from '@/app/inventory/transfers/page';
import type { StockMovementDto } from '@/services/inventoryManagementService';

const mocks = vi.hoisted(() => ({ download: vi.fn(), ship: vi.fn(), receive: vi.fn(), save: vi.fn(), toast: vi.fn(), close: vi.fn(), success: vi.fn() }));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));
vi.mock('next/dynamic', () => ({ default: () => (props: { fileData?: Uint8Array; fileName?: string }) =>
  <div data-testid="transfer-pdf-bytes">{props.fileName}:{Array.from(props.fileData || []).join(',')}</div> }));
vi.mock('@/services/api.service', () => ({ apiService: { downloadBlob: mocks.download } }));
vi.mock('@/services/document-management.service', () => ({ documentManagementService: { downloadVersionFile: vi.fn(), downloadRecordContent: vi.fn() } }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => true }) }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/hooks/useWorkflowEntitySummaries', () => ({ useWorkflowEntitySummaries: () => ({ summariesById: {} }), formatPendingApprovers: () => ({ short: '', full: '' }) }));
vi.mock('@/components/workflow/WorkflowApprovalActions', () => ({ WorkflowApprovalActions: () => null }));
vi.mock('@/components/inventory/TransferDialog', () => ({ TransferDialog: () => null }));
vi.mock('@/services/financeCommonService', () => ({ procurementCurrencyService: { getActive: async () => [] } }));
vi.mock('@/services/inventoryManagementService', () => {
  const detail = { id: 'transfer', transferNumber: 'TRF-1', rowVersion: 'AQID', status: 'Completed',
    requestedDate: '2026-09-12', totalItems: 1, approvalRequired: false,
    sourceWarehouseName: 'Project Demo', destinationWarehouseName: 'Stores warehouse',
    items: [{ id: 'line', inventoryItemId: 'pvc', itemCode: 'SKU-001', itemName: 'PVC Pipe',
      requestedQuantity: 5, shippedQuantity: 2, receivedQuantity: 0, unitOfMeasure: 'EACH',
      unitCost: 1918.85, totalCost: 9594.25, sourceLocationName: 'LOC-001', destinationLocationName: 'DEFAULT' }] };
  return { inventoryManagementService: { getInventoryTransferById: async () => detail,
    getInventoryTransferByNumber: async () => detail, getInventoryTransfers: async () => [detail],
    getWarehouses: async () => [], shipTransfer: mocks.ship, receiveTransfer: mocks.receive,
    shipTransferWithCosts: mocks.ship, saveTransferShippingCosts: mocks.save } };
});

const movement = { id: 'movement', movementNumber: 'MOV-1', movementType: 'Transfer-Out',
  movementDate: '2026-09-12', referenceType: 'Transfer', referenceNumber: 'TRF-1',
  quantity: -2, unitCost: 1918.85, totalCost: 3837.70 } as StockMovementDto;

beforeEach(() => {
  vi.clearAllMocks();
  mocks.download.mockReset().mockResolvedValue({ arrayBuffer: async () => new Uint8Array([37, 80, 68, 70]).buffer });
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

function expectNoMutation() {
  expect(mocks.ship).not.toHaveBeenCalled();
  expect(mocks.receive).not.toHaveBeenCalled();
  expect(mocks.save).not.toHaveBeenCalled();
  expect(mocks.success).not.toHaveBeenCalled();
  expect(mocks.close).not.toHaveBeenCalled();
}

async function closePreview(title: string) {
  fireEvent.click(within(screen.getByRole('dialog', { name: title })).getByRole('button', { name: 'Close' }));
  await waitFor(() => expect(screen.queryByTestId('transfer-pdf-bytes')).not.toBeInTheDocument());
}

describe('protected transfer document previews', () => {
  it('previews shipment without popup, saving or dispatching and retains unsaved ship values after closing', async () => {
    const popup = vi.spyOn(window, 'open').mockReturnValue(null);
    render(<ShipTransferDialog open transferId="transfer" onOpenChange={mocks.close} onSuccess={mocks.success} />);
    const quantity = await screen.findByRole('spinbutton', { name: 'Qty to Ship SKU-001' });
    fireEvent.change(quantity, { target: { value: '1' } });
    fireEvent.change(screen.getByLabelText('Notes (Optional)'), { target: { value: 'Unsaved dispatch note' } });
    fireEvent.click(screen.getByRole('button', { name: 'Preview Shipment Note' }));
    expect(await screen.findByTestId('transfer-pdf-bytes')).toHaveTextContent('ShipmentNote-transfer.pdf:37,80,68,70');
    expect(mocks.download).toHaveBeenCalledWith('/inventory/transfers/transfer/shipment-note');
    expect(popup).not.toHaveBeenCalled();
    await closePreview('Shipment Note TRF-1');
    expect(quantity).toHaveValue(1);
    expect(screen.getByLabelText('Notes (Optional)')).toHaveValue('Unsaved dispatch note');
    expectNoMutation();
  });

  it.each([
    ['Shipment Note', 'shipment-note', 'Shipment Note TRF-1'],
    ['Preview GRN', 'grn', 'Goods Received Note TRF-1'],
  ])('previews %s while keeping the partial receipt and notes unsaved', async (button, suffix, title) => {
    const popup = vi.spyOn(window, 'open').mockReturnValue(null);
    render(<ReceiveTransferDialog open transferId="transfer" onOpenChange={mocks.close} onSuccess={mocks.success} />);
    const quantity = await screen.findByRole('spinbutton', { name: 'Qty to receive SKU-001' });
    fireEvent.change(quantity, { target: { value: '1' } });
    fireEvent.change(screen.getByLabelText('Receiving Notes (Optional)'), { target: { value: 'Unsaved receipt note' } });
    fireEvent.click(screen.getByRole('button', { name: button }));
    await screen.findByTestId('transfer-pdf-bytes');
    expect(mocks.download).toHaveBeenCalledWith(`/inventory/transfers/transfer/${suffix}`);
    await closePreview(title);
    expect(quantity).toHaveValue(1);
    expect(screen.getByLabelText('Receiving Notes (Optional)')).toHaveValue('Unsaved receipt note');
    expect(popup).not.toHaveBeenCalled();
    expectNoMutation();
  });

  it.each(['ship', 'receive'] as const)('retains the %s form after a protected PDF error and permits a fresh preview retry', async (kind) => {
    mocks.download.mockRejectedValueOnce(new Error('Transfer document access denied. (INV_TRANSFER_SCOPE)'));
    render(kind === 'ship'
      ? <ShipTransferDialog open transferId="transfer" onOpenChange={mocks.close} onSuccess={mocks.success} />
      : <ReceiveTransferDialog open transferId="transfer" onOpenChange={mocks.close} onSuccess={mocks.success} />);
    const quantity = await screen.findByRole('spinbutton', { name: kind === 'ship' ? 'Qty to Ship SKU-001' : 'Qty to receive SKU-001' });
    fireEvent.change(quantity, { target: { value: '1' } });
    const previewName = kind === 'ship' ? 'Preview Shipment Note' : 'Shipment Note';
    fireEvent.click(screen.getByRole('button', { name: previewName }));
    expect(await screen.findByText('Transfer document access denied. (INV_TRANSFER_SCOPE)')).toBeInTheDocument();
    expect(screen.queryByTestId('transfer-pdf-bytes')).not.toBeInTheDocument();
    await closePreview('Shipment Note TRF-1');
    expect(quantity).toHaveValue(1);
    fireEvent.click(screen.getByRole('button', { name: previewName }));
    await screen.findByTestId('transfer-pdf-bytes');
    expect(mocks.download).toHaveBeenCalledTimes(2);
    expectNoMutation();
  });

  it('clears a shipment preview when its parent closes so it is not restored on reopening', async () => {
    const props = { transferId: 'transfer', onOpenChange: mocks.close, onSuccess: mocks.success };
    const view = render(<ShipTransferDialog {...props} open />);
    fireEvent.click(await screen.findByRole('button', { name: 'Preview Shipment Note' }));
    await screen.findByTestId('transfer-pdf-bytes');
    view.rerender(<ShipTransferDialog {...props} open={false} />);
    view.rerender(<ShipTransferDialog {...props} open />);
    await screen.findByRole('spinbutton', { name: 'Qty to Ship SKU-001' });
    expect(screen.queryByTestId('transfer-pdf-bytes')).not.toBeInTheDocument();
    expect(mocks.download).toHaveBeenCalledTimes(1);
    expectNoMutation();
  });

  it.each([['Shipment Note', 'shipment-note'], ['GRN', 'grn']])('opens movement %s from the exact protected transfer endpoint', async (button, suffix) => {
    render(<MovementDetailDialog open movement={movement} currencyCode="GHS" onOpenChange={mocks.close} />);
    fireEvent.click(await screen.findByRole('button', { name: button }));
    await screen.findByTestId('transfer-pdf-bytes');
    expect(mocks.download).toHaveBeenCalledWith(`/inventory/transfers/transfer/${suffix}`);
    expectNoMutation();
  });

  it.each([['Print Shipment Note', 'shipment-note'], ['Print GRN', 'grn']])('opens register %s in-page without a transfer mutation', async (button, suffix) => {
    render(<InventoryTransfersPage />);
    fireEvent.click(await screen.findByTitle(button));
    await screen.findByTestId('transfer-pdf-bytes');
    expect(mocks.download).toHaveBeenCalledWith(`/inventory/transfers/transfer/${suffix}`);
    expectNoMutation();
  });
});
