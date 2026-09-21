import React from 'react';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import DeltaLedgerPage from './page';
import { financeDataService } from '@/services/finance/finance-data.service';

vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'delta-1' }) }));
vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({ isLoading: false, hasPermission: (permission: string) => permission === 'Finance.Read' }),
}));
vi.mock('@/services/finance/finance-data.service', () => ({
  financeDataService: { getDeltaBookLedger: vi.fn() },
}));

describe('DeltaLedgerPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(financeDataService.getDeltaBookLedger).mockResolvedValue({
      deltaAccountingBookId: 'delta-1', deltaAccountingBookCode: 'IFRS_ADJUSTMENTS',
      baseAccountingBookId: 'base-1', baseAccountingBookCode: 'BASE', functionalCurrencyCode: 'GHS',
      entries: [
        { journalEntryId: 'base-journal', journalEntryNumber: 'BASE-001', accountingDate: '2026-09-20', description: 'Base sale', sourceBookCode: 'BASE', layer: 'Inherited', totalDebit: 100, totalCredit: 100 },
        { journalEntryId: 'delta-journal', journalEntryNumber: 'DELTA-001', accountingDate: '2026-09-21', description: 'IFRS adjustment', sourceBookCode: 'IFRS_ADJUSTMENTS', layer: 'Adjustment', totalDebit: 25, totalCredit: 25 },
      ],
    });
  });

  it('shows inherited base journals and Delta adjustments as distinct layers', async () => {
    render(<DeltaLedgerPage />);

    expect(await screen.findByText('BASE-001')).toBeInTheDocument();
    expect(screen.getByText('DELTA-001')).toBeInTheDocument();
    expect(screen.getByText('Inherited')).toBeInTheDocument();
    expect(screen.getByText('Adjustment')).toBeInTheDocument();
    expect(financeDataService.getDeltaBookLedger).toHaveBeenCalledWith('delta-1', undefined, undefined);
  });
});
