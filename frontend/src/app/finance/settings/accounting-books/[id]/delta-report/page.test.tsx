import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import DeltaCombinedReportPage from './page';
import { financeDataService } from '@/services/finance/finance-data.service';
import { documentOutputService } from '@/services/document-output.service';

vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'delta-1' }) }));
vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({ isLoading: false, hasPermission: (permission: string) => permission === 'Finance.Read' }),
}));
vi.mock('@/services/finance/finance-data.service', () => ({
  financeDataService: { getDeltaBookCombinedReport: vi.fn() },
}));
vi.mock('@/services/document-output.service', () => ({
  DOCUMENT_TYPES: { financeBaseDeltaReport: 'Finance.BaseDeltaReport' },
  documentOutputService: { printReportDocument: vi.fn() },
}));

const report = {
  deltaAccountingBookId: 'delta-1', deltaAccountingBookCode: 'IFRS_CONSOL_ADJ',
  baseAccountingBookId: 'ifrs-1', baseAccountingBookCode: 'IFRS', functionalCurrencyCode: 'GHS',
  asOfDate: '2026-09-21T00:00:00', baseTotal: 0, deltaTotal: 0, combinedTotal: 0,
  lines: [{ accountId: 'cash', accountNumber: '1000', accountName: 'Cash', accountType: 'Asset', baseSignedBalance: 1000, deltaSignedBalance: 125, combinedSignedBalance: 1125 }],
};

describe('DeltaCombinedReportPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(financeDataService.getDeltaBookCombinedReport).mockResolvedValue(report);
  });

  it('loads non-zero base-plus-Delta values and explains the net controls', async () => {
    render(<DeltaCombinedReportPage />);
    expect(await screen.findByText('IFRS + IFRS_CONSOL_ADJ')).toBeInTheDocument();
    expect(screen.getByText('Cash')).toBeInTheDocument();
    expect(screen.getByText(/Positive values are net debits/)).toBeInTheDocument();
    expect(screen.getByText(/reconciliation checks, not assets/)).toBeInTheDocument();
    expect(screen.getByText(/1,125.00/)).toBeInTheDocument();
  });

  it('refreshes the selected date and exports the evidence as CSV', async () => {
    const createObjectURL = vi.fn(() => 'blob:delta-report');
    const revokeObjectURL = vi.fn();
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: createObjectURL });
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revokeObjectURL });
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    render(<DeltaCombinedReportPage />);
    await screen.findByText('Cash');

    fireEvent.change(screen.getByLabelText('As of date'), { target: { value: '2026-08-31' } });
    fireEvent.click(screen.getByRole('button', { name: 'Refresh report' }));
    await waitFor(() => expect(financeDataService.getDeltaBookCombinedReport).toHaveBeenLastCalledWith('delta-1', '2026-08-31'));

    fireEvent.click(screen.getByRole('button', { name: 'Download CSV' }));
    expect(createObjectURL).toHaveBeenCalledOnce();
    expect(click).toHaveBeenCalledOnce();
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:delta-report');
    click.mockRestore();
  });

  it('uses the governed Finance document pipeline for printing', async () => {
    vi.mocked(documentOutputService.printReportDocument).mockResolvedValue(undefined);
    render(<DeltaCombinedReportPage />);
    await screen.findByText('Cash');

    fireEvent.click(screen.getByRole('button', { name: 'Print' }));

    await waitFor(() => expect(documentOutputService.printReportDocument).toHaveBeenCalledWith(
      'Finance.BaseDeltaReport',
      expect.objectContaining({ deltaAccountingBookId: 'delta-1' }),
    ));
  });
});
