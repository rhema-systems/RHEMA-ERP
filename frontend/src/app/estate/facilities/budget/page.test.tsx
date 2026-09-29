import React from 'react';
import { render, screen } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';

import { estateFacilitiesService } from '@/services/estate-facilities.service';
import FacilitiesBudgetPage from './page';

vi.mock('@/services/estate-facilities.service', () => ({
  estateFacilitiesService: { getBudgetYears: vi.fn(), getBudgetReport: vi.fn() },
}));

beforeEach(() => vi.clearAllMocks());

it('shows the setup state when no official budget exists', async () => {
  vi.mocked(estateFacilitiesService.getBudgetYears).mockResolvedValue([{
    id: 'year-1', fiscalYearName: 'Fiscal Year 2026', fiscalYearCode: '2026',
    startDate: '2026-01-01', endDate: '2026-12-31', hasOfficialBudget: false,
  }]);

  render(<FacilitiesBudgetPage />);

  expect(await screen.findByText('No official budget has been adopted for Fiscal Year 2026.')).toBeInTheDocument();
  expect(estateFacilitiesService.getBudgetReport).not.toHaveBeenCalled();
});

it('shows planned, actual, and variance for a Facilities budget', async () => {
  vi.mocked(estateFacilitiesService.getBudgetYears).mockResolvedValue([{
    id: 'year-1', fiscalYearName: 'Fiscal Year 2026', fiscalYearCode: '2026',
    startDate: '2026-01-01', endDate: '2026-12-31', hasOfficialBudget: true,
  }]);
  vi.mocked(estateFacilitiesService.getBudgetReport).mockResolvedValue({
    fiscalYearId: 'year-1', fiscalYearName: 'Fiscal Year 2026', currencyCode: 'GHS',
    scenarioName: 'Approved 2026', plannedAmount: 100, actualExpense: 130, variance: 30,
    lines: [{
      accountCode: '5000', accountName: 'Maintenance', periodCode: '2026-09',
      periodNumber: 9, plannedAmount: 100, actualExpense: 130, variance: 30,
    }],
  });

  render(<FacilitiesBudgetPage />);

  expect(await screen.findByText(/Approved 2026/)).toBeInTheDocument();
  expect(screen.getByText(/Maintenance/)).toBeInTheDocument();
  expect(screen.getAllByText('GHS 130.00')).toHaveLength(2);
  expect(estateFacilitiesService.getBudgetReport).toHaveBeenCalledWith('year-1');
});
