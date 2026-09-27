import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { LandedCostReceiptWeights } from './LandedCostReceiptWeights';
import { inventoryManagementService, type LandedCostDetailDto } from '@/services/inventoryManagementService';

vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: { setLandedCostReceiptWeight: vi.fn() } }));
const voucher = { id: 'voucher', status: 'Draft', editToken: 'version', receiptWeights: [{ goodsReceiptNoteItemId: 'line', itemCode: 'ITEM', itemName: 'Stock', stockUom: 'EA', unitWeightKg: null, isOverridden: false }] } as LandedCostDetailDto;
describe('voucher receipt weight declaration', () => {
  it('requires a reason and saves the source-bound override with concurrency token', async () => {
    const onSaved = vi.fn();
    vi.mocked(inventoryManagementService.setLandedCostReceiptWeight).mockResolvedValue(voucher);
    render(<LandedCostReceiptWeights voucher={voucher} onSaved={onSaved} />);
    fireEvent.click(screen.getByRole('button', { name: 'Set weight for ITEM' }));
    fireEvent.change(screen.getByLabelText('Voucher unit weight in kilograms'), { target: { value: '1.123456' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save weight' }));
    expect(screen.getByRole('alert')).toHaveTextContent('reason');
    fireEvent.change(screen.getByLabelText('Weight override reason'), { target: { value: 'Measured' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save weight' }));
    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(voucher));
    expect(inventoryManagementService.setLandedCostReceiptWeight).toHaveBeenCalledWith('voucher', 'line', { unitWeightKg: 1.123456, reason: 'Measured', editToken: 'version' });
  });
  it('keeps values visible but locked after allocation', () => {
    render(<LandedCostReceiptWeights voucher={{ ...voucher, status: 'Allocated' }} onSaved={vi.fn()} />);
    expect(screen.getByText(/Weight not recorded/)).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
  it('retains entered values and displays the server detail when an edit is stale', async () => {
    vi.mocked(inventoryManagementService.setLandedCostReceiptWeight).mockRejectedValue({ response: { data: { detail: 'Refresh the changed voucher.', code: 'LANDED_COST_WEIGHT_LOCKED' } } });
    render(<LandedCostReceiptWeights voucher={voucher} onSaved={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Set weight for ITEM' }));
    fireEvent.change(screen.getByLabelText('Voucher unit weight in kilograms'), { target: { value: '2' } });
    fireEvent.change(screen.getByLabelText('Weight override reason'), { target: { value: 'Measured' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save weight' }));
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Refresh the changed voucher.'));
    expect(screen.getByLabelText('Voucher unit weight in kilograms')).toHaveValue(2);
  });
});
