import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { financeDataService } from './finance-data.service';

vi.mock('@/services/api.service', () => ({
  apiService: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

describe('finance governed opening client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the typed Finance options and source-specific creation routes', async () => {
    vi.mocked(apiService.get).mockResolvedValueOnce({ blockers: [] });
    vi.mocked(apiService.post).mockResolvedValue({ id: 'batch-1' });

    await financeDataService.getGovernedOpeningBalanceOptions({
      openingDate: '2025-01-01',
      fiscalPeriodId: 'period-1',
      bookClassification: 'IFRS',
    });
    const bank = {
      sourceReference: 'BANK-SCHEDULE-2025',
      openingDate: '2025-01-01',
      fiscalPeriodId: 'period-1',
      bookClassification: 'IFRS',
      bankAccountId: 'bank-1',
      amount: 750000,
    };
    const residual = {
      sourceReference: 'RESIDUAL-SCHEDULE-2025',
      openingDate: '2025-01-01',
      fiscalPeriodId: 'period-1',
      bookClassification: 'IFRS',
      accruedExpensesAccountId: 'accrued-1',
      accruedExpensesAmount: 20000,
      shareCapitalAccountId: 'capital-1',
      shareCapitalAmount: 800000,
      retainedEarningsAmount: 380000,
    };
    await financeDataService.createBankAccountOpeningBalance(bank);
    await financeDataService.createResidualGlEquityOpeningBalance(residual);

    expect(apiService.get).toHaveBeenCalledWith(
      '/finance/opening-balances/governed-options?openingDate=2025-01-01&fiscalPeriodId=period-1&bookClassification=IFRS'
    );
    expect(apiService.post).toHaveBeenNthCalledWith(
      1,
      '/finance/opening-balances/bank-accounts',
      bank
    );
    expect(apiService.post).toHaveBeenNthCalledWith(
      2,
      '/finance/opening-balances/residual-gl-equity',
      residual
    );
  });

  it('consumes the Inventory-owned opening readiness and create boundary without account IDs', async () => {
    vi.mocked(apiService.get).mockResolvedValueOnce({
      isReady: false,
      blockers: ['No eligible items.'],
    });
    vi.mocked(apiService.post).mockResolvedValueOnce({
      id: 'adjustment-1',
      adjustmentNumber: 'ADJ-1',
    });
    const request = {
      warehouseId: 'warehouse-1',
      openingDate: '2025-01-01',
      bookClassification: 'IFRS',
      sourceScheduleReference: 'INVENTORY-SCHEDULE-2025',
      description: 'Approved opening stock',
      items: [
        {
          inventoryItemId: 'item-1',
          locationId: 'location-1',
          quantity: 10,
          unitCost: 18,
        },
      ],
    };

    await financeDataService.getOpeningStockOptions();
    await financeDataService.createOpeningStockAdjustment(request);

    expect(apiService.get).toHaveBeenCalledWith(
      '/inventory/adjustments/opening-stock/options'
    );
    expect(apiService.post).toHaveBeenCalledWith(
      '/inventory/adjustments/opening-stock',
      request
    );
    expect(JSON.stringify(request)).not.toContain('accountId');
  });
});
