import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import IncomeStatementPage from './page';
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
  DOCUMENT_TYPES: { financeIncomeStatement: 'FinanceIncomeStatement' },
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
    getIncomeStatement: vi.fn(),
  },
}));

describe('IncomeStatementPage report dates', () => {
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
    vi.mocked(financeDataService.getIncomeStatement).mockImplementation(
      async (request) =>
        ({
          periodStart: request.periodStart,
          periodEnd: request.periodEnd,
          bookClassification: request.bookClassification,
          currencyCode: 'GHS',
          sections: [],
          netProfit: 0,
        }) as never
    );
  });

  it('uses and preserves the selected historical range for run, print, and export', async () => {
    render(<IncomeStatementPage />);

    await screen.findByRole('heading', { name: 'Income Statement', level: 1 });
    const dateInputs =
      document.querySelectorAll<HTMLInputElement>('input[type="date"]');
    expect(dateInputs).toHaveLength(2);

    fireEvent.change(dateInputs[0], { target: { value: '2025-01-01' } });
    fireEvent.change(dateInputs[1], { target: { value: '2025-01-31' } });
    fireEvent.click(screen.getByRole('button', { name: 'Run Report' }));

    const expectedDates = {
      periodStart: '2025-01-01',
      periodEnd: '2025-01-31',
    };
    await waitFor(() =>
      expect(financeDataService.getIncomeStatement).toHaveBeenLastCalledWith(
        expect.objectContaining(expectedDates)
      )
    );
    expect(dateInputs[0]).toHaveValue('2025-01-01');
    expect(dateInputs[1]).toHaveValue('2025-01-31');

    fireEvent.click(screen.getByRole('button', { name: 'Print' }));
    await waitFor(() =>
      expect(documentOutputService.printReportDocument).toHaveBeenCalledWith(
        'FinanceIncomeStatement',
        expect.objectContaining(expectedDates)
      )
    );

    fireEvent.click(screen.getByRole('button', { name: 'Export' }));
    await waitFor(() =>
      expect(documentOutputService.downloadReportDocument).toHaveBeenCalledWith(
        'FinanceIncomeStatement',
        expect.objectContaining(expectedDates)
      )
    );
  });
});
