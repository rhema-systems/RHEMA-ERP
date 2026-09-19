import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import type { SaveAccountClassification } from '@/types/finance';
import { financeDataService } from './finance-data.service';

vi.mock('@/services/api.service', () => ({
    apiService: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}));

const request: SaveAccountClassification = {
    accountingBookId: 'book-1', parentClassificationId: 'root-1', code: 'CASH', name: 'Cash',
    coreAccountType: 'Asset', defaultRevaluationTreatment: 'Include', systemRole: 'Cash',
    isPostingClassification: true, status: 'Active', displayOrder: 10, rowVersion: 'AQID',
};

describe('finance classification client', () => {
    beforeEach(() => vi.clearAllMocks());

    it('loads inactive hierarchy nodes for one accounting book', async () => {
        vi.mocked(apiService.get).mockResolvedValueOnce([]);
        await financeDataService.getAccountClassifications('book-1', true);
        expect(apiService.get).toHaveBeenCalledWith('/finance/account-classifications?accountingBookId=book-1&includeInactive=true');
    });

    it('round-trips the row version on update and retirement', async () => {
        vi.mocked(apiService.put).mockResolvedValueOnce({});
        vi.mocked(apiService.post).mockResolvedValueOnce({});
        await financeDataService.updateAccountClassification('classification-1', request);
        await financeDataService.retireAccountClassification('classification-1', 'Superseded hierarchy.', 'AQID');
        expect(apiService.put).toHaveBeenCalledWith('/finance/account-classifications/classification-1', request);
        expect(apiService.post).toHaveBeenCalledWith('/finance/account-classifications/classification-1/retire', {
            reason: 'Superseded hierarchy.', rowVersion: 'AQID',
        });
    });

    it('uses the tenant-protected where-used route', async () => {
        vi.mocked(apiService.get).mockResolvedValueOnce({ mappings: [] });
        await financeDataService.getAccountClassificationWhereUsed('classification-1');
        expect(apiService.get).toHaveBeenCalledWith('/finance/account-classifications/classification-1/where-used');
    });
});
