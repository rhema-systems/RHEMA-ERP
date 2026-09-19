import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { financeDataService } from './finance-data.service';

vi.mock('@/services/api.service', () => ({
    apiService: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}));

describe('book-scoped FX revaluation policy client', () => {
    beforeEach(() => vi.clearAllMocks());

    it('loads effective policies for the account', async () => {
        vi.mocked(apiService.get).mockResolvedValueOnce([]);
        await financeDataService.getAccountBookCurrencyPolicies('account-1');
        expect(apiService.get).toHaveBeenCalledWith('/finance/accounts/account-1/revaluation-policies');
    });

    it('saves the nullable override against an exact currency link and accounting book', async () => {
        vi.mocked(apiService.put).mockResolvedValueOnce({});
        const request = {
            revaluationOverride: null,
            reason: 'Return to the reviewed classification default',
            confirmNonstandardInclusion: false,
            rowVersion: 'AQID',
        };
        await financeDataService.saveAccountBookCurrencyPolicy('account-1', 'link-1', 'book-1', request);
        expect(apiService.put).toHaveBeenCalledWith(
            '/finance/accounts/account-1/revaluation-policies/currency-links/link-1/books/book-1',
            request,
        );
    });

    it.each(['approve', 'reject'] as const)('sends %s through the governed decision route', async (decision) => {
        vi.mocked(apiService.post).mockResolvedValueOnce({});
        const request = { reason: 'Independent review complete', rowVersion: 'AQID' };
        await financeDataService.decideAccountBookCurrencyPolicy('account-1', 'policy-1', decision, request);
        expect(apiService.post).toHaveBeenCalledWith(
            `/finance/accounts/account-1/revaluation-policies/policy-1/${decision}`,
            request,
        );
    });
});
