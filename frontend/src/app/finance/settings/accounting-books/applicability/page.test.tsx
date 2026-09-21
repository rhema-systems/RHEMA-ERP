import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AccountingBookApplicabilityPage from './page';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { AccountingBookApplicabilityPolicy } from '@/types/finance';

let permissions = new Set<string>();
let authLoading = false;
let currentUserId = 'checker-user';

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({
    user: { id: currentUserId },
    isLoading: authLoading,
    error: null,
    hasPermission: (permission: string) => permissions.has(permission),
  }),
}));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/services/finance/finance-data.service', () => ({
  financeDataService: {
    getAccountingBookApplicabilityPolicies: vi.fn(),
    getAccountingBookApplicabilityEligibleBooks: vi.fn(),
    getAccountingBookPostingIdentities: vi.fn(),
    createAccountingBookApplicabilityPolicy: vi.fn(),
    updateAccountingBookApplicabilityPolicy: vi.fn(),
    decideAccountingBookApplicabilityPolicy: vi.fn(),
    resolveAccountingBookApplicability: vi.fn(),
    freezeAccountingBookSelection: vi.fn(),
  },
}));

const draft: AccountingBookApplicabilityPolicy = {
  id: 'policy-1',
  policyCode: 'SALES',
  version: 1,
  name: 'Sales representations',
  effectiveFrom: '2026-01-01T00:00:00Z',
  effectiveTo: null,
  status: 'Draft',
  reason: 'Initial configuration',
  preparedByUserId: 'maker-user',
  preparedAtUtc: '2026-01-01T00:00:00Z',
  rowVersion: 'AQID',
  rules: [
    {
      id: 'rule-1',
      ruleCode: 'SALES_POST',
      priority: 100,
      originatingModuleCode: 'SALES',
      sourceDocumentType: 'CUSTOMERINVOICE',
      postingAction: 'POST',
      sortOrder: 1,
      selectedBooks: [
        {
          accountingBookId: 'book-ifrs',
          accountingBookCode: 'IFRS',
          selectionOrder: 1,
        },
      ],
    },
  ],
};

const selectOption = (label: string, option: string) => {
  fireEvent.click(screen.getByLabelText(label));
  fireEvent.click(screen.getByRole('option', { name: option }));
};

describe('accounting-book applicability settings', () => {
  beforeEach(() => {
    permissions = new Set(['Finance.AccountingBooks.ApplicabilityPolicy.Read']);
    authLoading = false;
    currentUserId = 'checker-user';
    vi.clearAllMocks();
    vi.mocked(financeDataService.getAccountingBookApplicabilityPolicies).mockResolvedValue([draft]);
    vi.mocked(financeDataService.getAccountingBookApplicabilityEligibleBooks).mockResolvedValue([
      {
        accountingBookId: 'book-ifrs',
        code: 'IFRS',
        name: 'IFRS Primary',
        bookType: 'PrimaryFull',
        lifecycleStatus: 'Active',
        isDefault: true,
        isActive: true,
        allowsPosting: true,
        initializationReconciled: true,
        mappingClassificationReady: true,
      },
    ]);
    vi.mocked(financeDataService.getAccountingBookPostingIdentities).mockResolvedValue([
      {
        originatingModuleCode: 'FIN',
        moduleName: 'Finance',
        sourceDocumentType: 'MANUALJOURNAL',
        documentTypeName: 'Manual journal',
        postingAction: 'POST',
        postingActionName: 'Post',
      },
      {
        originatingModuleCode: 'SALES',
        moduleName: 'Sales',
        sourceDocumentType: 'CUSTOMERINVOICE',
        documentTypeName: 'Customer invoice',
        postingAction: 'POST',
        postingActionName: 'Post',
      },
    ]);
  });

  it('shows loading and denies Finance.Read without policy-read authority', () => {
    authLoading = true;
    const { rerender } = render(<AccountingBookApplicabilityPage />);
    expect(screen.getByLabelText('Loading applicability access')).toBeInTheDocument();
    authLoading = false;
    permissions = new Set(['Finance.Read']);
    rerender(<AccountingBookApplicabilityPage />);
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
    fireEvent.change(screen.getByLabelText('Stable policy code'), {
      target: { value: 'sales' },
    });
    fireEvent.change(screen.getByLabelText('Name'), {
      target: { value: 'Sales policy' },
    });
    fireEvent.change(screen.getByLabelText('Rule 1 code'), {
      target: { value: 'sales_post' },
    });
    selectOption('Rule 1 origin module', 'Sales (SALES)');
    selectOption('Rule 1 document type', 'Customer invoice');
    selectOption('Rule 1 posting action', 'Post');
    fireEvent.click(screen.getByRole('checkbox'));
    fireEvent.change(screen.getByLabelText('Reason'), {
      target: { value: 'Govern sales representations' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));
    await waitFor(() =>
      expect(financeDataService.createAccountingBookApplicabilityPolicy).toHaveBeenCalledWith(
        expect.objectContaining({
          policyCode: 'SALES',
          rules: [
            expect.objectContaining({
              ruleCode: 'SALES_POST',
              originatingModuleCode: 'SALES',
              accountingBookIds: ['book-ifrs'],
            }),
          ],
        })
      )
    );
  });

  it('explains an incomplete added rule and allows it to be removed', async () => {
    permissions.add('Finance.AccountingBooks.ApplicabilityPolicy.Manage');
    render(<AccountingBookApplicabilityPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'New policy draft' }));
    fireEvent.change(screen.getByLabelText('Stable policy code'), { target: { value: 'test_policy' } });
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Test policy' } });
    fireEvent.change(screen.getByLabelText('Rule 1 code'), { target: { value: 'test_001' } });
    selectOption('Rule 1 origin module', 'Finance (FIN)');
    selectOption('Rule 1 document type', 'Manual journal');
    selectOption('Rule 1 posting action', 'Post');
    fireEvent.click(screen.getByRole('checkbox'));
    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Test governed selection' } });

    const addRule = screen.getByRole('button', { name: 'Add rule' });
    expect(addRule).toBeEnabled();
    fireEvent.click(addRule);

    expect(screen.getByLabelText('Rule 2 code')).toHaveValue('');
    expect(screen.getByText(/Complete Rule 2: rule code, origin module, document type, posting action, at least one destination book/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save draft' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Add rule' })).toBeDisabled();

    fireEvent.click(screen.getByRole('button', { name: 'Remove rule 2' }));
    expect(screen.queryByLabelText('Rule 2 code')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save draft' })).toBeEnabled();
  });

  it('gates maker-checker actions independently', async () => {
    permissions.add('Finance.AccountingBooks.ApplicabilityPolicy.Approve');
    vi.mocked(financeDataService.getAccountingBookApplicabilityPolicies).mockResolvedValue([{ ...draft, status: 'PendingApproval' }]);
    render(<AccountingBookApplicabilityPage />);
    expect(await screen.findByRole('button', { name: 'Approve' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Submit' })).not.toBeInTheDocument();
  });

  it('never exposes checker decisions to the policy maker even with approve authority', async () => {
    permissions.add('Finance.AccountingBooks.ApplicabilityPolicy.Approve');
    currentUserId = 'maker-user';
    vi.mocked(financeDataService.getAccountingBookApplicabilityPolicies).mockResolvedValue([{ ...draft, status: 'PendingApproval' }]);
    render(<AccountingBookApplicabilityPage />);
    expect(await screen.findByText('Awaiting a different authorized checker.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reject' })).not.toBeInTheDocument();
  });

  it('separates retirement request from checker decisions', async () => {
    permissions.add('Finance.AccountingBooks.ApplicabilityPolicy.Manage');
    vi.mocked(financeDataService.getAccountingBookApplicabilityPolicies).mockResolvedValue([{ ...draft, status: 'Approved' }]);
    const makerView = render(<AccountingBookApplicabilityPage />);
    expect(await screen.findByRole('button', { name: 'Request retirement' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve retirement' })).not.toBeInTheDocument();
    makerView.unmount();

    permissions = new Set(['Finance.AccountingBooks.ApplicabilityPolicy.Read', 'Finance.AccountingBooks.ApplicabilityPolicy.Approve']);
    vi.mocked(financeDataService.getAccountingBookApplicabilityPolicies).mockResolvedValue([
      {
        ...draft,
        status: 'Approved',
        retirementDecisionStatus: 'Pending',
        retirementRequestedByUserId: 'maker-user',
        retirementReason: 'Superseded policy',
      },
    ]);
    render(<AccountingBookApplicabilityPage />);
    expect(await screen.findByText('Retirement awaiting checker')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Approve retirement' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reject retirement' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Request retirement' })).not.toBeInTheDocument();
  });

  it('copies an approved or retired policy into a linked successor draft', async () => {
    permissions.add('Finance.AccountingBooks.ApplicabilityPolicy.Manage');
    const retired = { ...draft, status: 'Retired' as const };
    vi.mocked(financeDataService.getAccountingBookApplicabilityPolicies).mockResolvedValue([retired]);
    vi.mocked(financeDataService.createAccountingBookApplicabilityPolicy).mockResolvedValue({
      ...retired,
      id: 'policy-2',
      version: 2,
      status: 'Draft',
      supersedesPolicyId: retired.id,
    });

    render(<AccountingBookApplicabilityPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Create successor version' }));

    expect(screen.getByText('Create successor policy version')).toBeInTheDocument();
    expect(screen.getByText(/Creating SALES version 2/)).toBeInTheDocument();
    expect(screen.getByLabelText('Stable policy code')).toBeDisabled();
    expect(screen.getByLabelText('Stable policy code')).toHaveValue('SALES');
    expect(screen.getByLabelText('Rule 1 code')).toHaveValue('SALES_POST');
    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Replace the retired policy' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));

    await waitFor(() => expect(financeDataService.createAccountingBookApplicabilityPolicy).toHaveBeenCalledWith(
      expect.objectContaining({
        policyCode: 'SALES',
        rowVersion: null,
        reason: 'Replace the retired policy',
        rules: [expect.objectContaining({ ruleCode: 'SALES_POST', accountingBookIds: ['book-ifrs'] })],
      })
    ));
  });

  it('renders primary fallback and explicit readiness blockers without posting', async () => {
    permissions.add('Finance.AccountingBooks.Applicability.Resolve');
    vi.mocked(financeDataService.resolveAccountingBookApplicability).mockResolvedValue({
      policyId: null,
      ruleId: null,
      policyVersion: null,
      effectiveDate: '2026-09-06T00:00:00Z',
      originatingModuleCode: 'FIN',
      sourceDocumentType: 'ManualJournal',
      postingAction: 'Post',
      usedPrimaryOnlyFallback: true,
      books: [
        {
          accountingBookId: 'book-ifrs',
          accountingBookCode: 'IFRS',
          selectionOrder: 1,
          authorityFingerprint: 'c'.repeat(64),
        },
        {
          accountingBookId: 'book-local',
          accountingBookCode: 'LOCAL_STATUTORY',
          selectionOrder: 2,
          authorityFingerprint: 'd'.repeat(64),
        },
        {
          accountingBookId: 'book-management',
          accountingBookCode: 'MANAGEMENT',
          selectionOrder: 3,
          authorityFingerprint: 'e'.repeat(64),
        },
      ],
      blockers: [
        {
          code: 'BOOK_PERIOD_NOT_READY',
          accountingBookId: 'book-ifrs',
          message: 'Exact book period is not open.',
        },
      ],
      calculationInputHash: 'a'.repeat(64),
      selectionFingerprint: 'b'.repeat(64),
    });
    render(<AccountingBookApplicabilityPage />);
    await screen.findByText('Selection preview');
    expect(screen.getByText(/Checks which active, ready accounting books would be selected/)).toBeInTheDocument();
    selectOption('Origin module', 'Finance (FIN)');
    selectOption('Document type', 'Manual journal');
    selectOption('Posting action', 'Post');
    fireEvent.click(screen.getByRole('button', { name: 'Preview selection' }));
    expect(await screen.findByText('Primary-only fallback')).toBeInTheDocument();
    expect(screen.getByLabelText('Selected destination books')).toHaveTextContent('IFRS, LOCAL_STATUTORY, MANAGEMENT');
    expect(screen.getByLabelText('Selected destination books')).not.toHaveTextContent('→');
    expect(screen.getByText(/BOOK_PERIOD_NOT_READY/)).toBeInTheDocument();
    expect(financeDataService.freezeAccountingBookSelection).not.toHaveBeenCalled();
  });
});
