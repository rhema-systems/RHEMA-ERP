import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { PartnerStatementReport } from './PartnerStatementReport';

describe('PartnerStatementReport currency totals', () => {
    it('renders separate currency totals instead of adding unlike currencies', async () => {
        render(
            <PartnerStatementReport
                title="Customer Statements"
                description="Statement test"
                partnerLabel="Customer"
                partnerPluralLabel="Customers"
                currencyToggleLabel="Show customer currency"
                exportFilePrefix="customer-statement"
                partners={[]}
                loadReport={vi.fn().mockResolvedValue({
                    fromDate: '2025-01-01',
                    toDate: '2025-01-31',
                    currencyCode: 'Customer Currency',
                    totalOpeningBalance: 0,
                    totalDebits: 370_000,
                    totalCredits: 0,
                    totalClosingBalance: 370_000,
                    currencyTotals: [
                        { currencyCode: 'GHS', openingBalance: 0, totalDebits: 300_000, totalCredits: 0, closingBalance: 300_000 },
                        { currencyCode: 'USD', openingBalance: 0, totalDebits: 70_000, totalCredits: 0, closingBalance: 70_000 },
                    ],
                    warnings: [],
                    accounts: [],
                })}
                downloadCsv={vi.fn()}
            />
        );

        fireEvent.click(screen.getByRole('button', { name: 'Run Report' }));

        await waitFor(() => expect(screen.getByText('GHS totals')).toBeInTheDocument());
        expect(screen.getByText('USD totals')).toBeInTheDocument();
        expect(screen.getAllByText('GHS 300,000.00')).toHaveLength(2);
        expect(screen.getAllByText('$70,000.00')).toHaveLength(2);
        expect(screen.queryByText('370,000.00')).not.toBeInTheDocument();
    });
});
