import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ReceiveTransferDialog } from './ReceiveTransferDialog';

const mocks = vi.hoisted(() => ({ receive: vi.fn(), toast: vi.fn(), records: vi.fn(), items: [] as any[] }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/services/document-management.service', () => ({ documentManagementService: { getRecords: mocks.records } }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getTransferDiscrepancyReasons: async () => ({}), receiveTransfer: mocks.receive,
  getInventoryTransferById: async () => ({ id: 'transfer', transferNumber: 'TRF-1', rowVersion: 'AQID',
    sourceWarehouseName: 'Project Demo', destinationWarehouseName: 'Project Demo',
    items: mocks.items,
  }),
} }));
afterEach(() => { cleanup(); vi.clearAllMocks(); });
beforeEach(() => {
  mocks.receive.mockReset().mockResolvedValue({});
  mocks.items = [{ id: 'line', itemCode: 'SKU-001', itemName: 'PVC Pipe', shippedQuantity: 2, receivedQuantity: 0,
    sourceLocationId: 'loc-1', destinationLocationId: 'default', sourceLocationName: 'LOC-001', destinationLocationName: 'DEFAULT' }];
});

describe('receive-transfer error safety', () => {
  it('expands and restores without losing partial quantities or notes and receives from full page', async () => {
    const onSuccess = vi.fn();
    render(<ReceiveTransferDialog open transferId="transfer" onOpenChange={vi.fn()} onSuccess={onSuccess} />);
    const quantity = await screen.findByRole('spinbutton', { name: 'Qty to receive SKU-001' });
    fireEvent.change(quantity, { target: { value: '1' } });
    fireEvent.change(screen.getByLabelText('Receiving Notes (Optional)'), { target: { value: 'Good partial receipt' } });
    expect(screen.getByRole('dialog').className).toContain('max-w-[1100px]');
    fireEvent.click(screen.getByRole('button', { name: 'Full page' }));
    expect(screen.getByRole('dialog').className).toContain('h-[calc(100dvh-32px)]');
    expect(screen.getByRole('dialog').className).not.toContain('max-w-[1100px]');
    expect(quantity).toHaveValue(1);
    expect(screen.getByLabelText('Receiving Notes (Optional)')).toHaveValue('Good partial receipt');
    expect(screen.getByRole('button', { name: 'Receive Items' })).toBeEnabled();
    expect(mocks.receive).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Restore' }));
    expect(screen.getByRole('dialog').className).toContain('max-w-[1100px]');
    expect(quantity).toHaveValue(1);
    fireEvent.click(screen.getByRole('button', { name: 'Full page' }));
    fireEvent.click(screen.getByRole('button', { name: 'Receive Items' }));
    await waitFor(() => expect(onSuccess).toHaveBeenCalledOnce());
    expect(mocks.receive).toHaveBeenCalledWith('transfer', expect.objectContaining({ comment: 'Good partial receipt' }), [{ id: 'line', receivedQuantity: 1 }]);
  });

  it('returns to normal dimensions when the receive dialog is reopened', async () => {
    const props = { transferId: 'transfer', onOpenChange: vi.fn(), onSuccess: vi.fn() };
    const view = render(<ReceiveTransferDialog {...props} open />);
    await screen.findByRole('spinbutton', { name: 'Qty to receive SKU-001' });
    fireEvent.click(screen.getByRole('button', { name: 'Full page' }));
    view.rerender(<ReceiveTransferDialog {...props} open={false} />);
    view.rerender(<ReceiveTransferDialog {...props} open />);
    expect(await screen.findByRole('button', { name: 'Full page' })).toBeInTheDocument();
    expect(screen.getByRole('dialog').className).toContain('max-w-[1100px]');
  });

  it('receives only good partial quantities without requiring comments, damage fields or evidence', async () => {
    const onSuccess = vi.fn();
    render(<ReceiveTransferDialog open transferId="transfer" onOpenChange={vi.fn()} onSuccess={onSuccess} />);
    const quantity = await screen.findByRole('spinbutton', { name: 'Qty to receive SKU-001' });
    expect(screen.getAllByRole('spinbutton')).toHaveLength(1);
    expect(screen.queryByRole('columnheader', { name: 'Damaged' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Shortage' })).not.toBeInTheDocument();
    expect(screen.queryByText(/DMS evidence/)).not.toBeInTheDocument();
    fireEvent.change(quantity, { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Receive Items' }));
    await waitFor(() => expect(onSuccess).toHaveBeenCalled());
    expect(mocks.receive).toHaveBeenCalledWith('transfer', expect.objectContaining({ rowVersion: 'AQID', comment: undefined }), [{ id: 'line', receivedQuantity: 1 }]);
    expect(mocks.records).not.toHaveBeenCalled();
  });

  it('uses shipped minus good receipts as remaining and offers only that quantity on the next receipt', async () => {
    mocks.items[0].receivedQuantity = 1;
    render(<ReceiveTransferDialog open transferId="transfer" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    const quantity = await screen.findByRole('spinbutton', { name: 'Qty to receive SKU-001' });
    const cells = within(quantity.closest('tr')!).getAllByRole('cell');
    expect(cells[4]).toHaveTextContent('1.00');
    expect(cells[5]).toHaveTextContent('1.00');
    expect(quantity).toHaveValue(1);
    fireEvent.change(quantity, { target: { value: '2' } });
    expect(quantity).toHaveValue(1);
  });

  it('does not treat historical damage as a receipt or allow duplicate recovery through ordinary receipt', async () => {
    mocks.items[0].receivedQuantity = 1;
    mocks.items[0].damagedQuantity = 1;
    render(<ReceiveTransferDialog open transferId="transfer" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    const quantity = await screen.findByRole('spinbutton', { name: 'Qty to receive SKU-001' });
    const cells = within(quantity.closest('tr')!).getAllByRole('cell');
    expect(cells[4]).toHaveTextContent('1.00');
    expect(cells[5]).toHaveTextContent('1.00');
    expect(quantity).toHaveValue(0);
    expect(quantity).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Receive Items' })).toBeDisabled();
    expect(screen.getByText(/previously reported damaged or missing/)).toBeInTheDocument();
  });

  it('shows shared bins once in the header and hides both repetitive grid columns by default', async () => {
    render(<ReceiveTransferDialog open transferId="transfer" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    expect(await screen.findByText('From Bin: LOC-001')).toBeInTheDocument();
    expect(screen.getByText('To Bin: DEFAULT')).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'From Bin' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'To Bin' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('spinbutton')[0]).toHaveValue(2);
  });

  it('identifies mixed bins and reveals each exact bin through Columns without altering quantities', async () => {
    mocks.items.push({ ...mocks.items[0], id: 'second', itemCode: 'SKU-002', sourceLocationId: 'loc-2', sourceLocationName: 'LOC-002' });
    render(<ReceiveTransferDialog open transferId="transfer" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    expect(await screen.findByText('From Bin: Multiple bins — use Columns')).toBeInTheDocument();
    const quantity = screen.getAllByRole('spinbutton')[0];
    fireEvent.change(quantity, { target: { value: '1' } });
    fireEvent.keyDown(screen.getByRole('button', { name: 'Columns' }), { key: 'Enter' });
    fireEvent.click(await screen.findByRole('menuitemcheckbox', { name: 'From Bin' }));
    expect(screen.getByRole('columnheader', { name: 'From Bin', hidden: true })).toBeInTheDocument();
    expect(screen.getByText('LOC-002')).toBeInTheDocument();
    expect(quantity).toHaveValue(1);
  });

  it('renders a structured failure as text and retains quantities and notes for correction', async () => {
    mocks.receive.mockRejectedValue({ response: { data: { detail: 'The transfer was changed.', extensions: { code: 'TRANSFER_CONCURRENCY' } } } });
    const onOpenChange = vi.fn();
    render(<ReceiveTransferDialog open transferId="transfer" onOpenChange={onOpenChange} onSuccess={vi.fn()} />);
    const quantities = await screen.findAllByRole('spinbutton');
    fireEvent.change(quantities[0], { target: { value: '1' } });
    fireEvent.change(screen.getByLabelText('Receiving Notes (Optional)'), { target: { value: 'Partial receipt' } });
    fireEvent.click(screen.getByRole('button', { name: 'Receive Items' }));
    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ description: 'The transfer was changed. (TRANSFER_CONCURRENCY)' })));
    expect(quantities[0]).toHaveValue(1);
    expect(screen.getByLabelText('Receiving Notes (Optional)')).toHaveValue('Partial receipt');
    expect(onOpenChange).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Receive Items' })).toBeEnabled();
  });
});
