import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import MultiCurrencyReportPage from './page';
import { documentOutputService } from '@/services/document-output.service';
import { financeDataService } from '@/services/finance/finance-data.service';

vi.mock('@/services/document-output.service', () => ({
  DOCUMENT_TYPES: { financeMultiCurrencyDetail: 'FinanceMultiCurrencyDetail' },
  documentOutputService: {
    printReportDocument: vi.fn(),
    downloadReportDocument: vi.fn(),
  },
}));

vi.mock('@/services/finance/finance-data.service', () => ({
  financeDataService: {
    getFinanceSettings: vi.fn(),
    getMultiCurrencyDetailReport: vi.fn(),
  },
}));

describe('MultiCurrencyReportPage report dates', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(financeDataService.getFinanceSettings).mockResolvedValue({
      baseCurrency: 'GHS',
      coaType: 'Standard',
    } as never);
    vi.mocked(
      financeDataService.getMultiCurrencyDetailReport
    ).mockImplementation(
      async (request) =>
        ({
          periodStart: request.startDate,
          periodEnd: request.endDate,
          baseCurrencyCode: 'GHS',
          accounts: [],
        }) as never
    );
  });

  it('uses and preserves the selected historical range for run, print, and export', async () => {
    render(<MultiCurrencyReportPage />);

    await screen.findByRole('heading', { name: 'Multi-Currency Detail' });
    const dateInputs =
      document.querySelectorAll<HTMLInputElement>('input[type="date"]');
    expect(dateInputs).toHaveLength(2);

    fireEvent.change(dateInputs[0], { target: { value: '2025-01-01' } });
    fireEvent.change(dateInputs[1], { target: { value: '2025-01-31' } });
    fireEvent.click(screen.getByRole('button', { name: 'Run Report' }));

    const expectedDates = { startDate: '2025-01-01', endDate: '2025-01-31' };
    await waitFor(() =>
      expect(
        financeDataService.getMultiCurrencyDetailReport
      ).toHaveBeenLastCalledWith(expect.objectContaining(expectedDates))
    );
    expect(dateInputs[0]).toHaveValue('2025-01-01');
    expect(dateInputs[1]).toHaveValue('2025-01-31');

    fireEvent.click(screen.getByRole('button', { name: 'Print' }));
    await waitFor(() =>
      expect(documentOutputService.printReportDocument).toHaveBeenCalledWith(
        'FinanceMultiCurrencyDetail',
        expect.objectContaining(expectedDates)
      )
    );

    fireEvent.click(screen.getByRole('button', { name: 'Export' }));
    await waitFor(() =>
      expect(documentOutputService.downloadReportDocument).toHaveBeenCalledWith(
        'FinanceMultiCurrencyDetail',
        expect.objectContaining(expectedDates)
      )
    );
  });
});
