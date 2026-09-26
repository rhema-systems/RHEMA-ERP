import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AccountingBookReadinessPage from './page';
import { financeDataService } from '@/services/finance/finance-data.service';

let permissions = new Set<string>();
let authLoading = false;
let authUserId = 'checker';
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'book-1' }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ user: { id: authUserId }, isLoading: authLoading, error: null, hasPermission: (value: string) => permissions.has(value) }) }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/services/finance/finance-data.service', () => ({ financeDataService: {
    getAccountingBook: vi.fn(), getAccountingBooks: vi.fn(), getAccountingBookPeriods: vi.fn(), getFiscalPeriods: vi.fn(),
    getAccountingBookInitialization: vi.fn(), getAccountingBookActivationReadiness: vi.fn(), createAccountingBookPeriod: vi.fn(),
    requestAccountingBookPeriodTransition: vi.fn(), decideAccountingBookPeriodTransition: vi.fn(), prepareAccountingBookInitialization: vi.fn(),
    prepareDeltaBookStructure: vi.fn(),
    configureAccountingBookInitialization: vi.fn(), submitAccountingBookInitialization: vi.fn(), decideAccountingBookInitialization: vi.fn(),
} }));

const book = { id: 'book-1', tenantId: 'tenant', code: 'LOCAL', name: 'Local', purpose: 'Statutory', bookType: 'ParallelFull', lifecycleStatus: 'Initializing', functionalCurrencyCode: 'GHS', isActive: false, isDefault: false, allowsPosting: false, isSystemDefined: false, sortOrder: 2, rowVersion: 'AQ==' };
const period = { id: 'bp-1', accountingBookId: 'book-1', accountingBookCode: 'LOCAL', fiscalPeriodId: 'fp-1', fiscalPeriodCode: '2026-01', startDate: '2026-01-01', endDate: '2026-01-31', status: 'Future', rowVersion: 'AQ==' };

describe('accounting book C4 readiness', () => {
    beforeEach(() => {
        vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
        Element.prototype.scrollIntoView = vi.fn();
        permissions = new Set(['Finance.Read']); authLoading = false; authUserId = 'checker'; vi.clearAllMocks();
        vi.mocked(financeDataService.getAccountingBook).mockResolvedValue(book as never);
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([book] as never);
        vi.mocked(financeDataService.getAccountingBookPeriods).mockResolvedValue([]);
        vi.mocked(financeDataService.getFiscalPeriods).mockResolvedValue([]);
        vi.mocked(financeDataService.getAccountingBookInitialization).mockResolvedValue(null);
        vi.mocked(financeDataService.getAccountingBookActivationReadiness).mockResolvedValue({ isReady: false, blockers: ['Approved initialization evidence is required.'], requiredPeriodCount: 1, readyPeriodCount: 0 });
    });

    it('does not treat action permissions as Finance.Read and preserves auth loading', () => {
        authLoading = true;
        const view = render(<AccountingBookReadinessPage />);
        expect(screen.getByLabelText('Loading book readiness')).toBeInTheDocument();
        authLoading = false; permissions = new Set(['Finance.AccountingBooks.Periods.Manage']); view.rerender(<AccountingBookReadinessPage />);
        expect(screen.getByText('Permission required')).toBeInTheDocument();
        expect(financeDataService.getAccountingBook).not.toHaveBeenCalled();
    });

    it('distinguishes request failure from empty evidence and retries', async () => {
        vi.mocked(financeDataService.getAccountingBook).mockRejectedValueOnce(new Error('network unavailable')).mockResolvedValueOnce(book as never);
        render(<AccountingBookReadinessPage />);
        expect(await screen.findByText('Book readiness unavailable')).toBeInTheDocument();
        expect(screen.queryByText('No exact-book periods are configured.')).not.toBeInTheDocument();
        fireEvent.click(screen.getByRole('button', { name: /Retry/ }));
        expect(await screen.findByText('No exact-book periods are configured.')).toBeInTheDocument();
    });

    it('keeps a Finance.Read user read-only with explicit activation blockers', async () => {
        render(<AccountingBookReadinessPage />);
        expect(await screen.findByText('Activation blocked')).toBeInTheDocument();
        expect(screen.getByText('Approved initialization evidence is required.')).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Add Future period' })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Load governed preparation' })).not.toBeInTheDocument();
    });

    it('shows a guided next action and defers posting periods to the tenant fiscal calendar', async () => {
        render(<AccountingBookReadinessPage />);
        expect(await screen.findByText('Setup progress')).toBeInTheDocument();
        expect(screen.getByText('Next required action')).toBeInTheDocument();
        expect(screen.getByText('Load governed preparation and save the initialization evidence.')).toBeInTheDocument();
        expect(screen.queryByText('Open period required')).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Add Future period' })).not.toBeInTheDocument();
    });

    it('loads governed preparation and submits exact mapped-account evidence', async () => {
        permissions.add('Finance.AccountingBooks.Initialization.Manage');
        vi.mocked(financeDataService.prepareAccountingBookInitialization).mockResolvedValue({ accountingBookId: 'book-1', accountingBookCode: 'LOCAL', mode: 'IndependentOpeningBalances', cutoffDate: '2026-01-01', cutoffFiscalPeriodId: 'period-1', cutoffFiscalPeriodCode: '2026-01', functionalCurrencyCode: 'GHS', accounts: [{ accountId: 'account-1', accountNumber: '1000', accountName: 'Cash', accountClassificationId: 'class-1', accountClassificationCode: 'CASH', authoritativeSignedBalance: 0 }] });
        render(<AccountingBookReadinessPage />);
        fireEvent.change(await screen.findByLabelText('Cutoff date'), { target: { value: '2026-01-01' } });
        expect(screen.getByText(/prevents this initialization evidence from being saved twice/i)).toBeInTheDocument();
        fireEvent.change(screen.getByLabelText('Preparation reference'), { target: { value: 'init-1' } });
        fireEvent.change(screen.getByLabelText('Preparation reason'), { target: { value: 'Reviewed opening evidence' } });
        fireEvent.click(screen.getByRole('button', { name: 'Load governed preparation' }));
        expect(await screen.findByText('1000 — Cash')).toBeInTheDocument();
        expect(screen.getByText(/server-derived from this book’s posted exact-book balances/i)).toBeInTheDocument();
        expect(screen.getByLabelText('1000 debit')).toHaveAttribute('readonly');
        expect(screen.getByLabelText('1000 credit')).toHaveAttribute('readonly');
        expect(screen.getByLabelText('1000 adjustment')).toBeDisabled();
        fireEvent.click(screen.getByRole('button', { name: 'Save draft evidence' }));
        await waitFor(() => expect(financeDataService.configureAccountingBookInitialization).toHaveBeenCalledWith('book-1', expect.objectContaining({ mode: 'IndependentOpeningBalances', cutoffFiscalPeriodId: 'period-1', cutoffFiscalPeriodCode: '2026-01', idempotencyKey: 'init-1', lines: [expect.objectContaining({ accountId: 'account-1', currencyCode: 'GHS' })] })));
    });

    it('keeps Delta zero-balance initialization read-only', async () => {
        permissions.add('Finance.AccountingBooks.Initialization.Manage');
        const delta = { ...book, code: 'IFRS_CONSOL_ADJ', name: 'IFRS consolidation adjustments', bookType: 'Delta', baseAccountingBookCode: 'IFRS' };
        vi.mocked(financeDataService.getAccountingBook).mockResolvedValue(delta as never);
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([delta] as never);
        vi.mocked(financeDataService.prepareDeltaBookStructure).mockResolvedValue({} as never);
        vi.mocked(financeDataService.prepareAccountingBookInitialization).mockResolvedValue({ accountingBookId: 'book-1', accountingBookCode: 'IFRS_CONSOL_ADJ', mode: 'IndependentOpeningBalances', cutoffDate: '2026-01-31', cutoffFiscalPeriodId: 'period-1', cutoffFiscalPeriodCode: '2026-01', functionalCurrencyCode: 'GHS', accounts: [{ accountId: 'account-1', accountNumber: '1000', accountName: 'Cash', accountClassificationId: 'class-1', accountClassificationCode: 'CASH', authoritativeSignedBalance: 0 }] });

        render(<AccountingBookReadinessPage />);
        fireEvent.change(await screen.findByLabelText('Cutoff date'), { target: { value: '2026-01-31' } });
        fireEvent.click(screen.getByRole('button', { name: 'Load governed preparation' }));

        expect(await screen.findByText(/This Delta layer starts at zero/i)).toBeInTheDocument();
        expect(financeDataService.prepareDeltaBookStructure).toHaveBeenCalledWith('book-1');
        expect(screen.getByLabelText('1000 debit')).toHaveAttribute('readonly');
        expect(screen.getByLabelText('1000 credit')).toHaveAttribute('readonly');
        expect(screen.getByLabelText('1000 adjustment')).toBeDisabled();
    });

    it('lets a full book edit only the governed opening adjustment and derives debit and credit', async () => {
        permissions.add('Finance.AccountingBooks.Initialization.Manage');
        const ifrs = { ...book, id: 'ifrs', code: 'IFRS', name: 'IFRS Primary', bookType: 'PrimaryFull', lifecycleStatus: 'Active', isActive: true, isDefault: true, allowsPosting: true };
        vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([book, ifrs] as never);
        vi.mocked(financeDataService.prepareAccountingBookInitialization).mockResolvedValue({ accountingBookId: 'book-1', accountingBookCode: 'LOCAL', mode: 'BaseBalancesWithOpeningAdjustments', cutoffDate: '2026-01-31', cutoffFiscalPeriodId: 'period-1', cutoffFiscalPeriodCode: '2026-01', sourceAccountingBookId: 'ifrs', sourceAccountingBookCode: 'IFRS', functionalCurrencyCode: 'GHS', accounts: [{ accountId: 'account-1', accountNumber: '1000', accountName: 'Cash', accountClassificationId: 'class-1', accountClassificationCode: 'CASH', authoritativeSignedBalance: 1200 }] });

        render(<AccountingBookReadinessPage />);
        fireEvent.click(await screen.findByLabelText('Initialization mode'));
        fireEvent.click(await screen.findByText('BaseBalancesWithOpeningAdjustments'));
        fireEvent.click(screen.getByLabelText('Source full book'));
        fireEvent.click(await screen.findByText('IFRS — IFRS Primary'));
        fireEvent.change(screen.getByLabelText('Cutoff date'), { target: { value: '2026-01-31' } });
        fireEvent.click(screen.getByRole('button', { name: 'Load governed preparation' }));

        expect(await screen.findByText(/Edit Adjustment only/i)).toBeInTheDocument();
        const debit = screen.getByLabelText('1000 debit');
        const credit = screen.getByLabelText('1000 credit');
        const adjustment = screen.getByLabelText('1000 adjustment');
        expect(debit).toHaveAttribute('readonly');
        expect(credit).toHaveAttribute('readonly');
        expect(adjustment).not.toBeDisabled();
        fireEvent.change(adjustment, { target: { value: '25' } });
        expect(debit).toHaveValue(1225);
        expect(credit).toHaveValue(0);
    });

    it('shows maker-checker initialization actions only under approval permission', async () => {
        permissions.add('Finance.AccountingBooks.Initialization.Approve');
        vi.mocked(financeDataService.getAccountingBookInitialization).mockResolvedValue({ id: 'init', accountingBookId: 'book-1', version: 1, accountingBookCode: 'LOCAL', mode: 'IndependentOpeningBalances', status: 'PendingApproval', cutoffDate: '2026-01-01', cutoffFiscalPeriodId: 'period-1', cutoffFiscalPeriodCode: '2026-01', idempotencyKey: 'key', reason: 'reason', totalDebits: 0, totalCredits: 0, requiredAccountCount: 1, coveredAccountCount: 1, isBalanced: true, isCoverageComplete: true, evidenceFingerprint: 'A'.repeat(64), reconciliationFingerprint: 'B'.repeat(64), preparedByUserId: 'maker', preparedAtUtc: '2026-01-01', rowVersion: 'AQ==', lines: [] });
        render(<AccountingBookReadinessPage />);
        fireEvent.click(await screen.findByRole('button', { name: 'Approve initialization…' }));
        fireEvent.change(screen.getByLabelText('Reason for approval'), { target: { value: 'Evidence independently reconciled' } });
        fireEvent.click(screen.getByRole('button', { name: 'Confirm initialization approval' }));
        await waitFor(() => expect(financeDataService.decideAccountingBookInitialization).toHaveBeenCalledWith('book-1', 'approve', 'Evidence independently reconciled', 'AQ=='));
    });

    it('gives the checker a source-to-opening reconciliation pack', async () => {
        permissions.add('Finance.AccountingBooks.Initialization.Approve');
        vi.mocked(financeDataService.getAccountingBookInitialization).mockResolvedValue({
            id: 'init', accountingBookId: 'book-1', version: 1, accountingBookCode: 'LOCAL',
            mode: 'BaseBalancesWithOpeningAdjustments', status: 'PendingApproval', cutoffDate: '2025-12-31',
            cutoffFiscalPeriodId: 'period-1', cutoffFiscalPeriodCode: '2025-12', sourceAccountingBookId: 'ifrs',
            sourceAccountingBookCode: 'IFRS', idempotencyKey: 'LOCAL-INIT-2025-12-31-V1',
            reason: 'Initialize the statutory book from approved IFRS balances', totalDebits: 1225, totalCredits: 1225,
            requiredAccountCount: 2, coveredAccountCount: 2, isBalanced: true, isCoverageComplete: true,
            evidenceFingerprint: 'A'.repeat(64), reconciliationFingerprint: 'B'.repeat(64), preparedByUserId: 'maker',
            preparedByName: 'System Administrator', preparedAtUtc: '2026-09-19T10:00:00Z', rowVersion: 'AQ==',
            lines: [
                { accountId: 'cash', accountNumber: '1000', accountName: 'Cash', accountType: 'Asset', currencyCode: 'GHS', openingDebit: 1225, openingCredit: 0, baseBookSignedBalance: 1200, openingAdjustment: 25 },
                { accountId: 'equity', accountNumber: '3000', accountName: 'Equity', accountType: 'Equity', currencyCode: 'GHS', openingDebit: 0, openingCredit: 1225, baseBookSignedBalance: -1200, openingAdjustment: -25 },
            ],
        });

        render(<AccountingBookReadinessPage />);

        expect(await screen.findByText('IFRS accounting book')).toBeInTheDocument();
        expect(screen.getByText('System Administrator')).toBeInTheDocument();
        expect(screen.getByText('Initialize the statutory book from approved IFRS balances')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Adjustments (2)' })).toBeInTheDocument();
        expect(screen.getByText('1000')).toBeInTheDocument();
        expect(screen.getByText('Cash')).toBeInTheDocument();
        expect(screen.getAllByText('1,225.00').length).toBeGreaterThan(1);
        fireEvent.click(screen.getByRole('button', { name: 'Exceptions (0)' }));
        expect(screen.getByText('No evidence lines match this view.')).toBeInTheDocument();
    });

    it('hides period and initialization decisions from their maker despite approval grants', async () => {
        authUserId = 'MAKER';
        permissions.add('Finance.AccountingBooks.Periods.Approve');
        permissions.add('Finance.AccountingBooks.Initialization.Approve');
        vi.mocked(financeDataService.getAccountingBookPeriods).mockResolvedValue([{
            ...period, pendingStatus: 'Open', requestedByUserId: 'maker',
        }] as never);
        vi.mocked(financeDataService.getAccountingBookInitialization).mockResolvedValue({
            id: 'init', accountingBookId: 'book-1', version: 1, accountingBookCode: 'LOCAL',
            mode: 'IndependentOpeningBalances', status: 'PendingApproval', cutoffDate: '2025-12-31',
            cutoffFiscalPeriodId: 'period-1', cutoffFiscalPeriodCode: '2025-12', idempotencyKey: 'key',
            reason: 'reason', totalDebits: 0, totalCredits: 0, requiredAccountCount: 1,
            coveredAccountCount: 1, isBalanced: true, isCoverageComplete: true,
            evidenceFingerprint: 'A'.repeat(64), reconciliationFingerprint: 'B'.repeat(64),
            preparedByUserId: 'maker', preparedAtUtc: '2026-01-01', rowVersion: 'AQ==', lines: [],
        } as never);

        render(<AccountingBookReadinessPage />);
        expect(await screen.findByText('Pending Open')).toBeInTheDocument();
        expect(screen.getAllByText('Awaiting a different authorized checker.')).toHaveLength(2);
        expect(screen.queryByRole('button', { name: 'Approve period…' })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Reject period…' })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Approve initialization…' })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Reject initialization…' })).not.toBeInTheDocument();
        expect(financeDataService.decideAccountingBookInitialization).not.toHaveBeenCalled();
    });

    it('shows rejection as decision evidence without presenting it as approval', async () => {
        vi.mocked(financeDataService.getAccountingBookInitialization).mockResolvedValue({
            id: 'init', accountingBookId: 'book-1', version: 2, accountingBookCode: 'LOCAL',
            mode: 'IndependentOpeningBalances', status: 'Rejected', cutoffDate: '2026-01-01',
            cutoffFiscalPeriodId: 'period-1', cutoffFiscalPeriodCode: '2026-01', idempotencyKey: 'key-2',
            reason: 'reason', totalDebits: 0, totalCredits: 0, requiredAccountCount: 1, coveredAccountCount: 1,
            isBalanced: true, isCoverageComplete: true, evidenceFingerprint: 'A'.repeat(64),
            reconciliationFingerprint: 'B'.repeat(64), preparedByUserId: 'maker', preparedAtUtc: '2026-01-01',
            rejectedByUserId: 'checker', rejectedByName: 'Abena Dapaah', rejectedAtUtc: '2026-01-02', decidedByUserId: 'checker',
            decidedByName: 'Abena Dapaah',
            decidedAtUtc: '2026-01-02', decisionReason: 'Opening evidence did not reconcile', rowVersion: 'AQ==', lines: [],
        });
        render(<AccountingBookReadinessPage />);
        expect(await screen.findByText(/Rejected by Abena Dapaah/)).toBeInTheDocument();
        expect(screen.getByText('Reason: Opening evidence did not reconcile')).toBeInTheDocument();
        expect(screen.queryByText(/Approved by Abena Dapaah/)).not.toBeInTheDocument();
        expect(screen.getByText(/A system-generated SHA-256 checksum/)).toBeInTheDocument();
    });
});
