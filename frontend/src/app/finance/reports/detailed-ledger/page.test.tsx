import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import DetailedLedgerPage from './page';
import { documentOutputService } from '@/services/document-output.service';
import { financeDataService } from '@/services/finance/finance-data.service';

vi.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/services/document-output.service', () => ({
  DOCUMENT_TYPES: { financeDetailedLedger: 'FinanceDetailedLedger' },
  documentOutputService: {
    printReportDocument: vi.fn(),
    downloadReportDocument: vi.fn(),
  },
}));

vi.mock('@/services/finance/finance-data.service', () => ({
  financeDataService: {
    getFinanceSettings: vi.fn(),
    getAccounts: vi.fn(),
    getAccountingBooks: vi.fn(),
    getDetailedLedger: vi.fn(),
  },
}));

describe('DetailedLedgerPage report dates', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(financeDataService.getFinanceSettings).mockResolvedValue({
      baseCurrency: 'GHS',
      coaType: 'Standard',
    } as never);
    vi.mocked(financeDataService.getAccounts).mockResolvedValue([]);
    vi.mocked(financeDataService.getAccountingBooks).mockResolvedValue([
      { code: 'IFRS', name: 'IFRS' },
    ] as never);
    vi.mocked(financeDataService.getDetailedLedger).mockImplementation(
      async (request) =>
        ({
          startDate: request.startDate,
          endDate: request.endDate,
          bookClassification: request.bookClassification,
          currencyCode: 'GHS',
          accounts: [],
          totalDebits: 0,
          totalCredits: 0,
        }) as never
    );
  });

  it('uses and preserves the selected historical range for run, print, and export', async () => {
    render(<DetailedLedgerPage />);

    await screen.findByRole('heading', { name: 'Detailed Ledger' });
    const dateInputs =
      document.querySelectorAll<HTMLInputElement>('input[type="date"]');
    expect(dateInputs).toHaveLength(2);

    fireEvent.change(dateInputs[0], { target: { value: '2025-01-01' } });
    fireEvent.change(dateInputs[1], { target: { value: '2025-01-31' } });
    fireEvent.click(screen.getByRole('button', { name: 'Run Report' }));

    const expectedDates = { startDate: '2025-01-01', endDate: '2025-01-31' };
    await waitFor(() =>
      expect(financeDataService.getDetailedLedger).toHaveBeenLastCalledWith(
        expect.objectContaining(expectedDates)
      )
    );
    expect(dateInputs[0]).toHaveValue('2025-01-01');
    expect(dateInputs[1]).toHaveValue('2025-01-31');

    fireEvent.click(screen.getByRole('button', { name: 'Print' }));
    await waitFor(() =>
      expect(documentOutputService.printReportDocument).toHaveBeenCalledWith(
        'FinanceDetailedLedger',
        expect.objectContaining(expectedDates)
      )
    );

    fireEvent.click(screen.getByRole('button', { name: 'Export' }));
    await waitFor(() =>
      expect(documentOutputService.downloadReportDocument).toHaveBeenCalledWith(
        'FinanceDetailedLedger',
        expect.objectContaining(expectedDates)
      )
    );
  });
});
