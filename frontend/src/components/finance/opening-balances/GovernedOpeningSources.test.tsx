import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { GovernedOpeningBalanceOptions } from '@/types/finance';
import type { OpeningStockOptions } from '@/lib/finance/opening-balance-governance';
import { GovernedOpeningSources } from './GovernedOpeningSources';

const financeOptions: GovernedOpeningBalanceOptions = {
  functionalCurrencyCode: 'GHS',
  blockers: [],
  bankAccounts: [
    {
      id: 'bank-1',
      accountNumber: '001',
      accountName: 'Operating Bank',
      bankName: 'Demo Bank',
      currencyCode: 'GHS',
      glAccountId: 'gl-bank',
      glAccountCode: '1000',
      glAccountName: 'Operating Bank GL',
      postingDirection: 'Debit',
      isEligible: true,
      blockers: [],
    },
  ],
  accruedExpensesAccounts: [
    {
      id: 'accrued-1',
      accountCode: '2100',
      accountName: 'Accrued Expenses',
      accountType: 'Liability',
      postingDirection: 'Credit',
    },
  ],
  shareCapitalAccounts: [
    {
      id: 'capital-1',
      accountCode: '3000',
      accountName: 'Share Capital',
      accountType: 'Equity',
      postingDirection: 'Credit',
    },
  ],
  migrationClearingAccount: {
    accountId: 'clearing',
    accountCode: '1990',
    accountName: 'Migration Clearing',
    postingDirection: 'Debit',
    isEligible: true,
    blockers: [],
  },
  retainedEarningsAccount: {
    accountId: 'retained',
    accountCode: '3100',
    accountName: 'Retained Earnings',
    postingDirection: 'Credit',
    isEligible: true,
    blockers: [],
  },
};

const blockedInventoryOptions: OpeningStockOptions = {
  isReady: false,
  blockers: [
    'No eligible opening-stock items are available for this tenant.',
    'Project Demo Warehouse has no eligible locations.',
  ],
  retrievedAtUtc: '2026-08-20T10:00:00Z',
  warehouses: [
    {
      id: 'warehouse-1',
      code: 'DEMO',
      name: 'Project Demo Warehouse',
      locations: [],
    },
  ],
  items: [],
};

const handlers = {
  onPrepareBank: vi.fn(async () => undefined),
  onPrepareResidual: vi.fn(async () => undefined),
  onPrepareInventory: vi.fn(async () => ({
    id: 'adjustment-1',
    adjustmentNumber: 'ADJ-1',
    status: 'Draft',
    rowVersion: 'v1',
  })),
  onOpeningDateChange: vi.fn(),
  onFiscalPeriodChange: vi.fn(),
  onBookClassificationChange: vi.fn(),
};

const periodOptions = [
  { id: 'period-1', periodCode: '2025-01', periodName: 'January 2025' },
  { id: 'period-2', periodCode: '2025-02', periodName: 'February 2025' },
];

const bookOptions = [{ code: 'IFRS', name: 'IFRS' }];

function renderPanel(key = 'TENANT-A:2025-01-01:period-1:IFRS') {
  return render(
    <GovernedOpeningSources
      key={key}
      openingDate="2025-01-01"
      fiscalPeriodId="period-1"
      fiscalPeriodCode="2025-01"
      bookClassification="IFRS"
      periodOptions={periodOptions}
      bookOptions={bookOptions}
      financeOptions={financeOptions}
      openingStockOptions={blockedInventoryOptions}
      canPrepareFinance
      canPrepareInventory
      busyAction={null}
      {...handlers}
    />
  );
}

describe('GovernedOpeningSources', () => {
  it('renders the three governed sources and keeps derived accounts and directions read-only', () => {
    renderPanel();

    expect(screen.getByText('1. Bank opening')).toBeInTheDocument();
    expect(
      screen.getByText('2. Inventory opening evidence')
    ).toBeInTheDocument();
    expect(screen.getByText('3. Residual GL & equity')).toBeInTheDocument();
    expect(screen.getByText(/3100 — Retained Earnings/)).toBeInTheDocument();
    expect(screen.getByText(/1990 — Migration Clearing/)).toBeInTheDocument();
    expect(
      screen.getByText('Server-derived Inventory Control')
    ).toBeInTheDocument();
    expect(screen.queryByLabelText(/account id/i)).not.toBeInTheDocument();
  });

  it('lets an Inventory-only maker select the governed accounting header', () => {
    render(
      <GovernedOpeningSources
        openingDate="2026-08-20"
        fiscalPeriodId="period-2"
        fiscalPeriodCode="2025-02"
        bookClassification="IFRS"
        periodOptions={periodOptions}
        bookOptions={bookOptions}
        financeOptions={undefined}
        openingStockOptions={blockedInventoryOptions}
        canPrepareFinance={false}
        canPrepareInventory
        busyAction={null}
        {...handlers}
      />
    );

    const openingDate = screen.getByLabelText('Governed opening date');
    expect(openingDate).toBeEnabled();
    fireEvent.change(openingDate, { target: { value: '2025-01-01' } });
    expect(handlers.onOpeningDateChange).toHaveBeenCalledWith('2025-01-01');

    fireEvent.click(
      screen.getByRole('combobox', { name: 'Governed fiscal period' })
    );
    fireEvent.click(
      screen.getByRole('option', { name: '2025-01 - January 2025' })
    );
    expect(handlers.onFiscalPeriodChange).toHaveBeenCalledWith('period-1');
  });

  it('surfaces authoritative Inventory readiness blockers and disables preparation', () => {
    renderPanel();

    expect(
      screen.getByText(
        'No eligible opening-stock items are available for this tenant.'
      )
    ).toBeInTheDocument();
    expect(
      screen.getByText('Project Demo Warehouse has no eligible locations.')
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Prepare immutable opening stock' })
    ).toBeDisabled();
  });

  it('does not let an out-of-scope Inventory blocker disable an eligible bank source', () => {
    renderPanel();

    fireEvent.click(screen.getByRole('combobox', { name: 'Bank account' }));
    fireEvent.click(
      screen.getByRole('option', { name: /Operating Bank.*001.*GHS/ })
    );
    fireEvent.change(screen.getByLabelText('Opening amount'), {
      target: { value: '750000' },
    });
    fireEvent.change(
      screen.getByLabelText('Source schedule reference', {
        selector: '#bank-source-reference',
      }),
      { target: { value: 'BANK-SCHEDULE-2025' } }
    );

    expect(
      screen.getByRole('button', { name: 'Prepare immutable bank batch' })
    ).toBeEnabled();
    expect(
      screen.getByRole('button', { name: 'Prepare immutable opening stock' })
    ).toBeDisabled();
  });

  it('requires source schedules and positive source values before any governed action', () => {
    renderPanel();

    expect(screen.getAllByText('Source schedule reference')).toHaveLength(3);
    expect(
      screen.getByRole('button', { name: 'Prepare immutable bank batch' })
    ).toBeDisabled();
    expect(
      screen.getByRole('button', { name: 'Prepare immutable residual batch' })
    ).toBeDisabled();
  });

  it('drops all local drafts when the tenant or opening header remount key changes', () => {
    const view = renderPanel();
    const bankAmount = screen.getByLabelText('Opening amount');
    const bankReference = screen.getByLabelText('Source schedule reference', {
      selector: '#bank-source-reference',
    });
    fireEvent.change(bankAmount, { target: { value: '750000' } });
    fireEvent.change(bankReference, {
      target: { value: 'BANK-SCHEDULE-2025' },
    });
    expect(bankAmount).toHaveValue(750000);
    expect(bankReference).toHaveValue('BANK-SCHEDULE-2025');

    view.rerender(
      <GovernedOpeningSources
        key="TENANT-B:2025-02-01:period-2:IFRS"
        openingDate="2025-02-01"
        fiscalPeriodId="period-2"
        fiscalPeriodCode="2025-02"
        bookClassification="IFRS"
        periodOptions={periodOptions}
        bookOptions={bookOptions}
        financeOptions={financeOptions}
        openingStockOptions={blockedInventoryOptions}
        canPrepareFinance
        canPrepareInventory
        busyAction={null}
        {...handlers}
      />
    );

    expect(screen.getByLabelText('Opening amount')).toHaveValue(null);
    expect(
      screen.getByLabelText('Source schedule reference', {
        selector: '#bank-source-reference',
      })
    ).toHaveValue('');
  });
});
