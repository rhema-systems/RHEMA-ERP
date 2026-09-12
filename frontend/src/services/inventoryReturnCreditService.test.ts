import { beforeEach, expect, it, vi } from 'vitest';
const calls = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('./api.service', () => ({ apiService: calls }));
import { inventoryReturnCreditService } from './inventoryReturnCreditService';
beforeEach(() => vi.clearAllMocks());
it('scopes both credit reads to this Inventory return through the canonical Finance API', async () => {
  await inventoryReturnCreditService.getNotes('return-id');
  expect(calls.get).toHaveBeenCalledWith('/ap/supplier-debit-notes', { inventoryPurchaseReturnId: 'return-id' });
  await inventoryReturnCreditService.getSources('return-id');
  expect(calls.get).toHaveBeenCalledWith('/ap/supplier-debit-notes/inventory-returns/return-id/source-invoices');
});
it('creates only a credit draft and never calls stock dispatch or posting', async () => {
  const request = { originalVendorInvoiceId: 'invoice-id', supplierCreditNoteReference: 'SCN-1', creditDate: '2026-09-12' };
  await inventoryReturnCreditService.create('return-id', request);
  expect(calls.post).toHaveBeenCalledExactlyOnceWith('/ap/supplier-debit-notes/inventory-returns/return-id/credit', request);
});
