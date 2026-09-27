import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import TrialBalancePage from './page';
import { financeDataService } from '@/services/finance/finance-data.service';
import { documentOutputService } from '@/services/document-output.service';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/components/finance/reports/ReportSegmentFilters', () => ({
  ReportSegmentFilters: () => null,
}));

vi.mock('@/components/finance/reports/AppliedReportSegmentFilters', () => ({
  AppliedReportSegmentFilters: () => null,
}));
vi.mock('@/components/finance/reports/ReportDimensionFilters', () => ({
  ReportDimensionFilters: () => null,
}));
vi.mock('@/components/finance/reports/AppliedReportDimensionFilters', () => ({
  AppliedReportDimensionFilters: () => null,
}));

vi.mock('@/services/document-output.service', () => ({
  DOCUMENT_TYPES: {
    financeTrialBalance: 'FinanceTrialBalance',
    financeBaseDeltaReport: 'Finance.BaseDeltaReport',
  },
  documentOutputService: {
    printReportDocument: vi.fn(),
    downloadReportDocument: vi.fn(),
  },
}));

vi.mock('@/services/finance/finance-data.service', () => ({
  financeDataService: {
    getFinanceSettings: vi.fn(),
    getAccountingBooks: vi.fn(),
    getReportingDimensions: vi.fn(),
    getFinanceDimensions: vi.fn(),
    getTrialBalance: vi.fn(),
    getMultiDeltaBookCombinedReport: vi.fn(),
  },
}));

describe('TrialBalancePage report date', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Element.prototype.scrollIntoView = vi.fn();
    vi.mocked(financeDataService.getFinanceSettings).mockResolvedValue({
      baseCurrency: 'GHS',
      coaType: 'Standard',
    } as never);
    vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([
      { id: 'ifrs-1', code: 'IFRS', name: 'IFRS', bookType: 'PrimaryFull', isActive: true, allowsPosting: true },
      { id: 'delta-1', code: 'IFRS_CONSOL_ADJ', name: 'IFRS consolidation adjustments', bookType: 'Delta', isActive: true, allowsPosting: true, baseAccountingBookCode: 'IFRS' },
    ] as never);
    vi.mocked(financeDataService.getReportingDimensions).mockResolvedValue([]);
    vi.mocked(financeDataService.getFinanceDimensions).mockResolvedValue([]);
    vi.mocked(financeDataService.getTrialBalance).mockImplementation(
      async (request) => {
        if (!request.asAtDate) throw new Error('Expected an explicit report date.');
        return {
        asAtDate: request.asAtDate,
        bookClassification: request.bookClassification ?? 'BASE',
        currencyCode: 'GHS',
        lines: [],
        totalDebits: 0,
        totalCredits: 0,
        difference: 0,
        isBalanced: true,
        companyName: 'Rhema ERP',
        };
      }
    );
    vi.mocked(financeDataService.getMultiDeltaBookCombinedReport).mockResolvedValue({
      deltaAccountingBookId: 'delta-1',
      deltaAccountingBookCode: 'IFRS_CONSOL_ADJ',
      deltaAccountingBookIds: ['delta-1'],
      deltaAccountingBookCodes: ['IFRS_CONSOL_ADJ'],
      baseAccountingBookId: 'ifrs-1',
      baseAccountingBookCode: 'IFRS',
      functionalCurrencyCode: 'GHS',
      asOfDate: '2026-09-21T00:00:00',
      baseTotal: 0,
      deltaTotal: 0,
      combinedTotal: 0,
      lines: [{
        accountId: 'receivable', accountNumber: '1100', accountName: 'Accounts Receivable', accountType: 'Asset',
        baseSignedBalance: 100000, deltaSignedBalance: -5000, combinedSignedBalance: 95000,
      }],
    });
  });

  it('preserves the selected historical date when Run Report is clicked', async () => {
    render(<TrialBalancePage />);

    await screen.findByRole('heading', { name: 'Trial Balance' });
    const dateInput =
      document.querySelector<HTMLInputElement>('input[type="date"]');
    if (!dateInput) {
      throw new Error('Expected the Trial Balance date input to render.');
    }

    fireEvent.change(dateInput, { target: { value: '2025-01-01' } });
    expect(dateInput).toHaveValue('2025-01-01');

    fireEvent.click(screen.getByRole('button', { name: 'Run Report' }));

    await waitFor(() =>
      expect(financeDataService.getTrialBalance).toHaveBeenLastCalledWith(
        expect.objectContaining({ asAtDate: '2025-01-01' })
      )
    );
    expect(dateInput).toHaveValue('2025-01-01');

    fireEvent.click(screen.getByRole('button', { name: 'Print' }));
    await waitFor(() =>
      expect(documentOutputService.printReportDocument).toHaveBeenCalledWith(
        'FinanceTrialBalance',
        expect.objectContaining({ asAtDate: '2025-01-01' })
      )
    );

    fireEvent.click(screen.getByRole('button', { name: 'Export' }));
    await waitFor(() =>
      expect(documentOutputService.downloadReportDocument).toHaveBeenCalledWith(
        'FinanceTrialBalance',
        expect.objectContaining({ asAtDate: '2025-01-01' })
      )
    );
  });

  it('runs and prints a Base + Delta reporting view from Trial Balance', async () => {
    render(<TrialBalancePage />);
    await screen.findByRole('heading', { name: 'Trial Balance' });

    fireEvent.click(screen.getByLabelText('Reporting view'));
    fireEvent.click(await screen.findByText('Base + Delta'));
    fireEvent.click(screen.getByRole('button', { name: 'Run Report' }));

    await waitFor(() => expect(financeDataService.getMultiDeltaBookCombinedReport).toHaveBeenCalledWith(
      ['delta-1'],
      expect.any(String),
    ));
    expect(await screen.findByText('IFRS + IFRS_CONSOL_ADJ')).toBeInTheDocument();
    expect(screen.getByText('Accounts Receivable')).toBeInTheDocument();
    expect(screen.getByText('100,000.00 Dr')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Print' }));
    await waitFor(() => expect(documentOutputService.printReportDocument).toHaveBeenCalledWith(
      'Finance.BaseDeltaReport',
      expect.objectContaining({ deltaAccountingBookIds: 'delta-1' }),
    ));
  });
});
