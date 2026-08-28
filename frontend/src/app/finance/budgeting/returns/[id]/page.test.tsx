import React, { Suspense } from 'react';
import { act, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import BudgetReturnEditorPage from './page';
import { budgetDataService } from '@/services/finance/budget-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({
    user: { id: 'admin-user', roles: ['SuperAdmin'] },
    hasPermission: () => true,
  }),
}));

vi.mock('@/services/finance/budget-data.service', () => ({
  budgetDataService: {
    getReturnById: vi.fn(),
    getScenarioById: vi.fn(),
    getEntries: vi.fn(),
    getReturnAuditHistory: vi.fn(),
    bulkSaveEntries: vi.fn(),
    submitReturn: vi.fn(),
    recallReturn: vi.fn(),
  },
}));

vi.mock('@/services/finance/finance-data.service', () => ({
  financeDataService: {
    getAccounts: vi.fn(),
    getFinanceDimensions: vi.fn(),
    getFiscalPeriods: vi.fn(),
  },
}));

describe('Budget return fiscal-period grid', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(budgetDataService.getReturnById).mockResolvedValue({
      id: 'return-1',
      budgetScenarioId: 'scenario-1',
      segmentValueName: 'Finance',
      status: 'Draft',
      rowVersion: 'return-version',
    } as never);
    vi.mocked(budgetDataService.getScenarioById).mockResolvedValue({
      id: 'scenario-1',
      name: 'FY2026 Budget',
      fiscalYearId: 'fy-2026',
      baseCurrencyCode: 'GHS',
      status: 'Collecting',
      controlDimensions: [],
    } as never);
    vi.mocked(budgetDataService.getEntries).mockResolvedValue([]);
    vi.mocked(budgetDataService.getReturnAuditHistory).mockResolvedValue([]);
    vi.mocked(financeDataService.getAccounts).mockResolvedValue([]);
    vi.mocked(financeDataService.getFinanceDimensions).mockResolvedValue([]);
    vi.mocked(financeDataService.getFiscalPeriods).mockResolvedValue([
      {
        id: 'period-9',
        fiscalYearId: 'fy-2026',
        periodNumber: 9,
        periodCode: '2026-09',
        periodName: 'September 2026',
        startDate: '2026-09-01',
        endDate: '2026-09-30',
        periodStatus: 'Open',
        isClosed: false,
        isLocked: false,
      },
    ] as never);
  });

  it('loads periods from the dedicated fiscal-period endpoint', async () => {
    await act(async () => {
      render(
        <Suspense fallback={<div>Loading</div>}>
          <BudgetReturnEditorPage
            params={Promise.resolve({ id: 'return-1' })}
          />
        </Suspense>
      );
      await Promise.resolve();
    });

    await waitFor(() =>
      expect(financeDataService.getFiscalPeriods).toHaveBeenCalledWith(
        'fy-2026'
      )
    );
    expect(
      await screen.findByRole('columnheader', { name: 'September 2026' })
    ).toBeInTheDocument();
    expect(screen.getByText('Account and period (legacy)')).toBeInTheDocument();
  });

  it('keeps the detailed account-period grid visible below the category tabs', async () => {
    await act(async () => {
      render(
        <Suspense fallback={<div>Loading</div>}>
          <BudgetReturnEditorPage
            params={Promise.resolve({ id: 'return-1' })}
          />
        </Suspense>
      );
      await Promise.resolve();
    });

    const workspace = await screen.findByTestId('budget-return-workspace');
    const grid = await screen.findByTestId('budget-account-period-grid');

    expect(workspace).toHaveClass('min-h-[calc(100vh-4rem)]');
    expect(workspace).not.toHaveClass('h-[calc(100vh-4rem)]');
    expect(grid).toHaveClass('min-h-[24rem]');
  });

  it('does not offer submission until the return has an assignee', async () => {
    await act(async () => {
      render(
        <Suspense fallback={<div>Loading</div>}>
          <BudgetReturnEditorPage
            params={Promise.resolve({ id: 'return-1' })}
          />
        </Suspense>
      );
      await Promise.resolve();
    });

    expect(
      await screen.findByText(
        'Assign this return from the scenario page before submission.'
      )
    ).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Submit Budget' })
    ).not.toBeInTheDocument();
  });

  it('offers submission to the assigned preparer', async () => {
    vi.mocked(budgetDataService.getReturnById).mockResolvedValueOnce({
      id: 'return-1',
      budgetScenarioId: 'scenario-1',
      segmentValueName: 'Finance',
      assignedToUserId: 'admin-user',
      status: 'Draft',
      rowVersion: 'return-version',
    } as never);

    await act(async () => {
      render(
        <Suspense fallback={<div>Loading</div>}>
          <BudgetReturnEditorPage
            params={Promise.resolve({ id: 'return-1' })}
          />
        </Suspense>
      );
      await Promise.resolve();
    });

    expect(
      await screen.findByRole('button', { name: 'Submit Budget' })
    ).toBeInTheDocument();
    expect(
      screen.queryByText(
        'Assign this return from the scenario page before submission.'
      )
    ).not.toBeInTheDocument();
  });
});
