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

vi.mock('@/services/document-output.service', () => ({
  DOCUMENT_TYPES: { financeTrialBalance: 'FinanceTrialBalance' },
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
    getTrialBalance: vi.fn(),
  },
}));

describe('TrialBalancePage report date', () => {
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
    vi.mocked(financeDataService.getTrialBalance).mockImplementation(
      async (request) => ({
        asAtDate: request.asAtDate,
        bookClassification: request.bookClassification,
        currencyCode: 'GHS',
        lines: [],
        totalDebits: 0,
        totalCredits: 0,
        difference: 0,
        isBalanced: true,
      })
    );
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
});
