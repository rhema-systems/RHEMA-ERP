import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import CashFlowStatementPage from './page';
import { documentOutputService } from '@/services/document-output.service';
import { financeDataService } from '@/services/finance/finance-data.service';

vi.mock('@/services/document-output.service', () => ({
  DOCUMENT_TYPES: { financeCashFlowStatement: 'FinanceCashFlowStatement' },
  documentOutputService: {
    printReportDocument: vi.fn(),
    downloadReportDocument: vi.fn(),
  },
}));

vi.mock('@/services/finance/finance-data.service', () => ({
  financeDataService: {
    getFinanceSettings: vi.fn(),
    getAccountingBooks: vi.fn(),
    getCashFlowStatement: vi.fn(),
  },
}));

const emptySection = (sectionName: string, sectionOrder: number) => ({
  sectionName,
  sectionOrder,
  lineItems: [],
  sectionTotal: 0,
});

describe('CashFlowStatementPage report dates', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(financeDataService.getFinanceSettings).mockResolvedValue({
      baseCurrency: 'GHS',
    } as never);
    vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([
      { code: 'IFRS', name: 'IFRS' },
    ] as never);
    vi.mocked(financeDataService.getCashFlowStatement).mockImplementation(
      async (request) => ({
        companyName: 'Finance Demo',
        periodStart: request.periodStart,
        periodEnd: request.periodEnd,
        bookClassification: request.bookClassification ?? 'IFRS',
        currencyCode: 'GHS',
        method: request.method ?? 'Indirect',
        presentationWarnings: [],
        operatingActivities: emptySection('Operating Activities', 1),
        investingActivities: emptySection('Investing Activities', 2),
        financingActivities: emptySection('Financing Activities', 3),
        netCashFromOperating: 0,
        netCashFromInvesting: 0,
        netCashFromFinancing: 0,
        netIncreaseInCash: 0,
        cashAtBeginning: 0,
        cashAtEnd: 0,
        isReconciled: true,
      })
    );
  });

  it('uses and preserves the selected historical range for run, print, and export', async () => {
    render(<CashFlowStatementPage />);

    await screen.findByRole('heading', { name: 'Cash Flow Statement' });
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
      expect(financeDataService.getCashFlowStatement).toHaveBeenLastCalledWith(
        expect.objectContaining(expectedDates)
      )
    );
    expect(dateInputs[0]).toHaveValue('2025-01-01');
    expect(dateInputs[1]).toHaveValue('2025-01-31');

    fireEvent.click(screen.getByRole('button', { name: 'Print' }));
    await waitFor(() =>
      expect(documentOutputService.printReportDocument).toHaveBeenCalledWith(
        'FinanceCashFlowStatement',
        expect.objectContaining(expectedDates)
      )
    );

    fireEvent.click(screen.getByRole('button', { name: 'Export' }));
    await waitFor(() =>
      expect(documentOutputService.downloadReportDocument).toHaveBeenCalledWith(
        'FinanceCashFlowStatement',
        expect.objectContaining(expectedDates)
      )
    );
  });
});
