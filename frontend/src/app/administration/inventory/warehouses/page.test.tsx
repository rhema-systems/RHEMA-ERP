import React from 'react';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import WarehousesPage from './page';
import { inventoryManagementService as service, type WarehouseLocationDto } from '@/services/inventoryManagementService';
import { toast } from 'sonner';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getWarehouses: vi.fn(), getWarehouseLocations: vi.fn(), createWarehouseLocation: vi.fn(),
  updateWarehouseLocation: vi.fn(), deleteWarehouseLocation: vi.fn(),
} }));

let bins: WarehouseLocationDto[];
beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
  Element.prototype.scrollIntoView = vi.fn();
  bins = [
    { id: 'main', warehouseId: 'wh-1', locationCode: 'MAIN', name: 'Main', locationType: 'Bin', isActive: true, isDefault: true, isPickingLocation: true, isReceivingLocation: true },
    { id: 'spare', warehouseId: 'wh-1', locationCode: 'SPARE', name: 'Spare', locationType: 'Bin', isActive: true, isDefault: false, isPickingLocation: true, isReceivingLocation: true },
  ] as WarehouseLocationDto[];
  vi.mocked(service.getWarehouses).mockResolvedValue([{ id: 'wh-1', name: 'Demo Warehouse', code: 'DEMO', isActive: true, warehouseType: 'Standard' }] as never);
  vi.mocked(service.getWarehouseLocations).mockImplementation(async () => bins);
  vi.mocked(service.updateWarehouseLocation).mockImplementation(async (id, data) => ({ ...bins.find(bin => bin.id === id)!, ...data }));
  vi.mocked(service.createWarehouseLocation).mockImplementation(async data => ({ ...data, id: 'new', isActive: true } as WarehouseLocationDto));
});

async function locations() {
  render(<WarehousesPage />);
  fireEvent.click(await screen.findByRole('button', { name: 'Locations', exact: true }));
  return screen.getByRole('dialog', { name: 'Locations - Demo Warehouse' });
}
async function edit(code: string) {
  await locations();
  fireEvent.click(screen.getByRole('button', { name: `Edit location ${code}` }));
  return screen.getByRole('dialog', { name: 'Edit Location' });
}

describe('warehouse default bin configuration', () => {
  it('sets a replacement default bin and removes the prior badge in this warehouse', async () => {
    const dialog = await edit('SPARE');
    const toggle = within(dialog).getByRole('switch', { name: 'Default bin' });
    expect(toggle).not.toBeChecked();
    fireEvent.click(toggle);
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save Changes' }));
    await waitFor(() => expect(service.updateWarehouseLocation).toHaveBeenCalledWith('spare', expect.objectContaining({ isDefault: true, warehouseId: 'wh-1' })));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Edit Location' })).not.toBeInTheDocument());
    const list = screen.getByRole('dialog', { name: 'Locations - Demo Warehouse' });
    expect(within(list).getAllByText('Default bin', { exact: true })).toHaveLength(1);
    expect(within(list).getByText('Default bin', { exact: true }).parentElement).toHaveTextContent('SPARE');
    expect(screen.getByText(/Default bin: SPARE/)).toBeInTheDocument();
  });

  it('creates a bin with the default flag and clears the creation toggle afterward', async () => {
    const dialog = await locations();
    fireEvent.change(within(dialog).getByLabelText('Code'), { target: { value: 'new-bin' } });
    fireEvent.click(within(dialog).getByRole('switch', { name: 'Use as default bin' }));
    fireEvent.click(within(dialog).getByRole('button', { name: 'Add', exact: true }));
    await waitFor(() => expect(service.createWarehouseLocation).toHaveBeenCalledWith(expect.objectContaining({ locationCode: 'NEW-BIN', isDefault: true, warehouseId: 'wh-1' })));
    await waitFor(() => expect(within(dialog).getByLabelText('Code')).toHaveValue(''));
    expect(within(dialog).getByRole('switch', { name: 'Use as default bin' })).not.toBeChecked();
    expect(within(dialog).getAllByText('Default bin', { exact: true })).toHaveLength(1);
  });

  it('prevents deactivating the default before another bin replaces it', async () => {
    const dialog = await edit('MAIN');
    fireEvent.click(within(dialog).getByRole('switch', { name: 'Active' }));
    expect(within(dialog).getByRole('alert')).toHaveTextContent('Select a replacement default before deactivating it.');
    expect(within(dialog).getByRole('button', { name: 'Save Changes' })).toBeDisabled();
    expect(service.updateWarehouseLocation).not.toHaveBeenCalled();
  });

  it.each(['inactive', 'consignment', 'zone'])('does not offer %s locations as default bins', async kind => {
    bins[1] = { ...bins[1], isActive: kind !== 'inactive', isConsignmentBin: kind === 'consignment', locationType: kind === 'zone' ? 'Zone' : 'Bin' };
    const dialog = await edit('SPARE');
    expect(within(dialog).getByRole('switch', { name: 'Default bin' })).toBeDisabled();
  });

  it('keeps the chosen default and shows server ProblemDetails if saving fails', async () => {
    vi.mocked(service.updateWarehouseLocation).mockRejectedValueOnce({ isAxiosError: true, response: { data: { detail: 'A quarantine bin cannot be the default.', extensions: { code: 'DEFAULT_BIN_INVALID' } } } });
    const dialog = await edit('SPARE');
    fireEvent.click(within(dialog).getByRole('switch', { name: 'Default bin' }));
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save Changes' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('A quarantine bin cannot be the default. (DEFAULT_BIN_INVALID)'));
    expect(dialog).toBeInTheDocument();
    expect(within(dialog).getByRole('switch', { name: 'Default bin' })).toBeChecked();
    expect(within(dialog).getByRole('button', { name: 'Save Changes' })).toBeEnabled();
  });
});
