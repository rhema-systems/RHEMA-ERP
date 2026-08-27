import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { accountsPayableService } from './accountsPayableService';

vi.mock('./api.service', () => ({
  apiService: {
    get: vi.fn(),
  },
}));

describe('accounts payable budget-cell client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('requests the exact dated Finance budget cells for the selected expense account', async () => {
    vi.mocked(apiService.get).mockResolvedValueOnce([]);

    await accountsPayableService.getInvoiceBudgetCells(
      '2026-07-05',
      'expense/account'
    );

    expect(apiService.get).toHaveBeenCalledWith(
      '/ap/invoices/budget-cells?budgetDate=2026-07-05&accountId=expense%2Faccount'
    );
  });
});
