import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AccountingBooksSettingsPage from './page';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { AccountingBook } from '@/types/finance';

let permissions = new Set<string>();
let authLoading = false;
let authError: Error | null = null;

vi.mock('@/hooks/use-auth', () => ({
    useAuth: () => ({
        isLoading: authLoading,
        error: authError,
        hasPermission: (permission: string) => permissions.has(permission),
    }),
}));

vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));

vi.mock('@/services/finance/finance-data.service', () => ({
    financeDataService: {
        getAccountingBooks: vi.fn(),
        getCurrencies: vi.fn(),
        getAccountingBook: vi.fn(),
        createAccountingBook: vi.fn(),
        updateAccountingBook: vi.fn(),
        requestAccountingBookTransition: vi.fn(),
        approveAccountingBookTransition: vi.fn(),
        rejectAccountingBookTransition: vi.fn(),
    },
}));

const primaryBook: AccountingBook = {
    id: 'book-primary', tenantId: 'tenant-a', code: 'IFRS', name: 'IFRS Primary',
    description: 'Primary ledger', purpose: 'IFRS reporting', bookType: 'PrimaryFull',
    lifecycleStatus: 'Active', functionalCurrencyCode: 'GHS', effectiveFromUtc: '2026-01-01T00:00:00Z',
    isActive: true, isDefault: true, allowsPosting: true, isSystemDefined: true, sortOrder: 1,
    hasAccountingUse: true, activationReady: false, readinessMessage: 'C4 readiness is unavailable.', rowVersion: 'AQID',
};

const initializingBook: AccountingBook = {
    ...primaryBook,
    id: 'book-local', code: 'LOCAL_STATUTORY', name: 'Local Statutory', purpose: 'Local statutory reporting',
    bookType: 'ParallelFull', lifecycleStatus: 'Initializing', isActive: false, isDefault: false,
    allowsPosting: false, isSystemDefined: false, hasAccountingUse: false, rowVersion: 'BAUG',
};

describe('accounting book settings', () => {
    beforeEach(() => {
        vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
        Element.prototype.scrollIntoView = vi.fn();
        permissions = new Set(['Finance.Read']);
        authLoading = false;
        authError = null;
        vi.clearAllMocks();
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([primaryBook]);
        vi.mocked(financeDataService.getCurrencies).mockResolvedValue([{ currencyCode: 'GHS', currencyName: 'Ghanaian Cedi', isActive: true }, { currencyCode: 'USD', currencyName: 'US Dollar', isActive: true }] as never);
        vi.mocked(financeDataService.getAccountingBook).mockResolvedValue(primaryBook);
    });

    it('shows auth loading and permission-denied states without issuing a data request', () => {
        authLoading = true;
        const { rerender } = render(<AccountingBooksSettingsPage />);
        expect(screen.getByLabelText('Loading accounting books')).toBeInTheDocument();

        authLoading = false;
        permissions.clear();
        rerender(<AccountingBooksSettingsPage />);
        expect(screen.getByText('Permission required')).toBeInTheDocument();
        expect(financeDataService.getAccountingBooks).not.toHaveBeenCalled();
    });

    it('distinguishes a request error from true empty and supports Retry', async () => {
        vi.mocked(financeDataService.getAccountingBooks)
            .mockRejectedValueOnce(new Error('network unavailable'))
            .mockResolvedValueOnce([]);

        render(<AccountingBooksSettingsPage />);
        expect(await screen.findByText('Could not load accounting books')).toBeInTheDocument();
        expect(screen.queryByText('No accounting books are configured.')).not.toBeInTheDocument();

        fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
        await waitFor(() => expect(financeDataService.getAccountingBooks).toHaveBeenCalledTimes(2));
        expect(await screen.findByText('No accounting books are configured.')).toBeInTheDocument();
    });

    it('keeps a Finance.Read user read-only while preserving detail loading', async () => {
        render(<AccountingBooksSettingsPage />);
        expect(await screen.findByText('IFRS — IFRS Primary')).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'New accounting book' })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Request transition' })).not.toBeInTheDocument();

        fireEvent.click(screen.getByRole('button', { name: 'View details' }));
        await waitFor(() => expect(financeDataService.getAccountingBook).toHaveBeenCalledWith('book-primary'));
        expect(await screen.findByText('Accounting use')).toBeInTheDocument();
        expect(screen.getByText('Yes — structure locked')).toBeInTheDocument();
    });

    it('separates manage and transition permissions and shows current C4 readiness', async () => {
        permissions.add('Finance.AccountingBooks.Manage');
        permissions.add('Finance.AccountingBooks.Transitions.Request');
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([initializingBook]);

        render(<AccountingBooksSettingsPage />);
        expect(await screen.findByRole('button', { name: 'New accounting book' })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument();
        fireEvent.click(screen.getByRole('button', { name: 'Request transition' }));
        expect(await screen.findByText('Activation readiness required')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Submit for approval' })).toBeDisabled();
        expect(financeDataService.requestAccountingBookTransition).not.toHaveBeenCalled();
    });

    it('creates a canonical full-book request without exposing lifecycle shortcuts', async () => {
        permissions.add('Finance.AccountingBooks.Manage');
        vi.mocked(financeDataService.createAccountingBook).mockResolvedValue(initializingBook);

        render(<AccountingBooksSettingsPage />);
        fireEvent.click(await screen.findByRole('button', { name: 'New accounting book' }));
        fireEvent.change(screen.getByLabelText('Stable code'), { target: { value: 'tax_book' } });
        fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Tax Book' } });
        fireEvent.change(screen.getByLabelText('Accounting purpose / principle'), { target: { value: 'Tax basis' } });
        fireEvent.click(screen.getByRole('combobox', { name: 'Functional currency' }));
        fireEvent.change(screen.getByPlaceholderText('Search code or name…'), { target: { value: 'Ghanaian' } });
        fireEvent.click(screen.getByText('GHS — Ghanaian Cedi'));
        fireEvent.click(screen.getByRole('button', { name: 'Save book' }));

        await waitFor(() => expect(financeDataService.createAccountingBook).toHaveBeenCalledWith(expect.objectContaining({
            code: 'TAX_BOOK',
            name: 'Tax Book',
            purpose: 'Tax basis',
            bookType: 'ParallelFull',
            functionalCurrencyCode: 'GHS',
            baseAccountingBookId: null,
        })));
        expect(screen.queryByLabelText('Lifecycle state')).not.toBeInTheDocument();
    });

    it('locks structural fields after accounting use while leaving display text editable', async () => {
        permissions.add('Finance.AccountingBooks.Manage');
        render(<AccountingBooksSettingsPage />);
        fireEvent.click(await screen.findByRole('button', { name: 'Edit' }));

        expect(screen.getByText('Structural identity locked')).toBeInTheDocument();
        expect(screen.getByLabelText('Stable code')).toBeDisabled();
        expect(screen.getByLabelText('Book type')).toHaveAttribute('data-disabled');
        expect(screen.getByLabelText('Accounting purpose / principle')).toBeDisabled();
        expect(screen.getByLabelText('Name')).not.toBeDisabled();
    });

    it.each(['Initializing', 'Active', 'Suspended'] as const)(
        'locks structural fields from lifecycle status alone when the book is %s',
        async lifecycleStatus => {
            permissions.add('Finance.AccountingBooks.Manage');
            vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([{
                ...initializingBook,
                lifecycleStatus,
                hasAccountingUse: false,
                initializationStartedAtUtc: null,
            }]);

            render(<AccountingBooksSettingsPage />);
            fireEvent.click(await screen.findByRole('button', { name: 'Edit' }));

            expect(screen.getByText('Structural identity locked')).toBeInTheDocument();
            expect(screen.getByLabelText('Stable code')).toBeDisabled();
            expect(screen.getByLabelText('Book type')).toHaveAttribute('data-disabled');
            expect(screen.getByLabelText('Accounting purpose / principle')).toBeDisabled();
            expect(screen.getByLabelText('Effective from')).toBeDisabled();
            expect(screen.getByLabelText('Name')).not.toBeDisabled();
            expect(screen.getByLabelText('Display order')).not.toBeDisabled();
        },
    );

    it('does not offer editing for a retired book', async () => {
        permissions.add('Finance.AccountingBooks.Manage');
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([{
            ...initializingBook,
            lifecycleStatus: 'Retired',
            hasAccountingUse: false,
            initializationStartedAtUtc: null,
        }]);

        render(<AccountingBooksSettingsPage />);
        expect(await screen.findByText('LOCAL_STATUTORY — Local Statutory')).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
    });

    it('submits an allowed transition with mandatory reason and rowversion', async () => {
        permissions.add('Finance.AccountingBooks.Transitions.Request');
        const draftBook = { ...initializingBook, lifecycleStatus: 'Draft' as const };
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([draftBook]);
        vi.mocked(financeDataService.requestAccountingBookTransition).mockResolvedValue({
            ...draftBook, pendingLifecycleStatus: 'Configuring', pendingTransitionReason: 'Begin governed setup',
        });

        render(<AccountingBooksSettingsPage />);
        fireEvent.click(await screen.findByRole('button', { name: 'Request transition' }));
        fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Begin governed setup' } });
        fireEvent.click(screen.getByRole('button', { name: 'Submit for approval' }));

        await waitFor(() => expect(financeDataService.requestAccountingBookTransition).toHaveBeenCalledWith('book-local', {
            targetStatus: 'Configuring',
            reason: 'Begin governed setup',
            rowVersion: 'BAUG',
        }));
    });

    it('shows pending lifecycle evidence only to an independent approval grant', async () => {
        const pending = {
            ...initializingBook,
            pendingLifecycleStatus: 'Suspended' as const,
            pendingTransitionReason: 'Pause configuration for review',
        };
        permissions.add('Finance.AccountingBooks.Manage');
        permissions.add('Finance.AccountingBooks.Transitions.Approve');
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([pending]);

        render(<AccountingBooksSettingsPage />);
        expect(await screen.findByText('Pending Suspended')).toBeInTheDocument();
        expect(screen.getByText('Pause configuration for review')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Reject' })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Edit' })).toBeDisabled();
        expect(screen.queryByRole('button', { name: 'Request transition' })).not.toBeInTheDocument();

        fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
        fireEvent.change(screen.getByLabelText('Checker reason'), { target: { value: 'Independent evidence reviewed' } });
        fireEvent.click(screen.getByRole('button', { name: 'Approve transition' }));
        await waitFor(() => expect(financeDataService.approveAccountingBookTransition).toHaveBeenCalledWith('book-local', {
            reason: 'Independent evidence reviewed',
            rowVersion: 'BAUG',
        }));
    });

    it('does not let a checker approve Active before C4 readiness', async () => {
        permissions.add('Finance.AccountingBooks.Transitions.Approve');
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([{
            ...initializingBook,
            pendingLifecycleStatus: 'Active',
            pendingTransitionReason: 'Attempt activation',
            activationReady: false,
        }]);

        render(<AccountingBooksSettingsPage />);
        expect(await screen.findByText(/C4 readiness is unavailable/)).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
        expect(screen.getByRole('button', { name: 'Reject' })).toBeEnabled();
    });

    it('does not treat an action permission as a substitute for Finance.Read', () => {
        permissions = new Set(['Finance.AccountingBooks.Manage', 'Finance.AccountingBooks.Transitions.Request', 'Finance.AccountingBooks.Transitions.Approve']);
        render(<AccountingBooksSettingsPage />);
        expect(screen.getByText('Permission required')).toBeInTheDocument();
        expect(financeDataService.getAccountingBooks).not.toHaveBeenCalled();
    });
});
