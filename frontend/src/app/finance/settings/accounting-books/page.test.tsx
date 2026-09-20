import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AccountingBooksSettingsPage from './page';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { AccountingBook, AccountingBookInitialization } from '@/types/finance';

let permissions = new Set<string>();
let authLoading = false;
let authError: Error | null = null;
let authUserId = 'checker-id';

vi.mock('@/hooks/use-auth', () => ({
    useAuth: () => ({
        user: { id: authUserId },
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
        getAccountingBookInitialization: vi.fn(),
        getAccountingBookActivationReadiness: vi.fn(),
        getAccountingBookPeriods: vi.fn(),
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

const approvedInitialization: AccountingBookInitialization = {
    id: 'init-1', accountingBookId: 'book-local', version: 1, accountingBookCode: 'LOCAL_STATUTORY',
    mode: 'BaseBookCopyAtCutoff', status: 'Approved', cutoffDate: '2025-12-31T00:00:00Z',
    cutoffFiscalPeriodId: 'period-2025-12', cutoffFiscalPeriodCode: '2025-12',
    sourceAccountingBookId: 'book-primary', sourceAccountingBookCode: 'IFRS', idempotencyKey: 'LOCAL-INIT-2025-12-31-V1',
    reason: 'Initialize the statutory book for FY2026.', totalDebits: 0, totalCredits: 0,
    requiredAccountCount: 1, coveredAccountCount: 1, isBalanced: true, isCoverageComplete: true,
    evidenceFingerprint: 'A'.repeat(64), reconciliationFingerprint: 'B'.repeat(64),
    preparedByUserId: 'maker-id', preparedByName: 'System Administrator', preparedAtUtc: '2026-09-19T10:00:00Z',
    approvedByUserId: 'checker-id', approvedByName: 'Abena Dapaah', approvedAtUtc: '2026-09-19T11:00:00Z',
    decidedByUserId: 'checker-id', decidedByName: 'Abena Dapaah', decidedAtUtc: '2026-09-19T11:00:00Z',
    decisionReason: 'Reconciliation evidence reviewed.', rowVersion: 'AQID',
    lines: [{ accountId: 'account-1000', accountNumber: 'DEFAULT-1000', accountName: 'Cash and Cash Equivalents', accountType: 'Asset', currencyCode: 'GHS', openingDebit: 0, openingCredit: 0, baseBookSignedBalance: 0, openingAdjustment: 0 }],
};

describe('accounting book settings', () => {
    beforeEach(() => {
        vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
        Element.prototype.scrollIntoView = vi.fn();
        permissions = new Set(['Finance.Read']);
        authLoading = false;
        authError = null;
        authUserId = 'checker-id';
        vi.clearAllMocks();
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([primaryBook]);
        vi.mocked(financeDataService.getCurrencies).mockResolvedValue([{ currencyCode: 'GHS', currencyName: 'Ghanaian Cedi', isActive: true }, { currencyCode: 'USD', currencyName: 'US Dollar', isActive: true }] as never);
        vi.mocked(financeDataService.getAccountingBook).mockResolvedValue(primaryBook);
        vi.mocked(financeDataService.getAccountingBookInitialization).mockResolvedValue(approvedInitialization);
        vi.mocked(financeDataService.getAccountingBookActivationReadiness).mockResolvedValue({ isReady: true, blockers: [], initializationFingerprint: 'A'.repeat(64), requiredPeriodCount: 1, readyPeriodCount: 1 });
        vi.mocked(financeDataService.getAccountingBookPeriods).mockResolvedValue([{ id: 'book-period-1', accountingBookId: 'book-local', accountingBookCode: 'LOCAL_STATUTORY', fiscalPeriodId: 'period-2026-01', fiscalPeriodCode: '2026-01', startDate: '2026-01-01', endDate: '2026-01-31', status: 'Open', rowVersion: 'AQID' }]);
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
        expect(screen.queryByRole('button', { name: 'Request lifecycle change' })).not.toBeInTheDocument();

        fireEvent.click(screen.getByRole('button', { name: 'View details' }));
        await waitFor(() => expect(financeDataService.getAccountingBook).toHaveBeenCalledWith('book-primary'));
        expect(await screen.findByText('Accounting use')).toBeInTheDocument();
        expect(screen.getByText('Yes — structure locked')).toBeInTheDocument();
    });

    it('directs an initializing book with incomplete evidence to readiness', async () => {
        permissions.add('Finance.AccountingBooks.Manage');
        permissions.add('Finance.AccountingBooks.Transitions.Request');
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([initializingBook]);

        render(<AccountingBooksSettingsPage />);
        expect(await screen.findByRole('button', { name: 'New accounting book' })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument();
        expect(screen.getByText('About book activation')).toBeInTheDocument();
        expect(screen.queryByText('Parallel posting remains disabled')).not.toBeInTheDocument();
        expect(screen.getByRole('link', { name: 'Complete activation readiness' })).toHaveAttribute('href', '/finance/settings/accounting-books/book-local/readiness');
        expect(screen.queryByRole('button', { name: 'Request Active' })).not.toBeInTheDocument();
        expect(financeDataService.requestAccountingBookTransition).not.toHaveBeenCalled();
    });

    it('names the next transition once activation evidence is ready', async () => {
        permissions.add('Finance.AccountingBooks.Transitions.Request');
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([{
            ...initializingBook, activationReady: true, readinessMessage: 'Ready for activation.',
        }]);

        render(<AccountingBooksSettingsPage />);
        fireEvent.click(await screen.findByRole('button', { name: 'Request Active' }));
        expect(await screen.findByText('Activation readiness confirmed')).toBeInTheDocument();
        expect(screen.getByRole('combobox', { name: 'Target lifecycle state' })).toHaveTextContent('Active');
        expect(screen.getByRole('button', { name: 'Submit for approval' })).toBeDisabled();
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
        fireEvent.click(await screen.findByRole('button', { name: 'Request lifecycle change' }));
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
            transitionRequestedByUserId: 'maker-id',
        };
        permissions.add('Finance.AccountingBooks.Manage');
        permissions.add('Finance.AccountingBooks.Transitions.Approve');
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([pending]);

        render(<AccountingBooksSettingsPage />);
        expect(await screen.findByText('Pending Suspended')).toBeInTheDocument();
        expect(screen.getByText('Pause configuration for review')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Review & approve' })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Review & reject' })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Edit' })).toBeDisabled();
        expect(screen.queryByRole('button', { name: 'Request Active' })).not.toBeInTheDocument();

        fireEvent.click(screen.getByRole('button', { name: 'Review & approve' }));
        expect(screen.getByText('Lifecycle decision evidence')).toBeInTheDocument();
        expect(screen.getByText('Initializing → Suspended')).toBeInTheDocument();
        fireEvent.change(screen.getByLabelText('Checker reason'), { target: { value: 'Independent evidence reviewed' } });
        fireEvent.click(screen.getByRole('button', { name: 'Approve transition' }));
        await waitFor(() => expect(financeDataService.approveAccountingBookTransition).toHaveBeenCalledWith('book-local', {
            reason: 'Independent evidence reviewed',
            rowVersion: 'BAUG',
        }));
    });

    it('explains the structural evidence and scope of an Initializing decision', async () => {
        permissions.add('Finance.AccountingBooks.Transitions.Approve');
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([{
            ...initializingBook,
            lifecycleStatus: 'Configuring',
            pendingLifecycleStatus: 'Initializing',
            pendingTransitionReason: 'Begin controlled opening preparation',
            transitionRequestedByUserId: 'maker-id',
            transitionRequestedAtUtc: '2026-09-19T09:00:00Z',
        }]);

        render(<AccountingBooksSettingsPage />);
        fireEvent.click(await screen.findByRole('button', { name: 'Review & approve' }));

        expect(screen.getByText('Configuring → Initializing')).toBeInTheDocument();
        expect(screen.getAllByText('Begin controlled opening preparation')).toHaveLength(2);
        expect(screen.getByText('What this approval authorizes')).toBeInTheDocument();
        expect(screen.getByText(/Initialization evidence does not exist yet/)).toBeInTheDocument();
        expect(financeDataService.getAccountingBookInitialization).not.toHaveBeenCalled();
    });

    it('reuses approved initialization and period evidence for an Active decision', async () => {
        permissions.add('Finance.AccountingBooks.Transitions.Approve');
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([{
            ...initializingBook,
            activationReady: true,
            readinessMessage: 'Ready for activation.',
            pendingLifecycleStatus: 'Active',
            pendingTransitionReason: 'Activate the statutory book',
            transitionRequestedByUserId: 'maker-id',
            transitionRequestedAtUtc: '2026-09-19T12:00:00Z',
        }]);

        render(<AccountingBooksSettingsPage />);
        fireEvent.click(await screen.findByRole('button', { name: 'Review & approve' }));

        expect(await screen.findByText('Activation readiness confirmed')).toBeInTheDocument();
        expect(screen.getByText('2026-01: Open')).toBeInTheDocument();
        expect(screen.getByText('Account-level reconciliation')).toBeInTheDocument();
        expect(screen.getByText(/Reason: Reconciliation evidence reviewed/)).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Approve transition' })).toBeDisabled();

        fireEvent.change(screen.getByLabelText('Checker reason'), { target: { value: 'Approved initialization and open-period evidence reviewed.' } });
        expect(screen.getByRole('button', { name: 'Approve transition' })).toBeEnabled();
    });

    it('hides approval actions from the requester even when that user has approval permission', async () => {
        authUserId = 'MAKER-ID';
        permissions.add('Finance.AccountingBooks.Transitions.Approve');
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([{
            ...initializingBook,
            pendingLifecycleStatus: 'Suspended',
            transitionRequestedByUserId: 'maker-id',
        }]);

        render(<AccountingBooksSettingsPage />);
        expect(await screen.findByText('Awaiting a different authorized checker.')).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Review & approve' })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Review & reject' })).not.toBeInTheDocument();
        expect(financeDataService.approveAccountingBookTransition).not.toHaveBeenCalled();
    });

    it('does not let a checker approve Active before C4 readiness', async () => {
        permissions.add('Finance.AccountingBooks.Transitions.Approve');
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([{
            ...initializingBook,
            pendingLifecycleStatus: 'Active',
            pendingTransitionReason: 'Attempt activation',
            activationReady: false,
            transitionRequestedByUserId: 'maker-id',
        }]);

        render(<AccountingBooksSettingsPage />);
        expect(await screen.findByText(/C4 readiness is unavailable/)).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Review & approve' })).toBeDisabled();
        expect(screen.getByRole('button', { name: 'Review & reject' })).toBeEnabled();
    });

    it('does not treat an action permission as a substitute for Finance.Read', () => {
        permissions = new Set(['Finance.AccountingBooks.Manage', 'Finance.AccountingBooks.Transitions.Request', 'Finance.AccountingBooks.Transitions.Approve']);
        render(<AccountingBooksSettingsPage />);
        expect(screen.getByText('Permission required')).toBeInTheDocument();
        expect(financeDataService.getAccountingBooks).not.toHaveBeenCalled();
    });
});
