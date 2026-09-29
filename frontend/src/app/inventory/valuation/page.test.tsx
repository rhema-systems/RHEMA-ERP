import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import InventoryValuationPage from './page';

const mocks = vi.hoisted(() => ({ items: vi.fn(), warehouses: vi.fn(), scoped: vi.fn(), transit: vi.fn(), currency: vi.fn() }));
vi.mock('@/services/financeCommonService', () => ({ currencyService: { getBaseCurrency: mocks.currency } }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getInventoryItems: mocks.items, getWarehouses: mocks.warehouses, getWarehouseItems: mocks.scoped,
  getTransferTransitStock: mocks.transit,
} }));
vi.mock('@/components/ui/select', () => ({
  Select: ({ children, value, onValueChange }: any) => <select value={value} onChange={event => onValueChange(event.target.value)}>{children}</select>,
  SelectTrigger: () => null, SelectValue: () => null, SelectContent: ({ children }: any) => <>{children}</>,
  SelectItem: ({ children, value }: any) => <option value={value}>{children}</option>,
}));

beforeEach(() => {
  vi.clearAllMocks();
  mocks.currency.mockResolvedValue({ code: 'GHS' });
  mocks.items.mockResolvedValue([{ id: 'item', itemCode: 'SKU-1', name: 'Test item', currentStock: 10, availableStock: 8, allocatedStock: 0, reorderLevel: 1, averageCost: 12, standardCost: 12, lastPurchaseCost: 12 }]);
  mocks.warehouses.mockResolvedValue([{ id: 'source', name: 'Source' }, { id: 'transit', name: 'Transit' }]);
  mocks.scoped.mockImplementation(async (id: string) => [{ inventoryItemId: 'item', currentStock: id === 'source' ? 6 : 2, availableStock: 6, averageCost: 12 }]);
  mocks.transit.mockResolvedValue({ legacyReconciliationRequiredCount: 1, items: [{
    transferId: 'transfer', transferNumber: 'TRF-1', transferItemId: 'line', dispatchAllocationId: 'dispatch',
    itemId: 'item', itemCode: 'SKU-1', itemName: 'Test item', sourceWarehouseId: 'source', sourceWarehouseName: 'Source',
    destinationWarehouseId: 'destination', destinationWarehouseName: 'Destination', inTransitWarehouseId: 'transit',
    sourceLocationName: 'BIN-A', inTransitLocationName: 'TRANSIT-1', carrierName: 'Carrier Ltd', vehicleNumber: 'GT-42',
    requestedQuantity: 5, dispatchedQuantity: 4, receivedQuantity: 1, returnedQuantity: 1, inTransitQuantity: 2, inTransitValue: 17.99,
  }] });
});
afterEach(cleanup);

describe('valuation transit breakdown', () => {
  it('formats all inventory money in the configured base currency', async () => {
    mocks.currency.mockResolvedValueOnce({ code: 'EUR' });
    render(<InventoryValuationPage />);
    expect(await screen.findByText('€17.99')).toBeInTheDocument();
    expect(screen.getByText('Total Inventory Value').parentElement).toHaveTextContent('€120.00');
    expect(screen.queryByText(/\$/)).not.toBeInTheDocument();
  });

  it('shows a report failure without presenting missing transit data as zero balances', async () => {
    mocks.transit.mockRejectedValueOnce({ detail: 'Transit ledger is unavailable', code: 'TRANSIT_UNAVAILABLE' });
    render(<InventoryValuationPage />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Transit ledger is unavailable (TRANSIT_UNAVAILABLE)');
    expect(screen.queryByText('Total Inventory Value')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });

  it('does not add physical transit to canonical company stock and uses ledger carrying value', async () => {
    render(<InventoryValuationPage />);
    await screen.findByText(/GHS\s*17\.99/);
    expect(screen.getByText('Total Units').parentElement).toHaveTextContent('10Total Units');
    expect(screen.getByText('Total Inventory Value').parentElement).toHaveTextContent(/GHS\s*120\.00/);
    expect(screen.getByRole('alert')).toHaveTextContent('1 historical transfer(s) require transit reconciliation');
    expect(screen.queryByRole('switch')).not.toBeInTheDocument();
    fireEvent.mouseDown(screen.getByRole('tab', { name: 'Stock in transit' }), { button: 0, ctrlKey: false });
    expect(await screen.findByRole('link', { name: 'TRF-1' })).toHaveAttribute('href', '/inventory/transfers?recordId=transfer');
    expect(screen.getByText('Carrier Ltd')).toBeInTheDocument();
    expect(screen.getByText('GT-42')).toBeInTheDocument();
  });

  it('keeps related transit separate from a normal warehouse and counts the transit warehouse once', async () => {
    render(<InventoryValuationPage />);
    await screen.findByText(/GHS\s*17\.99/);
    fireEvent.change(screen.getAllByRole('combobox')[1], { target: { value: 'source' } });
    await waitFor(() => expect(screen.getByText('Total Units').parentElement).toHaveTextContent('6Total Units'));
    expect(screen.getByText(/GHS\s*17\.99/)).toBeInTheDocument();
    fireEvent.change(screen.getAllByRole('combobox')[1], { target: { value: 'transit' } });
    await waitFor(() => expect(screen.getByText('Total Units').parentElement).toHaveTextContent('2Total Units'));
    expect(screen.getByText(/GHS\s*17\.99/)).toBeInTheDocument();
  });
});
