import React from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ReceiptItemWeightInput } from './ReceiptItemWeightInput';
import { inventoryManagementService } from '@/services/inventoryManagementService';

vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: { getInventoryItemById: vi.fn() } }));
afterEach(() => { cleanup(); vi.resetAllMocks(); });

describe('ReceiptItemWeightInput', () => {
  it('shows the converted master default per base stock unit and sends only an explicit override', async () => {
    vi.mocked(inventoryManagementService.getInventoryItemById).mockResolvedValue({ unitOfMeasure: 'EA', weight: 2500, weightUnit: 'g' } as any);
    const onChange = vi.fn();
    const { rerender } = render(<ReceiptItemWeightInput inventoryItemId="item" stockUnit="BOX" onChange={onChange} />);
    const input = await screen.findByPlaceholderText('Default: 2.5');
    expect(screen.getByText('Weight (kg / EA)')).toBeVisible();
    expect(onChange).not.toHaveBeenCalled();
    fireEvent.change(input, { target: { value: '3.125' } });
    expect(onChange).toHaveBeenCalledWith(3.125);
    rerender(<ReceiptItemWeightInput inventoryItemId="item" stockUnit="BOX" value={3.125} onChange={onChange} />);
    fireEvent.change(input, { target: { value: '' } });
    expect(onChange).toHaveBeenLastCalledWith(undefined);
  });

  it('keeps an unlabelled historical master weight unspecified', async () => {
    vi.mocked(inventoryManagementService.getInventoryItemById).mockResolvedValue({ unitOfMeasure: 'EA', weight: 2500 } as any);
    render(<ReceiptItemWeightInput inventoryItemId="item" stockUnit="BOX" onChange={vi.fn()} />);
    await screen.findByText('Weight (kg / EA)');
    expect(screen.getByPlaceholderText('Not specified')).toHaveValue(null);
  });

  it('prevents an override when the base stock unit cannot be loaded', async () => {
    vi.mocked(inventoryManagementService.getInventoryItemById).mockRejectedValue(new Error('Denied'));
    render(<ReceiptItemWeightInput inventoryItemId="item" stockUnit="BOX" onChange={vi.fn()} />);
    await screen.findByText(/Unable to load the stock unit/);
    expect(screen.getByRole('spinbutton')).toBeDisabled();
  });
});
