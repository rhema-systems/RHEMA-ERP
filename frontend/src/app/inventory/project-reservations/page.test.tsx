import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { InventoryProjectReservation } from '@/services/inventoryProjectReservationService';

const mocks = vi.hoisted(() => ({ getAll: vi.fn(), getById: vi.fn(), substitute: vi.fn(), success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast: { success: mocks.success, error: mocks.error } }));
vi.mock('@/services/inventoryProjectReservationService', () => ({ inventoryProjectReservationService: {
  getAll: mocks.getAll, getById: mocks.getById, substitute: mocks.substitute,
} }));
vi.mock('@/services/inventoryRequisitionService', () => ({ inventoryRequisitionService: {
  getPendingIssue: async () => [],
} }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getInventoryItems: async () => [
    { id: 'original-item', itemCode: 'OLD-001', name: 'Original item' },
    { id: 'replacement-item', itemCode: 'NEW-002', name: 'Replacement item' },
  ],
} }));

import Page from './page';

const reservation = (overrides: Partial<InventoryProjectReservation> = {}): InventoryProjectReservation => ({
  id: 'reservation-1', inventoryRequisitionId: 'requisition-1', inventoryRequisitionItemId: 'requisition-line-1',
  requisitionNumber: 'REQ-001', projectId: 'project-1', projectCode: 'PROJ-001', projectTitle: 'Estate project',
  departmentId: 'department-1', departmentName: 'Works', warehouseId: 'warehouse-1', warehouseName: 'Main store',
  locationId: 'bin-1', locationCode: 'BIN-001', inventoryItemId: 'original-item', itemCode: 'OLD-001', itemName: 'Original item',
  reservedQuantity: 5, fulfilledQuantity: 0, releasedQuantity: 0, remainingQuantity: 5, status: 1,
  reservedAtUtc: '2026-09-25T12:00:00Z', expiresAtUtc: '2026-10-25T12:00:00Z',
  reservedById: 'user-1', reservedByName: 'Storekeeper', rowVersion: 'AAAA', actions: [], notifications: [], ...overrides,
});

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal('React', React);
  mocks.getAll.mockResolvedValue([reservation()]);
  mocks.getById.mockResolvedValue(reservation());
  mocks.substitute.mockResolvedValue(reservation({ id: 'reservation-2', inventoryItemId: 'replacement-item',
    itemCode: 'NEW-002', itemName: 'Replacement item', substitutedFromReservationId: 'reservation-1', rowVersion: 'BBBB' }));
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', { configurable: true, value: vi.fn() });
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

async function prepareSubstitution() {
  render(<Page />);
  fireEvent.click(await screen.findByRole('button', { name: 'History' }));
  fireEvent.click((await screen.findByText('Replacement active item')).closest('button')!);
  fireEvent.click(await screen.findByRole('option', { name: 'NEW-002 · Replacement item' }));
  fireEvent.change(screen.getByPlaceholderText('Substitution reason'), { target: { value: 'Approved equivalent item' } });
}

describe('project reservation substitution', () => {
  it('sends the selected replacement item and shows the server-created replacement reservation', async () => {
    await prepareSubstitution();
    fireEvent.click(screen.getByRole('button', { name: 'Substitute' }));

    await waitFor(() => expect(mocks.substitute).toHaveBeenCalledWith('reservation-1', {
      replacementInventoryItemId: 'replacement-item', reason: 'Approved equivalent item',
      rowVersion: 'AAAA', idempotencyKey: expect.stringMatching(/^substitute:/),
    }));
    await waitFor(() => expect(mocks.success).toHaveBeenCalledWith('Pre-fulfillment substitution completed atomically.'));
    const originalRow = screen.getByText('OLD-001 · Original item').closest('tr')!;
    expect(within(originalRow).getByText('Substituted')).toBeInTheDocument();
    expect(screen.getByText('NEW-002 · Replacement item')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Substitution reason')).toHaveValue('');
  });

  it('retains selected item, reason and original reservation when the server rejects substitution', async () => {
    mocks.substitute.mockRejectedValue({ response: { data: { detail: 'Replacement stock is unavailable.' } } });
    await prepareSubstitution();
    fireEvent.click(screen.getByRole('button', { name: 'Substitute' }));

    await waitFor(() => expect(mocks.error).toHaveBeenCalledWith('Replacement stock is unavailable.'));
    expect(screen.getByPlaceholderText('Substitution reason')).toHaveValue('Approved equivalent item');
    expect(screen.getByText('NEW-002 · Replacement item').closest('button')).toBeInTheDocument();
    expect(within(screen.getByText('OLD-001 · Original item').closest('tr')!).getByText('Reserved')).toBeInTheDocument();
    expect(screen.queryByText('Substituted')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Substitute' })).toBeEnabled();
    expect(mocks.success).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Substitute' }));
    await waitFor(() => expect(mocks.substitute).toHaveBeenCalledTimes(2));
    expect(mocks.substitute.mock.calls[1][1]).toMatchObject({ replacementInventoryItemId: 'replacement-item', rowVersion: 'AAAA' });
  });
});
