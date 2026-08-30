import { describe, expect, it } from 'vitest';
import type {
  GovernedOpeningBalanceOptions,
  OpeningBalanceBatch,
} from '@/types/finance';
import {
  canLoadOpeningBalanceQueries,
  canPostOpeningBalanceBatch,
  getBankOpeningBlockers,
  hasCompleteGovernedOpeningHeader,
  isOpeningBalanceBatchImmutable,
  openingBalanceQueryKeys,
} from './opening-balance-governance';

const options: GovernedOpeningBalanceOptions = {
  functionalCurrencyCode: 'GHS',
  blockers: ['Retained Earnings Account is not configured.'],
  bankAccounts: [
    {
      id: 'bank-1',
      accountNumber: '001',
      accountName: 'Operating Bank',
      bankName: 'Demo Bank',
      currencyCode: 'GHS',
      glAccountId: 'gl-bank',
      glAccountCode: '1000',
      glAccountName: 'Bank',
      postingDirection: 'Debit',
      exchangeRate: 1,
      isEligible: true,
      blockers: [],
    },
  ],
  accruedExpensesAccounts: [],
  shareCapitalAccounts: [],
  migrationClearingAccount: {
    accountId: 'clearing',
    accountCode: '1990',
    accountName: 'Migration Clearing',
    postingDirection: 'Debit',
    isEligible: true,
    blockers: [],
  },
};

describe('opening balance governance helpers', () => {
  it('partitions every query key by tenant', () => {
    const request = {
      openingDate: '2025-01-01',
      fiscalPeriodId: 'period-1',
      bookClassification: 'IFRS',
    };
    expect(
      openingBalanceQueryKeys.governedOptions('TENANT-A', request)
    ).not.toEqual(openingBalanceQueryKeys.governedOptions('TENANT-B', request));
    expect(openingBalanceQueryKeys.openingStockOptions(null)).toContain(
      'missing-tenant'
    );
  });

  it('partitions governed options by the complete dated header', () => {
    const request = {
      openingDate: '2025-01-01',
      fiscalPeriodId: 'period-1',
      bookClassification: 'IFRS',
    };
    const key = openingBalanceQueryKeys.governedOptions('TENANT-A', request);

    expect(key).toEqual([
      'finance',
      'opening-balances',
      'TENANT-A',
      'governed-options',
      '2025-01-01',
      'period-1',
      'IFRS',
    ]);
    expect(
      openingBalanceQueryKeys.governedOptions('TENANT-A', {
        ...request,
        openingDate: '2025-02-01',
      })
    ).not.toEqual(key);
    expect(
      openingBalanceQueryKeys.governedOptions('TENANT-A', {
        ...request,
        fiscalPeriodId: 'period-2',
      })
    ).not.toEqual(key);
    expect(
      openingBalanceQueryKeys.governedOptions('TENANT-A', {
        ...request,
        bookClassification: 'TAX',
      })
    ).not.toEqual(key);
  });

  it('loads dated governed options only after every header field is present', () => {
    const request = {
      openingDate: '2025-01-01',
      fiscalPeriodId: 'period-1',
      bookClassification: 'IFRS',
    };

    expect(hasCompleteGovernedOpeningHeader(request)).toBe(true);
    expect(
      hasCompleteGovernedOpeningHeader({ ...request, openingDate: '' })
    ).toBe(false);
    expect(
      hasCompleteGovernedOpeningHeader({ ...request, fiscalPeriodId: ' ' })
    ).toBe(false);
    expect(
      hasCompleteGovernedOpeningHeader({
        ...request,
        bookClassification: '',
      })
    ).toBe(false);
  });

  it('permits posting and posting retries only for backend-supported statuses', () => {
    expect(canPostOpeningBalanceBatch('Approved')).toBe(true);
    expect(canPostOpeningBalanceBatch('PostingFailed')).toBe(true);
    expect(canPostOpeningBalanceBatch('Failed')).toBe(false);
    expect(canPostOpeningBalanceBatch('Posted')).toBe(false);
  });

  it('waits for auth, tenant hydration, a tenant identity and view authority', () => {
    expect(
      canLoadOpeningBalanceQueries({
        authLoading: false,
        tenantLoading: false,
        tenantCode: 'TDC',
        canView: true,
      })
    ).toBe(true);
    expect(
      canLoadOpeningBalanceQueries({
        authLoading: true,
        tenantLoading: false,
        tenantCode: 'TDC',
        canView: true,
      })
    ).toBe(false);
    expect(
      canLoadOpeningBalanceQueries({
        authLoading: false,
        tenantLoading: true,
        tenantCode: 'TDC',
        canView: true,
      })
    ).toBe(false);
    expect(
      canLoadOpeningBalanceQueries({
        authLoading: false,
        tenantLoading: false,
        tenantCode: null,
        canView: true,
      })
    ).toBe(false);
    expect(
      canLoadOpeningBalanceQueries({
        authLoading: false,
        tenantLoading: false,
        tenantCode: 'TDC',
        canView: false,
      })
    ).toBe(false);
  });

  it('locks system-generated batches while retaining backward-safe free-form behavior', () => {
    expect(
      isOpeningBalanceBatchImmutable({
        isSystemGenerated: true,
        isEditable: false,
      } as OpeningBalanceBatch)
    ).toBe(true);
    expect(
      isOpeningBalanceBatchImmutable({
        isSystemGenerated: false,
        isEditable: false,
      } as OpeningBalanceBatch)
    ).toBe(true);
    expect(
      isOpeningBalanceBatchImmutable({
        isSystemGenerated: false,
        isEditable: true,
      } as OpeningBalanceBatch)
    ).toBe(false);
  });

  it('does not let a residual-only retained-earnings blocker disable an eligible bank source', () => {
    expect(getBankOpeningBlockers(options, 'bank-1')).toEqual([]);

    expect(
      getBankOpeningBlockers(
        { ...options, migrationClearingAccount: undefined },
        'bank-1'
      )
    ).toEqual([
      'Migration Clearing Account is not available for the governed bank opening.',
    ]);
  });
});
