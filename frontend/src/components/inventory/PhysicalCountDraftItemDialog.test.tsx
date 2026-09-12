import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { PhysicalCountDraftItemDialog } from './PhysicalCountDraftItemDialog';
import { inventoryManagementService as service, type BinStockDto, type PhysicalCountDetailDto, type WarehouseLocationDto } from '@/services/inventoryManagementService';

vi.mock('@/components/ui/dialog', () => ({
  Dialog: ({ open, children }: any) => open ? <div>{children}</div> : null,
  DialogContent: ({ children }: any) => <div>{children}</div>,
  DialogHeader: ({ children }: any) => <div>{children}</div>,
  DialogTitle: ({ children }: any) => <h2>{children}</h2>,
  DialogDescription: ({ children }: any) => <p>{children}</p>,
  DialogFooter: ({ children }: any) => <div>{children}</div>,
}));
vi.mock('@/components/ui/select', () => ({
  Select: ({ children, value, onValueChange, disabled }: any) => <select aria-label="Location" value={value} disabled={disabled} onChange={e => onValueChange(e.target.value)}><option value="">Choose</option>{children}</select>,
  SelectTrigger: () => null, SelectValue: () => null, SelectContent: ({ children }: any) => children,
  SelectItem: ({ value, children }: any) => <option value={value}>{children}</option>,
}));
vi.mock('@/components/ui/popover', () => ({
  Popover: ({ children }: any) => <div>{children}</div>,
  PopoverTrigger: ({ children }: any) => children,
  PopoverContent: ({ children }: any) => <div>{children}</div>,
}));
vi.mock('@/components/ui/command', () => ({
  Command: ({ children }: any) => <div>{children}</div>, CommandList: ({ children }: any) => <div>{children}</div>,
  CommandGroup: ({ children }: any) => <div>{children}</div>, CommandEmpty: () => null,
  CommandInput: () => <input aria-label="Search count items" />,
  CommandItem: ({ children, onSelect }: any) => <button onClick={onSelect}>{children}</button>,
}));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getWarehouseLocations: vi.fn(), getBinStock: vi.fn(),
} }));

const location = (id: string, warehouseId = 'warehouse', isActive = true) => ({ id, warehouseId, locationCode: id, isActive }) as WarehouseLocationDto;
const row = (inventoryItemId: string, locationId: string, quantity = 13) => ({ inventoryItemId, itemCode: inventoryItemId, itemName: `Item ${inventoryItemId}`, locationId, warehouseId: 'warehouse', quantity }) as BinStockDto;
const page = (items: BinStockDto[], totalCount = items.length) => ({ items, totalCount, page: 1, pageSize: 200, totalPages: 1, hasPrevious: false, hasNext: false });
let count: PhysicalCountDetailDto;
const save = vi.fn();
const changed = vi.fn();
const show = () => render(<PhysicalCountDraftItemDialog open count={count} busy={false} onOpenChange={changed} onSave={save} />);

beforeEach(() => {
  vi.clearAllMocks();
  count = { id: 'count', warehouseId: 'warehouse', warehouseName: 'Project Demo Warehouse', status: 'Draft', items: [] } as unknown as PhysicalCountDetailDto;
  vi.mocked(service.getWarehouseLocations).mockResolvedValue([location('A'), location('B'), location('inactive', 'warehouse', false), location('foreign', 'other')]);
  vi.mocked(service.getBinStock).mockResolvedValue(page([]));
  save.mockResolvedValue(true);
});

describe('physical count draft item scope', () => {
  it('fixes location counts to their location and includes only items from that location', async () => {
    count.locationId = 'A';
    vi.mocked(service.getBinStock).mockResolvedValue(page([row('in-scope', 'A'), row('wrong-location', 'B'), { ...row('wrong-warehouse', 'A'), warehouseId: 'other' }]));
    show();
    await screen.findByText('Item in-scope');
    expect(screen.getByRole('combobox', { name: 'Location' })).toBeDisabled();
    expect(screen.queryByText('Item wrong-location')).not.toBeInTheDocument();
    expect(screen.queryByText('Item wrong-warehouse')).not.toBeInTheDocument();
    expect(service.getBinStock).toHaveBeenCalledWith({ warehouseId: 'warehouse', locationId: 'A', includeZero: true, page: 1, pageSize: 200 });
    fireEvent.click(screen.getByText('Item in-scope'));
    fireEvent.click(screen.getByRole('button', { name: 'Add item' }));
    await waitFor(() => expect(save).toHaveBeenCalledWith({ inventoryItemId: 'in-scope', locationId: 'A' }));
  });

  it('waits for a location for warehouse-wide counts, excluding inactive and foreign locations', async () => {
    show();
    await screen.findByRole('option', { name: 'A' });
    expect(service.getBinStock).not.toHaveBeenCalled();
    expect(screen.getByRole('combobox', { name: 'Count item' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Add item' })).toBeDisabled();
    expect(screen.queryByRole('option', { name: 'inactive' })).not.toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'foreign' })).not.toBeInTheDocument();
  });

  it('allows the same item in another location and never exposes its system quantity', async () => {
    count.items = [{ inventoryItemId: 'existing', locationId: 'B' }, { inventoryItemId: 'duplicate', locationId: 'A' }] as any;
    vi.mocked(service.getBinStock).mockResolvedValue(page([row('existing', 'A', 93271), row('duplicate', 'A'), row('zero', 'A', 0)]));
    show(); await screen.findByRole('option', { name: 'A' });
    fireEvent.change(screen.getByRole('combobox', { name: 'Location' }), { target: { value: 'A' } });
    await screen.findByText('Item existing');
    expect(screen.getByText('Item zero')).toBeInTheDocument();
    expect(screen.queryByText('Item duplicate')).not.toBeInTheDocument();
    expect(screen.queryByText(/93271/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByText('Item existing'));
    fireEvent.click(screen.getByRole('button', { name: 'Add item' }));
    await waitFor(() => expect(save).toHaveBeenCalledWith({ inventoryItemId: 'existing', locationId: 'A' }));
  });

  it('loads all pages of the selected location inventory', async () => {
    count.locationId = 'A';
    vi.mocked(service.getBinStock).mockResolvedValueOnce(page([row('first', 'A')], 2)).mockResolvedValueOnce(page([row('last', 'A')], 2));
    show(); await screen.findByText('Item last');
    expect(screen.getByText('Item first')).toBeInTheDocument();
    expect(service.getBinStock).toHaveBeenNthCalledWith(2, expect.objectContaining({ locationId: 'A', page: 2 }));
  });

  it('clears the item selection when the location changes', async () => {
    vi.mocked(service.getBinStock).mockImplementation(async args => page([row('selected', args!.locationId!)]));
    show(); await screen.findByRole('option', { name: 'A' });
    fireEvent.change(screen.getByRole('combobox', { name: 'Location' }), { target: { value: 'A' } });
    fireEvent.click(await screen.findByText('Item selected'));
    expect(screen.getByRole('button', { name: 'Add item' })).toBeEnabled();
    fireEvent.change(screen.getByRole('combobox', { name: 'Location' }), { target: { value: 'B' } });
    await waitFor(() => expect(service.getBinStock).toHaveBeenCalledWith(expect.objectContaining({ locationId: 'B' })));
    expect(screen.getByRole('button', { name: 'Add item' })).toBeDisabled();
    expect(save).not.toHaveBeenCalled();
  });

  it('does not allow saving if location inventory could not be fully loaded', async () => {
    count.locationId = 'A';
    vi.mocked(service.getBinStock).mockRejectedValue(new Error('Network unavailable'));
    show(); await screen.findByRole('alert');
    expect(screen.getByRole('button', { name: 'Add item' })).toBeDisabled();
    expect(save).not.toHaveBeenCalled();
  });
});
