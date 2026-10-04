/**
 * Finance Data Service
 * Service layer for finance module API calls
 */

import type {
  Account,
  AccountTransactionInquiryPage,
  AccountingBook,
  DecideAccountingBookTransition,
  RequestAccountingBookTransition,
  SaveAccountingBook,
  AccountClassification,
  AccountClassificationWhereUsed,
  SaveAccountClassification,
  Currency,
  ExchangeRate,
  FiscalYear,
  FiscalPeriod,
  FinanceCloseTemplate,
  FinanceCloseWorkspace,
  FinancePeriodReopenRequest,
  SaveFinanceCloseTemplateVersion,
  JournalEntry,
  JournalEntryAttachment,
  FinanceBudgetControlEvaluation,
  FinanceBudgetOverrideRequest,
  FinanceJournalAuditLog,
  FinanceSettings,
  SegmentStructure,
  SegmentLookupValue,
  ReportingSegmentOptionsResponse,
  AccountCurrencyLink,
  AccountBookCurrencyPolicy,
  SaveAccountBookCurrencyPolicyDto,
  DecideAccountBookCurrencyPolicyDto,
  CreateAccountDto,
  UpdateAccountDto,
  CreateCurrencyDto,
  UpdateCurrencyDto,
  CreateExchangeRateDto,
  CreateJournalEntryDto,
  CreateOpeningBalanceBatchDto,
  CreateFixedAssetOpeningBalanceBatchDto,
  CreateBankAccountOpeningBalanceDto,
  CreateResidualGlEquityOpeningBalanceDto,
  CreateSupplierAdvanceOpeningBalanceDto,
  CreateCustomerAdvanceOpeningBalanceDto,
  CreateApWithholdingOpeningBalanceDto,
  CreateArWithholdingOpeningBalanceDto,
  SpecializedOpeningBalanceOptions,
  GovernedOpeningBalanceOptions,
  GovernedOpeningBalanceOptionsRequest,
  UpdateOpeningBalanceBatchDto,
  CreateSubledgerAdjustmentJournalDto,
  UpdateFinanceSettingsDto,
  AddCurrencyLinkDto,
  UpdateCurrencyLinkRatePolicyDto,
  ModuleDefinition,
  OpeningBalanceBatch,
  OpeningBalanceBatchReversal,
  RequestOpeningBalanceBatchReversalDto,
  ReviewOpeningBalanceBatchReversalDto,
  OpeningBalanceDiagnostic,
  OpeningBalanceValidationResult,
  SubledgerOpeningBalanceReadiness,
  ReverseSubledgerAdjustmentJournalDto,
  CreateFiscalYearDto,
  BalanceSheetReportDto,
  BalanceSheetRequestDto,
  CashFlowStatementReportDto,
  CashFlowStatementRequestDto,
  DetailedLedgerReportDto,
  DetailedLedgerRequestDto,
  IncomeStatementReportDto,
  IncomeStatementRequestDto,
  FinancialStatementLayoutSummaryDto,
  FinancialStatementType,
  MultiCurrencyDetailReportDto,
  MultiCurrencyDetailRequestDto,
  SubledgerAdjustmentJournal,
  SubledgerModule,
  TrialBalanceReportDto,
  TrialBalanceRequestDto,
  FinanceDimensionDefinition,
  FinanceDimensionValue,
  FinanceDimensionAccountRule,
  UpsertFinanceDimensionDefinition,
  UpsertFinanceDimensionValue,
  UpsertFinanceDimensionAccountRule,
  FinanceDimensionRouteId,
  FinanceDimensionRouteCertification,
  FinanceDimensionReadinessAssessment,
  FinanceDimensionCertificationState,
} from '@/types/finance';
import type {
  CreateOpeningStockAdjustmentDto,
  GovernedInventoryOpeningResult,
  OpeningStockOptions,
} from '@/lib/finance/opening-balance-governance';
import { appendFinanceSegmentFilters } from '@/lib/finance/report-segment-filters';
import { appendFinanceDimensionFilters } from '@/lib/finance/report-dimension-filters';
import type { FinanceDashboardData } from '@/types/finance-dashboard';

import { apiService } from '@/services/api.service';
import {
  normalizeJournalEntry,
  normalizeJournalEntries,
} from '@/lib/finance/journal-entry-normalizer';

// Mirrors the backend PeriodCloseResultDto returned by fiscal year close/reopen.
export interface FiscalYearCloseResult {
  accountingBookId?: string;
  bookCloseCycleId?: string;
  closingJournalEntryId?: string;
  reversalJournalEntryId?: string;
  netIncomeTransferred?: number;
  success: boolean;
  message: string;
  fiscalPeriodId: string;
  periodName: string;
  closedDate?: string | null;
  closedByUserName?: string | null;
  errors: string[];
}

export interface YearEndBookCloseCycle {
  id: string;
  fiscalYearId: string;
  accountingBookId: string;
  accountingBookCode: string;
  functionalCurrencyCode: string;
  cycleNumber: number;
  status: 'Closing' | 'Closed' | 'Reopened';
  closingJournalEntryId?: string;
  reversalJournalEntryId?: string;
  netIncomeTransferred: number;
  closedAtUtc: string;
  reopenedAtUtc?: string;
}

// =============================================================================
// FINANCE DATA SERVICE
// =============================================================================

class FinanceDataService {
  private resolveBackendFileUrl(url?: string): string {
    if (!url) return '';
    if (/^https?:\/\//i.test(url)) return url;

    // Use the deployed origin when NEXT_PUBLIC_API_URL is not configured.
    const apiBase = process.env.NEXT_PUBLIC_API_URL || '/api';
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

  async getAccountingBook(id: string): Promise<AccountingBook> {
    return apiService.get<AccountingBook>(`/finance/accounting-books/${id}`);
  }

  async getDeltaBookCombinedReport(
    id: string,
    asOfDate: string
  ): Promise<import('@/types/finance').DeltaBookCombinedReport> {
    const query = new URLSearchParams({ asOfDate });
    return apiService.get(`/finance/accounting-books/${id}/delta-combined-report?${query}`);
  }

  async getMultiDeltaBookCombinedReport(
    ids: string[],
    asOfDate: string
  ): Promise<import('@/types/finance').DeltaBookCombinedReport> {
    const query = new URLSearchParams({ asOfDate });
    ids.forEach(id => query.append('deltaAccountingBookIds', id));
    return apiService.get(`/finance/accounting-books/delta-combined-report?${query}`);
  }

  async getDeltaBookLedger(
    id: string,
    fromDate?: string,
    toDate?: string
  ): Promise<import('@/types/finance').DeltaBookLedgerInquiry> {
    const query = new URLSearchParams();
    if (fromDate) query.set('fromDate', fromDate);
    if (toDate) query.set('toDate', toDate);
    const suffix = query.toString() ? `?${query}` : '';
    return apiService.get(`/finance/accounting-books/${id}/delta-ledger${suffix}`);
  }

  async createAccountingBook(dto: SaveAccountingBook): Promise<AccountingBook> {
    return apiService.post<AccountingBook>('/finance/accounting-books', dto);
  }

  async updateAccountingBook(
    id: string,
    dto: SaveAccountingBook
  ): Promise<AccountingBook> {
    return apiService.put<AccountingBook>(
      `/finance/accounting-books/${id}`,
      dto
    );
  }

  async requestAccountingBookTransition(
    id: string,
    dto: RequestAccountingBookTransition
  ): Promise<AccountingBook> {
    return apiService.post<AccountingBook>(
      `/finance/accounting-books/${id}/transitions`,
      dto
    );
  }

  async approveAccountingBookTransition(
    id: string,
    dto: DecideAccountingBookTransition
  ): Promise<AccountingBook> {
    return apiService.post<AccountingBook>(
      `/finance/accounting-books/${id}/transitions/approve`,
      dto
    );
  }

  async rejectAccountingBookTransition(
    id: string,
    dto: DecideAccountingBookTransition
  ): Promise<AccountingBook> {
    return apiService.post<AccountingBook>(
      `/finance/accounting-books/${id}/transitions/reject`,
      dto
    );
  }

  async requestPrimaryAccountingBookReplacement(
    id: string,
    dto: import('@/types/finance').RequestPrimaryAccountingBookReplacement
  ): Promise<AccountingBook> {
    return apiService.post<AccountingBook>(
      `/finance/accounting-books/${id}/primary-replacement`,
      dto
    );
  }

  async approvePrimaryAccountingBookReplacement(
    id: string,
    dto: DecideAccountingBookTransition
  ): Promise<AccountingBook> {
    return apiService.post<AccountingBook>(
      `/finance/accounting-books/${id}/primary-replacement/approve`,
      dto
    );
  }

  async rejectPrimaryAccountingBookReplacement(
    id: string,
    dto: DecideAccountingBookTransition
  ): Promise<AccountingBook> {
    return apiService.post<AccountingBook>(
      `/finance/accounting-books/${id}/primary-replacement/reject`,
      dto
    );
  }

  async requestPrimaryAccountingBookReplacementReversal(
    id: string,
    dto: import('@/types/finance').RequestPrimaryAccountingBookReversal
  ): Promise<AccountingBook> {
    return apiService.post<AccountingBook>(
      `/finance/accounting-books/${id}/primary-replacement/reversal`,
      dto
    );
  }

  async approvePrimaryAccountingBookReplacementReversal(
    id: string,
    dto: DecideAccountingBookTransition
  ): Promise<AccountingBook> {
    return apiService.post<AccountingBook>(
      `/finance/accounting-books/${id}/primary-replacement/reversal/approve`,
      dto
    );
  }

  async rejectPrimaryAccountingBookReplacementReversal(
    id: string,
    dto: DecideAccountingBookTransition
  ): Promise<AccountingBook> {
    return apiService.post<AccountingBook>(
      `/finance/accounting-books/${id}/primary-replacement/reversal/reject`,
      dto
    );
  }

  async getAccountingBookPeriods(
    accountingBookId: string
  ): Promise<import('@/types/finance').AccountingBookPeriod[]> {
    return apiService.get(
      `/finance/accounting-books/${accountingBookId}/periods`
    );
  }

  async createAccountingBookPeriod(
    accountingBookId: string,
    fiscalPeriodId: string
  ): Promise<import('@/types/finance').AccountingBookPeriod> {
    return apiService.post(
      `/finance/accounting-books/${accountingBookId}/periods`,
      { fiscalPeriodId, initialStatus: 'Future' }
    );
  }

  async requestAccountingBookPeriodTransition(
    accountingBookId: string,
    periodId: string,
    targetStatus: string,
    reason: string,
    rowVersion: string
  ): Promise<import('@/types/finance').AccountingBookPeriod> {
    return apiService.post(
      `/finance/accounting-books/${accountingBookId}/periods/${periodId}/transitions`,
      { targetStatus, reason, rowVersion }
    );
  }

  async decideAccountingBookPeriodTransition(
    accountingBookId: string,
    periodId: string,
    action: 'approve' | 'reject',
    reason: string,
    rowVersion: string
  ): Promise<import('@/types/finance').AccountingBookPeriod> {
    return apiService.post(
      `/finance/accounting-books/${accountingBookId}/periods/${periodId}/transitions/${action}`,
      { reason, rowVersion }
    );
  }

  async getAccountingBookInitialization(
    accountingBookId: string
  ): Promise<import('@/types/finance').AccountingBookInitialization | null> {
    return apiService.get(
      `/finance/accounting-books/${accountingBookId}/initialization`
    );
  }

  async getAccountingBookActivationReadiness(
    accountingBookId: string
  ): Promise<import('@/types/finance').AccountingBookActivationReadiness> {
    return apiService.get(
      `/finance/accounting-books/${accountingBookId}/initialization/readiness`
    );
  }

  async prepareAccountingBookInitialization(
    accountingBookId: string,
    mode: string,
    cutoffDate: string,
    sourceAccountingBookId?: string | null
  ): Promise<
    import('@/types/finance').AccountingBookInitializationPreparation
  > {
    const query = new URLSearchParams({ mode, cutoffDate });
    if (sourceAccountingBookId)
      query.set('sourceAccountingBookId', sourceAccountingBookId);
    return apiService.get(
      `/finance/accounting-books/${accountingBookId}/initialization/preparation?${query}`
    );
  }

  async prepareDeltaBookStructure(
    accountingBookId: string
  ): Promise<import('@/types/finance').DeltaBookStructurePreparation> {
    return apiService.post(
      `/finance/accounting-books/${accountingBookId}/initialization/delta-structure`,
      {}
    );
  }

  async prepareAccountingBookStructure(accountingBookId: string): Promise<import('@/types/finance').DeltaBookStructurePreparation> {
    return apiService.post(`/finance/accounting-books/${accountingBookId}/initialization/structure`, {});
  }

  async configureAccountingBookInitialization(
    accountingBookId: string,
    request: Record<string, unknown>
  ): Promise<import('@/types/finance').AccountingBookInitialization> {
    return apiService.put(
      `/finance/accounting-books/${accountingBookId}/initialization`,
      request
    );
  }

  async submitAccountingBookInitialization(
    accountingBookId: string
  ): Promise<import('@/types/finance').AccountingBookInitialization> {
    return apiService.post(
      `/finance/accounting-books/${accountingBookId}/initialization/submit`,
      {}
    );
  }

  async decideAccountingBookInitialization(
    accountingBookId: string,
    action: 'approve' | 'reject',
    reason: string,
    rowVersion: string
  ): Promise<import('@/types/finance').AccountingBookInitialization> {
    return apiService.post(
      `/finance/accounting-books/${accountingBookId}/initialization/${action}`,
      { reason, rowVersion }
    );
  }

  async getAccountClassifications(
    accountingBookId?: string,
    includeInactive = false
  ): Promise<AccountClassification[]> {
    const queryParams = new URLSearchParams();
    if (accountingBookId)
      queryParams.append('accountingBookId', accountingBookId);
    if (includeInactive) queryParams.append('includeInactive', 'true');
    const suffix = queryParams.toString() ? `?${queryParams}` : '';
    return apiService.get<AccountClassification[]>(
      `/finance/account-classifications${suffix}`
    );
  }

  async getAccountClassificationWhereUsed(
    id: string
  ): Promise<AccountClassificationWhereUsed> {
    return apiService.get<AccountClassificationWhereUsed>(
      `/finance/account-classifications/${id}/where-used`
    );
  }

  async createAccountClassification(
    dto: SaveAccountClassification
  ): Promise<AccountClassification> {
    return apiService.post<AccountClassification>(
      '/finance/account-classifications',
      dto
    );
  }

  async updateAccountClassification(
    id: string,
    dto: SaveAccountClassification
  ): Promise<AccountClassification> {
    return apiService.put<AccountClassification>(
      `/finance/account-classifications/${id}`,
      dto
    );
  }

  async retireAccountClassification(
    id: string,
    reason: string,
    rowVersion: string
  ): Promise<AccountClassification> {
    return apiService.post<AccountClassification>(
      `/finance/account-classifications/${id}/retire`,
      { reason, rowVersion }
    );
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
    if (filters?.accountType)
      queryParams.append('accountType', filters.accountType);
    if (filters?.status) queryParams.append('status', filters.status);
    if (filters?.isMultiCurrency !== undefined)
      queryParams.append('isMultiCurrency', String(filters.isMultiCurrency));
    if (filters?.coaType) queryParams.append('coaType', filters.coaType);
    if (filters?.page !== undefined)
      queryParams.append('page', String(filters.page));
    if (filters?.pageSize !== undefined)
      queryParams.append('pageSize', String(filters.pageSize));
    if (filters?.take !== undefined)
      queryParams.append('take', String(filters.take));

    const endpoint = `/finance/accounts${queryParams.toString() ? `?${queryParams}` : ''}`;
    return apiService.get<Account[]>(endpoint);
  }

  async getAccountById(id: string): Promise<Account> {
    return apiService.get<Account>(`/finance/accounts/${id}`);
  }

  async getAccountTransactions(
    accountId: string,
    accountingBookCode: string,
    page = 1,
    pageSize = 10
  ): Promise<AccountTransactionInquiryPage> {
    const queryParams = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
      accountingBookCode,
    });
    return apiService.get<AccountTransactionInquiryPage>(
      `/finance/accounts/${accountId}/transactions?${queryParams}`
    );
  }

  // ===== CODING DIMENSIONS =====

  async getFinanceDimensions(
    includeInactive = false
  ): Promise<FinanceDimensionDefinition[]> {
    const suffix = includeInactive ? '?includeInactive=true' : '';
    return apiService.get<FinanceDimensionDefinition[]>(
      `/finance/dimensions${suffix}`
    );
  }

  async createFinanceDimension(
    dto: UpsertFinanceDimensionDefinition
  ): Promise<FinanceDimensionDefinition> {
    return apiService.post<FinanceDimensionDefinition>(
      '/finance/dimensions',
      dto
    );
  }

  async updateFinanceDimension(
    id: string,
    dto: UpsertFinanceDimensionDefinition
  ): Promise<FinanceDimensionDefinition> {
    return apiService.put<FinanceDimensionDefinition>(
      `/finance/dimensions/${id}`,
      dto
    );
  }

  async createFinanceDimensionValue(
    definitionId: string,
    dto: UpsertFinanceDimensionValue
  ): Promise<FinanceDimensionValue> {
    return apiService.post<FinanceDimensionValue>(
      `/finance/dimensions/${definitionId}/values`,
      dto
    );
  }

  async updateFinanceDimensionValue(
    definitionId: string,
    valueId: string,
    dto: UpsertFinanceDimensionValue
  ): Promise<FinanceDimensionValue> {
    return apiService.put<FinanceDimensionValue>(
      `/finance/dimensions/${definitionId}/values/${valueId}`,
      dto
    );
  }

  async getFinanceDimensionRules(
    accountId?: string
  ): Promise<FinanceDimensionAccountRule[]> {
    const suffix = accountId
      ? `?accountId=${encodeURIComponent(accountId)}`
      : '';
    return apiService.get<FinanceDimensionAccountRule[]>(
      `/finance/dimensions/rules${suffix}`
    );
  }

  async createFinanceDimensionRule(
    dto: UpsertFinanceDimensionAccountRule
  ): Promise<FinanceDimensionAccountRule> {
    return apiService.post<FinanceDimensionAccountRule>(
      '/finance/dimensions/rules',
      dto
    );
  }

  async updateFinanceDimensionRule(
    id: string,
    dto: UpsertFinanceDimensionAccountRule
  ): Promise<FinanceDimensionAccountRule> {
    return apiService.put<FinanceDimensionAccountRule>(
      `/finance/dimensions/rules/${id}`,
      dto
    );
  }

  async getFinanceDimensionCertificationRoutes(): Promise<
    FinanceDimensionRouteCertification[]
  > {
    return apiService.get<FinanceDimensionRouteCertification[]>(
      '/finance/dimensions/certifications'
    );
  }

  async assessFinanceDimensionRoute(
    routeId: FinanceDimensionRouteId,
    targetState: FinanceDimensionCertificationState = 'Enforced'
  ): Promise<FinanceDimensionReadinessAssessment> {
    return apiService.post<FinanceDimensionReadinessAssessment>(
      `/finance/dimensions/certifications/${routeId}/readiness`,
      { targetState }
    );
  }

  async getFinanceDimensionReadinessAssessment(
    assessmentId: string
  ): Promise<FinanceDimensionReadinessAssessment> {
    return apiService.get<FinanceDimensionReadinessAssessment>(
      `/finance/dimensions/certifications/readiness/${assessmentId}`
    );
  }

  async promoteFinanceDimensionRoute(
    route: FinanceDimensionRouteCertification,
    assessmentId: string,
    reason: string,
    targetState: FinanceDimensionCertificationState = 'Enforced'
  ): Promise<FinanceDimensionRouteCertification> {
    return apiService.post<FinanceDimensionRouteCertification>(
      `/finance/dimensions/certifications/${route.routeId}/promote`,
      {
        readinessAssessmentId: assessmentId,
        targetState,
        reason,
        effectiveDate: new Date().toISOString(),
        rowVersion: route.rowVersion,
      }
    );
  }

  async downloadFinanceDimensionReadinessCsv(
    assessmentId: string
  ): Promise<Blob> {
    return apiService.downloadBlob(
      `/finance/dimensions/certifications/readiness/${assessmentId}/csv`
    );
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
    if (filters?.currencyCode)
      queryParams.append('currencyCode', filters.currencyCode);
    if (filters?.isActive !== undefined)
      queryParams.append('isActive', String(filters.isActive));

    const endpoint = `/finance/currencies${queryParams.toString() ? `?${queryParams}` : ''}`;
    return apiService.get<Currency[]>(endpoint);
  }

  async getCurrencyByCode(code: string): Promise<Currency> {
    return apiService.get<Currency>(`/finance/currencies/${code}`);
  }

  async createCurrency(dto: CreateCurrencyDto): Promise<Currency> {
    return apiService.post<Currency>('/finance/currencies', dto);
  }

  async updateCurrency(
    code: string,
    dto: UpdateCurrencyDto
  ): Promise<Currency> {
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
    quoteSide?: string;
    startDate?: string;
    endDate?: string;
  }): Promise<ExchangeRate[]> {
    const queryParams = new URLSearchParams();
    if (filters?.baseCurrencyCode)
      queryParams.append('fromCurrency', filters.baseCurrencyCode);
    if (filters?.targetCurrencyCode)
      queryParams.append('toCurrency', filters.targetCurrencyCode);
    if (filters?.rateType) queryParams.append('rateType', filters.rateType);
    if (filters?.quoteSide) queryParams.append('quoteSide', filters.quoteSide);
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

  async updateFiscalYear(
    id: string,
    dto: { fiscalYearName?: string; notes?: string }
  ): Promise<FiscalYear> {
    return apiService.put<FiscalYear>(`/finance/fiscal-years/${id}`, dto);
  }

  async deleteFiscalYear(id: string): Promise<void> {
    return apiService.delete(`/finance/fiscal-years/${id}`);
  }

  async closeFiscalYear(
    id: string,
    options: { accountingBookId: string; idempotencyKey: string; retainedEarningsAccountId?: string; closingNotes?: string }
  ): Promise<FiscalYearCloseResult> {
    // The retained earnings account defaults from Finance Settings when omitted.
    return apiService.post<FiscalYearCloseResult>(
      `/finance/fiscal-years/${id}/close`,
      options
    );
  }

  async reopenFiscalYear(
    id: string,
    request: { accountingBookId: string; bookCloseCycleId: string; reason: string }
  ): Promise<FiscalYearCloseResult> {
    return apiService.post<FiscalYearCloseResult>(
      `/finance/fiscal-years/${id}/reopen`,
      request
    );
  }

  async getYearEndCloseCycles(id: string): Promise<YearEndBookCloseCycle[]> {
    return apiService.get<YearEndBookCloseCycle[]>(`/finance/fiscal-years/${id}/book-close-cycles`);
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

  async openFiscalPeriod(id: string, reason: string): Promise<FiscalPeriod> {
    // Future-to-Open is a first-use lifecycle transition. Certified Closed periods continue
    // through requestFiscalPeriodReopen so their signed close evidence remains protected.
    return apiService.post<FiscalPeriod>(`/finance/periods/${id}/open`, {
      reason,
    });
  }

  async updateFiscalPeriodPostingDatePolicy(
    id: string,
    allowFutureDating: boolean,
    reason: string
  ): Promise<FiscalPeriod> {
    return apiService.put<FiscalPeriod>(
      `/finance/periods/${id}/posting-date-policy`,
      {
        allowFutureDating,
        reason,
      }
    );
  }

  async evaluateFiscalPeriodClose(id: string): Promise<FinanceCloseWorkspace> {
    return apiService.post<FinanceCloseWorkspace>(
      `/finance/periods/${id}/close-workspace/evaluate`,
      {}
    );
  }

  async prepareFiscalPeriodClose(
    id: string,
    declaration: string
  ): Promise<FinanceCloseWorkspace> {
    return apiService.post<FinanceCloseWorkspace>(
      `/finance/periods/${id}/close-workspace/prepare`,
      { declaration }
    );
  }

  async getFinanceCloseTemplates(): Promise<FinanceCloseTemplate[]> {
    return apiService.get<FinanceCloseTemplate[]>('/finance/close-templates');
  }

  async createFinanceCloseTemplateVersion(
    request: SaveFinanceCloseTemplateVersion
  ): Promise<FinanceCloseTemplate> {
    return apiService.post<FinanceCloseTemplate>(
      '/finance/close-templates/versions',
      request
    );
  }

  async updateFinanceCloseTemplateDraft(
    id: string,
    request: SaveFinanceCloseTemplateVersion
  ): Promise<FinanceCloseTemplate> {
    return apiService.put<FinanceCloseTemplate>(
      `/finance/close-templates/${id}`,
      request
    );
  }

  async approveFinanceCloseTemplate(
    id: string,
    declaration: string
  ): Promise<FinanceCloseTemplate> {
    return apiService.post<FinanceCloseTemplate>(
      `/finance/close-templates/${id}/approve`,
      { declaration }
    );
  }

  async updateFinanceCloseTask(
    periodId: string,
    taskId: string,
    request: {
      assignToCurrentUser?: boolean;
      assignedToUserId?: string;
      dueAt?: string;
      markCompleted?: boolean;
      evidenceSummary?: string;
    }
  ): Promise<FinanceCloseWorkspace> {
    return apiService.put<FinanceCloseWorkspace>(
      `/finance/periods/${periodId}/close-workspace/tasks/${taskId}`,
      request
    );
  }

  async linkFinanceCloseEvidence(
    periodId: string,
    taskId: string,
    request: {
      fileUploadRecordId: string;
      evidenceType: string;
      description?: string;
    }
  ): Promise<FinanceCloseWorkspace> {
    return apiService.post<FinanceCloseWorkspace>(
      `/finance/periods/${periodId}/close-workspace/tasks/${taskId}/evidence`,
      request
    );
  }

  async removeFinanceCloseEvidence(
    periodId: string,
    taskId: string,
    attachmentId: string
  ): Promise<FinanceCloseWorkspace> {
    return apiService.delete<FinanceCloseWorkspace>(
      `/finance/periods/${periodId}/close-workspace/tasks/${taskId}/evidence/${attachmentId}`
    );
  }

  async requestFinanceCloseWaiver(
    periodId: string,
    snapshotId: string,
    financeCloseEvidenceAttachmentId: string,
    justification: string
  ): Promise<FinanceCloseWorkspace> {
    return apiService.post<FinanceCloseWorkspace>(
      `/finance/periods/${periodId}/close-workspace/checks/${snapshotId}/waivers`,
      { financeCloseEvidenceAttachmentId, justification }
    );
  }

  async reviewFinanceCloseWaiver(
    periodId: string,
    waiverId: string,
    approve: boolean,
    comment: string
  ): Promise<FinanceCloseWorkspace> {
    return apiService.post<FinanceCloseWorkspace>(
      `/finance/periods/${periodId}/close-workspace/waivers/${waiverId}/review`,
      { approve, comment }
    );
  }

  async closeFiscalPeriod(
    id: string,
    reviewerDeclaration: string,
    closingNotes?: string
  ): Promise<FiscalYearCloseResult> {
    // Mandatory server checks have no bypass. The reviewer declaration is the second-person
    // approval evidence linked to the active numbered close cycle.
    return apiService.post<FiscalYearCloseResult>(
      `/finance/periods/${id}/close`,
      {
        reviewerDeclaration,
        closingNotes,
      }
    );
  }

  // ===== MODULE LOCKING =====

  async getModuleDefinitions(): Promise<ModuleDefinition[]> {
    return apiService.get<ModuleDefinition[]>('/finance/modules');
  }

  async lockPeriodForModule(
    periodId: string,
    moduleCode: string,
    reason: string
  ): Promise<void> {
    return apiService.post(`/finance/periods/${periodId}/lock-module`, {
      moduleCode,
      reason,
    });
  }

  async unlockPeriodForModule(
    periodId: string,
    moduleCode: string,
    reason: string,
    reopenUntilUtc: string
  ): Promise<void> {
    return apiService.post(`/finance/periods/${periodId}/unlock-module`, {
      moduleCode,
      reason,
      reopenUntilUtc,
    });
  }

  async requestFiscalPeriodReopen(
    id: string,
    reason: string,
    affectedPeriodAssessment: string
  ): Promise<FinancePeriodReopenRequest> {
    return apiService.post<FinancePeriodReopenRequest>(
      `/finance/periods/${id}/reopen-requests`,
      {
        reason,
        affectedPeriodAssessment,
      }
    );
  }

  async reviewFiscalPeriodReopen(
    periodId: string,
    requestId: string,
    approved: boolean,
    reviewComment: string
  ): Promise<FinancePeriodReopenRequest> {
    return apiService.post<FinancePeriodReopenRequest>(
      `/finance/periods/${periodId}/reopen-requests/${requestId}/review`,
      { approved, reviewComment }
    );
  }

  // ===== JOURNAL ENTRIES =====

  async getJournalEntries(filters?: {
    status?: string;
    startDate?: string;
    endDate?: string;
    fiscalYearId?: string;
    fiscalPeriodId?: string;
    sourceModule?: string;
  }): Promise<JournalEntry[]> {
    const queryParams = new URLSearchParams();
    if (filters?.status) queryParams.append('status', filters.status);
    if (filters?.startDate) queryParams.append('startDate', filters.startDate);
    if (filters?.endDate) queryParams.append('endDate', filters.endDate);
    if (filters?.fiscalYearId)
      queryParams.append('fiscalYearId', filters.fiscalYearId);
    if (filters?.fiscalPeriodId)
      queryParams.append('fiscalPeriodId', filters.fiscalPeriodId);
    if (filters?.sourceModule)
      queryParams.append('sourceModule', filters.sourceModule);

    const endpoint = `/finance/journal-entries${queryParams.toString() ? `?${queryParams}` : ''}`;
    const raw = await apiService.get<any[]>(endpoint);
    const entries = normalizeJournalEntries(raw);
    if (!filters?.sourceModule) return entries;

    return entries.filter(
      (entry) =>
        (entry.sourceModule || '').toUpperCase() ===
        filters.sourceModule?.toUpperCase()
    );
  }

  async getPendingJournalApprovals(): Promise<JournalEntry[]> {
    const raw = await apiService.get<any[]>(
      '/finance/journal-entries/pending-approvals'
    );
    return normalizeJournalEntries(raw);
  }

  async getJournalEntryById(id: string): Promise<JournalEntry> {
    const raw = await apiService.get<any>(`/finance/journal-entries/${id}`);
    return normalizeJournalEntry(raw);
  }

  async getJournalEntryAuditTrail(
    id: string
  ): Promise<FinanceJournalAuditLog[]> {
    return apiService.get<FinanceJournalAuditLog[]>(
      `/finance/journal-entries/${id}/audit-trail`
    );
  }

  async createJournalEntry(dto: CreateJournalEntryDto): Promise<JournalEntry> {
    const raw = await apiService.post<any>('/finance/journal-entries', dto);
    return normalizeJournalEntry(raw);
  }

  async updateJournalEntry(
    id: string,
    dto: Partial<CreateJournalEntryDto>
  ): Promise<JournalEntry> {
    const raw = await apiService.put<any>(
      `/finance/journal-entries/${id}`,
      dto
    );
    return normalizeJournalEntry(raw);
  }

  async deleteJournalEntry(id: string): Promise<void> {
    return apiService.delete(`/finance/journal-entries/${id}`);
  }

  async postJournalEntry(id: string): Promise<void> {
    await apiService.post(`/finance/journal-entries/${id}/post`, {});
  }

  async reverseJournalEntry(
    id: string,
    reason: string,
    reversalDatePolicy: 'CurrentOpenPeriod' | 'OriginalDocumentPeriodIfOpen',
    reversalDate?: string
  ): Promise<JournalEntry> {
    const raw = await apiService.post<any>(
      `/finance/journal-entries/${id}/reverse`,
      {
        reason,
        reversalDatePolicy,
        reversalDate: reversalDate || undefined,
      }
    );
    return normalizeJournalEntry(raw);
  }

  async getNextJournalNumber(): Promise<string> {
    const response = await apiService.get<{ number: string }>(
      '/finance/journal-entries/next-number'
    );
    return response.number;
  }

  // ===== SUBLEDGER ADJUSTMENT JOURNALS =====

  async getSubledgerAdjustmentJournals(
    module?: SubledgerModule
  ): Promise<SubledgerAdjustmentJournal[]> {
    const queryParams = new URLSearchParams();
    if (module) queryParams.append('module', module);

    const endpoint = `/finance/subledger-adjustment-journals${queryParams.toString() ? `?${queryParams}` : ''}`;
    return apiService.get<SubledgerAdjustmentJournal[]>(endpoint);
  }

  async createSubledgerAdjustmentJournal(
    dto: CreateSubledgerAdjustmentJournalDto
  ): Promise<SubledgerAdjustmentJournal> {
    return apiService.post<SubledgerAdjustmentJournal>(
      '/finance/subledger-adjustment-journals',
      dto
    );
  }

  async reverseSubledgerAdjustmentJournal(
    id: string,
    dto: ReverseSubledgerAdjustmentJournalDto
  ): Promise<SubledgerAdjustmentJournal> {
    return apiService.post<SubledgerAdjustmentJournal>(
      `/finance/subledger-adjustment-journals/${id}/reverse`,
      dto
    );
  }

  // ===== JOURNAL ENTRY APPROVAL WORKFLOW =====

  async requestJournalEntryApproval(id: string): Promise<JournalEntry> {
    const raw = await apiService.post<any>(
      `/finance/journal-entries/${id}/request-approval`
    );
    return normalizeJournalEntry(raw);
  }

  async getJournalEntryBudgetControl(
    id: string
  ): Promise<FinanceBudgetControlEvaluation> {
    return apiService.get<FinanceBudgetControlEvaluation>(
      `/finance/journal-entries/${id}/budget-control`
    );
  }

  async requestJournalEntryBudgetOverride(
    id: string,
    reason: string
  ): Promise<FinanceBudgetOverrideRequest> {
    return apiService.post<FinanceBudgetOverrideRequest>(
      `/finance/journal-entries/${id}/budget-override`,
      { reason }
    );
  }

  async withdrawJournalEntryApproval(
    id: string,
    reason?: string
  ): Promise<JournalEntry> {
    const raw = await apiService.post<any>(
      `/finance/journal-entries/${id}/withdraw-approval`,
      {
        reason: reason || 'Approval request withdrawn.',
      }
    );
    return normalizeJournalEntry(raw);
  }

  async approveJournalEntry(
    id: string,
    comments?: string
  ): Promise<JournalEntry> {
    const raw = await apiService.post<any>(
      `/finance/journal-entries/${id}/approve`,
      { comments }
    );
    return normalizeJournalEntry(raw);
  }

  async rejectJournalEntry(id: string, reason: string): Promise<JournalEntry> {
    const raw = await apiService.post<any>(
      `/finance/journal-entries/${id}/reject`,
      { reason }
    );
    return normalizeJournalEntry(raw);
  }

  // ===== CONTROLLED OPENING BALANCES =====

  async createOpeningBalanceBatch(
    dto: CreateOpeningBalanceBatchDto
  ): Promise<OpeningBalanceBatch> {
    return apiService.post<OpeningBalanceBatch>(
      '/finance/opening-balances',
      dto
    );
  }

  async createFixedAssetOpeningBalanceBatch(
    dto: CreateFixedAssetOpeningBalanceBatchDto
  ): Promise<OpeningBalanceBatch> {
    return apiService.post<OpeningBalanceBatch>(
      '/finance/opening-balances/fixed-assets',
      dto
    );
  }

  async createBankAccountOpeningBalance(
    dto: CreateBankAccountOpeningBalanceDto
  ): Promise<OpeningBalanceBatch> {
    return apiService.post<OpeningBalanceBatch>(
      '/finance/opening-balances/bank-accounts',
      dto
    );
  }

  async createResidualGlEquityOpeningBalance(
    dto: CreateResidualGlEquityOpeningBalanceDto
  ): Promise<OpeningBalanceBatch> {
    return apiService.post<OpeningBalanceBatch>(
      '/finance/opening-balances/residual-gl-equity',
      dto
    );
  }

  async getGovernedOpeningBalanceOptions(
    request: GovernedOpeningBalanceOptionsRequest
  ): Promise<GovernedOpeningBalanceOptions> {
    const queryParams = new URLSearchParams();
    queryParams.append('openingDate', request.openingDate);
    queryParams.append('fiscalPeriodId', request.fiscalPeriodId);
    queryParams.append('bookClassification', request.bookClassification);

    return apiService.get<GovernedOpeningBalanceOptions>(
      `/finance/opening-balances/governed-options?${queryParams}`
    );
  }

  async getOpeningStockOptions(): Promise<OpeningStockOptions> {
    // Inventory owns opening-stock masters, readiness and workflow. Finance consumes this
    // typed boundary only; it does not duplicate Inventory lookups or accept account IDs.
    return apiService.get<OpeningStockOptions>(
      '/inventory/adjustments/opening-stock/options'
    );
  }

  async createOpeningStockAdjustment(
    dto: CreateOpeningStockAdjustmentDto
  ): Promise<GovernedInventoryOpeningResult> {
    return apiService.post<GovernedInventoryOpeningResult>(
      '/inventory/adjustments/opening-stock',
      dto
    );
  }

  // These specialised cutover endpoints create canonical AP/AR facts and a controlled
  // opening batch together. Callers must never recreate them as freehand GL lines because
  // later allocation, certificate and remittance workflows depend on the source linkage.
  async createSupplierAdvanceOpeningBalance(
    dto: CreateSupplierAdvanceOpeningBalanceDto
  ): Promise<OpeningBalanceBatch> {
    return apiService.post<OpeningBalanceBatch>(
      '/finance/opening-balances/supplier-advances',
      dto
    );
  }

  async createCustomerAdvanceOpeningBalance(
    dto: CreateCustomerAdvanceOpeningBalanceDto
  ): Promise<OpeningBalanceBatch> {
    return apiService.post<OpeningBalanceBatch>(
      '/finance/opening-balances/customer-advances',
      dto
    );
  }

  async createApWithholdingOpeningBalance(
    dto: CreateApWithholdingOpeningBalanceDto
  ): Promise<OpeningBalanceBatch> {
    return apiService.post<OpeningBalanceBatch>(
      '/finance/opening-balances/ap-withholding',
      dto
    );
  }

  async createArWithholdingOpeningBalance(
    dto: CreateArWithholdingOpeningBalanceDto
  ): Promise<OpeningBalanceBatch> {
    return apiService.post<OpeningBalanceBatch>(
      '/finance/opening-balances/ar-withholding',
      dto
    );
  }

  async getSpecializedOpeningBalanceOptions(): Promise<SpecializedOpeningBalanceOptions> {
    return apiService.get<SpecializedOpeningBalanceOptions>(
      '/finance/opening-balances/specialized-options'
    );
  }

  async getSubledgerOpeningBalanceReadiness(): Promise<SubledgerOpeningBalanceReadiness> {
    return apiService.get<SubledgerOpeningBalanceReadiness>(
      '/finance/opening-balances/subledger-readiness'
    );
  }

  async getOpeningBalanceBatches(): Promise<OpeningBalanceBatch[]> {
    return apiService.get<OpeningBalanceBatch[]>('/finance/opening-balances');
  }

  async getOpeningBalanceBatch(batchId: string): Promise<OpeningBalanceBatch> {
    return apiService.get<OpeningBalanceBatch>(
      `/finance/opening-balances/${batchId}`
    );
  }

  async updateOpeningBalanceBatch(
    batchId: string,
    dto: UpdateOpeningBalanceBatchDto
  ): Promise<OpeningBalanceBatch> {
    return apiService.put<OpeningBalanceBatch>(
      `/finance/opening-balances/${batchId}`,
      dto
    );
  }

  async validateOpeningBalanceBatch(
    batchId: string
  ): Promise<OpeningBalanceValidationResult> {
    return apiService.post<OpeningBalanceValidationResult>(
      `/finance/opening-balances/${batchId}/validate`,
      {}
    );
  }

  async submitOpeningBalanceBatch(
    batchId: string,
    comment?: string
  ): Promise<OpeningBalanceBatch> {
    return apiService.post<OpeningBalanceBatch>(
      `/finance/opening-balances/${batchId}/submit`,
      { comment }
    );
  }

  async postOpeningBalanceBatch(
    batchId: string,
    comment?: string
  ): Promise<OpeningBalanceBatch> {
    return apiService.post<OpeningBalanceBatch>(
      `/finance/opening-balances/${batchId}/post`,
      { comment }
    );
  }

  async requestOpeningBalanceReversal(
    batchId: string,
    dto: RequestOpeningBalanceBatchReversalDto
  ): Promise<OpeningBalanceBatchReversal> {
    return apiService.post<OpeningBalanceBatchReversal>(
      `/finance/opening-balances/${batchId}/reversals`,
      dto
    );
  }

  async reviewOpeningBalanceReversal(
    batchId: string,
    requestId: string,
    dto: ReviewOpeningBalanceBatchReversalDto
  ): Promise<OpeningBalanceBatchReversal> {
    return apiService.post<OpeningBalanceBatchReversal>(
      `/finance/opening-balances/${batchId}/reversals/${requestId}/review`,
      dto
    );
  }

  async postOpeningBalanceReversal(
    batchId: string,
    requestId: string
  ): Promise<OpeningBalanceBatchReversal> {
    return apiService.post<OpeningBalanceBatchReversal>(
      `/finance/opening-balances/${batchId}/reversals/${requestId}/post`,
      {}
    );
  }

  async getOpeningBalanceDiagnostics(): Promise<OpeningBalanceDiagnostic[]> {
    return apiService.get<OpeningBalanceDiagnostic[]>(
      '/finance/opening-balances/diagnostics'
    );
  }

  // ===== ATTACHMENTS =====

  async uploadAttachment(
    file: File,
    referenceType: string,
    referenceId: string
  ): Promise<{ fileId: string; url: string; name: string }> {
    const formData = new FormData();
    formData.append('file', file);
    // Use the canonical single-file upload endpoint.
    // Keep a specific category so backend policies/audits can distinguish finance journal attachments.
    formData.append('category', 'finance-journal-attachments');
    const response = await apiService.post<any>(`/fileupload/single`, formData);

    const fileId =
      response.fileId || response.id || response.fileUploadRecordId;
    if (!fileId) {
      throw new Error(
        'Upload succeeded but fileId was not returned by server.'
      );
    }

    return {
      fileId,
      url: this.resolveBackendFileUrl(
        response.url || response.fileUrl || response.publicUrl || ''
      ),
      name: response.fileName || file.name,
    };
  }

  async linkJournalEntryAttachment(
    journalEntryId: string,
    fileId: string
  ): Promise<void> {
    return apiService.post(
      `/finance/journal-entries/${journalEntryId}/attachments/${fileId}`,
      {}
    );
  }

  async unlinkJournalEntryAttachment(
    journalEntryId: string,
    fileId: string
  ): Promise<void> {
    return apiService.delete(
      `/finance/journal-entries/${journalEntryId}/attachments/${fileId}`
    );
  }

  async getJournalEntryAttachments(
    journalEntryId: string
  ): Promise<JournalEntryAttachment[]> {
    const attachments = await apiService.get<JournalEntryAttachment[]>(
      `/finance/journal-entries/${journalEntryId}/attachments`
    );
    return (attachments || []).map((a) => ({
      ...a,
      fileUrl: this.resolveBackendFileUrl(a.fileUrl),
    }));
  }

  // ===== FINANCE SETTINGS =====

  async getFinanceSettings(): Promise<FinanceSettings> {
    return apiService.get<FinanceSettings>('/finance/settings');
  }

  async updateFinanceSettings(
    dto: UpdateFinanceSettingsDto
  ): Promise<FinanceSettings> {
    return apiService.patch<FinanceSettings>('/finance/settings', dto);
  }

  // ===== FINANCIAL STATEMENTS =====

  async getTrialBalance(
    params: TrialBalanceRequestDto
  ): Promise<TrialBalanceReportDto> {
    const queryParams = new URLSearchParams();
    if (params.asAtDate) queryParams.append('asAtDate', params.asAtDate);
    if (params.bookClassification)
      queryParams.append('bookClassification', params.bookClassification);
    if (params.includeZeroBalances !== undefined)
      queryParams.append(
        'includeZeroBalances',
        String(params.includeZeroBalances)
      );
    appendFinanceSegmentFilters(queryParams, params.segmentFilters);
    appendFinanceDimensionFilters(queryParams, params.dimensionFilters);

    return apiService.get<TrialBalanceReportDto>(
      `/finance/statements/trial-balance?${queryParams}`
    );
  }

  async getDetailedLedger(
    params: DetailedLedgerRequestDto
  ): Promise<DetailedLedgerReportDto> {
    const queryParams = new URLSearchParams();
    queryParams.append('startDate', params.startDate);
    queryParams.append('endDate', params.endDate);
    if (params.bookClassification)
      queryParams.append('bookClassification', params.bookClassification);
    if (params.includeReversed !== undefined)
      queryParams.append('includeReversed', String(params.includeReversed));
    if (params.includeOpeningBalances !== undefined)
      queryParams.append(
        'includeOpeningBalances',
        String(params.includeOpeningBalances)
      );
    if (params.accountIds && params.accountIds.length > 0) {
      params.accountIds.forEach((accountId) =>
        queryParams.append('accountIds', accountId)
      );
    }
    appendFinanceDimensionFilters(queryParams, params.dimensionFilters);

    return apiService.get<DetailedLedgerReportDto>(
      `/finance/statements/detailed-ledger?${queryParams}`
    );
  }

  async getIncomeStatement(
    params: IncomeStatementRequestDto
  ): Promise<IncomeStatementReportDto> {
    const queryParams = new URLSearchParams();
    if (params.periodStart)
      queryParams.append('periodStart', params.periodStart);
    if (params.periodEnd) queryParams.append('periodEnd', params.periodEnd);
    if (params.bookClassification)
      queryParams.append('bookClassification', params.bookClassification);
    if (params.includeAccountDetails !== undefined)
      queryParams.append(
        'includeAccountDetails',
        String(params.includeAccountDetails)
      );
    if (params.layoutId) queryParams.append('layoutId', params.layoutId);
    if (params.useDefaultLayout !== undefined)
      queryParams.append('useDefaultLayout', String(params.useDefaultLayout));
    appendFinanceSegmentFilters(queryParams, params.segmentFilters);
    appendFinanceDimensionFilters(queryParams, params.dimensionFilters);

    return apiService.get<IncomeStatementReportDto>(
      `/finance/statements/income-statement?${queryParams}`
    );
  }

  async getBalanceSheet(
    params: BalanceSheetRequestDto
  ): Promise<BalanceSheetReportDto> {
    const queryParams = new URLSearchParams();
    if (params.asAtDate) queryParams.append('asAtDate', params.asAtDate);
    if (params.bookClassification)
      queryParams.append('bookClassification', params.bookClassification);
    if (params.includeAccountDetails !== undefined)
      queryParams.append(
        'includeAccountDetails',
        String(params.includeAccountDetails)
      );
    if (params.layoutId) queryParams.append('layoutId', params.layoutId);
    if (params.useDefaultLayout !== undefined)
      queryParams.append('useDefaultLayout', String(params.useDefaultLayout));
    appendFinanceSegmentFilters(queryParams, params.segmentFilters);
    appendFinanceDimensionFilters(queryParams, params.dimensionFilters);

    return apiService.get<BalanceSheetReportDto>(
      `/finance/statements/balance-sheet?${queryParams}`
    );
  }

  async getFinancialStatementLayouts(
    statementType: FinancialStatementType,
    accountingBookId?: string
  ): Promise<FinancialStatementLayoutSummaryDto[]> {
    const queryParams = new URLSearchParams();
    queryParams.append('statementType', statementType);
    if (accountingBookId)
      queryParams.append('accountingBookId', accountingBookId);

    return apiService.get<FinancialStatementLayoutSummaryDto[]>(
      `/finance/financial-statement-layouts?${queryParams}`
    );
  }

  async getCashFlowStatement(
    params: CashFlowStatementRequestDto
  ): Promise<CashFlowStatementReportDto> {
    const queryParams = new URLSearchParams();
    queryParams.append('periodStart', params.periodStart);
    queryParams.append('periodEnd', params.periodEnd);
    if (params.bookClassification)
      queryParams.append('bookClassification', params.bookClassification);
    if (params.includeAccountDetails !== undefined)
      queryParams.append(
        'includeAccountDetails',
        String(params.includeAccountDetails)
      );
    if (params.method) queryParams.append('method', params.method);

    return apiService.get<CashFlowStatementReportDto>(
      `/finance/statements/cash-flow?${queryParams}`
    );
  }

  async getMultiCurrencyDetailReport(
    params: MultiCurrencyDetailRequestDto
  ): Promise<MultiCurrencyDetailReportDto> {
    const queryParams = new URLSearchParams();
    queryParams.append('startDate', params.startDate);
    queryParams.append('endDate', params.endDate);
    if (params.accountId) queryParams.append('accountId', params.accountId);
    if (params.currencyCode)
      queryParams.append('currencyCode', params.currencyCode);
    if (params.includeRevaluation !== undefined)
      queryParams.append(
        'includeRevaluation',
        String(params.includeRevaluation)
      );

    return apiService.get<MultiCurrencyDetailReportDto>(
      `/finance/statements/multi-currency-detail?${queryParams}`
    );
  }

  // ===== SEGMENT STRUCTURES =====

  async getSegmentStructures(): Promise<SegmentStructure[]> {
    return apiService.get<SegmentStructure[]>('/finance/segments');
  }

  async getReportingDimensions(): Promise<SegmentStructure[]> {
    return apiService.get<SegmentStructure[]>(
      '/finance/segments/reporting-dimensions'
    );
  }

  async getReportingSegmentOptions(
    segmentId: string,
    search: string,
    take = 50,
    signal?: AbortSignal
  ): Promise<ReportingSegmentOptionsResponse> {
    const endpoint = `/finance/segments/${segmentId}/reporting-options`;
    const query = { search, take };
    return signal
      ? apiService.getWithSignal<ReportingSegmentOptionsResponse>(
          endpoint,
          query,
          signal
        )
      : apiService.get<ReportingSegmentOptionsResponse>(endpoint, query);
  }

  async getSegmentStructureById(id: string): Promise<SegmentStructure> {
    return apiService.get<SegmentStructure>(`/finance/segments/${id}`);
  }

  async createSegmentStructure(
    dto: Partial<SegmentStructure>
  ): Promise<SegmentStructure> {
    return apiService.post<SegmentStructure>('/finance/segments', dto);
  }

  async updateSegmentStructure(
    id: string,
    dto: Partial<SegmentStructure>
  ): Promise<SegmentStructure> {
    return apiService.put<SegmentStructure>(`/finance/segments/${id}`, dto);
  }

  async activateSegmentStructure(
    id: string,
    rowVersion: string,
    options?: {
      reason?: string;
      confirmExistingAccountBackfill?: boolean;
      defaultSegmentValue?: string;
      defaultSegmentLookupValueId?: string;
    }
  ): Promise<SegmentStructure> {
    return apiService.post<SegmentStructure>(
      `/finance/segments/${id}/activate`,
      { rowVersion, ...options }
    );
  }

  async freezeSegmentStructure(
    id: string,
    rowVersion: string,
    reason?: string
  ): Promise<SegmentStructure> {
    return apiService.post<SegmentStructure>(`/finance/segments/${id}/freeze`, {
      rowVersion,
      reason,
    });
  }

  async deleteSegmentStructure(id: string, rowVersion: string): Promise<void> {
    return apiService.delete(`/finance/segments/${id}`, { rowVersion });
  }

  // ===== SEGMENT LOOKUP VALUES =====

  async getSegmentLookupValues(
    segmentId: string
  ): Promise<SegmentLookupValue[]> {
    return apiService.get<SegmentLookupValue[]>(
      `/finance/segments/${segmentId}/values`
    );
  }

  async createSegmentLookupValue(
    segmentId: string,
    dto: Partial<SegmentLookupValue>
  ): Promise<SegmentLookupValue> {
    return apiService.post<SegmentLookupValue>(
      `/finance/segments/${segmentId}/values`,
      dto
    );
  }

  async updateSegmentLookupValue(
    segmentId: string,
    valueId: string,
    dto: Partial<SegmentLookupValue>
  ): Promise<SegmentLookupValue> {
    return apiService.put<SegmentLookupValue>(
      `/finance/segments/${segmentId}/values/${valueId}`,
      dto
    );
  }

  async deleteSegmentLookupValue(
    segmentId: string,
    valueId: string
  ): Promise<void> {
    return apiService.delete(
      `/finance/segments/${segmentId}/values/${valueId}`
    );
  }

  async reorderSegmentStructures(
    reorderList: {
      segmentId: string;
      newPosition: number;
      rowVersion: string;
    }[]
  ): Promise<void> {
    return apiService.post('/finance/segments/reorder', reorderList);
  }

  // ===== ACCOUNT CURRENCY LINKS =====

  async getAccountCurrencyLinks(
    accountId: string
  ): Promise<AccountCurrencyLink[]> {
    return apiService.get<AccountCurrencyLink[]>(
      `/finance/accounts/${accountId}/currencies`
    );
  }

  async addAccountCurrencyLink(
    accountId: string,
    dto: AddCurrencyLinkDto
  ): Promise<AccountCurrencyLink> {
    const currencyCode = (dto.currencyCode || dto.linkedCurrencyCode || '')
      .trim()
      .toUpperCase();
    return apiService.post<AccountCurrencyLink>(
      `/finance/accounts/${accountId}/currencies`,
      {
        ...dto,
        accountId,
        currencyCode,
        linkedCurrencyCode: currencyCode,
      }
    );
  }

  async updateAccountCurrencyLinkRatePolicy(
    accountId: string,
    currencyCode: string,
    dto: UpdateCurrencyLinkRatePolicyDto
  ): Promise<AccountCurrencyLink> {
    return apiService.put<AccountCurrencyLink>(
      `/finance/accounts/${accountId}/currencies/${currencyCode}/rate-policy`,
      dto
    );
  }

  async getAccountBookCurrencyPolicies(
    accountId: string
  ): Promise<AccountBookCurrencyPolicy[]> {
    return apiService.get<AccountBookCurrencyPolicy[]>(
      `/finance/accounts/${accountId}/revaluation-policies`
    );
  }

  async saveAccountBookCurrencyPolicy(
    accountId: string,
    accountCurrencyLinkId: string,
    accountingBookId: string,
    dto: SaveAccountBookCurrencyPolicyDto
  ): Promise<AccountBookCurrencyPolicy> {
    return apiService.put<AccountBookCurrencyPolicy>(
      `/finance/accounts/${accountId}/revaluation-policies/currency-links/${accountCurrencyLinkId}/books/${accountingBookId}`,
      dto
    );
  }

  async decideAccountBookCurrencyPolicy(
    accountId: string,
    policyId: string,
    action: 'approve' | 'reject',
    dto: DecideAccountBookCurrencyPolicyDto
  ): Promise<AccountBookCurrencyPolicy> {
    return apiService.post<AccountBookCurrencyPolicy>(
      `/finance/accounts/${accountId}/revaluation-policies/${policyId}/${action}`,
      dto
    );
  }

  async removeAccountCurrencyLink(
    accountId: string,
    currencyCode: string
  ): Promise<void> {
    return apiService.delete(
      `/finance/accounts/${accountId}/currencies/${currencyCode}`
    );
  }
}

// Export singleton instance
export const financeDataService = new FinanceDataService();
