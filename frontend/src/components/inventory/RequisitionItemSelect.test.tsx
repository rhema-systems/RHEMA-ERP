import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import { RequisitionItemSelect } from './RequisitionItemSelect';

beforeAll(() => {
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
  Element.prototype.scrollIntoView = vi.fn();
});

const items = [
  { inventoryItemId: 'pipe', itemCode: 'PVC-50', itemName: 'PVC Pipe 50mm', availableStock: 19, unitOfMeasure: 'EACH', locationLabel: 'LOC-001' },
  { inventoryItemId: 'kit', itemCode: 'KIT-001', itemName: 'Barcode Device Kit', availableStock: 21, unitOfMeasure: 'EACH' },
];

describe('requisition item searchable dropdown', () => {
  it.each(['PVC Pipe', 'PVC-50'])('searches inside the dropdown using %s, with no false empty message', async search => {
    const onChange = vi.fn();
    render(<RequisitionItemSelect items={items} value="" onChange={onChange} loading={false} />);
    fireEvent.click(screen.getByRole('combobox', { name: 'Inventory item' }));
    fireEvent.change(screen.getByRole('combobox', { name: 'Search inventory items' }), { target: { value: search } });
    await waitFor(() => expect(screen.queryByRole('option', { name: /Barcode/ })).not.toBeInTheDocument());
    expect(screen.queryByText(/No matching items/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('option', { name: /PVC Pipe.*Available: 19 EACH/ }));
    expect(onChange).toHaveBeenCalledWith('pipe');
    expect(screen.queryByRole('combobox', { name: 'Search inventory items' })).not.toBeInTheDocument();
  });

  it('shows a real empty state and keeps the previously selected label', async () => {
    render(<RequisitionItemSelect items={items} value="pipe" onChange={vi.fn()} loading={false} />);
    fireEvent.click(screen.getByRole('combobox', { name: 'Inventory item' }));
    fireEvent.change(screen.getByRole('combobox', { name: 'Search inventory items' }), { target: { value: 'no-such-item' } });
    expect(await screen.findByText(/No matching items/)).toBeVisible();
    expect(screen.getByRole('combobox', { name: 'Inventory item' })).toHaveTextContent('PVC-50 - PVC Pipe 50mm');
  });

  it('disables selection while stock is loading', () => {
    render(<RequisitionItemSelect items={[]} value="" onChange={vi.fn()} loading />);
    expect(screen.getByRole('combobox', { name: 'Inventory item' })).toBeDisabled();
    expect(screen.getByText('Loading items...')).toBeVisible();
  });
});
