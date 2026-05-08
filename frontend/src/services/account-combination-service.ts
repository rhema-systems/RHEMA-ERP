import {
    CombinationRequest,
    AccountCombinationPreview,
    BulkCreateAccountsRequest,
    BulkCreationResult
} from '@/types/finance';
import { apiService as api } from '@/services/api.service';

export const accountCombinationService = {
    /**
     * Generate preview of account combinations based on selected segments
     */
    generateCombinations: async (request: CombinationRequest): Promise<AccountCombinationPreview[]> => {
        const response = await api.post<AccountCombinationPreview[]>('/finance/accounts/combinations/preview', request);
        return response;
    },

    /**
     * Bulk create accounts from confirmed combinations
     */
    bulkCreateAccounts: async (request: BulkCreateAccountsRequest): Promise<BulkCreationResult> => {
        const response = await api.post<BulkCreationResult>('/finance/accounts/combinations/bulk-create', request);
        return response;
    }
};
