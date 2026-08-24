import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import BalanceSheetPage from './page';
import { documentOutputService } from '@/services/document-output.service';
import { financeDataService } from '@/services/finance/finance-data.service';

vi.mock('@/components/finance/reports/ReportSegmentFilters', () => ({
  ReportSegmentFilters: () => null,
}));

vi.mock('@/components/finance/reports/AppliedReportSegmentFilters', () => ({
  AppliedReportSegmentFilters: () => null,
}));

vi.mock('@/components/finance/reports/FinancialStatementLayoutRows', () => ({
  FinancialStatementLayoutRows: () => null,
}));

vi.mock('@/services/document-output.service', () => ({
  DOCUMENT_TYPES: { financeBalanceSheet: 'FinanceBalanceSheet' },
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
    getFinancialStatementLayouts: vi.fn(),
    getBalanceSheet: vi.fn(),
  },
}));

describe('BalanceSheetPage report date', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(financeDataService.getFinanceSettings).mockResolvedValue({
      baseCurrency: 'GHS',
      coaType: 'Standard',
    } as never);
    vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([
      { code: 'IFRS', name: 'IFRS' },
    ] as never);
    vi.mocked(financeDataService.getReportingDimensions).mockResolvedValue([]);
    vi.mocked(
      financeDataService.getFinancialStatementLayouts
    ).mockResolvedValue([]);
    vi.mocked(financeDataService.getBalanceSheet).mockImplementation(
      async (request) =>
        ({
          asAtDate: request.asAtDate,
          bookClassification: request.bookClassification,
          currencyCode: 'GHS',
          sections: [],
          totalAssets: 0,
          totalLiabilities: 0,
          totalEquity: 0,
          isBalanced: true,
          difference: 0,
        }) as never
    );
  });

  it('uses and preserves the selected historical date for run, print, and export', async () => {
    render(<BalanceSheetPage />);

    await screen.findByRole('heading', { name: 'Balance Sheet', level: 1 });
    const dateInput =
      document.querySelector<HTMLInputElement>('input[type="date"]');
    if (!dateInput)
      throw new Error('Expected the Balance Sheet date input to render.');

    fireEvent.change(dateInput, { target: { value: '2025-01-01' } });
    fireEvent.click(screen.getByRole('button', { name: 'Run Report' }));

    await waitFor(() =>
      expect(financeDataService.getBalanceSheet).toHaveBeenLastCalledWith(
        expect.objectContaining({ asAtDate: '2025-01-01' })
      )
    );
    expect(dateInput).toHaveValue('2025-01-01');

    fireEvent.click(screen.getByRole('button', { name: 'Print' }));
    await waitFor(() =>
      expect(documentOutputService.printReportDocument).toHaveBeenCalledWith(
        'FinanceBalanceSheet',
        expect.objectContaining({ asAtDate: '2025-01-01' })
      )
    );

    fireEvent.click(screen.getByRole('button', { name: 'Export' }));
    await waitFor(() =>
      expect(documentOutputService.downloadReportDocument).toHaveBeenCalledWith(
        'FinanceBalanceSheet',
        expect.objectContaining({ asAtDate: '2025-01-01' })
      )
    );
  });
});
