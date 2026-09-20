import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import {
  purchaseReceiptDistributionService,
  type SavePurchaseReceiptDistribution,
} from './purchaseReceiptDistributionService';

vi.mock('./api.service', () => ({
  apiService: { get: vi.fn(), put: vi.fn(), post: vi.fn() },
}));

describe('receipt-scoped distribution API', () => {
  beforeEach(() => vi.clearAllMocks());
  it('loads a receipt view and its posting-account lookup without broad Finance access', async () => {
    await purchaseReceiptDistributionService.get('receipt');
    await purchaseReceiptDistributionService.accounts('receipt');
    expect(apiService.get).toHaveBeenNthCalledWith(
      1,
      '/PurchaseOrderReceipts/receipt/distribution'
    );
    expect(apiService.get).toHaveBeenNthCalledWith(
      2,
      '/PurchaseOrderReceipts/receipt/distribution/accounts'
    );
  });
  it('passes source lineage and optimistic concurrency tokens unchanged on save', async () => {
    const input: SavePurchaseReceiptDistribution = {
      version: 'v1',
      basisVersion: 'b1',
      lines: [
        {
          lineId: 'line',
          inventoryItemId: 'item',
          purpose: 'Inventory',
          accountId: 'account',
          debit: 25.5,
          credit: 0,
        },
      ],
    };
    await purchaseReceiptDistributionService.save('receipt', input);
    expect(apiService.put).toHaveBeenCalledWith(
      '/PurchaseOrderReceipts/receipt/distribution',
      input
    );
  });
  it('resets only the selected receipt with both version tokens', async () => {
    await purchaseReceiptDistributionService.reset('receipt', {
      version: 'v1',
      basisVersion: 'b1',
    });
    expect(apiService.post).toHaveBeenCalledWith(
      '/PurchaseOrderReceipts/receipt/distribution/reset',
      { version: 'v1', basisVersion: 'b1' }
    );
  });
});
