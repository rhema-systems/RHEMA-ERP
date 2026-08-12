import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { accountsPayableService } from './accountsPayableService';

vi.mock('./api.service', () => ({
    apiService: {
        get: vi.fn(),
        post: vi.fn(),
        put: vi.fn(),
    },
}));

describe('accounts payable accepted-supply client', () => {
    beforeEach(() => vi.clearAllMocks());

    it('loads tenant-authenticated category-aware options for the selected PO', async () => {
        vi.mocked(apiService.get).mockResolvedValueOnce({
            purchaseOrderId: 'po/id',
            category: 'TechnicalServices',
            ready: false,
            options: [],
            blockedReasons: [],
            worksHandoffRoute: '/quantity-survey/payment-certificates',
        });

        await accountsPayableService.getAcceptedSupplyOptions('po/id');

        expect(apiService.get).toHaveBeenCalledWith(
            '/ap/invoices/accepted-supply-options?purchaseOrderId=po%2Fid'
        );
    });
});
