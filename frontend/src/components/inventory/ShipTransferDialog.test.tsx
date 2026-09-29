import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ShipTransferDialog } from './ShipTransferDialog';

const mocks = vi.hoisted(() => ({ detail: vi.fn(), ship: vi.fn(), shipCosts: vi.fn(), options: vi.fn(), carriers: vi.fn(), toast: vi.fn() }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getAllPartnersForDropdown: mocks.carriers } }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getInventoryTransferById: mocks.detail, shipTransfer: mocks.ship,
  getTransferPickingOptions: mocks.options, shipTransferWithCosts: mocks.shipCosts,
} }));

beforeEach(() => {
  vi.clearAllMocks();
  mocks.ship.mockReset().mockResolvedValue({});
  mocks.shipCosts.mockReset().mockResolvedValue({});
  mocks.options.mockResolvedValue([{ itemId: 'line', sourceLocationId: 'bin-a', sourceLocationName: 'Bin A', quantityOnHand: 8, quantityAllocated: 3, quantityAvailable: 5 }]);
  mocks.carriers.mockResolvedValue([{ id: 'supplier', partnerCode: 'SUP-1', partnerName: 'Carrier Ltd', partnerType: 'Supplier', isActive: true }]);
  mocks.detail.mockResolvedValue({ id: 'transfer', transferNumber: 'TRF-1', rowVersion: 'AQID',
    sourceWarehouseName: 'Project Demo', destinationWarehouseName: 'Project Demo',
    items: [{ id: 'line', inventoryItemId: 'pvc', itemCode: 'SKU-001', itemName: 'PVC Pipe',
      requestedQuantity: 5, shippedQuantity: 2, receivedQuantity: 0, unitOfMeasure: 'EACH',
      unitCost: 1918.85, totalCost: 9594.25, sourceLocationName: 'LOC-001', destinationLocationName: 'DEFAULT' }],
  });
});
afterEach(cleanup);

describe('compact ship transfer', () => {
  it('expands and restores without losing quantities or notes and keeps shipping available', async () => {
    mocks.ship.mockResolvedValue({});
    const onSuccess = vi.fn();
    render(<ShipTransferDialog open transferId="transfer" onOpenChange={vi.fn()} onSuccess={onSuccess} />);
    const quantity = await screen.findByRole('spinbutton', { name: 'Qty to Ship SKU-001' });
    fireEvent.change(quantity, { target: { value: '1' } });
    fireEvent.change(screen.getByLabelText('Notes (Optional)'), { target: { value: 'Partial dispatch' } });
    expect(screen.getByRole('dialog').className).toContain('max-w-[1100px]');
    fireEvent.click(screen.getByRole('button', { name: 'Full page' }));
    expect(screen.getByRole('dialog').className).toContain('h-[calc(100dvh-32px)]');
    expect(screen.getByRole('dialog').className).not.toContain('max-w-[1100px]');
    expect(quantity).toHaveValue(1);
    expect(screen.getByLabelText('Notes (Optional)')).toHaveValue('Partial dispatch');
    expect(screen.getByRole('button', { name: 'Ship Items' })).toBeEnabled();
    expect(mocks.ship).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Restore' }));
    expect(screen.getByRole('dialog').className).toContain('max-w-[1100px]');
    expect(quantity).toHaveValue(1);
    fireEvent.click(screen.getByRole('button', { name: 'Full page' }));
    fireEvent.click(screen.getByRole('button', { name: 'Ship Items' }));
    await waitFor(() => expect(onSuccess).toHaveBeenCalledOnce());
    expect(mocks.ship).toHaveBeenCalledOnce();
  });

  it('returns to normal dimensions when the ship dialog is reopened', async () => {
    const props = { transferId: 'transfer', onOpenChange: vi.fn(), onSuccess: vi.fn() };
    const view = render(<ShipTransferDialog {...props} open />);
    await screen.findByRole('spinbutton', { name: 'Qty to Ship SKU-001' });
    fireEvent.click(screen.getByRole('button', { name: 'Full page' }));
    view.rerender(<ShipTransferDialog {...props} open={false} />);
    view.rerender(<ShipTransferDialog {...props} open />);
    expect(await screen.findByRole('button', { name: 'Full page' })).toBeInTheDocument();
    expect(screen.getByRole('dialog').className).toContain('max-w-[1100px]');
  });

  it('shows compact picking columns, hides values, and retains editable partial quantities', async () => {
    render(<ShipTransferDialog open transferId="transfer" currencyCode="GHS" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    const quantity = await screen.findByRole('spinbutton', { name: 'Qty to Ship SKU-001' });
    expect(screen.getAllByRole('columnheader').map(cell => cell.textContent)).toEqual(['Item', 'Source bin picks', 'Remaining', 'Qty to Ship']);
    expect(screen.getByText('PVC Pipe')).toBeInTheDocument();
    expect(screen.queryByText('Financial Summary')).not.toBeInTheDocument();
    expect(screen.queryByText(/1,918\.85/)).not.toBeInTheDocument();
    expect(quantity).toHaveValue(3);
    fireEvent.change(quantity, { target: { value: '1' } });
    expect(quantity).toHaveValue(1);
    expect(screen.getByRole('button', { name: 'Ship Items' })).toBeEnabled();
    expect(screen.getByRole('checkbox', { name: 'Include shipping costs' })).toBeInTheDocument();
    expect(screen.getByRole('dialog').className).toContain('w-[calc(100vw-32px)]');
    expect(screen.getByRole('dialog').className).toContain('overflow-hidden');
    expect(screen.getByLabelText('Notes (Optional)')).toBeInTheDocument();
    expect(screen.queryByText('Dispatch control comment')).not.toBeInTheDocument();
    expect(screen.queryByText('Capture shipping and miscellaneous costs for this transfer')).not.toBeInTheDocument();
  });

  it('offers only optional quantity columns and does not change entered quantities when toggled', async () => {
    render(<ShipTransferDialog open transferId="transfer" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    const quantity = await screen.findByRole('spinbutton', { name: 'Qty to Ship SKU-001' });
    fireEvent.change(quantity, { target: { value: '1' } });
    fireEvent.keyDown(screen.getByRole('button', { name: 'Columns' }), { key: 'Enter' });
    const requested = await screen.findByRole('menuitemcheckbox', { name: 'Requested' });
    expect(screen.getAllByRole('menuitemcheckbox').map(item => item.textContent)).toEqual(['Requested', 'Already Shipped', 'UOM']);
    fireEvent.click(requested);
    await waitFor(() => expect(screen.getByRole('columnheader', { name: 'Requested', hidden: true })).toBeInTheDocument());
    expect(quantity).toHaveValue(1);
  });

  it('keeps a failed shipment editable and renders ProblemDetails as text', async () => {
    mocks.ship.mockRejectedValue({ response: { data: { detail: 'Transfer changed; refresh it.', code: 'TRANSFER_CONCURRENCY' } } });
    const onOpenChange = vi.fn();
    render(<ShipTransferDialog open transferId="transfer" onOpenChange={onOpenChange} onSuccess={vi.fn()} />);
    const quantity = await screen.findByRole('spinbutton', { name: 'Qty to Ship SKU-001' });
    fireEvent.change(quantity, { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Ship Items' }));
    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ description: 'Transfer changed; refresh it. (TRANSFER_CONCURRENCY)' })));
    expect(quantity).toHaveValue(1);
    expect(onOpenChange).not.toHaveBeenCalled();
  });

  it.each([false, true])('ships split-bin picks and canonical carrier with costs=%s', async withCosts => {
    mocks.options.mockResolvedValue([
      { itemId: 'line', sourceLocationId: 'bin-a', sourceLocationName: 'Bin A', quantityOnHand: 4, quantityAllocated: 2, quantityAvailable: 2 },
      { itemId: 'line', sourceLocationId: 'bin-b', sourceLocationName: 'Bin B', quantityOnHand: 6, quantityAllocated: 1, quantityAvailable: 5 },
    ]);
    render(<ShipTransferDialog open transferId="transfer" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    fireEvent.change(await screen.findByLabelText('Source bin SKU-001 1'), { target: { value: 'bin-a' } });
    fireEvent.change(screen.getByLabelText('Pick quantity SKU-001 1'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add bin' }));
    fireEvent.change(screen.getByLabelText('Source bin SKU-001 2'), { target: { value: 'bin-b' } });
    fireEvent.change(screen.getByLabelText('Pick quantity SKU-001 2'), { target: { value: '1' } });
    fireEvent.change(screen.getByLabelText('Carrier supplier (optional)'), { target: { value: 'supplier' } });
    fireEvent.change(screen.getByLabelText('Vehicle number (optional)'), { target: { value: 'GT-42' } });
    expect(screen.getByText('On hand 4 · Allocated 2 · Available 2')).toBeInTheDocument();
    if (withCosts) fireEvent.click(screen.getByRole('checkbox', { name: 'Include shipping costs' }));
    fireEvent.click(screen.getByRole('button', { name: 'Ship Items' }));
    const items = [{ itemId: 'line', shippedQuantity: 3, picks: [{ sourceLocationId: 'bin-a', quantity: 2 }, { sourceLocationId: 'bin-b', quantity: 1 }] }];
    if (withCosts) await waitFor(() => expect(mocks.shipCosts).toHaveBeenCalledWith('transfer', expect.objectContaining({ items, carrierBusinessPartnerId: 'supplier', vehicleNumber: 'GT-42' })));
    else await waitFor(() => expect(mocks.ship).toHaveBeenCalledWith('transfer', expect.any(Object), undefined, items, { carrierBusinessPartnerId: 'supplier', vehicleNumber: 'GT-42' }));
  });

  it('sends an exact fractional dispatch total across bin picks', async () => {
    mocks.options.mockResolvedValue([
      { itemId: 'line', sourceLocationId: 'bin-a', sourceLocationName: 'Bin A', quantityOnHand: 2, quantityAllocated: 0, quantityAvailable: 2 },
      { itemId: 'line', sourceLocationId: 'bin-b', sourceLocationName: 'Bin B', quantityOnHand: 2, quantityAllocated: 0, quantityAvailable: 2 },
    ]);
    render(<ShipTransferDialog open transferId="transfer" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    fireEvent.change(await screen.findByLabelText('Source bin SKU-001 1'), { target: { value: 'bin-a' } });
    fireEvent.change(screen.getByLabelText('Pick quantity SKU-001 1'), { target: { value: '0.1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add bin' }));
    fireEvent.change(screen.getByLabelText('Source bin SKU-001 2'), { target: { value: 'bin-b' } });
    fireEvent.change(screen.getByLabelText('Pick quantity SKU-001 2'), { target: { value: '0.2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Ship Items' }));
    await waitFor(() => expect(mocks.ship).toHaveBeenCalledWith('transfer', expect.any(Object), undefined,
      [expect.objectContaining({ shippedQuantity: 0.3 })], expect.any(Object)));
  });

  it('rejects over-allocation and supports removing a bin pick without shipping', async () => {
    mocks.options.mockResolvedValue([{ itemId: 'line', sourceLocationId: 'bin-a', sourceLocationName: 'Bin A', quantityOnHand: 4, quantityAllocated: 2, quantityAvailable: 2 }]);
    render(<ShipTransferDialog open transferId="transfer" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    await screen.findByLabelText('Pick quantity SKU-001 1');
    fireEvent.click(screen.getByRole('button', { name: 'Ship Items' }));
    expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ description: 'Picked quantities exceed available stock in a source bin.' }));
    expect(mocks.ship).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Remove pick SKU-001 1' }));
    expect(screen.queryByLabelText('Pick quantity SKU-001 1')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ship Items' })).toBeDisabled();
  });

  it('fails closed when picking options cannot load', async () => {
    mocks.options.mockRejectedValueOnce({ detail: 'Warehouse access denied', code: 'WAREHOUSE_DENIED' });
    render(<ShipTransferDialog open transferId="transfer" onOpenChange={vi.fn()} onSuccess={vi.fn()} />);
    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ description: 'Warehouse access denied (WAREHOUSE_DENIED)' })));
    expect(screen.getByRole('button', { name: 'Ship Items' })).toBeDisabled();
    expect(mocks.ship).not.toHaveBeenCalled();
  });
});
