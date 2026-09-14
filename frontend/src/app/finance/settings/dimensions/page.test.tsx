import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import TransactionDimensionSettingsPage from './page';
import { financeDataService } from '@/services/finance/finance-data.service';

let permissions = new Set<string>();

vi.mock('@/hooks/use-auth', () => ({
    useAuth: () => ({
        isLoading: false,
        hasPermission: (permission: string) => permissions.has(permission),
    }),
}));

vi.mock('@/services/finance/finance-data.service', () => ({
    financeDataService: { getFinanceDimensions: vi.fn() },
}));

describe('transaction dimension settings', () => {
    beforeEach(() => {
        permissions = new Set(['Finance.Read']);
        vi.mocked(financeDataService.getFinanceDimensions).mockReset();
    });

    it('shows an explicit permission-denied state without loading data', () => {
        permissions.clear();
        render(<TransactionDimensionSettingsPage />);
        expect(screen.getByText('Permission required')).toBeInTheDocument();
        expect(financeDataService.getFinanceDimensions).not.toHaveBeenCalled();
    });

    it('distinguishes request failure from empty state and retries', async () => {
        vi.mocked(financeDataService.getFinanceDimensions)
            .mockRejectedValueOnce(new Error('network unavailable'))
            .mockResolvedValueOnce([]);

        render(<TransactionDimensionSettingsPage />);
        expect(await screen.findByText('Could not load dimensions')).toBeInTheDocument();
        expect(screen.queryByText(/No transaction dimensions are configured/)).not.toBeInTheDocument();

        fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
        await waitFor(() => expect(financeDataService.getFinanceDimensions).toHaveBeenCalledTimes(2));
        expect(await screen.findByText(/No transaction dimensions are configured/)).toBeInTheDocument();
    });

    it('shows the catalog separately and requires the additional permission for configuration access', async () => {
        permissions.add('Finance.Dimensions.Manage');
        vi.mocked(financeDataService.getFinanceDimensions).mockResolvedValue([{
            id: 'department', code: 'DEPARTMENT', name: 'Department / Cost Centre',
            classification: 'Analytical', valueSourceType: 'Lookup', isActive: true,
            displayOrder: 1, values: [],
        }]);

        render(<TransactionDimensionSettingsPage />);
        expect(await screen.findByText('Department / Cost Centre')).toBeInTheDocument();
        expect(screen.getByText('Configuration access')).toBeInTheDocument();
        expect(screen.getByText(/They do not form part of a GL account number/)).toBeInTheDocument();
    });
});
