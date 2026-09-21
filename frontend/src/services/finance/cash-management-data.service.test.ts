import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { cashManagementDataService } from './cash-management-data.service';

vi.mock('@/services/api.service', () => ({
    apiService: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
}));

describe('cash-management register queries', () => {
    beforeEach(() => vi.clearAllMocks());

    it('keeps the ordinary deposit register bounded and summary-only', async () => {
        vi.mocked(apiService.get).mockResolvedValueOnce([]);
        await cashManagementDataService.getBankDeposits();
        expect(apiService.get).toHaveBeenCalledWith('/finance/banking/deposits?limit=200');
    });

    it('requests allocation evidence explicitly for returned-cheque selection', async () => {
        vi.mocked(apiService.get).mockResolvedValueOnce([]);
        await cashManagementDataService.getBankDeposits('Posted', true, 500);
        expect(apiService.get).toHaveBeenCalledWith(
            '/finance/banking/deposits?status=Posted&includeAllocations=true&limit=500',
        );
    });
});
