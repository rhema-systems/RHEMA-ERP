/**
 * Finance Data Service
 * Unified service layer that checks demo mode and returns either mock data or makes API calls
 */

import type {
    Account,
    Currency,
    ExchangeRate,
    FiscalYear,
    FiscalPeriod,
    JournalEntry,
    FinanceSettings,
    SegmentStructure,
    SegmentLookupValue,
    CreateAccountDto,
    UpdateAccountDto,
    CreateCurrencyDto,
    UpdateCurrencyDto,
    CreateExchangeRateDto,
    CreateJournalEntryDto,
    UpdateFinanceSettingsDto,
} from '@/types/finance';

import { apiService } from './api.service';
import {
    getAccountsMockData,
    MOCK_CURRENCIES,
    MOCK_EXCHANGE_RATES,
    MOCK_FISCAL_YEARS,
    MOCK_FISCAL_PERIODS,
    MOCK_JOURNAL_ENTRIES,
    MOCK_FINANCE_SETTINGS,
    ALL_SEGMENT_STRUCTURES,
    simulateApiDelay,
} from '@/lib/mock-data/finance';

/**
 * Check if Finance module is in demo mode
 * This function must be called from client-side only
 */
function isFinanceDemoMode(): boolean {
    if (typeof window === 'undefined') {
        return true; // Default to demo mode during SSR
    }

    try {
        const stored = localStorage.getItem('rhema-erp-demo-mode');
        if (stored) {
            const parsed = JSON.parse(stored);
            return parsed.finance ?? true; // Default to true
        }
    } catch (error) {
        console.error('Failed to read demo mode state:', error);
    }

    return true; // Default to demo mode
}

class FinanceDataService {
    // ===== ACCOUNTS =====

    async getAccounts(filters?: {
        accountType?: string;
        status?: string;
        isMultiCurrency?: boolean;
        coaType?: 'Standard' | 'Segmented';
    }): Promise<Account[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            const coaType = filters?.coaType || 'Standard';
            let accounts = getAccountsMockData(coaType);

            // Apply filters
            if (filters?.accountType && filters.accountType !== 'all') {
                accounts = accounts.filter(a => a.accountType === filters.accountType);
            }
            if (filters?.status && filters.status !== 'all') {
                accounts = accounts.filter(a => a.status === filters.status);
            }
            if (filters?.isMultiCurrency !== undefined) {
                accounts = accounts.filter(a => a.isMultiCurrency === filters.isMultiCurrency);
            }

            return accounts;
        }

        // Live mode - make API call
        const queryParams = new URLSearchParams();
        if (filters?.accountType) queryParams.append('accountType', filters.accountType);
        if (filters?.status) queryParams.append('status', filters.status);
        if (filters?.isMultiCurrency !== undefined) queryParams.append('isMultiCurrency', String(filters.isMultiCurrency));

        const endpoint = `/finance/accounts${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<Account[]>(endpoint);
    }

    async getAccountById(id: string): Promise<Account> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            const accounts = getAccountsMockData('Standard').concat(getAccountsMockData('Segmented'));
            const account = accounts.find(a => a.id === id);
            if (!account) {
                throw new Error(`Account with ID ${id} not found`);
            }
            return account;
        }

        return apiService.get<Account>(`/finance/accounts/${id}`);
    }

    async createAccount(dto: CreateAccountDto): Promise<Account> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(800);
            // In demo mode, just return a mock account
            const newAccount: Account = {
                id: `acc-${Date.now()}`,
                tenantId: 'tenant-1',
                accountCode: dto.accountCode,
                accountNumber: dto.accountNumber,
                accountName: dto.accountName,
                accountType: dto.accountType,
                accountCategory: dto.accountCategory,
                currencyCode: dto.currencyCode,
                isMultiCurrency: dto.isMultiCurrency || false,
                isSegmented: dto.isSegmented || false,
                isIFRSClassified: dto.isIFRSClassified || false,
                isBaseClassified: dto.isBaseClassified || false,
                isLocalClassified: dto.isLocalClassified || false,
                allowDirectPosting: dto.allowDirectPosting ?? true,
                isControlAccount: dto.isControlAccount || false,
                budgetTrackingEnabled: dto.budgetTrackingEnabled || false,
                status: 'Active',
                currentBalance: 0,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };
            return newAccount;
        }

        return apiService.post<Account>('/finance/accounts', dto);
    }

    async updateAccount(id: string, dto: UpdateAccountDto): Promise<Account> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const account = await this.getAccountById(id);
            return { ...account, ...dto, updatedAt: new Date().toISOString() };
        }

        return apiService.put<Account>(`/finance/accounts/${id}`, dto);
    }

    async deleteAccount(id: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(400);
            return;
        }

        return apiService.delete(`/finance/accounts/${id}`);
    }

    // ===== CURRENCIES =====

    async getCurrencies(): Promise<Currency[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            return MOCK_CURRENCIES;
        }

        return apiService.get<Currency[]>('/finance/currencies');
    }

    async getCurrencyByCode(code: string): Promise<Currency> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            const currency = MOCK_CURRENCIES.find(c => c.currencyCode === code);
            if (!currency) {
                throw new Error(`Currency with code ${code} not found`);
            }
            return currency;
        }

        return apiService.get<Currency>(`/finance/currencies/${code}`);
    }

    async createCurrency(dto: CreateCurrencyDto): Promise<Currency> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(800);
            const newCurrency: Currency = {
                id: `curr-${Date.now()}`,
                tenantId: 'tenant-1',
                ...dto,
                hasTransactionHistory: false,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };
            return newCurrency;
        }

        return apiService.post<Currency>('/finance/currencies', dto);
    }

    async updateCurrency(code: string, dto: UpdateCurrencyDto): Promise<Currency> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const currency = await this.getCurrencyByCode(code);
            return { ...currency, ...dto, updatedAt: new Date().toISOString() };
        }

        return apiService.put<Currency>(`/finance/currencies/${code}`, dto);
    }

    async toggleCurrencyStatus(code: string, isActive: boolean): Promise<Currency> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(400);
            const currency = await this.getCurrencyByCode(code);
            return { ...currency, isActive, updatedAt: new Date().toISOString() };
        }

        const endpoint = isActive
            ? `/finance/currencies/${code}/activate`
            : `/finance/currencies/${code}/deactivate`;
        return apiService.patch<Currency>(endpoint);
    }

    // ===== EXCHANGE RATES =====

    async getExchangeRates(filters?: {
        fromCurrency?: string;
        toCurrency?: string;
        rateType?: string;
        startDate?: string;
        endDate?: string;
    }): Promise<ExchangeRate[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            let rates = MOCK_EXCHANGE_RATES;

            if (filters?.fromCurrency) {
                rates = rates.filter(r => r.fromCurrencyCode === filters.fromCurrency);
            }
            if (filters?.toCurrency) {
                rates = rates.filter(r => r.toCurrencyCode === filters.toCurrency);
            }
            if (filters?.rateType) {
                rates = rates.filter(r => r.rateType === filters.rateType);
            }

            return rates;
        }

        const queryParams = new URLSearchParams();
        if (filters?.fromCurrency) queryParams.append('fromCurrency', filters.fromCurrency);
        if (filters?.toCurrency) queryParams.append('toCurrency', filters.toCurrency);
        if (filters?.rateType) queryParams.append('rateType', filters.rateType);
        if (filters?.startDate) queryParams.append('startDate', filters.startDate);
        if (filters?.endDate) queryParams.append('endDate', filters.endDate);

        const endpoint = `/finance/exchange-rates${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<ExchangeRate[]>(endpoint);
    }

    async createExchangeRate(dto: CreateExchangeRateDto): Promise<ExchangeRate> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(800);
            const newRate: ExchangeRate = {
                id: `er-${Date.now()}`,
                tenantId: 'tenant-1',
                ...dto,
                isActive: true,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };
            return newRate;
        }

        return apiService.post<ExchangeRate>('/finance/exchange-rates', dto);
    }

    // ===== FISCAL YEARS & PERIODS =====

    async getFiscalYears(): Promise<FiscalYear[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            return MOCK_FISCAL_YEARS;
        }

        return apiService.get<FiscalYear[]>('/finance/fiscal-years');
    }

    async getFiscalPeriods(yearId?: string): Promise<FiscalPeriod[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            if (yearId) {
                return MOCK_FISCAL_PERIODS.filter(p => p.fiscalYearId === yearId);
            }
            return MOCK_FISCAL_PERIODS;
        }

        const endpoint = yearId
            ? `/finance/fiscal-periods?yearId=${yearId}`
            : '/finance/fiscal-periods';
        return apiService.get<FiscalPeriod[]>(endpoint);
    }

    async closePeriod(periodId: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(1000);
            return;
        }

        return apiService.post(`/finance/periods/${periodId}/close`);
    }

    async reopenPeriod(periodId: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(1000);
            return;
        }

        return apiService.post(`/finance/periods/${periodId}/reopen`);
    }

    // ===== JOURNAL ENTRIES =====

    async getJournalEntries(filters?: {
        status?: string;
        startDate?: string;
        endDate?: string;
        periodId?: string;
    }): Promise<JournalEntry[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            let entries = MOCK_JOURNAL_ENTRIES;

            if (filters?.status && filters.status !== 'all') {
                entries = entries.filter(e => e.status === filters.status);
            }
            if (filters?.periodId) {
                entries = entries.filter(e => e.fiscalPeriodId === filters.periodId);
            }

            return entries;
        }

        const queryParams = new URLSearchParams();
        if (filters?.status) queryParams.append('status', filters.status);
        if (filters?.startDate) queryParams.append('startDate', filters.startDate);
        if (filters?.endDate) queryParams.append('endDate', filters.endDate);
        if (filters?.periodId) queryParams.append('periodId', filters.periodId);

        const endpoint = `/finance/journal-entries${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<JournalEntry[]>(endpoint);
    }

    async getJournalEntryById(id: string): Promise<JournalEntry> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            const entry = MOCK_JOURNAL_ENTRIES.find(e => e.id === id);
            if (!entry) {
                throw new Error(`Journal entry with ID ${id} not found`);
            }
            return entry;
        }

        return apiService.get<JournalEntry>(`/finance/journal-entries/${id}`);
    }

    async createJournalEntry(dto: CreateJournalEntryDto): Promise<JournalEntry> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(1000);
            const newEntry: JournalEntry = {
                id: `je-${Date.now()}`,
                tenantId: 'tenant-1',
                journalEntryNumber: `JE-${new Date().getFullYear()}-${String(Date.now()).slice(-3)}`,
                ...dto,
                status: 'Draft',
                createdBy: 'current-user',
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };
            return newEntry;
        }

        return apiService.post<JournalEntry>('/finance/journal-entries', dto);
    }

    async postJournalEntry(id: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(800);
            return;
        }

        return apiService.post(`/finance/journal-entries/${id}/post`);
    }

    async reverseJournalEntry(id: string): Promise<JournalEntry> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(1000);
            const original = await this.getJournalEntryById(id);
            const reversed: JournalEntry = {
                ...original,
                id: `je-${Date.now()}`,
                journalEntryNumber: `JE-${new Date().getFullYear()}-${String(Date.now()).slice(-3)}-REV`,
                description: `Reversal of ${original.journalEntryNumber}: ${original.description}`,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };
            return reversed;
        }

        return apiService.post<JournalEntry>(`/finance/journal-entries/${id}/reverse`);
    }

    // ===== SEGMENTS =====

    async getSegmentStructures(): Promise<SegmentStructure[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            return ALL_SEGMENT_STRUCTURES;
        }

        return apiService.get<SegmentStructure[]>('/finance/segments');
    }

    async getSegmentLookupValues(segmentId: string): Promise<SegmentLookupValue[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            const segment = ALL_SEGMENT_STRUCTURES.find(s => s.id === segmentId);
            return segment?.lookupValues || [];
        }

        return apiService.get<SegmentLookupValue[]>(`/finance/segments/${segmentId}/values`);
    }

    // ===== SETTINGS =====

    async getFinanceSettings(): Promise<FinanceSettings> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            return MOCK_FINANCE_SETTINGS;
        }

        return apiService.get<FinanceSettings>('/finance/settings');
    }

    async updateFinanceSettings(dto: UpdateFinanceSettingsDto): Promise<FinanceSettings> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            return { ...MOCK_FINANCE_SETTINGS, ...dto };
        }

        return apiService.put<FinanceSettings>('/finance/settings', dto);
    }
}

export const financeDataService = new FinanceDataService();
