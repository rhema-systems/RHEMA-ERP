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

    it('creates only a Future book period under the manage permission', async () => {
        permissions.add('Finance.AccountingBooks.Periods.Manage');
        vi.mocked(financeDataService.getFiscalPeriods).mockResolvedValue([{ id: 'fp-2', fiscalYearId: 'fy', periodNumber: 2, periodCode: '2026-02', periodName: 'February', startDate: '2026-02-01', endDate: '2026-02-28', periodStatus: 'Open', allowFutureDating: false, isClosed: false, isLocked: false }] as never);
        vi.mocked(financeDataService.createAccountingBookPeriod).mockResolvedValue(period as never);
        render(<AccountingBookReadinessPage />);
        fireEvent.click(await screen.findByLabelText('Fiscal period'));
        fireEvent.change(screen.getByPlaceholderText('Search period code or name…'), { target: { value: 'February' } });
        fireEvent.click(screen.getByText('2026-02 — February'));
        fireEvent.click(screen.getByRole('button', { name: 'Add Future period' }));
        await waitFor(() => expect(financeDataService.createAccountingBookPeriod).toHaveBeenCalledWith('book-1', 'fp-2'));
        await waitFor(() => expect(screen.getByLabelText('Fiscal period')).toHaveTextContent('Select another fiscal period'));
        expect(screen.getByLabelText('Fiscal period')).not.toHaveTextContent('fp-2');
    });

    it('requires a reason and rowversion for a period transition request', async () => {
        permissions.add('Finance.AccountingBooks.Periods.Manage');
        vi.mocked(financeDataService.getAccountingBookPeriods).mockResolvedValue([period] as never);
        render(<AccountingBookReadinessPage />);
        fireEvent.click(await screen.findByRole('button', { name: 'Request Open…' }));
        expect(screen.getByRole('button', { name: 'Submit Open request' })).toBeDisabled();
        fireEvent.change(screen.getByLabelText('Reason to request Open'), { target: { value: 'Open after close checks' } });
        fireEvent.click(screen.getByRole('button', { name: 'Submit Open request' }));
        await waitFor(() => expect(financeDataService.requestAccountingBookPeriodTransition).toHaveBeenCalledWith('book-1', 'bp-1', 'Open', 'Open after close checks', 'AQ=='));
    });

    it('makes period approval a clear confirmation and does not clear a repeated selection', async () => {
        permissions.add('Finance.AccountingBooks.Periods.Approve');
        vi.mocked(financeDataService.getAccountingBookPeriods).mockResolvedValue([{
            ...period, pendingStatus: 'Open', requestedByUserId: 'maker',
        }] as never);
        vi.mocked(financeDataService.decideAccountingBookPeriodTransition).mockResolvedValue(period as never);
        render(<AccountingBookReadinessPage />);

        const selectApproval = await screen.findByRole('button', { name: 'Approve period…' });
        fireEvent.click(selectApproval);
        fireEvent.change(screen.getByLabelText('Reason for approval'), { target: { value: 'Calendar and posting controls reviewed' } });
        fireEvent.click(selectApproval);
        expect(screen.getByLabelText('Reason for approval')).toHaveValue('Calendar and posting controls reviewed');
        expect(financeDataService.decideAccountingBookPeriodTransition).not.toHaveBeenCalled();

        fireEvent.click(screen.getByRole('button', { name: 'Confirm period approval' }));
        await waitFor(() => expect(financeDataService.decideAccountingBookPeriodTransition).toHaveBeenCalledWith(
            'book-1', 'bp-1', 'approve', 'Calendar and posting controls reviewed', 'AQ=='));
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
        fireEvent.click(screen.getByRole('button', { name: 'Save draft evidence' }));
        await waitFor(() => expect(financeDataService.configureAccountingBookInitialization).toHaveBeenCalledWith('book-1', expect.objectContaining({ mode: 'IndependentOpeningBalances', cutoffFiscalPeriodId: 'period-1', cutoffFiscalPeriodCode: '2026-01', idempotencyKey: 'init-1', lines: [expect.objectContaining({ accountId: 'account-1', currencyCode: 'GHS' })] })));
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
