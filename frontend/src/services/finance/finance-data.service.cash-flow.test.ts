import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { financeDataService } from './finance-data.service';

vi.mock('@/services/api.service', () => ({
  apiService: {
    get: vi.fn(),
  },
}));

describe('finance cash-flow client', () => {
  beforeEach(() => vi.clearAllMocks());

  it.each(['Direct', 'Indirect'] as const)(
    'sends the selected %s method to the report endpoint',
    async (method) => {
      vi.mocked(apiService.get).mockResolvedValueOnce({ method });

      await financeDataService.getCashFlowStatement({
        periodStart: '2026-01-01',
        periodEnd: '2026-08-23',
        bookClassification: 'IFRS',
        includeAccountDetails: true,
        method,
      });

      expect(apiService.get).toHaveBeenCalledWith(
        `/finance/statements/cash-flow?periodStart=2026-01-01&periodEnd=2026-08-23&bookClassification=IFRS&includeAccountDetails=true&method=${method}`
      );
    }
  );
});
