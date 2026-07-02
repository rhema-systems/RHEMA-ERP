/**
 * Finance Data Service
 * Service layer for finance module API calls
 */

import type {
    Account,
    AccountingBook,
    Currency,
    ExchangeRate,
    FiscalYear,
    FiscalPeriod,
    JournalEntry,
    JournalEntryAttachment,
    FinanceJournalAuditLog,
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
    BalanceSheetReportDto,
    BalanceSheetRequestDto,
    CashFlowStatementReportDto,
    CashFlowStatementRequestDto,
    DetailedLedgerReportDto,
    DetailedLedgerRequestDto,
    IncomeStatementReportDto,
    IncomeStatementRequestDto,
    MultiCurrencyDetailReportDto,
    MultiCurrencyDetailRequestDto,
    TrialBalanceReportDto,
} from '@/types/finance';
import type { FinanceDashboardData } from '@/types/finance-dashboard';

import { apiService } from '@/services/api.service';
import { normalizeJournalEntry, normalizeJournalEntries } from '@/lib/finance/journal-entry-normalizer';

// =============================================================================
// FINANCE DATA SERVICE
// =============================================================================

class FinanceDataService {
    private resolveBackendFileUrl(url?: string): string {
        if (!url) return '';
        if (/^https?:\/\//i.test(url)) return url;

        const apiBase = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:53484/api';
        const backendBase = apiBase.replace(/\/api\/?$/, '');
        const normalized = url.startsWith('/') ? url : `/${url}`;
        return `${backendBase}${normalized}`;
    }

    // ===== DASHBOARD =====
    async getDashboard(): Promise<FinanceDashboardData> {
        return apiService.get<FinanceDashboardData>('/finance/dashboard');
    }

    // ===== ACCOUNTS =====

    async getAccountingBooks(includeInactive = false): Promise<AccountingBook[]> {
        const queryParams = new URLSearchParams();
        if (includeInactive) queryParams.append('includeInactive', 'true');

        const endpoint = `/finance/accounting-books${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<AccountingBook[]>(endpoint);
    }

    async getAccounts(filters?: {
        accountType?: string;
        status?: string;
        isMultiCurrency?: boolean;
        coaType?: 'Standard' | 'Segmented';
        page?: number;
        pageSize?: number;
        take?: number;
    }): Promise<Account[]> {
        const queryParams = new URLSearchParams();
        if (filters?.accountType) queryParams.append('accountType', filters.accountType);
        if (filters?.status) queryParams.append('status', filters.status);
        if (filters?.isMultiCurrency !== undefined) queryParams.append('isMultiCurrency', String(filters.isMultiCurrency));
        if (filters?.page !== undefined) queryParams.append('page', String(filters.page));
        if (filters?.pageSize !== undefined) queryParams.append('pageSize', String(filters.pageSize));
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
            ? `/finance/fiscal-periods?yearId=${fiscalYearId}`
            : '/finance/fiscal-periods';
        return apiService.get<FiscalPeriod[]>(endpoint);
    }

    async getFiscalPeriodById(id: string): Promise<FiscalPeriod> {
        return apiService.get<FiscalPeriod>(`/finance/fiscal-periods/${id}`);
    }

    async openFiscalPeriod(id: string): Promise<FiscalPeriod> {
        return apiService.post<FiscalPeriod>(`/finance/periods/${id}/open`, {});
    }

    async closeFiscalPeriod(id: string): Promise<FiscalPeriod> {
        return apiService.post<FiscalPeriod>(`/finance/periods/${id}/close`, {});
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
        const raw = await apiService.get<any[]>(endpoint);
        return normalizeJournalEntries(raw);
    }

    async getPendingJournalApprovals(): Promise<JournalEntry[]> {
        const raw = await apiService.get<any[]>('/finance/journal-entries/pending-approvals');
        return normalizeJournalEntries(raw);
    }

    async getJournalEntryById(id: string): Promise<JournalEntry> {
        const raw = await apiService.get<any>(`/finance/journal-entries/${id}`);
        return normalizeJournalEntry(raw);
    }

    async getJournalEntryAuditTrail(id: string): Promise<FinanceJournalAuditLog[]> {
        return apiService.get<FinanceJournalAuditLog[]>(`/finance/journal-entries/${id}/audit-trail`);
    }

    async createJournalEntry(dto: CreateJournalEntryDto): Promise<JournalEntry> {
        const raw = await apiService.post<any>('/finance/journal-entries', dto);
        return normalizeJournalEntry(raw);
    }

    async updateJournalEntry(id: string, dto: Partial<CreateJournalEntryDto>): Promise<JournalEntry> {
        const raw = await apiService.put<any>(`/finance/journal-entries/${id}`, dto);
        return normalizeJournalEntry(raw);
    }

    async deleteJournalEntry(id: string): Promise<void> {
        return apiService.delete(`/finance/journal-entries/${id}`);
    }

    async postJournalEntry(id: string): Promise<void> {
        await apiService.post(`/finance/journal-entries/${id}/post`, {});
    }

    async reverseJournalEntry(id: string, reason: string, reversalDate?: string): Promise<JournalEntry> {
        const raw = await apiService.post<any>(`/finance/journal-entries/${id}/reverse`, {
            reason,
            reversalDate: reversalDate || undefined,
        });
        return normalizeJournalEntry(raw);
    }

    async getNextJournalNumber(): Promise<string> {
        const response = await apiService.get<{ number: string }>('/finance/journal-entries/next-number');
        return response.number;
    }

    // ===== JOURNAL ENTRY APPROVAL WORKFLOW =====

    async requestJournalEntryApproval(id: string): Promise<JournalEntry> {
        const raw = await apiService.post<any>(`/finance/journal-entries/${id}/request-approval`);
        return normalizeJournalEntry(raw);
    }

    async withdrawJournalEntryApproval(id: string, reason?: string): Promise<JournalEntry> {
        const raw = await apiService.post<any>(`/finance/journal-entries/${id}/withdraw-approval`, {
            reason: reason || 'Approval request withdrawn.',
        });
        return normalizeJournalEntry(raw);
    }

    async approveJournalEntry(id: string, comments?: string): Promise<JournalEntry> {
        const raw = await apiService.post<any>(`/finance/journal-entries/${id}/approve`, { comments });
        return normalizeJournalEntry(raw);
    }

    async rejectJournalEntry(id: string, reason: string): Promise<JournalEntry> {
        const raw = await apiService.post<any>(`/finance/journal-entries/${id}/reject`, { reason });
        return normalizeJournalEntry(raw);
    }

    // ===== ATTACHMENTS =====

    async uploadAttachment(file: File, referenceType: string, referenceId: string): Promise<{ fileId: string; url: string; name: string }> {
        const formData = new FormData();
        formData.append('file', file);
        // Use the canonical single-file upload endpoint.
        // Keep a specific category so backend policies/audits can distinguish finance journal attachments.
        formData.append('category', 'finance-journal-attachments');
        const response = await apiService.post<any>(`/fileupload/single`, formData);

        const fileId = response.fileId || response.id || response.fileUploadRecordId;
        if (!fileId) {
            throw new Error('Upload succeeded but fileId was not returned by server.');
        }

        return {
            fileId,
            url: this.resolveBackendFileUrl(response.url || response.fileUrl || response.publicUrl || ''),
            name: response.fileName || file.name,
        };
    }

    async linkJournalEntryAttachment(journalEntryId: string, fileId: string): Promise<void> {
        return apiService.post(`/finance/journal-entries/${journalEntryId}/attachments/${fileId}`, {});
    }

    async unlinkJournalEntryAttachment(journalEntryId: string, fileId: string): Promise<void> {
        return apiService.delete(`/finance/journal-entries/${journalEntryId}/attachments/${fileId}`);
    }

    async getJournalEntryAttachments(journalEntryId: string): Promise<JournalEntryAttachment[]> {
        const attachments = await apiService.get<JournalEntryAttachment[]>(`/finance/journal-entries/${journalEntryId}/attachments`);
        return (attachments || []).map(a => ({
            ...a,
            fileUrl: this.resolveBackendFileUrl(a.fileUrl),
        }));
    }

    // ===== FINANCE SETTINGS =====

    async getFinanceSettings(): Promise<FinanceSettings> {
        return apiService.get<FinanceSettings>('/finance/settings');
    }

    async updateFinanceSettings(dto: UpdateFinanceSettingsDto): Promise<FinanceSettings> {
        return apiService.put<FinanceSettings>('/finance/settings', dto);
    }

    // ===== FINANCIAL STATEMENTS =====

    async getTrialBalance(params: { asAtDate: string; bookClassification?: string; includeZeroBalances?: boolean }): Promise<TrialBalanceReportDto> {
        const queryParams = new URLSearchParams();
        if (params.asAtDate) queryParams.append('asAtDate', params.asAtDate);
        if (params.bookClassification) queryParams.append('bookClassification', params.bookClassification);
        if (params.includeZeroBalances !== undefined) queryParams.append('includeZeroBalances', String(params.includeZeroBalances));

        return apiService.get<TrialBalanceReportDto>(`/finance/statements/trial-balance?${queryParams}`);
    }

    async getDetailedLedger(params: DetailedLedgerRequestDto): Promise<DetailedLedgerReportDto> {
        const queryParams = new URLSearchParams();
        queryParams.append('startDate', params.startDate);
        queryParams.append('endDate', params.endDate);
        if (params.bookClassification) queryParams.append('bookClassification', params.bookClassification);
        if (params.includeReversed !== undefined) queryParams.append('includeReversed', String(params.includeReversed));
        if (params.includeOpeningBalances !== undefined) queryParams.append('includeOpeningBalances', String(params.includeOpeningBalances));
        if (params.accountIds && params.accountIds.length > 0) {
            params.accountIds.forEach(accountId => queryParams.append('accountIds', accountId));
        }

        return apiService.get<DetailedLedgerReportDto>(`/finance/statements/detailed-ledger?${queryParams}`);
    }

    async getIncomeStatement(params: IncomeStatementRequestDto): Promise<IncomeStatementReportDto> {
        const queryParams = new URLSearchParams();
        if (params.periodStart) queryParams.append('periodStart', params.periodStart);
        if (params.periodEnd) queryParams.append('periodEnd', params.periodEnd);
        if (params.bookClassification) queryParams.append('bookClassification', params.bookClassification);
        if (params.includeAccountDetails !== undefined) queryParams.append('includeAccountDetails', String(params.includeAccountDetails));

        return apiService.get<IncomeStatementReportDto>(`/finance/statements/income-statement?${queryParams}`);
    }

    async getBalanceSheet(params: BalanceSheetRequestDto): Promise<BalanceSheetReportDto> {
        const queryParams = new URLSearchParams();
        if (params.asAtDate) queryParams.append('asAtDate', params.asAtDate);
        if (params.bookClassification) queryParams.append('bookClassification', params.bookClassification);
        if (params.includeAccountDetails !== undefined) queryParams.append('includeAccountDetails', String(params.includeAccountDetails));

        return apiService.get<BalanceSheetReportDto>(`/finance/statements/balance-sheet?${queryParams}`);
    }

    async getCashFlowStatement(params: CashFlowStatementRequestDto): Promise<CashFlowStatementReportDto> {
        const queryParams = new URLSearchParams();
        queryParams.append('periodStart', params.periodStart);
        queryParams.append('periodEnd', params.periodEnd);
        if (params.bookClassification) queryParams.append('bookClassification', params.bookClassification);
        if (params.includeAccountDetails !== undefined) queryParams.append('includeAccountDetails', String(params.includeAccountDetails));
        if (params.method) queryParams.append('method', params.method);

        return apiService.get<CashFlowStatementReportDto>(`/finance/statements/cash-flow?${queryParams}`);
    }

    async getMultiCurrencyDetailReport(params: MultiCurrencyDetailRequestDto): Promise<MultiCurrencyDetailReportDto> {
        const queryParams = new URLSearchParams();
        queryParams.append('startDate', params.startDate);
        queryParams.append('endDate', params.endDate);
        if (params.accountId) queryParams.append('accountId', params.accountId);
        if (params.currencyCode) queryParams.append('currencyCode', params.currencyCode);
        if (params.includeRevaluation !== undefined) queryParams.append('includeRevaluation', String(params.includeRevaluation));

        return apiService.get<MultiCurrencyDetailReportDto>(`/finance/statements/multi-currency-detail?${queryParams}`);
    }

    // ===== SEGMENT STRUCTURES =====

    async getSegmentStructures(): Promise<SegmentStructure[]> {
        return apiService.get<SegmentStructure[]>('/finance/segments');
    }

    async getReportingDimensions(): Promise<SegmentStructure[]> {
        return apiService.get<SegmentStructure[]>('/finance/segments/reporting-dimensions');
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
