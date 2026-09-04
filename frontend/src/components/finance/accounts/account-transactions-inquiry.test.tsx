import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AccountTransactionsInquiry } from './account-transactions-inquiry';
import { financeDataService } from '@/services/finance/finance-data.service';

const auth = vi.hoisted(() => ({
    isLoading: false,
    error: null as Error | null,
    canRead: true,
}));

vi.mock('@/hooks/use-auth', () => ({
    useAuth: () => ({
        isLoading: auth.isLoading,
        error: auth.error,
        hasPermission: (permission: string) => permission === 'Finance.Read' && auth.canRead,
    }),
}));

vi.mock('@/services/finance/finance-data.service', () => ({
    financeDataService: {
        getAccountingBooks: vi.fn(),
        getAccountTransactions: vi.fn(),
    },
}));

const books = [
    { id: 'book-a', tenantId: 'tenant', code: 'BOOK-A', name: 'Primary', purpose: '', isActive: true, isDefault: true, allowsPosting: true, isSystemDefined: false, sortOrder: 1 },
    { id: 'book-b', tenantId: 'tenant', code: 'BOOK-B', name: 'Statutory', purpose: '', isActive: true, isDefault: false, allowsPosting: true, isSystemDefined: false, sortOrder: 2 },
];

const mappings = books.map((book, index) => ({
    id: `mapping-${index}`,
    accountId: 'account-1',
    accountingBookId: book.id,
    accountingBookCode: book.code,
    accountingBookName: book.name,
    accountingBookIsDefault: book.isDefault,
    isEnabled: true,
    isMigrationReady: true,
    rowVersion: '',
}));

const page = (code: string, description = `${code} row`) => ({
    items: [{
        id: `${code}-line`, journalEntryId: `${code}-journal`, journalEntryNumber: `JE-${code}`,
        transactionDate: '2026-09-01', postingDate: '2026-09-02', reference: 'REF-1',
        journalDescription: 'Journal description', lineDescription: description,
        debitAmount: 125, creditAmount: 0, functionalCurrencyCode: 'GHS',
        transactionCurrencyCode: 'USD', transactionDebitAmount: 10, transactionCreditAmount: null,
        foreignAmount: 10, exchangeRateId: 'rate-1', exchangeRate: 12.5,
        exchangeRateSource: 'BoG', exchangeRateDate: '2026-09-01', accountingBookCode: code,
        accountingBookName: code === 'BOOK-A' ? 'Primary' : 'Statutory', postingEventId: 'event-1',
        sourceModule: 'AP', originModuleCode: 'AP', sourceDocumentId: 'invoice-1',
        sourceDocumentType: 'SupplierInvoice', sourceReference: 'INV-1', lineNumber: 2,
        financeDimensionSetId: 'set-1', financeDimensionSnapshotId: 'snapshot-1',
        dimensionDisplayValue: 'DEPT: OPS',
        dimensions: [{ definitionId: 'def-1', valueId: 'value-1', dimensionCode: 'DEPT', dimensionName: 'Department', valueCode: 'OPS', valueName: 'Operations' }],
    }],
    totalCount: 11, page: 1, pageSize: 10, totalPages: 2,
    accountingBookCode: code, accountingBookName: code === 'BOOK-A' ? 'Primary' : 'Statutory',
});

beforeEach(() => {
    auth.isLoading = false;
    auth.error = null;
    auth.canRead = true;
    window.localStorage.clear();
    vi.clearAllMocks();
    vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue(books);
    vi.mocked(financeDataService.getAccountTransactions).mockResolvedValue(page('BOOK-A'));
});

describe('AccountTransactionsInquiry', () => {
    it('distinguishes authorization loading and permission denial without making requests', () => {
        auth.isLoading = true;
        const view = render(<AccountTransactionsInquiry accountId="account-1" accountBooks={mappings} />);
        expect(screen.getByText('Checking Finance access…')).not.toBeNull();
        view.unmount();

        auth.isLoading = false;
        auth.canRead = false;
        render(<AccountTransactionsInquiry accountId="account-1" accountBooks={mappings} />);
        expect(screen.getByText('Permission denied')).not.toBeNull();
        expect(financeDataService.getAccountingBooks).not.toHaveBeenCalled();
        expect(financeDataService.getAccountTransactions).not.toHaveBeenCalled();
    });

    it('fails closed for missing membership and ambiguous book setup', async () => {
        const view = render(<AccountTransactionsInquiry accountId="account-1" accountBooks={[]} />);
        expect(await screen.findByText('Accounting-book setup required')).not.toBeNull();
        view.unmount();

        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue(books.map(book => ({ ...book, isDefault: false })));
        render(<AccountTransactionsInquiry accountId="account-1" accountBooks={mappings} />);
        expect(await screen.findByText('Select an accounting book')).not.toBeNull();
        expect(financeDataService.getAccountTransactions).not.toHaveBeenCalled();
    });

    it('shows exact-book evidence, per-row currencies, dimensions, drill-down, and pagination', async () => {
        render(<AccountTransactionsInquiry accountId="account-1" accountBooks={mappings} />);
        expect(await screen.findByText('BOOK-A row')).not.toBeNull();
        expect(screen.getByText('GHS 125.00')).not.toBeNull();
        expect(screen.getByText('USD 10.00')).not.toBeNull();
        expect(screen.getByText(/Rate 12.5 · BoG/)).not.toBeNull();
        expect(screen.getByText('DEPT: OPS')).not.toBeNull();
        expect(screen.getByRole('link', { name: 'JE-BOOK-A' }).getAttribute('href')).toBe('/finance/journal-entries/BOOK-A-journal');

        fireEvent.click(screen.getByRole('button', { name: 'Next' }));
        await waitFor(() => expect(financeDataService.getAccountTransactions).toHaveBeenLastCalledWith('account-1', 'BOOK-A', 2, 10));
    });

    it('keeps an older book request from replacing the selected book rows', async () => {
        let resolveBookA!: (value: ReturnType<typeof page>) => void;
        const bookARequest = new Promise<ReturnType<typeof page>>(resolve => { resolveBookA = resolve; });
        vi.mocked(financeDataService.getAccountTransactions).mockImplementation((_account, code) =>
            code === 'BOOK-A' ? bookARequest : Promise.resolve(page('BOOK-B')));

        render(<AccountTransactionsInquiry accountId="account-1" accountBooks={mappings} />);
        const selector = await screen.findByLabelText('Accounting book');
        await waitFor(() => expect(financeDataService.getAccountTransactions).toHaveBeenCalledWith('account-1', 'BOOK-A', 1, 10));
        fireEvent.change(selector, { target: { value: 'BOOK-B' } });
        expect(await screen.findByText('BOOK-B row')).not.toBeNull();
        resolveBookA(page('BOOK-A'));
        await waitFor(() => expect(screen.queryByText('BOOK-A row')).toBeNull());
    });

    it('does not turn request failure into an empty state and supports retry', async () => {
        vi.mocked(financeDataService.getAccountTransactions)
            .mockRejectedValueOnce(new Error('network'))
            .mockResolvedValueOnce({ ...page('BOOK-A'), items: [], totalCount: 0, totalPages: 0 });
        render(<AccountTransactionsInquiry accountId="account-1" accountBooks={mappings} />);
        expect(await screen.findByText('Transactions unavailable')).not.toBeNull();
        expect(screen.queryByText('No posted transactions')).toBeNull();
        fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
        expect(await screen.findByText('No posted transactions')).not.toBeNull();
    });
});
