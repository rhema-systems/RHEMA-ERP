import React from 'react';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import Page from './page';

const mocks = vi.hoisted(() => ({ create: vi.fn(), price: vi.fn(), push: vi.fn() }));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mocks.push }), useSearchParams: () => new URLSearchParams() }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
// Test the page's source/stock/save wiring without Radix portal mechanics.
vi.mock('@/components/ui/select', () => ({
  Select: ({ value, onValueChange, disabled, children }: any) => <select value={value} disabled={disabled} onChange={event => onValueChange(event.target.value)}><option value="">Choose</option>{children}</select>,
  SelectTrigger: () => null,
  SelectValue: () => null,
  SelectContent: ({ children }: any) => <>{children}</>,
  SelectItem: ({ value, children }: any) => <option value={value}>{children}</option>,
}));
vi.mock('@/services/purchasingService', () => ({ purchasingService: {
  getPurchaseOrderSourceOptions: vi.fn().mockResolvedValue({ ready: true, sources: [{
    sourceType: 'Contract', sourceId: 'contract-1', sourceLabel: 'Contract: CTR-TEST',
    sourceReference: 'CTR-TEST', businessPartnerId: 'supplier-1', businessPartnerName: 'Test Supplier',
    purchaseRequisitionNumber: 'PR-TEST', currencyCode: 'GHS', approvedAmount: 38000,
    approvedLines: [{ sourceLineId: 'pvc-bid', inventoryItemId: null, itemCode: '', description: 'PVC Pipe 50mm', quantity: 20, unitOfMeasure: 'EACH', unitPrice: 1900, lineTotal: 38000 }],
  }] }),
  createPurchaseOrder: mocks.create,
} }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getInventoryItems: vi.fn().mockResolvedValue([{ id: 'pvc-stock', itemCode: 'SKU-001', name: 'PVC Pipe 50mm', description: 'Catalogue pipe description', unitOfMeasure: 'EACH', standardCost: 17 }]),
  getWarehouses: vi.fn().mockResolvedValue([{ id: 'store-1', code: 'MAIN', name: 'Main Store', isActive: true }]),
  getWarehouseItemsByInventoryItem: vi.fn().mockResolvedValue([{ warehouseId: 'store-1', inventoryItemId: 'pvc-stock' }]),
  getWarehouseItems: vi.fn().mockResolvedValue([{ warehouseId: 'store-1', inventoryItemId: 'pvc-stock' }]),
  getItemUnitsOfMeasure: vi.fn().mockResolvedValue([{ id: 'schedule-ea', unitOfMeasureId: '00000000-0000-0000-0000-000000000000', unitCode: 'EA', unitName: 'Each', conversionFactor: 1, isPurchaseUnit: true, isBaseUnit: true }]),
} }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: {
  getActivePartners: vi.fn().mockResolvedValue([{ id: 'supplier-1', partnerCode: 'SUP-TEST', partnerName: 'Test Supplier', partnerType: 'Supplier' }]),
  getPartnerById: vi.fn().mockResolvedValue({ id: 'supplier-1', partnerName: 'Test Supplier' }),
} }));
vi.mock('@/services/procurementSettingsService', () => ({ default: { getSettings: vi.fn().mockResolvedValue({ allowNonInventoryItems: false }) } }));
vi.mock('@/services/financeCommonService', () => ({ procurementCurrencyService: { getActive: vi.fn().mockResolvedValue([{ code: 'GHS', isActive: true, isBaseCurrency: true }]) } }));
vi.mock('@/services/pricingService', () => ({ default: { getSupplierItemPrice: mocks.price } }));

afterEach(() => { localStorage.clear(); vi.clearAllMocks(); });

describe('contract-first purchase order page', () => {
  it.each([1, 2, 3] as const)('saves a descriptive line of type %s without inventory or warehouse mapping', async lineType => {
    localStorage.setItem('user', JSON.stringify({ id: 'officer-1' }));
    mocks.create.mockResolvedValue({ id: 'po-test' });
    render(<Page />);
    const source = (await screen.findByRole('option', { name: /Contract: CTR-TEST/ })).closest('select')!;
    fireEvent.change(source, { target: { value: 'Contract:contract-1' } });
    const row = await screen.findByRole('row', { name: /PVC Pipe 50mm/ });
    fireEvent.keyDown(within(row).getByRole('button', { name: /Actions for/ }), { key: 'ArrowDown' });
    fireEvent.click(await screen.findByRole('menuitem', { name: 'Edit item' }));
    const editing = (await screen.findByRole('textbox', { name: 'Approved source unit' })).closest('tr')!;
    const typeSelect = within(editing).getByRole('option', { name: 'Service', exact: true }).closest('select')!;
    fireEvent.change(typeSelect, { target: { value: String(lineType) } });
    fireEvent.click(within(editing).getAllByRole('button')[0]);
    fireEvent.click(screen.getByRole('button', { name: 'Save as Draft' }));
    await waitFor(() => expect(mocks.create).toHaveBeenCalledOnce());
    expect(mocks.create.mock.calls[0][0].items[0]).toEqual(expect.objectContaining({
      inventoryItemId: undefined, warehouseId: undefined, lineType,
      itemDescription: 'PVC Pipe 50mm', orderedQuantity: 20, unitOfMeasure: 'EACH', unitPrice: 1900,
    }));
  }, 20000);
  it('loads approved lines, maps stock offering only EA, and saves the exact EACH terms', async () => {
    localStorage.setItem('user', JSON.stringify({ id: 'officer-1' }));
    mocks.create.mockResolvedValue({ id: 'po-test' });
    render(<Page />);
    const source = (await screen.findByRole('option', { name: /Contract: CTR-TEST/ })).closest('select')!;
    fireEvent.change(source, { target: { value: 'Contract:contract-1' } });
    const initialRow = await screen.findByRole('row', { name: /PVC Pipe 50mm/ });
    expect(within(initialRow).getByText('EACH')).toBeInTheDocument();
    fireEvent.keyDown(within(initialRow).getByRole('button', { name: /Actions for/ }), { key: 'ArrowDown' });
    fireEvent.click(await screen.findByRole('menuitem', { name: 'Edit item' }));
    expect(await screen.findByRole('textbox', { name: 'Approved source unit' })).toHaveValue('EACH');

    const editingRow = screen.getByRole('textbox', { name: 'Approved source unit' }).closest('tr')!;
    fireEvent.change(within(editingRow).getAllByRole('combobox')[1], { target: { value: 'pvc-stock' } });
    await waitFor(() => expect(within(editingRow).getAllByRole('combobox')[1]).toHaveValue('pvc-stock'));
    fireEvent.change(within(editingRow).getAllByRole('combobox')[2], { target: { value: 'store-1' } });
    await waitFor(() => expect(within(editingRow).getAllByRole('combobox')[2]).toHaveValue('store-1'));
    expect(screen.getByRole('textbox', { name: 'Approved source unit' })).toHaveValue('EACH');
    expect(screen.getByPlaceholderText('Description')).toHaveValue('PVC Pipe 50mm');
    expect(mocks.price).not.toHaveBeenCalled();
    fireEvent.click(within(editingRow).getAllByRole('button')[0]);
    const save = screen.getByRole('button', { name: 'Save as Draft' });
    await waitFor(() => expect(save).toBeEnabled());
    fireEvent.click(save);
    await waitFor(() => expect(mocks.create).toHaveBeenCalledOnce());
    expect(mocks.create).toHaveBeenCalledWith(expect.objectContaining({
      sourceType: 'Contract', sourceId: 'contract-1', supplierId: 'supplier-1',
      items: [expect.objectContaining({ inventoryItemId: 'pvc-stock', itemDescription: 'PVC Pipe 50mm', unitOfMeasure: 'EACH', itemUnitOfMeasureId: undefined, orderedQuantity: 20, unitPrice: 1900, warehouseId: 'store-1' })],
    }));
    expect(mocks.create.mock.calls[0][0].items[0]).not.toHaveProperty('approvedSourceLineId');
  }, 20000);
});
