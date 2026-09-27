import React from 'react';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ReceiptLandedCostEntry } from './ReceiptLandedCostEntry';
import { purchasingService, type PurchaseOrderReceiptDto } from '@/services/purchasingService';
import { inventoryManagementService, type LandedCostDetailDto } from '@/services/inventoryManagementService';
import { businessPartnerService } from '@/services/businessPartnerService';

vi.mock('@/services/purchasingService', () => ({ purchasingService: { getPurchaseOrderById: vi.fn() } }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: { saveReceiptLandedCost: vi.fn() } }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getAllPartnersForDropdown: vi.fn() } }));
const receipt = { id: 'receipt', purchaseOrderId: 'po', receiptNumber: 'REC-1', items: [
  { id: 'a', purchaseOrderItemId: 'line-a', itemCode: 'A', itemName: 'Pipe', receivedQuantity: 10, acceptedQuantity: 10, rejectedQuantity: 0 },
] } as PurchaseOrderReceiptDto;
const saved = { id: 'voucher', goodsReceiptNoteId: 'receipt', status: 'Draft', currency: 'GHS', editToken: 'original-token',
  costItems: [{ id: 'cost', costType: 'Freight', description: 'Freight', amount: 200, currency: 'GHS', exchangeRate: 1, allocationMethod: 'ByQuantity', purchaseOrderItemId: 'line-a' }], allocations: [] } as unknown as LandedCostDetailDto;
beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(businessPartnerService.getAllPartnersForDropdown).mockResolvedValue([
    { id: 'supplier-freight', partnerCode: 'FRT', partnerName: 'Freight supplier', partnerType: 'Supplier', isActive: true, approvalStatus: 'Approved' },
    { id: 'supplier-handling', partnerCode: 'HND', partnerName: 'Handling supplier', partnerType: 'Supplier', isActive: true, approvalStatus: 'Approved' },
    { id: 'blocked', partnerCode: 'BLK', partnerName: 'Blacklisted supplier', partnerType: 'Supplier', isActive: true, approvalStatus: 'Approved', isBlacklisted: true },
    { id: 'customer', partnerCode: 'CUS', partnerName: 'Customer only', partnerType: 'Customer', isActive: true, approvalStatus: 'Approved' },
  ] as never);
  // jsdom does not implement the scrolling API used by the real Radix select.
  HTMLElement.prototype.scrollIntoView = vi.fn();
  vi.mocked(purchasingService.getPurchaseOrderById).mockResolvedValue({ currency: 'GHS', items: [{ id: 'line-a', inventoryItemId: 'stock-a' }] } as never);
  vi.mocked(inventoryManagementService.saveReceiptLandedCost).mockResolvedValue(saved);
});
afterEach(cleanup);
async function open(selected: LandedCostDetailDto | null = null) {
  const onSaved = vi.fn();
  render(<ReceiptLandedCostEntry receipt={receipt} selected={selected} onSaved={onSaved} />);
  const button = screen.getByRole('button', { name: selected?.status === 'Draft' ? 'Edit draft costs' : 'Add receipt costs' });
  await waitFor(() => expect(button).toBeEnabled());
  await act(async () => { fireEvent.click(button); });
  return onSaved;
}
describe('receipt landed costs', () => {
  it('saves the independently selected supplier on each charge', async () => {
    await open();
    await waitFor(() => expect(screen.getByLabelText('Charge 1 supplier')).toBeEnabled());
    fireEvent.change(screen.getByLabelText('Charge 1 amount'), { target: { value: '310' } });
    fireEvent.keyDown(screen.getByLabelText('Charge 1 supplier'), { key: 'Enter' });
    expect(screen.queryByRole('option', { name: /Blacklisted|Customer only/ })).not.toBeInTheDocument();
    fireEvent.click(await screen.findByRole('option', { name: 'FRT — Freight supplier' }));
    fireEvent.click(screen.getByRole('button', { name: 'Add another charge' }));
    fireEvent.change(screen.getByLabelText('Charge 2 amount'), { target: { value: '50' } });
    fireEvent.keyDown(screen.getByLabelText('Charge 2 supplier'), { key: 'Enter' });
    fireEvent.click(await screen.findByRole('option', { name: 'HND — Handling supplier' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save draft costs' }));
    await waitFor(() => expect(inventoryManagementService.saveReceiptLandedCost).toHaveBeenCalledWith(expect.objectContaining({
      costItems: [expect.objectContaining({ amount: 310, supplierId: 'supplier-freight' }),
        expect.objectContaining({ amount: 50, supplierId: 'supplier-handling' })]
    }), undefined));
  });
  it('retains an existing supplier when editing without choosing it again', async () => {
    await open({ ...saved, costItems: saved.costItems.map(c => ({ ...c, supplierId: 'supplier-freight', supplierName: 'Freight supplier' })) });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft costs' }));
    await waitFor(() => expect(inventoryManagementService.saveReceiptLandedCost).toHaveBeenCalledWith(expect.objectContaining({
      costItems: [expect.objectContaining({ supplierId: 'supplier-freight' })]
    }), 'voucher'));
  });
  it('does not default to the goods supplier and supports retry after lookup failure', async () => {
    vi.mocked(businessPartnerService.getAllPartnersForDropdown).mockRejectedValueOnce(new Error('Unavailable'));
    await open();
    await screen.findByRole('button', { name: 'Retry suppliers' });
    expect(screen.getByLabelText('Charge 1 supplier')).toHaveTextContent('Select before invoicing');
    expect(screen.getByLabelText('Charge 1 supplier')).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Retry suppliers' }));
    await waitFor(() => expect(screen.getByLabelText('Charge 1 supplier')).toBeEnabled());
    expect(inventoryManagementService.saveReceiptLandedCost).not.toHaveBeenCalled();
  });
  it('creates a GHS draft directly without any PO plan and does not allocate or post', async () => {
    const onSaved = await open();
    fireEvent.change(screen.getByLabelText('Charge 1 amount'), { target: { value: '300' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft costs' }));
    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(saved));
    expect(inventoryManagementService.saveReceiptLandedCost).toHaveBeenCalledWith(expect.objectContaining({
      goodsReceiptNoteId: 'receipt', requestId: expect.any(String), currency: 'GHS',
      costItems: [expect.objectContaining({ amount: 300, allocationMethod: 'ByValue' })]
    }), undefined);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
  it('edits existing draft and preserves the exact line target and concurrency token', async () => {
    await open(saved);
    fireEvent.change(screen.getByLabelText('Charge 1 amount'), { target: { value: '250' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft costs' }));
    await waitFor(() => expect(inventoryManagementService.saveReceiptLandedCost).toHaveBeenCalledWith(expect.objectContaining({
      editToken: 'original-token', costItems: [expect.objectContaining({ amount: 250, purchaseOrderItemId: 'line-a' })]
    }), 'voucher'));
  });
  it('saves separate shared and item-specific charges from the selector', async () => {
    await open();
    fireEvent.change(screen.getByLabelText('Charge 1 amount'), { target: { value: '300' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add another charge' }));
    fireEvent.change(screen.getByLabelText('Charge 2 amount'), { target: { value: '50' } });
    fireEvent.keyDown(screen.getByLabelText('Charge 2 target'), { key: 'Enter' });
    fireEvent.click(await screen.findByRole('option', { name: 'A — Pipe' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save draft costs' }));
    await waitFor(() => expect(inventoryManagementService.saveReceiptLandedCost).toHaveBeenCalledWith(expect.objectContaining({
      costItems: [expect.objectContaining({ amount: 300, allocationMethod: 'ByValue' }),
        expect.objectContaining({ amount: 50, purchaseOrderItemId: 'line-a', allocationMethod: 'ByQuantity' })]
    }), undefined));
  });
  it('blocks missing amounts and keeps the form open', async () => {
    await open(); fireEvent.click(screen.getByRole('button', { name: 'Save draft costs' }));
    expect(screen.getByRole('alert')).toHaveTextContent('positive amount');
    expect(inventoryManagementService.saveReceiptLandedCost).not.toHaveBeenCalled();
  });
  it('keeps values and displays server detail on a failed save', async () => {
    vi.mocked(inventoryManagementService.saveReceiptLandedCost).mockRejectedValue({ response: { data: { detail: 'Receipt line is not eligible.', extensions: { code: 'COST_LINE' } } } });
    await open(); fireEvent.change(screen.getByLabelText('Charge 1 amount'), { target: { value: '300' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft costs' }));
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Receipt line is not eligible.'));
    expect(screen.getByLabelText('Charge 1 amount')).toHaveValue(300);
  });
  it('cannot edit a posted voucher', async () => {
    render(<ReceiptLandedCostEntry receipt={receipt} selected={{ ...saved, status: 'Posted' }} onSaved={vi.fn()} />);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Add receipt costs' })).toBeEnabled());
    expect(screen.queryByRole('button', { name: 'Edit draft costs' })).not.toBeInTheDocument();
  });
  it('cancel does not save', async () => {
    await open(); fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(inventoryManagementService.saveReceiptLandedCost).not.toHaveBeenCalled();
  });
  it('blocks repeated clicks while saving', async () => {
    let finish!: (value: typeof saved) => void;
    vi.mocked(inventoryManagementService.saveReceiptLandedCost).mockImplementation(() => new Promise(resolve => { finish = resolve; }));
    await open(); fireEvent.change(screen.getByLabelText('Charge 1 amount'), { target: { value: '300' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft costs' }));
    expect(screen.getByRole('button', { name: 'Saving…' })).toBeDisabled();
    expect(inventoryManagementService.saveReceiptLandedCost).toHaveBeenCalledTimes(1);
    await act(async () => finish(saved));
  });
});
