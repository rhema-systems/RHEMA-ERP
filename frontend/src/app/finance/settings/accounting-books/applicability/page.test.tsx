import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AccountingBookApplicabilityPage from './page';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { AccountingBookApplicabilityPolicy } from '@/types/finance';

let permissions = new Set<string>();
let authLoading = false;

vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ isLoading: authLoading, error: null, hasPermission: (permission: string) => permissions.has(permission) }) }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/services/finance/finance-data.service', () => ({ financeDataService: {
    getAccountingBookApplicabilityPolicies: vi.fn(), getAccountingBookApplicabilityEligibleBooks: vi.fn(),
    createAccountingBookApplicabilityPolicy: vi.fn(), updateAccountingBookApplicabilityPolicy: vi.fn(),
    decideAccountingBookApplicabilityPolicy: vi.fn(), resolveAccountingBookApplicability: vi.fn(), freezeAccountingBookSelection: vi.fn(),
} }));

const draft: AccountingBookApplicabilityPolicy = {
    id: 'policy-1', policyCode: 'SALES', version: 1, name: 'Sales representations', effectiveFrom: '2026-01-01T00:00:00Z',
    effectiveTo: null, status: 'Draft', reason: 'Initial configuration', rowVersion: 'AQID', rules: [{
        id: 'rule-1', ruleCode: 'SALES_POST', priority: 100, originatingModuleCode: 'SALES', sourceDocumentType: 'Invoice',
        postingAction: 'Post', sortOrder: 1, selectedBooks: [{ accountingBookId: 'book-ifrs', accountingBookCode: 'IFRS', selectionOrder: 1 }],
    }],
};

describe('accounting-book applicability settings', () => {
    beforeEach(() => {
        permissions = new Set(['Finance.AccountingBooks.ApplicabilityPolicy.Read']); authLoading = false; vi.clearAllMocks();
        vi.mocked(financeDataService.getAccountingBookApplicabilityPolicies).mockResolvedValue([draft]);
        vi.mocked(financeDataService.getAccountingBookApplicabilityEligibleBooks).mockResolvedValue([{
            accountingBookId: 'book-ifrs', code: 'IFRS', name: 'IFRS Primary', bookType: 'PrimaryFull', lifecycleStatus: 'Active',
            isDefault: true, isActive: true, allowsPosting: true, initializationReconciled: true, mappingClassificationReady: true,
        }]);
    });

    it('shows loading and denies Finance.Read without policy-read authority', () => {
        authLoading = true; const { rerender } = render(<AccountingBookApplicabilityPage />);
        expect(screen.getByLabelText('Loading applicability access')).toBeInTheDocument();
        authLoading = false; permissions = new Set(['Finance.Read']); rerender(<AccountingBookApplicabilityPage />);
        expect(screen.getByText('Permission required')).toBeInTheDocument();
        expect(financeDataService.getAccountingBookApplicabilityPolicies).not.toHaveBeenCalled();
    });

    it('distinguishes request failure from true empty and supports retry', async () => {
        vi.mocked(financeDataService.getAccountingBookApplicabilityPolicies).mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce([]);
        render(<AccountingBookApplicabilityPage />);
        expect(await screen.findByText('Could not load applicability policy')).toBeInTheDocument();
        expect(screen.queryByText(/No applicability policies/)).not.toBeInTheDocument();
        fireEvent.click(screen.getByRole('button', { name: /Retry/ }));
        expect(await screen.findByText(/No applicability policies are configured/)).toBeInTheDocument();
    });

    it('keeps read-only policy access separate from management and resolution', async () => {
        render(<AccountingBookApplicabilityPage />);
        expect(await screen.findByText('SALES · v1')).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'New policy draft' })).not.toBeInTheDocument();
        expect(screen.queryByText('Selection preview')).not.toBeInTheDocument();
        expect(screen.getByText(/selects exactly the primary\/default full book/)).toBeInTheDocument();
    });

    it('creates an exact normalized rule from eligible full-book IDs', async () => {
        permissions.add('Finance.AccountingBooks.ApplicabilityPolicy.Manage');
        vi.mocked(financeDataService.createAccountingBookApplicabilityPolicy).mockResolvedValue(draft);
        render(<AccountingBookApplicabilityPage />);
        fireEvent.click(await screen.findByRole('button', { name: 'New policy draft' }));
        fireEvent.change(screen.getByLabelText('Stable policy code'), { target: { value: 'sales' } });
        fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Sales policy' } });
        fireEvent.change(screen.getByLabelText('Rule 1 code'), { target: { value: 'sales_post' } });
        fireEvent.change(screen.getByLabelText('Rule 1 origin module'), { target: { value: 'sales' } });
        fireEvent.change(screen.getByLabelText('Rule 1 document type'), { target: { value: 'Invoice' } });
        fireEvent.change(screen.getByLabelText('Rule 1 posting action'), { target: { value: 'Post' } });
        fireEvent.click(screen.getByRole('checkbox'));
        fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Govern sales representations' } });
        fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));
        await waitFor(() => expect(financeDataService.createAccountingBookApplicabilityPolicy).toHaveBeenCalledWith(expect.objectContaining({
            policyCode: 'SALES', rules: [expect.objectContaining({ ruleCode: 'SALES_POST', originatingModuleCode: 'SALES', accountingBookIds: ['book-ifrs'] })],
        })));
    });

    it('gates maker-checker actions independently', async () => {
        permissions.add('Finance.AccountingBooks.ApplicabilityPolicy.Approve');
        vi.mocked(financeDataService.getAccountingBookApplicabilityPolicies).mockResolvedValue([{ ...draft, status: 'PendingApproval' }]);
        render(<AccountingBookApplicabilityPage />);
        expect(await screen.findByRole('button', { name: 'Approve' })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Reject' })).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Submit' })).not.toBeInTheDocument();
    });

    it('separates retirement request from checker decisions', async () => {
        permissions.add('Finance.AccountingBooks.ApplicabilityPolicy.Manage');
        vi.mocked(financeDataService.getAccountingBookApplicabilityPolicies).mockResolvedValue([{ ...draft, status: 'Approved' }]);
        const makerView = render(<AccountingBookApplicabilityPage />);
        expect(await screen.findByRole('button', { name: 'Request retirement' })).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Approve retirement' })).not.toBeInTheDocument();
        makerView.unmount();

        permissions = new Set(['Finance.AccountingBooks.ApplicabilityPolicy.Read', 'Finance.AccountingBooks.ApplicabilityPolicy.Approve']);
        vi.mocked(financeDataService.getAccountingBookApplicabilityPolicies).mockResolvedValue([{
            ...draft, status: 'Approved', retirementDecisionStatus: 'Pending', retirementReason: 'Superseded policy',
        }]);
        render(<AccountingBookApplicabilityPage />);
        expect(await screen.findByText('Retirement awaiting checker')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Approve retirement' })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Reject retirement' })).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Request retirement' })).not.toBeInTheDocument();
    });

    it('renders primary fallback and explicit readiness blockers without posting', async () => {
        permissions.add('Finance.AccountingBooks.Applicability.Resolve');
        vi.mocked(financeDataService.resolveAccountingBookApplicability).mockResolvedValue({
            policyId: null, ruleId: null, policyVersion: null, effectiveDate: '2026-09-06T00:00:00Z', originatingModuleCode: 'FIN',
            sourceDocumentType: 'ManualJournal', postingAction: 'Post', usedPrimaryOnlyFallback: true, books: [{ accountingBookId: 'book-ifrs', accountingBookCode: 'IFRS', selectionOrder: 1, authorityFingerprint: 'c'.repeat(64) }],
            blockers: [{ code: 'BOOK_PERIOD_NOT_READY', accountingBookId: 'book-ifrs', message: 'Exact book period is not open.' }],
            calculationInputHash: 'a'.repeat(64), selectionFingerprint: 'b'.repeat(64),
        });
        render(<AccountingBookApplicabilityPage />);
        await screen.findByText('Selection preview');
        fireEvent.change(screen.getByLabelText('Origin module'), { target: { value: 'fin' } });
        fireEvent.change(screen.getByLabelText('Document type'), { target: { value: 'ManualJournal' } });
        fireEvent.change(screen.getByLabelText('Posting action'), { target: { value: 'Post' } });
        fireEvent.click(screen.getByRole('button', { name: 'Preview selection' }));
        expect(await screen.findByText('Primary-only fallback')).toBeInTheDocument();
        expect(screen.getByText(/BOOK_PERIOD_NOT_READY/)).toBeInTheDocument();
        expect(financeDataService.freezeAccountingBookSelection).not.toHaveBeenCalled();
    });
});
