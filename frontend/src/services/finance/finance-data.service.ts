/**
 * Finance Data Service
 * Service layer for finance module API calls
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
    ModuleDefinition,
    CreateFiscalYearDto,
} from '@/types/finance';

import { apiService } from '@/services/api.service';

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
        search?: string;
        take?: number;
    }): Promise<Account[]> {
        const queryParams = new URLSearchParams();
        if (filters?.accountType) queryParams.append('accountType', filters.accountType);
        if (filters?.status) queryParams.append('status', filters.status);
        if (filters?.isMultiCurrency !== undefined) queryParams.append('isMultiCurrency', String(filters.isMultiCurrency));
        if (filters?.coaType) queryParams.append('coaType', filters.coaType);
        if (filters?.search) queryParams.append('search', filters.search);
        if (filters?.take !== undefined) queryParams.append('take', String(filters.take));

        const endpoint = `/finance/accounts${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<Account[]>(endpoint);
    }

    async getAccountById(id: string): Promise<Account> {
        return apiService.get<Account>(`/finance/accounts/${id}`);
    }

    async createAccount(dto: CreateAccountDto): Promise<Account> {
        return apiService.post<Account>('/finance/accounts', dto);
    }

    async updateAccount(id: string, dto: UpdateAccountDto): Promise<Account> {
        return apiService.put<Account>(`/finance/accounts/${id}`, dto);
    }

    async deleteAccount(id: string): Promise<void> {
        return apiService.delete(`/finance/accounts/${id}`);
    }

    // ===== CURRENCIES =====

    async getCurrencies(filters?: {
        currencyCode?: string;
        isActive?: boolean;
    }): Promise<Currency[]> {
        const queryParams = new URLSearchParams();
        if (filters?.currencyCode) queryParams.append('currencyCode', filters.currencyCode);
        if (filters?.isActive !== undefined) queryParams.append('isActive', String(filters.isActive));

        const endpoint = `/finance/currencies${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<Currency[]>(endpoint);
    }

    async getCurrencyByCode(code: string): Promise<Currency> {
        return apiService.get<Currency>(`/finance/currencies/${code}`);
    }

    async createCurrency(dto: CreateCurrencyDto): Promise<Currency> {
        return apiService.post<Currency>('/finance/currencies', dto);
    }

    async updateCurrency(code: string, dto: UpdateCurrencyDto): Promise<Currency> {
        return apiService.put<Currency>(`/finance/currencies/${code}`, dto);
    }

    async deleteCurrency(code: string): Promise<void> {
        return apiService.delete(`/finance/currencies/${code}`);
    }

    // ===== EXCHANGE RATES =====

    async getExchangeRates(filters?: {
        baseCurrencyCode?: string;
        targetCurrencyCode?: string;
        rateType?: string;
        startDate?: string;
        endDate?: string;
    }): Promise<ExchangeRate[]> {
        const queryParams = new URLSearchParams();
        if (filters?.baseCurrencyCode) queryParams.append('baseCurrencyCode', filters.baseCurrencyCode);
        if (filters?.targetCurrencyCode) queryParams.append('targetCurrencyCode', filters.targetCurrencyCode);
        if (filters?.rateType) queryParams.append('rateType', filters.rateType);
        if (filters?.startDate) queryParams.append('startDate', filters.startDate);
        if (filters?.endDate) queryParams.append('endDate', filters.endDate);

        const endpoint = `/finance/exchange-rates${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<ExchangeRate[]>(endpoint);
    }

    async getExchangeRateById(id: string): Promise<ExchangeRate> {
        return apiService.get<ExchangeRate>(`/finance/exchange-rates/${id}`);
    }

    async createExchangeRate(dto: CreateExchangeRateDto): Promise<ExchangeRate> {
        return apiService.post<ExchangeRate>('/finance/exchange-rates', dto);
    }

    async deleteExchangeRate(id: string): Promise<void> {
        return apiService.delete(`/finance/exchange-rates/${id}`);
    }

    // ===== FISCAL YEARS =====

    async getFiscalYears(): Promise<FiscalYear[]> {
        return apiService.get<FiscalYear[]>('/finance/fiscal-years');
    }

    async getFiscalYearById(id: string): Promise<FiscalYear> {
        return apiService.get<FiscalYear>(`/finance/fiscal-years/${id}`);
    }

    async createFiscalYear(dto: CreateFiscalYearDto): Promise<FiscalYear> {
        return apiService.post<FiscalYear>('/finance/fiscal-years', dto);
    }

    async updateFiscalYear(id: string, dto: Partial<FiscalYear>): Promise<FiscalYear> {
        return apiService.put<FiscalYear>(`/finance/fiscal-years/${id}`, dto);
    }

    async deleteFiscalYear(id: string): Promise<void> {
        return apiService.delete(`/finance/fiscal-years/${id}`);
    }

    async closeFiscalYear(id: string): Promise<FiscalYear> {
        return apiService.put<FiscalYear>(`/finance/fiscal-years/${id}/close`, {});
    }

    async reopenFiscalYear(id: string): Promise<FiscalYear> {
        return apiService.put<FiscalYear>(`/finance/fiscal-years/${id}/reopen`, {});
    }

    // ===== FISCAL PERIODS =====

    async getFiscalPeriods(fiscalYearId?: string): Promise<FiscalPeriod[]> {
        const endpoint = fiscalYearId
            ? `/finance/fiscal-periods?fiscalYearId=${fiscalYearId}`
            : '/finance/fiscal-periods';
        return apiService.get<FiscalPeriod[]>(endpoint);
    }

    async getFiscalPeriodById(id: string): Promise<FiscalPeriod> {
        return apiService.get<FiscalPeriod>(`/finance/fiscal-periods/${id}`);
    }

    async closeFiscalPeriod(id: string): Promise<FiscalPeriod> {
        return apiService.put<FiscalPeriod>(`/finance/fiscal-periods/${id}/close`, {});
    }

    // ===== MODULE LOCKING =====

    async getModuleDefinitions(): Promise<ModuleDefinition[]> {
        return apiService.get<ModuleDefinition[]>('/finance/modules');
    }

    async lockPeriodForModule(periodId: string, moduleCode: string, reason: string): Promise<void> {
        return apiService.post(`/finance/periods/${periodId}/lock-module`, { moduleCode, reason });
    }

    async unlockPeriodForModule(periodId: string, moduleCode: string, reason: string): Promise<void> {
        return apiService.post(`/finance/periods/${periodId}/unlock-module`, { moduleCode, reason });
    }

    async reopenFiscalPeriod(id: string): Promise<FiscalPeriod> {
        return apiService.put<FiscalPeriod>(`/finance/fiscal-periods/${id}/reopen`, {});
    }

    // ===== JOURNAL ENTRIES =====

    async getJournalEntries(filters?: {
        status?: string;
        startDate?: string;
        endDate?: string;
        fiscalYearId?: string;
        fiscalPeriodId?: string;
    }): Promise<JournalEntry[]> {
        const queryParams = new URLSearchParams();
        if (filters?.status) queryParams.append('status', filters.status);
        if (filters?.startDate) queryParams.append('startDate', filters.startDate);
        if (filters?.endDate) queryParams.append('endDate', filters.endDate);
        if (filters?.fiscalYearId) queryParams.append('fiscalYearId', filters.fiscalYearId);
        if (filters?.fiscalPeriodId) queryParams.append('fiscalPeriodId', filters.fiscalPeriodId);

        const endpoint = `/finance/journal-entries${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<JournalEntry[]>(endpoint);
    }

    async getJournalEntryById(id: string): Promise<JournalEntry> {
        return apiService.get<JournalEntry>(`/finance/journal-entries/${id}`);
    }

    async createJournalEntry(dto: CreateJournalEntryDto): Promise<JournalEntry> {
        return apiService.post<JournalEntry>('/finance/journal-entries', dto);
    }

    async updateJournalEntry(id: string, dto: Partial<JournalEntry>): Promise<JournalEntry> {
        return apiService.put<JournalEntry>(`/finance/journal-entries/${id}`, dto);
    }

    async deleteJournalEntry(id: string): Promise<void> {
        return apiService.delete(`/finance/journal-entries/${id}`);
    }

    async postJournalEntry(id: string): Promise<JournalEntry> {
        return apiService.put<JournalEntry>(`/finance/journal-entries/${id}/post`, {});
    }

    async reverseJournalEntry(id: string): Promise<JournalEntry> {
        return apiService.post<JournalEntry>(`/finance/journal-entries/${id}/reverse`, {});
    }

    async getNextJournalNumber(): Promise<string> {
        const response = await apiService.get<{ number: string }>('/finance/journal-entries/next-number');
        return response.number;
    }

    // ===== JOURNAL ENTRY APPROVAL WORKFLOW =====

    async requestJournalEntryApproval(id: string): Promise<JournalEntry> {
        return apiService.post<JournalEntry>(`/finance/journal-entries/${id}/request-approval`);
    }

    async approveJournalEntry(id: string, comments?: string): Promise<JournalEntry> {
        return apiService.post<JournalEntry>(`/finance/journal-entries/${id}/approve`, { comments });
    }

    async rejectJournalEntry(id: string, reason: string): Promise<JournalEntry> {
        return apiService.post<JournalEntry>(`/finance/journal-entries/${id}/reject`, { reason });
    }

    // ===== FINANCE SETTINGS =====

    async getFinanceSettings(): Promise<FinanceSettings> {
        return apiService.get<FinanceSettings>('/finance/settings');
    }

    async updateFinanceSettings(dto: UpdateFinanceSettingsDto): Promise<FinanceSettings> {
        return apiService.put<FinanceSettings>('/finance/settings', dto);
    }

    // ===== SEGMENT STRUCTURES =====

    async getSegmentStructures(): Promise<SegmentStructure[]> {
        return apiService.get<SegmentStructure[]>('/finance/segments');
    }

    async getSegmentStructureById(id: string): Promise<SegmentStructure> {
        return apiService.get<SegmentStructure>(`/finance/segments/${id}`);
    }

    async createSegmentStructure(dto: Partial<SegmentStructure>): Promise<SegmentStructure> {
        return apiService.post<SegmentStructure>('/finance/segments', dto);
    }

    async updateSegmentStructure(id: string, dto: Partial<SegmentStructure>): Promise<SegmentStructure> {
        return apiService.put<SegmentStructure>(`/finance/segments/${id}`, dto);
    }

    async deleteSegmentStructure(id: string): Promise<void> {
        return apiService.delete(`/finance/segments/${id}`);
    }

    // ===== SEGMENT LOOKUP VALUES =====

    async getSegmentLookupValues(segmentId: string): Promise<SegmentLookupValue[]> {
        return apiService.get<SegmentLookupValue[]>(`/finance/segments/${segmentId}/values`);
    }

    async createSegmentLookupValue(segmentId: string, dto: Partial<SegmentLookupValue>): Promise<SegmentLookupValue> {
        return apiService.post<SegmentLookupValue>(`/finance/segments/${segmentId}/values`, dto);
    }

    async updateSegmentLookupValue(segmentId: string, valueId: string, dto: Partial<SegmentLookupValue>): Promise<SegmentLookupValue> {
        return apiService.put<SegmentLookupValue>(`/finance/segments/${segmentId}/values/${valueId}`, dto);
    }

    async deleteSegmentLookupValue(segmentId: string, valueId: string): Promise<void> {
        return apiService.delete(`/finance/segments/${segmentId}/values/${valueId}`);
    }

    async reorderSegmentStructures(reorderList: { segmentId: string; newPosition: number }[]): Promise<void> {
        return apiService.post('/finance/segments/reorder', reorderList);
    }

    async regenerateAccountNumbers(): Promise<void> {
        return apiService.post('/finance/segments/regenerate', {});
    }

    // ===== ACCOUNT CURRENCY LINKS =====

    async getAccountCurrencyLinks(accountId: string): Promise<AccountCurrencyLink[]> {
        return apiService.get<AccountCurrencyLink[]>(`/finance/accounts/${accountId}/currencies`);
    }

    async addAccountCurrencyLink(accountId: string, dto: AddCurrencyLinkDto): Promise<AccountCurrencyLink> {
        return apiService.post<AccountCurrencyLink>(`/finance/accounts/${accountId}/currencies`, { ...dto, accountId });
    }

    async removeAccountCurrencyLink(accountId: string, currencyCode: string): Promise<void> {
        return apiService.delete(`/finance/accounts/${accountId}/currencies/${currencyCode}`);
    }
}

// Export singleton instance
export const financeDataService = new FinanceDataService();
