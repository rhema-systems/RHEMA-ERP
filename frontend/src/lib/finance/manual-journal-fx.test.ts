import { describe, expect, it } from 'vitest';
import type {
  Account,
  AccountCurrencyLink,
  Currency,
  FinanceSettings,
} from '@/types/finance';
import {
  applyCanonicalJournalRate,
  getAllowedJournalCurrencies,
  getManualJournalFxBlocker,
  getManualJournalRateRequest,
  requireFunctionalCurrency,
  roundJournalMoney,
} from './manual-journal-fx';

const account = (overrides: Partial<Account> = {}) =>
  ({
    id: 'account-1',
    accountNumber: '6100',
    accountName: 'Travel expense',
    currencyCode: 'GHS',
    isMultiCurrency: false,
    ...overrides,
  }) as Account;

const link = (overrides: Partial<AccountCurrencyLink> = {}) =>
  ({
    id: 'link-1',
    accountId: 'account-1',
    linkedCurrencyCode: 'USD',
    isActive: true,
    transactionRateType: 'Month-End',
    transactionQuoteSide: 'Selling',
    effectiveDate: '2024-01-01',
    ...overrides,
  }) as AccountCurrencyLink;

const currency = (currencyCode: string, isActive = true) =>
  ({ currencyCode, isActive }) as Currency;

const settings = (overrides: Partial<FinanceSettings> = {}) =>
  ({
    baseCurrency: 'GHS',
    directionalExchangeRatePolicyEnabled: true,
    defaultTransactionQuoteSide: 'Bid',
    ...overrides,
  }) as FinanceSettings;

describe('manual journal FX policy', () => {
  it('requires a valid Finance functional currency instead of assuming one', () => {
    expect(requireFunctionalCurrency(settings({ baseCurrency: ' ghs ' }))).toBe(
      'GHS'
    );
    expect(() =>
      requireFunctionalCurrency(settings({ baseCurrency: '' }))
    ).toThrow('Finance Settings does not contain a valid functional currency.');
  });

  it('limits ordinary accounts to their own active currency', () => {
    expect(
      getAllowedJournalCurrencies(
        account({ currencyCode: 'EUR', isMultiCurrency: false }),
        [link()],
        [currency('GHS'), currency('EUR'), currency('USD')],
        'GHS',
        '2025-01-01'
      )
    ).toEqual(['EUR']);
  });

  it('uses only active catalogue currencies linked to a multi-currency account', () => {
    expect(
      getAllowedJournalCurrencies(
        account({ isMultiCurrency: true }),
        [
          link(),
          link({
            id: 'inactive-link',
            linkedCurrencyCode: 'EUR',
            isActive: false,
          }),
        ],
        [
          currency('GHS'),
          currency('USD'),
          currency('EUR'),
          currency('GBP', false),
        ],
        'GHS',
        '2025-01-01'
      )
    ).toEqual(['GHS', 'USD']);
  });

  it('excludes a currency link outside the journal date', () => {
    expect(
      getAllowedJournalCurrencies(
        account({ isMultiCurrency: true }),
        [link({ effectiveDate: '2025-02-01' })],
        [currency('GHS'), currency('USD')],
        'GHS',
        '2025-01-01'
      )
    ).toEqual(['GHS']);
  });

  it('uses the account link directional policy for the selected journal date', () => {
    expect(
      getManualJournalRateRequest(
        account({ isMultiCurrency: true }),
        'usd',
        [link()],
        settings(),
        '2025-01-01'
      )
    ).toEqual({
      baseCurrencyCode: 'GHS',
      effectiveDate: '2025-01-01',
      rateType: 'MonthEnd',
      quoteSide: 'Selling',
    });
  });

  it('uses Daily/Mid when directional policy is disabled', () => {
    expect(
      getManualJournalRateRequest(
        account({ isMultiCurrency: true }),
        'USD',
        [link()],
        settings({ directionalExchangeRatePolicyEnabled: false }),
        '2025-01-01'
      )
    ).toMatchObject({ rateType: 'Daily', quoteSide: 'Mid' });
  });

  it('applies the approved snapshot and recalculates the functional amount', () => {
    const result = applyCanonicalJournalRate(
      {
        currencyCode: 'USD',
        exchangeRate: '' as const,
        foreignDebit: 100,
        debit: 0,
        credit: 0,
        rateStatus: 'loading' as const,
      },
      {
        id: 'rate-1',
        rate: 12.5,
        currentExchangeRate: 12.5,
        rateSource: 'Approved daily rate',
        effectiveDate: '2025-01-01',
      }
    );

    expect(result).toMatchObject({
      exchangeRateId: 'rate-1',
      exchangeRate: 12.5,
      debit: 1250,
      rateStatus: 'ready',
      rateSource: 'Approved daily rate',
      rateDate: '2025-01-01',
    });
  });

  it('rounds every converted functional amount to posting precision', () => {
    expect(roundJournalMoney(3 * 12.345)).toBe(37.04);

    const result = applyCanonicalJournalRate(
      {
        currencyCode: 'USD',
        exchangeRate: '' as const,
        foreignDebit: 3,
        debit: 0,
        credit: 0,
        rateStatus: 'loading' as const,
      },
      {
        id: 'rate-precision',
        rate: 12.345,
        currentExchangeRate: 12.345,
        rateSource: 'Approved daily rate',
        effectiveDate: '2025-01-01',
      }
    );

    expect(result.debit).toBe(37.04);
  });

  it('blocks foreign lines until an approved positive rate is ready', () => {
    expect(
      getManualJournalFxBlocker(
        { currencyCode: 'USD', exchangeRate: '', rateStatus: 'loading' },
        'GHS'
      )
    ).toContain('still loading');
    expect(
      getManualJournalFxBlocker(
        {
          currencyCode: 'USD',
          exchangeRate: '',
          rateStatus: 'error',
          rateError: 'Missing rate',
        },
        'GHS'
      )
    ).toBe('Missing rate');
    expect(
      getManualJournalFxBlocker(
        {
          currencyCode: 'USD',
          exchangeRateId: 'rate-1',
          exchangeRate: 12.5,
          rateStatus: 'ready',
          debit: 1250,
        },
        'GHS'
      )
    ).toBe('Enter the original USD debit amount.');
    expect(
      getManualJournalFxBlocker(
        {
          currencyCode: 'USD',
          exchangeRateId: 'rate-1',
          exchangeRate: 12.5,
          rateStatus: 'ready',
          debit: 1250,
          foreignDebit: 100,
        },
        'GHS'
      )
    ).toBeNull();
  });
});
