import React from 'react';
import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type {
  GovernedOpeningBalanceOptions,
  SubledgerOpeningBalanceReadiness,
} from '@/types/finance';
import type { OpeningStockOptions } from '@/lib/finance/opening-balance-governance';
import { GovernedOpeningPreflight } from './GovernedOpeningPreflight';

const subledgerReadiness: SubledgerOpeningBalanceReadiness = {
  apOpeningInvoiceCount: 2,
  postedApOpeningInvoiceCount: 2,
  apOpeningInvoiceFunctionalAmount: 220000,
  arOpeningInvoiceCount: 2,
  postedArOpeningInvoiceCount: 1,
  arOpeningInvoiceFunctionalAmount: 300000,
  supplierAdvanceOpeningCount: 0,
  postedSupplierAdvanceOpeningCount: 0,
  supplierAdvanceOpeningFunctionalAmount: 0,
  customerAdvanceOpeningCount: 0,
  postedCustomerAdvanceOpeningCount: 0,
  customerAdvanceOpeningFunctionalAmount: 0,
  apWithholdingOpeningCount: 0,
  postedApWithholdingOpeningCount: 0,
  apWithholdingOpeningAmount: 0,
  arWithholdingOpeningCount: 0,
  postedArWithholdingOpeningCount: 0,
  arWithholdingOpeningAmount: 0,
  fixedAssetOpeningBookValueCount: 1,
  postedFixedAssetOpeningBookValueCount: 1,
  fixedAssetOpeningCost: 250000,
  fixedAssetOpeningAccumulatedDepreciation: 60000,
  fixedAssetOpeningNetBookValue: 190000,
  fixedAssetCandidates: [],
  warnings: [],
};

const financeOptions: GovernedOpeningBalanceOptions = {
  functionalCurrencyCode: 'GHS',
  bankAccounts: [],
  accruedExpensesAccounts: [],
  shareCapitalAccounts: [],
  blockers: ['Retained Earnings Account is not configured.'],
};

const blockedInventoryOptions: OpeningStockOptions = {
  isReady: false,
  blockers: ['No eligible locations.', 'No eligible items.'],
  retrievedAtUtc: '2026-08-20T10:00:00Z',
  warehouses: [],
  items: [],
};

const defaultProps = {
  subledgerReadiness,
  subledgerReadinessLoading: false,
  subledgerReadinessError: false,
  financeOptions,
  financeOptionsLoading: false,
  financeOptionsError: false,
  financeHeaderComplete: true,
  inventoryOptions: blockedInventoryOptions,
  inventoryOptionsLoading: false,
  inventoryOptionsError: false,
  inventoryAvailable: true,
};

describe('GovernedOpeningPreflight', () => {
  it('summarises cross-source readiness and makes scope-specific stop-lines explicit', () => {
    render(<GovernedOpeningPreflight {...defaultProps} />);

    expect(screen.getByText('Opening-pack preflight')).toBeInTheDocument();
    expect(
      within(screen.getByTestId('preflight-ap')).getByText('Ready')
    ).toBeInTheDocument();
    expect(
      within(screen.getByTestId('preflight-ap')).getByText('2/2 posted.')
    ).toBeInTheDocument();
    expect(
      within(screen.getByTestId('preflight-ar')).getByText('Incomplete')
    ).toBeInTheDocument();
    expect(
      within(screen.getByTestId('preflight-ar')).getByText(
        '1/2 posted; stop if AR is required.'
      )
    ).toBeInTheDocument();
    expect(
      within(screen.getByTestId('preflight-finance')).getByText('Review')
    ).toBeInTheDocument();
    expect(
      within(screen.getByTestId('preflight-inventory')).getByText(
        'Blocked if in scope'
      )
    ).toBeInTheDocument();
    expect(
      screen.getByText(/advisory, not a product-wide dependency/i)
    ).toBeInTheDocument();
  });

  it('does not claim Inventory is required when the current operator cannot prepare it', () => {
    render(
      <GovernedOpeningPreflight
        {...defaultProps}
        inventoryAvailable={false}
        inventoryOptions={undefined}
      />
    );

    const inventory = within(screen.getByTestId('preflight-inventory'));
    expect(inventory.getByText('Confirm owner')).toBeInTheDocument();
    expect(
      inventory.getByText(
        'Ask the Inventory owner only when this source is in scope.'
      )
    ).toBeInTheDocument();
  });
});
