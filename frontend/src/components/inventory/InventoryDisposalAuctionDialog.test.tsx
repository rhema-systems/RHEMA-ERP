import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
const mocks = vi.hoisted(() => ({ customers: vi.fn(), taxes: vi.fn(), create: vi.fn(), error: vi.fn(), success: vi.fn() }));
vi.mock('@/services/ar-service', () => ({ arService: { getCustomers: mocks.customers } }));
vi.mock('@/services/finance/tax-data.service', () => ({ taxDataService: { getActiveTaxGroups: mocks.taxes } }));
vi.mock('@/services/inventoryDisposalService', () => ({ inventoryDisposalService: { createAuctionInvoice: mocks.create } }));
vi.mock('sonner', () => ({ toast: { error: mocks.error, success: mocks.success } }));
vi.mock('@/components/finance/dimensions/source-document-dimension-panel', () => ({
  SourceDocumentDimensionDefaultsPanel: ({ onChange }: { onChange: (values: Record<string, string>) => void }) =>
    <button onClick={() => onChange({ DEPARTMENT: 'ESTATE' })}>Set auction dimensions</button>,
}));
import { InventoryDisposalAuctionDialog } from './InventoryDisposalAuctionDialog';
const disposal = { id: 'disposal', disposalNumber: 'DSP-01', currencyCode: 'GHS', rowVersion: 'original-version',
  lines: [{ id: 'source-line', itemCode: 'INV-01', itemName: 'Obsolete motor', quantity: 4, unitOfMeasure: 'EA' }] } as any;
async function choose(label: string, option: string) {
  fireEvent.click(screen.getByRole('combobox', { name: label }));
  fireEvent.click(await screen.findByRole('option', { name: option }));
}
beforeEach(() => {
  vi.clearAllMocks();
  mocks.customers.mockResolvedValue({ items: [{ id: 'customer', customerCode: 'C-01', customerName: 'Buyer' }] });
  mocks.taxes.mockResolvedValue([{ id: 'tax', name: 'Sales tax' }]);
  mocks.create.mockResolvedValue({ ...disposal, auctionInvoiceId: 'invoice' });
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', { configurable: true, value: vi.fn() });
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
async function prepare() {
  await waitFor(() => expect(mocks.customers).toHaveBeenCalled());
  await choose('Auction customer', 'C-01 — Buyer');
  fireEvent.change(screen.getByLabelText('Auction price INV-01'), { target: { value: '15.25' } });
}
it('requires an explicit invoice tax treatment and preserves disposal source line identity', async () => {
  const created = vi.fn();
  render(<InventoryDisposalAuctionDialog disposal={disposal} open onOpenChange={vi.fn()} onCreated={created} />);
  await prepare();
  expect(screen.getByRole('button', { name: 'Create invoice draft' })).toBeDisabled();
  await choose('Auction tax treatment', 'Standard');
  expect(screen.getByRole('button', { name: 'Create invoice draft' })).toBeDisabled();
  await choose('Auction tax group', 'Sales tax');
  fireEvent.click(screen.getByRole('button', { name: 'Create invoice draft' }));
  await waitFor(() => expect(created).toHaveBeenCalled());
  expect(mocks.create).toHaveBeenCalledWith(disposal, expect.objectContaining({ businessPartnerId: 'customer',
    lines: [{ disposalLineId: 'source-line', unitPrice: 15.25, taxTreatment: 1, taxGroupId: 'tax' }] }));
});
it('retains entered prices and the same retry key after a server failure', async () => {
  mocks.create.mockRejectedValueOnce({ response: { data: { detail: 'Posting profile is missing', code: 'PROFILE' } } });
  render(<InventoryDisposalAuctionDialog disposal={disposal} open onOpenChange={vi.fn()} onCreated={vi.fn()} />);
  await prepare(); await choose('Auction tax treatment', 'Exempt');
  fireEvent.click(screen.getByRole('button', { name: 'Create invoice draft' }));
  await waitFor(() => expect(mocks.error).toHaveBeenCalledWith('Posting profile is missing (PROFILE)'));
  expect(screen.getByLabelText('Auction price INV-01')).toHaveValue(15.25);
  fireEvent.click(screen.getByRole('button', { name: 'Create invoice draft' }));
  await waitFor(() => expect(mocks.create).toHaveBeenCalledTimes(2));
  expect(mocks.create.mock.calls[1][1].idempotencyKey).toBe(mocks.create.mock.calls[0][1].idempotencyKey);
});
it('rejects sub-cent unit prices instead of silently rounding Finance amounts', async () => {
  render(<InventoryDisposalAuctionDialog disposal={disposal} open onOpenChange={vi.fn()} onCreated={vi.fn()} />);
  await prepare(); await choose('Auction tax treatment', 'Exempt');
  fireEvent.change(screen.getByLabelText('Auction price INV-01'), { target: { value: '15.255' } });
  expect(screen.getByRole('button', { name: 'Create invoice draft' })).toBeDisabled();
  expect(mocks.create).not.toHaveBeenCalled();
});
it('includes dimension choices in the payload and changes the retry key only when the choices change', async () => {
  mocks.create.mockRejectedValue({ response: { data: { detail: 'Select the required Finance dimension.' } } });
  render(<InventoryDisposalAuctionDialog disposal={disposal} open onOpenChange={vi.fn()} onCreated={vi.fn()} />);
  await prepare(); await choose('Auction tax treatment', 'Exempt');
  fireEvent.click(screen.getByRole('button', { name: 'Create invoice draft' }));
  await waitFor(() => expect(mocks.error).toHaveBeenCalledTimes(1));
  fireEvent.click(screen.getByText('Set auction dimensions'));
  fireEvent.click(screen.getByRole('button', { name: 'Create invoice draft' }));
  await waitFor(() => expect(mocks.error).toHaveBeenCalledTimes(2));
  expect(mocks.create.mock.calls[1][1].financeDimensions).toEqual({
    defaultDimensions: [{ dimensionCode: 'DEPARTMENT', valueCode: 'ESTATE' }], lines: [], applyDefaultToEligibleLines: true,
  });
  expect(mocks.create.mock.calls[1][1].idempotencyKey).not.toBe(mocks.create.mock.calls[0][1].idempotencyKey);
  fireEvent.click(screen.getByRole('button', { name: 'Create invoice draft' }));
  await waitFor(() => expect(mocks.create).toHaveBeenCalledTimes(3));
  expect(mocks.create.mock.calls[2][1].idempotencyKey).toBe(mocks.create.mock.calls[1][1].idempotencyKey);
});
