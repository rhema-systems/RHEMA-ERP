/**
 * Central export file for all Finance module mock data
 * Provides easy access to mock data based on COA type and other settings
 */

import type { Account, Currency, ExchangeRate, FiscalYear, FiscalPeriod, JournalEntry, FinanceSettings, SegmentStructure } from '@/types/finance';
import { STANDARD_COA_ACCOUNTS } from './standard-accounts';
import { SEGMENTED_COA_ACCOUNTS } from './segmented-accounts';
import { ALL_SEGMENT_STRUCTURES } from './segment-structures';

// ===== ACCOUNTS =====
/**
 * Get accounts based on COA type
 */
export function getAccountsMockData(coaType: 'Standard' | 'Segmented'): Account[] {
    return coaType === 'Segmented' ? SEGMENTED_COA_ACCOUNTS : STANDARD_COA_ACCOUNTS;
}

export { STANDARD_COA_ACCOUNTS, SEGMENTED_COA_ACCOUNTS };

// ===== SEGMENTS =====
export { ALL_SEGMENT_STRUCTURES };

export function getSegmentStructures(): SegmentStructure[] {
    return ALL_SEGMENT_STRUCTURES;
}

// ===== CURRENCIES =====
export const MOCK_CURRENCIES: Currency[] = [
    {
        id: 'curr-1',
        tenantId: 'tenant-1',
        currencyCode: 'GHS',
        numericCode: '936',
        currencyName: 'Ghana Cedi',
        currencySymbol: '₵',
        decimalPlaces: 2,
        roundingMethod: 'Normal',
        roundingPrecision: 2,
        symbolPosition: 'Before',
        decimalSeparator: '.',
        thousandsSeparator: ',',
        countryCode: 'GH',
        isActive: true,
        isBaseCurrency: true,
        hasTransactionHistory: true,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'curr-2',
        tenantId: 'tenant-1',
        currencyCode: 'USD',
        numericCode: '840',
        currencyName: 'US Dollar',
        currencySymbol: '$',
        decimalPlaces: 2,
        roundingMethod: 'Normal',
        roundingPrecision: 2,
        symbolPosition: 'Before',
        decimalSeparator: '.',
        thousandsSeparator: ',',
        countryCode: 'US',
        isActive: true,
        isBaseCurrency: false,
        hasTransactionHistory: true,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'curr-3',
        tenantId: 'tenant-1',
        currencyCode: 'EUR',
        numericCode: '978',
        currencyName: 'Euro',
        currencySymbol: '€',
        decimalPlaces: 2,
        roundingMethod: 'Normal',
        roundingPrecision: 2,
        symbolPosition: 'Before',
        decimalSeparator: '.',
        thousandsSeparator: ',',
        countryCode: 'EU',
        isActive: true,
        isBaseCurrency: false,
        hasTransactionHistory: false,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'curr-4',
        tenantId: 'tenant-1',
        currencyCode: 'GBP',
        numericCode: '826',
        currencyName: 'British Pound',
        currencySymbol: '£',
        decimalPlaces: 2,
        roundingMethod: 'Normal',
        roundingPrecision: 2,
        symbolPosition: 'Before',
        decimalSeparator: '.',
        thousandsSeparator: ',',
        countryCode: 'GB',
        isActive: true,
        isBaseCurrency: false,
        hasTransactionHistory: true,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
];

// ===== EXCHANGE RATES =====
export const MOCK_EXCHANGE_RATES: ExchangeRate[] = [
    {
        id: 'er-1',
        tenantId: 'tenant-1',
        baseCurrencyCode: 'GHS',
        targetCurrencyCode: 'USD',
        rate: 0.08,
        rateType: 'Daily',
        quoteSide: 'Mid',
        effectiveDate: '2024-12-15T00:00:00Z',
        rateSource: 'Bank of Ghana',
        isActive: true,
        createdAt: '2024-12-15T00:00:00Z',
        updatedAt: '2024-12-15T00:00:00Z',
    },
    {
        id: 'er-2',
        tenantId: 'tenant-1',
        baseCurrencyCode: 'GHS',
        targetCurrencyCode: 'EUR',
        rate: 0.076,
        rateType: 'Daily',
        quoteSide: 'Mid',
        effectiveDate: '2024-12-15T00:00:00Z',
        rateSource: 'Bank of Ghana',
        isActive: true,
        createdAt: '2024-12-15T00:00:00Z',
        updatedAt: '2024-12-15T00:00:00Z',
    },
    {
        id: 'er-3',
        tenantId: 'tenant-1',
        baseCurrencyCode: 'GHS',
        targetCurrencyCode: 'GBP',
        rate: 0.063,
        rateType: 'Daily',
        quoteSide: 'Mid',
        effectiveDate: '2024-12-15T00:00:00Z',
        rateSource: 'Bank of Ghana',
        isActive: true,
        createdAt: '2024-12-15T00:00:00Z',
        updatedAt: '2024-12-15T00:00:00Z',
    },
];

// ===== FISCAL YEARS =====
export const MOCK_FISCAL_YEARS: FiscalYear[] = [
    {
        id: 'fy-2024',
        tenantId: 'tenant-1',
        fiscalYearCode: 'FY2024',
        fiscalYearName: 'Fiscal Year 2024',
        year: 2024,
        startDate: '2024-01-01T00:00:00Z',
        endDate: '2024-12-31T23:59:59Z',
        fiscalYearType: 'Calendar',
        status: 'Open',
        numberOfPeriods: 12,
        baseCurrency: 'GHS',
        isClosed: false,
        isLocked: false,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'fy-2023',
        tenantId: 'tenant-1',
        fiscalYearCode: 'FY2023',
        fiscalYearName: 'Fiscal Year 2023',
        year: 2023,
        startDate: '2023-01-01T00:00:00Z',
        endDate: '2023-12-31T23:59:59Z',
        fiscalYearType: 'Calendar',
        status: 'Closed',
        numberOfPeriods: 12,
        baseCurrency: 'GHS',
        isClosed: true,
        isLocked: true,
        createdAt: '2023-01-01T00:00:00Z',
        updatedAt: '2023-12-31T00:00:00Z',
    },
];

// ===== FISCAL PERIODS =====
export const MOCK_FISCAL_PERIODS: FiscalPeriod[] = [
    {
        id: 'fp-2024-01',
        tenantId: 'tenant-1',
        fiscalYearId: 'fy-2024',
        periodNumber: 1,
        periodCode: '2024-01',
        periodName: 'January 2024',
        startDate: '2024-01-01T00:00:00Z',
        endDate: '2024-01-31T23:59:59Z',
        periodStatus: 'Closed',
        status: 'Closed',
        isClosed: true,
        isLocked: false,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-02-01T00:00:00Z',
    },
    {
        id: 'fp-2024-11',
        tenantId: 'tenant-1',
        fiscalYearId: 'fy-2024',
        periodNumber: 11,
        periodCode: '2024-11',
        periodName: 'November 2024',
        startDate: '2024-11-01T00:00:00Z',
        endDate: '2024-11-30T23:59:59Z',
        periodStatus: 'Closed',
        status: 'Closed',
        isClosed: true,
        isLocked: true,
        createdAt: '2024-11-01T00:00:00Z',
        updatedAt: '2024-12-01T00:00:00Z',
    },
    {
        id: 'fp-2024-12',
        tenantId: 'tenant-1',
        fiscalYearId: 'fy-2024',
        periodNumber: 12,
        periodCode: '2024-12',
        periodName: 'December 2024',
        startDate: '2024-12-01T00:00:00Z',
        endDate: '2024-12-31T23:59:59Z',
        periodStatus: 'Open',
        status: 'Open',
        isClosed: false,
        isLocked: false,
        createdAt: '2024-12-01T00:00:00Z',
        updatedAt: '2024-12-01T00:00:00Z',
    },
];

// ===== JOURNAL ENTRIES =====
export const MOCK_JOURNAL_ENTRIES: JournalEntry[] = [
    {
        id: 'je-1',
        tenantId: 'tenant-1',
        journalNumber: 'JE-2024-001',
        journalEntryNumber: 'JE-2024-001',
        journalType: 'General',
        fiscalPeriodId: 'fp-2024-12',
        transactionDate: '2024-12-10T00:00:00Z',
        entryDate: '2024-12-10T00:00:00Z',
        postingDate: '2024-12-10T00:00:00Z',
        description: 'December rent payment',
        totalDebit: 5000,
        totalCredit: 5000,
        totalDebitAmount: 5000,
        totalCreditAmount: 5000,
        isBalanced: true,
        isMultiCurrency: false,
        bookClassification: 'Base',
        status: 'Posted',
        postingStatus: 'Posted',
        requiresApproval: false,
        isReversed: false,
        isRevaluationEntry: false,
        transactions: [],
        createdBy: 'user-1',
        updatedBy: 'user-1',
        createdAt: '2024-12-10T00:00:00Z',
        updatedAt: '2024-12-10T00:00:00Z',
    },
    {
        id: 'je-2',
        tenantId: 'tenant-1',
        journalNumber: 'JE-2024-002',
        journalEntryNumber: 'JE-2024-002',
        journalType: 'General',
        fiscalPeriodId: 'fp-2024-12',
        transactionDate: '2024-12-12T00:00:00Z',
        entryDate: '2024-12-12T00:00:00Z',
        description: 'Sales revenue - December',
        totalDebit: 50000,
        totalCredit: 50000,
        totalDebitAmount: 50000,
        totalCreditAmount: 50000,
        isBalanced: true,
        isMultiCurrency: false,
        bookClassification: 'Base',
        status: 'Draft',
        postingStatus: 'Draft',
        requiresApproval: false,
        isReversed: false,
        isRevaluationEntry: false,
        transactions: [],
        createdBy: 'user-1',
        updatedBy: 'user-1',
        createdAt: '2024-12-12T00:00:00Z',
        updatedAt: '2024-12-12T00:00:00Z',
    },
];

// ===== FINANCE SETTINGS =====
export const MOCK_FINANCE_SETTINGS: FinanceSettings = {
    apInvoicePriceTolerancePercent: 1,
    apInvoiceQuantityTolerancePercent: 1,
    id: 'settings-1',
    tenantId: 'tenant-1',
    coaType: 'Standard',
    coaConfigurationLocked: false,
    baseCurrency: 'GHS',
    retainedEarningsAccountId: 'acc-3100',
    unrealizedGainLossAccountId: 'acc-7100',
    realizedGainLossAccountId: 'acc-7200',
    suspenseAccountId: 'acc-9999',
};

// ===== HELPER FUNCTIONS =====

/**
 * Get all mock data for Finance module
 */
export function getAllFinanceMockData(coaType: 'Standard' | 'Segmented' = 'Standard') {
    return {
        accounts: getAccountsMockData(coaType),
        currencies: MOCK_CURRENCIES,
        exchangeRates: MOCK_EXCHANGE_RATES,
        fiscalYears: MOCK_FISCAL_YEARS,
        fiscalPeriods: MOCK_FISCAL_PERIODS,
        journalEntries: MOCK_JOURNAL_ENTRIES,
        settings: MOCK_FINANCE_SETTINGS,
        segmentStructures: coaType === 'Segmented' ? ALL_SEGMENT_STRUCTURES : [],
    };
}

/**
 * Simulate API delay for realistic demo mode
 */
export function simulateApiDelay(ms: number = 500): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
}
