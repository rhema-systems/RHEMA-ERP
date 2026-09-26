import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import StockMovementsPage from './page';
import { inventoryManagementService } from '@/services/inventoryManagementService';

vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getWarehouses: vi.fn(), getInventoryItems: vi.fn(), getStockMovementTypes: vi.fn(), getStockMovements: vi.fn(),
} }));
beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(inventoryManagementService.getWarehouses).mockResolvedValue([]);
  vi.mocked(inventoryManagementService.getInventoryItems).mockResolvedValue([]);
  vi.mocked(inventoryManagementService.getStockMovementTypes).mockResolvedValue(['Receipt']);
  vi.mocked(inventoryManagementService.getStockMovements).mockResolvedValue([{
    id: 'movement', movementNumber: 'MV-TEST', inventoryItemId: 'item', itemName: 'Device kit',
    movementType: 'Receipt', quantity: 20, unitCost: 700, totalCost: 14000,
    movementDate: '2026-09-09T17:50:00Z', referenceNumber: 'REC260003', referenceType: 'PO',
  }]);
});
afterEach(cleanup);

describe('stock movement currency', () => {
  it('sends the selected item to the history API and links to the ledger', async () => {
    vi.mocked(inventoryManagementService.getInventoryItems).mockResolvedValue([{ id: 'item', itemCode: 'ITEM-1', name: 'Device kit' } as any]);
    render(<StockMovementsPage />);
    await screen.findByRole('option', { name: 'ITEM-1 — Device kit' });
    fireEvent.change(screen.getByLabelText('Movement item'), { target: { value: 'item' } });
    await waitFor(() => expect(inventoryManagementService.getStockMovements).toHaveBeenLastCalledWith(expect.objectContaining({ inventoryItemId: 'item' })));
    expect(screen.getByRole('link', { name: 'Inventory Ledger' })).toHaveAttribute('href', '/reports/inventory/inventory-ledger');
  });
  it.each([['GHS', 'GHS'], ['EUR', '€']])('uses tenant %s for history, total and detail costs', async (code, label) => {
    const movements = await inventoryManagementService.getStockMovements();
    vi.mocked(inventoryManagementService.getStockMovements).mockResolvedValue(movements.map(m => ({ ...m, currencyCode: code })));
    render(<StockMovementsPage />);
    await screen.findByText('MV-TEST');
    expect(screen.getAllByText(new RegExp(`${label}\\s*14,000\\.00`))).toHaveLength(2);
    expect(document.body.textContent).not.toContain('$');
    fireEvent.click(screen.getByText('MV-TEST'));
    fireEvent.mouseDown(screen.getByRole('tab', { name: 'Details' }), { button: 0, ctrlKey: false });
    await waitFor(() => expect(screen.getByText(new RegExp(`${label}\\s*700\\.00`))).toBeVisible());
    expect(screen.getAllByText(new RegExp(`${label}\\s*14,000\\.00`))).toHaveLength(3);
    expect(document.body.textContent).not.toContain('$');
  });

  it('does not invent a currency when the tenant currency is unavailable and retries on refresh', async () => {
    render(<StockMovementsPage />);
    await screen.findByText('MV-TEST');
    expect(screen.getByText(/Valuation currency unavailable/)).toBeVisible();
    expect(document.body.textContent).not.toMatch(/\$|GHS|14,000/);
    const movements = await inventoryManagementService.getStockMovements();
    vi.mocked(inventoryManagementService.getStockMovements).mockResolvedValue(movements.map(m => ({ ...m, currencyCode: 'GHS' })));
    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));
    await waitFor(() => expect(screen.getAllByText(/GHS\s*14,000\.00/)).toHaveLength(2));
  });

  it('does not add together values in different currencies', async () => {
    const [movement] = await inventoryManagementService.getStockMovements();
    vi.mocked(inventoryManagementService.getStockMovements).mockResolvedValue([
      { ...movement, currencyCode: 'GHS' }, { ...movement, id: 'other', movementNumber: 'MV-OTHER', currencyCode: 'EUR' },
    ]);
    render(<StockMovementsPage />);
    await screen.findByText('MV-TEST');
    expect(screen.getByText(/no combined total/)).toBeVisible();
    expect(document.body.textContent).not.toContain('28,000');
  });
});
