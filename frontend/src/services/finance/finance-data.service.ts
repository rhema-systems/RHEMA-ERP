/**
 * Finance Data Service
 * Unified service layer that checks demo mode and returns either mock data or makes API calls
 * 
 * DEMO MODE PERSISTENCE:
 * - Accounts: Persisted to localStorage, merges with default mock data
 * - Currency Links: Persisted to localStorage per account
 * - Journal Entries: Persisted to localStorage
 * - Settings: Persisted to localStorage
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
    AccountCurrencyLink,
    CreateAccountDto,
    UpdateAccountDto,
    CreateCurrencyDto,
    UpdateCurrencyDto,
    CreateExchangeRateDto,
    CreateJournalEntryDto,
    UpdateFinanceSettingsDto,
    AddCurrencyLinkDto,
} from '@/types/finance';

import { apiService } from '@/services/api.service';
import {
    getAccountsMockData,
    MOCK_CURRENCIES,
    MOCK_EXCHANGE_RATES,
    MOCK_FISCAL_YEARS,
    MOCK_FISCAL_PERIODS,
    MOCK_JOURNAL_ENTRIES,
    ALL_SEGMENT_STRUCTURES,
    MOCK_FINANCE_SETTINGS,
} from '@/lib/mock-data/finance';

// Delay to simulate API latency in demo mode
const simulateApiDelay = (ms: number = 300) => new Promise(resolve => setTimeout(resolve, ms));

// Check if finance demo mode is enabled
function isFinanceDemoMode(): boolean {
    if (typeof window === 'undefined') return true;
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

// =============================================================================
// DEMO MODE STORAGE MANAGER
// Centralized localStorage persistence for demo mode data
// =============================================================================

class DemoStorageManager {
    private readonly STORAGE_KEYS = {
        accounts: 'rhema-erp-demo-accounts',
        currencyLinks: 'rhema-erp-demo-currency-links',
        journalEntries: 'rhema-erp-demo-journal-entries',
        settings: 'rhema-erp-finance-settings',
    };

    // Generic get method
    private get<T>(key: string): T | null {
        if (typeof window === 'undefined') return null;
        try {
            const stored = localStorage.getItem(key);
            return stored ? JSON.parse(stored) : null;
        } catch (error) {
            console.error(`Failed to read ${key}:`, error);
            return null;
        }
    }

    // Generic save method
    private save<T>(key: string, data: T): void {
        if (typeof window === 'undefined') return;
        try {
            localStorage.setItem(key, JSON.stringify(data));
        } catch (error) {
            console.error(`Failed to save ${key}:`, error);
        }
    }

    // ===== ACCOUNTS =====
    getAccounts(coaType: 'Standard' | 'Segmented'): Account[] {
        const stored = this.get<Account[]>(this.STORAGE_KEYS.accounts);
        const defaultAccounts = getAccountsMockData(coaType);

        if (!stored) {
            // Initialize with default accounts
            this.save(this.STORAGE_KEYS.accounts, defaultAccounts);
            return defaultAccounts;
        }

        // Merge: stored accounts + any new default accounts not in stored
        const storedIds = new Set(stored.map(a => a.id));
        const newDefaults = defaultAccounts.filter(a => !storedIds.has(a.id));
        return [...stored, ...newDefaults];
    }

    saveAccounts(accounts: Account[]): void {
        this.save(this.STORAGE_KEYS.accounts, accounts);
    }

    addAccount(account: Account): Account {
        const accounts = this.get<Account[]>(this.STORAGE_KEYS.accounts) || [];
        accounts.push(account);
        this.saveAccounts(accounts);
        return account;
    }

    updateAccount(id: string, updates: Partial<Account>): Account | null {
        const accounts = this.get<Account[]>(this.STORAGE_KEYS.accounts) || [];
        const index = accounts.findIndex(a => a.id === id);
        if (index === -1) return null;

        accounts[index] = { ...accounts[index], ...updates, updatedAt: new Date().toISOString() };
        this.saveAccounts(accounts);
        return accounts[index];
    }

    deleteAccount(id: string): boolean {
        const accounts = this.get<Account[]>(this.STORAGE_KEYS.accounts) || [];
        const filtered = accounts.filter(a => a.id !== id);
        if (filtered.length === accounts.length) return false;

        this.saveAccounts(filtered);
        return true;
    }

    getAccountById(id: string, coaType: 'Standard' | 'Segmented'): Account | null {
        const accounts = this.getAccounts(coaType);
        return accounts.find(a => a.id === id) || null;
    }

    // ===== CURRENCY LINKS =====
    getCurrencyLinks(accountId: string): AccountCurrencyLink[] {
        const allLinks = this.get<Record<string, AccountCurrencyLink[]>>(this.STORAGE_KEYS.currencyLinks) || {};
        return allLinks[accountId] || [];
    }

    addCurrencyLink(accountId: string, link: AccountCurrencyLink): AccountCurrencyLink {
        const allLinks = this.get<Record<string, AccountCurrencyLink[]>>(this.STORAGE_KEYS.currencyLinks) || {};
        if (!allLinks[accountId]) {
            allLinks[accountId] = [];
        }
        allLinks[accountId].push(link);
        this.save(this.STORAGE_KEYS.currencyLinks, allLinks);
        return link;
    }

    removeCurrencyLink(accountId: string, currencyCode: string): boolean {
        const allLinks = this.get<Record<string, AccountCurrencyLink[]>>(this.STORAGE_KEYS.currencyLinks) || {};
        if (!allLinks[accountId]) return false;

        const original = allLinks[accountId].length;
        allLinks[accountId] = allLinks[accountId].filter(l => l.linkedCurrencyCode !== currencyCode);

        if (allLinks[accountId].length === original) return false;

        this.save(this.STORAGE_KEYS.currencyLinks, allLinks);
        return true;
    }

    // ===== JOURNAL ENTRIES =====
    getJournalEntries(): JournalEntry[] {
        const stored = this.get<JournalEntry[]>(this.STORAGE_KEYS.journalEntries);

        if (!stored) {
            // Initialize with default entries
            this.save(this.STORAGE_KEYS.journalEntries, MOCK_JOURNAL_ENTRIES);
            return MOCK_JOURNAL_ENTRIES;
        }

        return stored;
    }

    saveJournalEntries(entries: JournalEntry[]): void {
        this.save(this.STORAGE_KEYS.journalEntries, entries);
    }

    addJournalEntry(entry: JournalEntry): JournalEntry {
        const entries = this.getJournalEntries();
        entries.unshift(entry); // Add to beginning (most recent first)
        this.saveJournalEntries(entries);
        return entry;
    }

    updateJournalEntry(id: string, updates: Partial<JournalEntry>): JournalEntry | null {
        const entries = this.getJournalEntries();
        const index = entries.findIndex(e => e.id === id);
        if (index === -1) return null;

        entries[index] = { ...entries[index], ...updates, updatedAt: new Date().toISOString() };
        this.saveJournalEntries(entries);
        return entries[index];
    }

    deleteJournalEntry(id: string): boolean {
        const entries = this.getJournalEntries();
        const filtered = entries.filter(e => e.id !== id);
        if (filtered.length === entries.length) return false;

        this.saveJournalEntries(filtered);
        return true;
    }

    getJournalEntryById(id: string): JournalEntry | null {
        const entries = this.getJournalEntries();
        return entries.find(e => e.id === id) || null;
    }

    // ===== SETTINGS =====
    getSettings(): FinanceSettings | null {
        return this.get<FinanceSettings>(this.STORAGE_KEYS.settings);
    }

    saveSettings(settings: FinanceSettings): void {
        this.save(this.STORAGE_KEYS.settings, settings);
    }
}

const demoStorage = new DemoStorageManager();

// =============================================================================
// FINANCE DATA SERVICE
// =============================================================================

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
            let accounts = demoStorage.getAccounts(coaType);

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
            // Try both Standard and Segmented to find the account
            let account = demoStorage.getAccountById(id, 'Standard');
            if (!account) {
                account = demoStorage.getAccountById(id, 'Segmented');
            }
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
            const newAccount: Account = {
                id: `acc-${Date.now()}`,
                tenantId: 'tenant-1',
                accountCode: dto.accountCode,
                accountNumber: dto.accountNumber,
                accountName: dto.accountName,
                accountType: dto.accountType,
                accountCategory: dto.accountCategory,
                description: dto.description,
                currencyCode: dto.currencyCode,
                isMultiCurrency: dto.isMultiCurrency || false,
                isSegmented: dto.isSegmented || false,
                isIFRSClassified: dto.isIFRSClassified || false,
                isBaseClassified: dto.isBaseClassified || false,
                isLocalClassified: dto.isLocalClassified || false,
                allowDirectPosting: dto.allowDirectPosting ?? true,
                isControlAccount: dto.isControlAccount || false,
                budgetTrackingEnabled: dto.budgetTrackingEnabled || false,
                status: dto.status || 'Active',
                currentBalance: 0,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };

            // Persist to localStorage
            return demoStorage.addAccount(newAccount);
        }

        return apiService.post<Account>('/finance/accounts', dto);
    }

    async updateAccount(id: string, dto: UpdateAccountDto): Promise<Account> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const updated = demoStorage.updateAccount(id, dto as Partial<Account>);
            if (!updated) {
                throw new Error(`Account with ID ${id} not found`);
            }
            return updated;
        }

        return apiService.put<Account>(`/finance/accounts/${id}`, dto);
    }

    async deleteAccount(id: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(400);
            const deleted = demoStorage.deleteAccount(id);
            if (!deleted) {
                throw new Error(`Account with ID ${id} not found`);
            }
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

    async getCurrencyById(id: string): Promise<Currency> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            const currency = MOCK_CURRENCIES.find(c => c.id === id);
            if (!currency) {
                throw new Error(`Currency with ID ${id} not found`);
            }
            return currency;
        }

        return apiService.get<Currency>(`/finance/currencies/${id}`);
    }

    async createCurrency(dto: CreateCurrencyDto): Promise<Currency> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const newCurrency: Currency = {
                id: `curr-${Date.now()}`,
                ...dto,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };
            return newCurrency;
        }

        return apiService.post<Currency>('/finance/currencies', dto);
    }

    async updateCurrency(id: string, dto: UpdateCurrencyDto): Promise<Currency> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const currency = MOCK_CURRENCIES.find(c => c.id === id);
            if (!currency) {
                throw new Error(`Currency with ID ${id} not found`);
            }
            return { ...currency, ...dto, updatedAt: new Date().toISOString() };
        }

        return apiService.put<Currency>(`/finance/currencies/${id}`, dto);
    }

    // ===== EXCHANGE RATES =====

    async getExchangeRates(filters?: {
        currencyCode?: string;
        rateType?: string;
    }): Promise<ExchangeRate[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            let rates = MOCK_EXCHANGE_RATES;

            if (filters?.currencyCode) {
                rates = rates.filter(r =>
                    r.baseCurrencyCode === filters.currencyCode ||
                    r.targetCurrencyCode === filters.currencyCode
                );
            }
            if (filters?.rateType) {
                rates = rates.filter(r => r.rateType === filters.rateType);
            }

            return rates;
        }

        const queryParams = new URLSearchParams();
        if (filters?.currencyCode) queryParams.append('currencyCode', filters.currencyCode);
        if (filters?.rateType) queryParams.append('rateType', filters.rateType);

        const endpoint = `/finance/exchange-rates${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<ExchangeRate[]>(endpoint);
    }

    async createExchangeRate(dto: CreateExchangeRateDto): Promise<ExchangeRate> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const newRate: ExchangeRate = {
                id: `rate-${Date.now()}`,
                ...dto,
                isActive: true,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };
            return newRate;
        }

        return apiService.post<ExchangeRate>('/finance/exchange-rates', dto);
    }

    // ===== FISCAL YEARS =====

    async getFiscalYears(): Promise<FiscalYear[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            return MOCK_FISCAL_YEARS;
        }

        return apiService.get<FiscalYear[]>('/finance/fiscal-years');
    }

    async getFiscalYearById(id: string): Promise<FiscalYear> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            const year = MOCK_FISCAL_YEARS.find(y => y.id === id);
            if (!year) {
                throw new Error(`Fiscal year with ID ${id} not found`);
            }
            return year;
        }

        return apiService.get<FiscalYear>(`/finance/fiscal-years/${id}`);
    }

    // ===== FISCAL PERIODS =====

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
            let entries = demoStorage.getJournalEntries();

            if (filters?.status && filters.status !== 'all') {
                entries = entries.filter(e => e.postingStatus === filters.status);
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
            const entry = demoStorage.getJournalEntryById(id);
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

            // Calculate totals from transactions
            let totalDebitAmount = 0;
            let totalCreditAmount = 0;
            dto.transactions.forEach(t => {
                totalDebitAmount += t.debitAmount || 0;
                totalCreditAmount += t.creditAmount || 0;
            });

            const newEntry: JournalEntry = {
                id: `je-${Date.now()}`,
                journalEntryNumber: `JE-${new Date().getFullYear()}-${String(Date.now()).slice(-4)}`,
                journalType: dto.journalType,
                entryDate: dto.entryDate,
                description: dto.description,
                referenceNumber: dto.referenceNumber,
                bookClassification: dto.bookClassification,
                fiscalPeriodId: dto.fiscalPeriodId || 'period-1',
                notes: dto.notes,
                totalDebitAmount,
                totalCreditAmount,
                isBalanced: Math.abs(totalDebitAmount - totalCreditAmount) < 0.01,
                isMultiCurrency: dto.transactions.some(t => t.transactionCurrency && t.transactionCurrency !== 'GHS'),
                postingStatus: 'Draft',
                requiresApproval: false,
                isReversed: false,
                isRevaluationEntry: false,
                transactions: dto.transactions.map((t, idx) => ({
                    id: `jel-${Date.now()}-${idx}`,
                    journalEntryId: '',
                    lineNumber: idx + 1,
                    accountId: t.accountId,
                    accountCode: '', // Would need to look up
                    accountName: '', // Would need to look up
                    description: t.description || '',
                    debitAmount: t.debitAmount,
                    creditAmount: t.creditAmount,
                    transactionCurrency: t.transactionCurrency,
                    foreignCurrencyAmount: t.foreignCurrencyAmount,
                    exchangeRate: t.exchangeRate,
                    isRevaluationEntry: false,
                    createdAt: new Date().toISOString(),
                    updatedAt: new Date().toISOString(),
                })),
                createdBy: 'demo-user',
                updatedBy: 'demo-user',
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };

            // Persist to localStorage
            return demoStorage.addJournalEntry(newEntry);
        }

        return apiService.post<JournalEntry>('/finance/journal-entries', dto);
    }

    async postJournalEntry(id: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(800);
            demoStorage.updateJournalEntry(id, {
                postingStatus: 'Posted',
                postingDate: new Date().toISOString(),
            });
            return;
        }

        return apiService.post(`/finance/journal-entries/${id}/post`);
    }

    async reverseJournalEntry(id: string): Promise<JournalEntry> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(1000);
            const original = await this.getJournalEntryById(id);

            // Mark original as reversed
            demoStorage.updateJournalEntry(id, { isReversed: true });

            // Create reversal entry
            const reversed: JournalEntry = {
                ...original,
                id: `je-${Date.now()}`,
                journalEntryNumber: `${original.journalEntryNumber}-REV`,
                journalType: 'Reversing',
                description: `Reversal of ${original.journalEntryNumber}: ${original.description}`,
                reversalJournalEntryId: original.id,
                reversalDate: new Date().toISOString(),
                reversalReason: 'Manual reversal',
                postingStatus: 'Draft',
                isReversed: false,
                transactions: original.transactions.map(t => ({
                    ...t,
                    id: `jel-${Date.now()}-${t.lineNumber}`,
                    // Swap debits and credits
                    debitAmount: t.creditAmount,
                    creditAmount: t.debitAmount,
                })),
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };

            return demoStorage.addJournalEntry(reversed);
        }

        return apiService.post<JournalEntry>(`/finance/journal-entries/${id}/reverse`);
    }

    async deleteJournalEntry(id: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(400);
            const deleted = demoStorage.deleteJournalEntry(id);
            if (!deleted) {
                throw new Error(`Journal entry with ID ${id} not found`);
            }
            return;
        }

        return apiService.delete(`/finance/journal-entries/${id}`);
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
            // Try to get persisted settings first
            const stored = demoStorage.getSettings();
            if (stored) {
                return stored;
            }
            // Initialize with default mock settings
            demoStorage.saveSettings(MOCK_FINANCE_SETTINGS);
            return MOCK_FINANCE_SETTINGS;
        }

        return apiService.get<FinanceSettings>('/finance/settings');
    }

    async updateFinanceSettings(dto: UpdateFinanceSettingsDto): Promise<FinanceSettings> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            // Get current settings and merge with updates
            const current = demoStorage.getSettings() || MOCK_FINANCE_SETTINGS;
            const updated: FinanceSettings = {
                ...current,
                ...dto,
                id: current.id,
                tenantId: current.tenantId,
            };
            // Persist to localStorage
            demoStorage.saveSettings(updated);
            return updated;
        }

        return apiService.put<FinanceSettings>('/finance/settings', dto);
    }

    // ===== ACCOUNT CURRENCY LINKS =====

    async getAccountCurrencyLinks(accountId: string): Promise<AccountCurrencyLink[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            return demoStorage.getCurrencyLinks(accountId);
        }

        return apiService.get<AccountCurrencyLink[]>(`/finance/accounts/${accountId}/currency-links`);
    }

    async addAccountCurrencyLink(accountId: string, dto: AddCurrencyLinkDto): Promise<AccountCurrencyLink> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const link: AccountCurrencyLink = {
                id: `cl-${Date.now()}`,
                accountId,
                linkedCurrencyCode: dto.linkedCurrencyCode,
                revaluationRequired: dto.revaluationRequired,
                revaluationFrequency: dto.revaluationFrequency as any,
                transactionRateType: dto.transactionRateType,
                revaluationRateType: dto.revaluationRateType,
                foreignCurrencyBalance: 0,
                baseCurrencyBalance: 0,
                cumulativeRevaluationAdjustment: 0,
                isActive: true,
                notes: dto.notes,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
            };

            // Persist to localStorage
            return demoStorage.addCurrencyLink(accountId, link);
        }

        return apiService.post<AccountCurrencyLink>(`/finance/accounts/${accountId}/currency-links`, dto);
    }

    async removeAccountCurrencyLink(accountId: string, currencyCode: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            demoStorage.removeCurrencyLink(accountId, currencyCode);
            return;
        }

        return apiService.delete(`/finance/accounts/${accountId}/currency-links/${currencyCode}`);
    }
}

export const financeDataService = new FinanceDataService();
