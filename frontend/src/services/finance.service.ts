/**
 * Finance Module API Service
 * 
 * Wraps all Finance module API endpoints for the Rhema ERP frontend.
 * 
 * PHASE STATUS:
 * - Phase 1: General Ledger & Core Accounting ✅ (Current)
 * - Phase 2: Cash Management & Payables (Planned)
 * - Phase 3: Receivables & Tax (Planned)
 * - Phase 4: Inventory, Budget & Fixed Assets (Planned)
 * - Phase 5: Integration & Testing (Planned)
 * 
 * @module services/finance.service
 * @version 1.0.0 - Phase 1
 */

import { apiService } from './api.service';
import type {
  // Core Types
  Currency,
  ExchangeRate,
  FiscalYear,
  FiscalPeriod,
  Account,
  JournalEntry,
  JournalEntryLine,
  SegmentStructure,
  SegmentLookupValue,
  AccountCurrencyLink,
  // Enums
  ExchangeRateType,
  ExchangeRateQuoteSide,
  AccountType,
  AccountStatus,
  JournalType,
  PostingStatus,
  PeriodStatus,
  // Response Types
  PaginatedResponse,
  TrendAnalysisDto,
  CurrencyRevaluationPreviewDto,
  CurrencyRevaluationPostingResultDto,
  FxRevaluationBatchSummaryDto,
} from '@/types/finance';

/**
 * Returns the exchange-rate snapshot exactly as supplied by the Finance API.
 * The API's `rate` is the value persisted and later validated by the posting engine;
 * callers must not infer direction from its magnitude or substitute `inverseRate`.
 */
export function resolvePostingExchangeRate(rate: Pick<ExchangeRate, 'rate' | 'currentExchangeRate'>): number {
  const resolvedRate = Number(rate.rate ?? rate.currentExchangeRate);
  if (!Number.isFinite(resolvedRate) || resolvedRate <= 0) {
    throw new Error('Finance API returned an invalid exchange-rate snapshot.');
  }

  return resolvedRate;
}

// ============================================
// REQUEST/RESPONSE INTERFACES
// ============================================

// --- Settings ---
export interface FinanceSettings {
  coaType: 'Standard' | 'Segmented';
  coaConfigurationLocked: boolean;
  baseCurrency: string;
  whtStatutoryYearStartMonth?: number;
  whtStatutoryYearStartDay?: number;
  retainedEarningsAccountId?: string;
  unrealizedGainLossAccountId?: string;
  unrealizedFxGainAccountId?: string;
  unrealizedFxLossAccountId?: string;
  realizedGainLossAccountId?: string;
  realizedFxGainAccountId?: string;
  realizedFxLossAccountId?: string;
  suspenseAccountId?: string;
  discountAllowedAccountId?: string;
  discountReceivedAccountId?: string;
  directionalExchangeRatePolicyEnabled?: boolean;
  defaultTransactionQuoteSide?: ExchangeRateQuoteSide;
  arInvoiceQuoteSide?: ExchangeRateQuoteSide;
  arSettlementQuoteSide?: ExchangeRateQuoteSide;
  apInvoiceQuoteSide?: ExchangeRateQuoteSide;
  apSettlementQuoteSide?: ExchangeRateQuoteSide;
  closingQuoteSide?: ExchangeRateQuoteSide;
  requireExchangeRateOverrideApproval?: boolean;
}

// --- Currency ---
export interface CreateCurrencyDto {
  currencyCode: string;
  numericCode: string;
  currencyName: string;
  currencySymbol?: string;
  decimalPlaces: number;
  roundingMethod: string;
  roundingPrecision: number;
  symbolPosition: string;
  decimalSeparator: string;
  thousandsSeparator?: string;
  countryCode?: string;
  countryName?: string;
  isActive: boolean;
  isBaseCurrency: boolean;
  createInitialExchangeRate?: boolean;
  initialExchangeRate?: number;
  initialExchangeRateDate?: string;
  initialExchangeRateType?: string;
  initialExchangeRateSource?: string;
  initialExchangeRateSourceReference?: string;
}

export interface UpdateCurrencyDto extends CreateCurrencyDto {}

// --- Exchange Rate ---
export interface CreateExchangeRateDto {
  baseCurrencyCode: string;
  targetCurrencyCode: string;
  rate: number;
  effectiveDate: string;
  expiryDate?: string;
  rateType: ExchangeRateType;
  quoteSide?: ExchangeRateQuoteSide;
  rateSource: string;
  sourceName?: string;
  sourceReference?: string;
  isActive?: boolean;
  approvalStatus?: string;
  comments?: string;
}

export interface ExchangeRateFilters {
  currencyCode?: string;
  rateType?: ExchangeRateType;
  quoteSide?: ExchangeRateQuoteSide;
  from?: string;
  to?: string;
  isActive?: boolean;
}

// --- Fiscal Year ---
export interface CreateFiscalYearDto {
  fiscalYearName: string;
  fiscalYearCode: string;
  year: number;
  startDate: string;
  endDate: string;
  fiscalYearType: string;
  numberOfPeriods: number;
  baseCurrency: string;
}

// --- Fiscal Period ---
export interface PeriodCloseRequestDto {
  fiscalPeriodId: string;
  closingNotes?: string;
  reviewerDeclaration: string;
}

export interface PeriodReopenRequestDto {
  fiscalPeriodId: string;
  reason: string;
  affectedPeriodAssessment: string;
}

export interface PeriodLockRequestDto {
  lockReason: string;
}

export interface PeriodCloseValidationDto {
  canClose: boolean;
  validationResults: {
    trialBalanceValidated: boolean;
    bankReconciliationComplete: boolean;
    currencyRevaluationComplete: boolean;
    depreciationComplete: boolean;
    accrualsComplete: boolean;
  };
  blockers: string[];
  warnings: string[];
}

// --- Account ---
export interface AccountFilters {
  accountType?: AccountType;
  status?: AccountStatus;
  classification?: 'IFRS' | 'Base' | 'Local';
  isMultiCurrency?: boolean;
  parentAccountId?: string;
  search?: string;
}

export interface CreateAccountDto {
  accountCode: string;
  accountNumber: string;
  accountName: string;
  accountType: AccountType;
  accountCategory?: string;
  accountSubCategory?: string;
  description?: string;
  parentAccountId?: string;
  isSegmented: boolean;
  segmentValues?: SegmentValueInput[];
  currencyCode: string;
  isMultiCurrency: boolean;
  isIFRSClassified: boolean;
  isBaseClassified: boolean;
  isLocalClassified: boolean;
  ifrsLineItem?: string;
  baseLineItem?: string;
  localLineItem?: string;
  allowDirectPosting: boolean;
  isControlAccount: boolean;
  budgetTrackingEnabled: boolean;
  status: AccountStatus;
}

export interface SegmentValueInput {
  segmentPosition: number;
  segmentValue: string;
}

export interface UpdateAccountDto extends Partial<CreateAccountDto> {
  id: string;
}

// --- Account Currency Link ---
export interface AddCurrencyLinkDto {
  accountId: string;
  linkedCurrencyCode: string;
  revaluationRequired: boolean;
  revaluationFrequency: string;
  transactionRateType: string;
  revaluationRateType: string;
  notes?: string;
}

export interface CurrencyLinkRemovalResultDto {
  success: boolean;
  wasDeleted: boolean;
  wasInactivated: boolean;
  message: string;
  transactionCount?: number;
}

// --- Segment ---
export interface CreateSegmentDto {
  segmentName: string;
  segmentCode: string;
  segmentPosition: number;
  segmentLength: number;
  dataType: string;
  separatorCharacter?: string;
  lookupTableRequired: boolean;
  isNaturalAccount: boolean;
  description?: string;
}

export interface UpdateSegmentDto extends CreateSegmentDto {
  id: string;
  rowVersion: string;
}

export interface CreateSegmentLookupValueDto {
  segmentStructureId: string;
  segmentValue: string;
  description: string;
  effectiveDate: string;
  expirationDate?: string;
  isActive: boolean;
  displayOrder: number;
}

export interface SegmentValidationResult {
  isValid: boolean;
  accountNumber: string;
  segmentValues: {
    segmentPosition: number;
    segmentCode: string;
    segmentName: string;
    value: string;
    description?: string;
    isValid: boolean;
  }[];
  validationErrors: string[];
}

// --- Journal Entry ---
export interface JournalEntryFilters {
  periodId?: string;
  status?: PostingStatus;
  journalType?: JournalType;
  from?: string;
  to?: string;
  search?: string;
}

export interface CreateJournalEntryDto {
  journalType: JournalType;
  entryDate: string;
  description: string;
  referenceNumber?: string;
  bookClassification: string;
  fiscalPeriodId?: string;
  notes?: string;
  transactions: CreateJournalEntryLineDto[];
}

export interface CreateJournalEntryLineDto {
  accountId: string;
  description?: string;
  debitAmount: number;
  creditAmount: number;
  transactionCurrency?: string;
  foreignCurrencyAmount?: number;
  exchangeRate?: number;
}

// --- Reports ---
export interface TrialBalanceRequestDto {
  periodId?: string;
  asOfDate?: string;
  classification?: string;
  includeZeroBalances?: boolean;
  accountTypes?: AccountType[];
  segmentFilters?: Record<string, string>;
}

export interface TrialBalanceReportDto {
  reportDate: string;
  periodName: string;
  classification: string;
  accounts: TrialBalanceLineDto[];
  totalDebits: number;
  totalCredits: number;
  isBalanced: boolean;
}

export interface TrialBalanceLineDto {
  accountId: string;
  accountCode: string;
  accountNumber: string;
  accountName: string;
  accountType: string;
  debitBalance: number;
  creditBalance: number;
  netBalance: number;
  segmentValues?: Record<string, string>;
}

export interface IncomeStatementRequestDto {
  periodId?: string;
  startDate?: string;
  endDate?: string;
  classification?: string;
  comparePeriodId?: string;
  includeBudget?: boolean;
}

export interface BalanceSheetRequestDto {
  asOfDate: string;
  classification?: string;
  compareAsOfDate?: string;
}

export interface CashFlowStatementRequestDto {
  periodId?: string;
  startDate?: string;
  endDate?: string;
  classification?: string;
}

export interface MultiCurrencyDetailRequestDto {
  accountId?: string;
  currencyCode?: string;
  asOfDate?: string;
}

// --- Revaluation ---
export interface RevaluationRequestDto {
  revaluationDate: string;
  revaluationType: string;
  accountingBookCode: string;
  currencyCode?: string;
  unrealizedGainLossAccountId: string;
  previewOnly: boolean;
  notes?: string;
  expectedPreviewFingerprint?: string;
}

export interface RevaluationResultDto {
  journalEntry: JournalEntry;
  revaluationDetails: RevaluationDetailDto[];
  totalAdjustment: number;
  unrealizedGain: number;
  unrealizedLoss: number;
}

export interface RevaluationDetailDto {
  accountId: string;
  accountCode: string;
  accountName: string;
  currencyCode: string;
  foreignBalance: number;
  previousBaseBalance: number;
  previousRate: number;
  newRate: number;
  newBaseBalance: number;
  adjustment: number;
  adjustmentType: 'Gain' | 'Loss';
}

// --- Year End Close ---
export interface YearEndCloseRequestDto {
  fiscalYearId: string;
  retainedEarningsAccountId: string;
  closingNotes?: string;
}

// ============================================
// FINANCE SERVICE CLASS
// ============================================

class FinanceService {
  private readonly baseUrl = '/finance';

  // ==========================================
  // SETTINGS
  // ==========================================

  /**
   * Get finance module settings for current tenant
   */
  async getSettings(): Promise<FinanceSettings> {
    return apiService.get<FinanceSettings>(`${this.baseUrl}/settings`);
  }

  /**
   * Update finance module settings
   */
  async updateSettings(settings: Partial<FinanceSettings>): Promise<FinanceSettings> {
    return apiService.put<FinanceSettings>(`${this.baseUrl}/settings`, settings);
  }

  /**
   * Check if COA type can be changed (no accounts exist)
   */
  async canChangeCOAType(): Promise<boolean> {
    const accounts = await this.getAccounts({ page: 1, pageSize: 1 });
    return accounts.total === 0;
  }

  // ==========================================
  // CURRENCIES
  // ==========================================

  /**
   * Get all currencies
   */
  async getCurrencies(params?: { isActive?: boolean }): Promise<Currency[]> {
    const query = params?.isActive !== undefined ? `?isActive=${params.isActive}` : '';
    return apiService.get<Currency[]>(`${this.baseUrl}/currencies${query}`);
  }

  /**
   * Get currency by code
   */
  async getCurrencyByCode(code: string): Promise<Currency> {
    return apiService.get<Currency>(`${this.baseUrl}/currencies/${code}`);
  }

  /**
   * Create new currency
   */
  async createCurrency(data: CreateCurrencyDto): Promise<Currency> {
    return apiService.post<Currency>(`${this.baseUrl}/currencies`, data);
  }

  /**
   * Update currency
   */
  async updateCurrency(code: string, data: UpdateCurrencyDto): Promise<Currency> {
    return apiService.put<Currency>(`${this.baseUrl}/currencies/${code}`, data);
  }

  /**
   * Delete currency (only if no transaction history)
   */
  async deleteCurrency(code: string): Promise<void> {
    return apiService.delete(`${this.baseUrl}/currencies/${code}`);
  }

  /**
   * Activate currency
   */
  async activateCurrency(code: string): Promise<Currency> {
    return apiService.patch<Currency>(`${this.baseUrl}/currencies/${code}/activate`);
  }

  /**
   * Deactivate currency
   */
  async deactivateCurrency(code: string): Promise<Currency> {
    return apiService.patch<Currency>(`${this.baseUrl}/currencies/${code}/deactivate`);
  }

  // ==========================================
  // EXCHANGE RATES
  // ==========================================

  /**
   * Get exchange rates with optional filters
   */
  async getExchangeRates(filters?: ExchangeRateFilters): Promise<ExchangeRate[]> {
    const params = new URLSearchParams();
    if (filters?.currencyCode) params.append('currencyCode', filters.currencyCode);
    if (filters?.rateType) params.append('rateType', filters.rateType);
    if (filters?.quoteSide) params.append('quoteSide', filters.quoteSide);
    if (filters?.from) params.append('from', filters.from);
    if (filters?.to) params.append('to', filters.to);
    if (filters?.isActive !== undefined) params.append('isActive', String(filters.isActive));
    
    const query = params.toString() ? `?${params.toString()}` : '';
    return apiService.get<ExchangeRate[]>(`${this.baseUrl}/exchange-rates${query}`);
  }

  /**
   * Get current exchange rate for a currency
   */
  async getCurrentExchangeRate(
    currencyCode: string,
    options?: {
      baseCurrencyCode?: string;
      effectiveDate?: string;
      rateType?: ExchangeRateType;
      quoteSide?: ExchangeRateQuoteSide;
    },
  ): Promise<ExchangeRate> {
    const params = new URLSearchParams();
    if (options?.baseCurrencyCode) params.set('baseCurrencyCode', options.baseCurrencyCode);
    if (options?.effectiveDate) params.set('effectiveDate', options.effectiveDate);
    if (options?.rateType) params.set('rateType', options.rateType);
    if (options?.quoteSide) params.set('quoteSide', options.quoteSide);
    const query = params.size > 0 ? `?${params.toString()}` : '';
    return apiService.get<ExchangeRate>(`${this.baseUrl}/exchange-rates/current/${currencyCode}${query}`);
  }

  /**
   * Get exchange rate trends
   */
  async getExchangeRateTrends(currencyCode: string, months: number = 6): Promise<TrendAnalysisDto[]> {
    return apiService.get<TrendAnalysisDto[]>(`${this.baseUrl}/exchange-rates/trends/${currencyCode}?months=${months}`);
  }

  /**
   * Create new exchange rate
   */
  async createExchangeRate(data: CreateExchangeRateDto): Promise<ExchangeRate> {
    return apiService.post<ExchangeRate>(`${this.baseUrl}/exchange-rates`, data);
  }

  async updateExchangeRate(id: string, data: CreateExchangeRateDto): Promise<ExchangeRate> {
    return apiService.put<ExchangeRate>(`${this.baseUrl}/exchange-rates/${id}`, data);
  }

  /**
   * Bulk upload exchange rates.
   *
   * The Finance API accepts the parsed DTO list as JSON. File parsing stays in the
   * upload UI so CSV/XLSX validation can report row-level guidance before posting.
   */
  async bulkUploadExchangeRates(rates: CreateExchangeRateDto[]): Promise<ExchangeRate[]> {
    return apiService.post<ExchangeRate[]>(`${this.baseUrl}/exchange-rates/bulk`, rates);
  }

  /**
   * Delete exchange rate (only if not used in transactions)
   */
  async deleteExchangeRate(id: string): Promise<void> {
    return apiService.delete(`${this.baseUrl}/exchange-rates/${id}`);
  }

  // ==========================================
  // FISCAL YEARS
  // ==========================================

  /**
   * Get all fiscal years
   */
  async getFiscalYears(): Promise<FiscalYear[]> {
    return apiService.get<FiscalYear[]>(`${this.baseUrl}/fiscal-years`);
  }

  /**
   * Get fiscal year by ID with periods
   */
  async getFiscalYear(id: string): Promise<FiscalYear> {
    return apiService.get<FiscalYear>(`${this.baseUrl}/fiscal-years/${id}`);
  }

  /**
   * Create fiscal year (auto-generates periods)
   */
  async createFiscalYear(data: CreateFiscalYearDto): Promise<FiscalYear> {
    return apiService.post<FiscalYear>(`${this.baseUrl}/fiscal-years`, data);
  }

  /**
   * Close fiscal year
   */
  async closeFiscalYear(id: string, data: YearEndCloseRequestDto): Promise<FiscalYear> {
    return apiService.post<FiscalYear>(`${this.baseUrl}/fiscal-years/${id}/close`, data);
  }

  // ==========================================
  // FISCAL PERIODS
  // ==========================================

  /**
   * Get fiscal periods (optionally filtered by year)
   */
  async getFiscalPeriods(fiscalYearId?: string): Promise<FiscalPeriod[]> {
    const query = fiscalYearId ? `?yearId=${fiscalYearId}` : '';
    return apiService.get<FiscalPeriod[]>(`${this.baseUrl}/fiscal-periods${query}`);
  }

  /**
   * Get fiscal period by ID
   */
  async getFiscalPeriod(id: string): Promise<FiscalPeriod> {
    return apiService.get<FiscalPeriod>(`${this.baseUrl}/fiscal-periods/${id}`);
  }

  /**
   * Get open periods for transaction entry
   */
  async getOpenPeriods(): Promise<FiscalPeriod[]> {
    return apiService.get<FiscalPeriod[]>(`${this.baseUrl}/fiscal-periods?status=Open`);
  }

  /**
   * Open a previously unopened Future period.
   */
  async openPeriod(id: string, reason: string): Promise<FiscalPeriod> {
    return apiService.post<FiscalPeriod>(`${this.baseUrl}/periods/${id}/open`, { reason });
  }

  /**
   * Validate period can be closed
   */
  async validatePeriodClose(id: string): Promise<PeriodCloseValidationDto> {
    return apiService.post<PeriodCloseValidationDto>(`${this.baseUrl}/periods/${id}/validate-close`);
  }

  /**
   * Close fiscal period
   */
  async closePeriod(id: string, data: PeriodCloseRequestDto): Promise<FiscalPeriod> {
    return apiService.post<FiscalPeriod>(`${this.baseUrl}/periods/${id}/close`, data);
  }

  /**
   * Request controlled reopening of a closed period. The period remains closed until reviewed.
   */
  async reopenPeriod(id: string, data: PeriodReopenRequestDto): Promise<unknown> {
    return apiService.post(`${this.baseUrl}/periods/${id}/reopen-requests`, data);
  }

  /**
   * Lock period (highest level of protection)
   */
  async lockPeriod(id: string, data: PeriodLockRequestDto): Promise<void> {
    return apiService.post(`${this.baseUrl}/periods/${id}/lock`, data);
  }

  /**
   * Unlock period
   */
  async unlockPeriod(id: string, reason: string): Promise<void> {
    return apiService.post(`${this.baseUrl}/periods/${id}/unlock`, { reason });
  }

  // ==========================================
  // ACCOUNTS (Chart of Accounts)
  // ==========================================

  /**
   * Get accounts with filters and pagination
   */
  async getAccounts(params: {
    page?: number;
    pageSize?: number;
    filters?: AccountFilters;
  } = {}): Promise<PaginatedResponse<Account>> {
    const { page = 1, pageSize = 50, filters } = params;
    const queryParams = new URLSearchParams();
    queryParams.append('page', String(page));
    queryParams.append('pageSize', String(pageSize));
    
    if (filters?.accountType) queryParams.append('accountType', filters.accountType);
    if (filters?.status) queryParams.append('status', filters.status);
    if (filters?.classification) queryParams.append('classification', filters.classification);
    if (filters?.isMultiCurrency !== undefined) queryParams.append('isMultiCurrency', String(filters.isMultiCurrency));
    if (filters?.parentAccountId) queryParams.append('parentAccountId', filters.parentAccountId);
    if (filters?.search) queryParams.append('search', filters.search);

    return apiService.get<PaginatedResponse<Account>>(`${this.baseUrl}/accounts?${queryParams.toString()}`);
  }

  /**
   * Get all accounts (for dropdowns) - no pagination
   */
  async getAllAccounts(filters?: AccountFilters): Promise<Account[]> {
    const result = await this.getAccounts({ pageSize: 10000, filters });
    if (Array.isArray(result)) {
      return result;
    }

    return result.items || (result as any).data || [];
  }

  /**
   * Get account by ID
   */
  async getAccount(id: string): Promise<Account> {
    return apiService.get<Account>(`${this.baseUrl}/accounts/${id}`);
  }

  /**
   * Create account
   */
  async createAccount(data: CreateAccountDto): Promise<Account> {
    return apiService.post<Account>(`${this.baseUrl}/accounts`, data);
  }

  /**
   * Update account
   */
  async updateAccount(id: string, data: UpdateAccountDto): Promise<Account> {
    return apiService.put<Account>(`${this.baseUrl}/accounts/${id}`, data);
  }

  /**
   * Delete account (only if no transactions)
   */
  async deleteAccount(id: string): Promise<void> {
    return apiService.delete(`${this.baseUrl}/accounts/${id}`);
  }

  // --- Account Currency Links ---

  /**
   * Get currency links for an account
   */
  async getAccountCurrencyLinks(accountId: string, includeInactive = false): Promise<AccountCurrencyLink[]> {
    return apiService.get<AccountCurrencyLink[]>(
      `${this.baseUrl}/accounts/${accountId}/currencies?includeInactive=${includeInactive}`
    );
  }

  /**
   * Add currency link to account
   */
  async addCurrencyLink(data: AddCurrencyLinkDto): Promise<AccountCurrencyLink> {
    return apiService.post<AccountCurrencyLink>(
      `${this.baseUrl}/accounts/${data.accountId}/currencies`,
      data
    );
  }

  /**
   * Remove currency link (will fail if has transactions, use inactivate instead)
   */
  async removeCurrencyLink(
    accountId: string,
    currencyCode: string,
    forceRemove = false
  ): Promise<CurrencyLinkRemovalResultDto> {
    return apiService.delete(
      `${this.baseUrl}/accounts/${accountId}/currencies/${currencyCode}?forceRemove=${forceRemove}`
    );
  }

  /**
   * Inactivate currency link (safe way to "remove" with history)
   */
  async inactivateCurrencyLink(accountId: string, currencyCode: string): Promise<AccountCurrencyLink> {
    return apiService.patch<AccountCurrencyLink>(
      `${this.baseUrl}/accounts/${accountId}/currencies/${currencyCode}/inactivate`
    );
  }

  // ==========================================
  // SEGMENT CONFIGURATION
  // ==========================================

  /**
   * Get all segment structures
   */
  async getSegments(): Promise<SegmentStructure[]> {
    return apiService.get<SegmentStructure[]>(`${this.baseUrl}/segments`);
  }

  /**
   * Get segment by ID
   */
  async getSegment(id: string): Promise<SegmentStructure> {
    return apiService.get<SegmentStructure>(`${this.baseUrl}/segments/${id}`);
  }

  /**
   * Create segment structure
   */
  async createSegment(data: CreateSegmentDto): Promise<SegmentStructure> {
    return apiService.post<SegmentStructure>(`${this.baseUrl}/segments`, data);
  }

  /**
   * Update segment structure
   */
  async updateSegment(id: string, data: UpdateSegmentDto): Promise<SegmentStructure> {
    return apiService.put<SegmentStructure>(`${this.baseUrl}/segments/${id}`, data);
  }

  /**
   * Delete segment (only if unused)
   */
  async deleteSegment(id: string): Promise<void> {
    return apiService.delete(`${this.baseUrl}/segments/${id}`);
  }

  /**
   * Validate account number against segment structure
   */
  async validateAccountNumber(accountNumber: string): Promise<SegmentValidationResult> {
    return apiService.post<SegmentValidationResult>(`${this.baseUrl}/segments/validate`, accountNumber);
  }

  /**
   * Construct account number from segment values
   */
  async constructAccountNumber(segmentValues: Record<number, string>): Promise<{ accountNumber: string }> {
    return apiService.post(`${this.baseUrl}/segments/construct`, segmentValues);
  }

  /**
   * Parse account number into segment values
   */
  async parseAccountNumber(accountNumber: string): Promise<SegmentValidationResult['segmentValues']> {
    return apiService.get(`${this.baseUrl}/segments/parse/${encodeURIComponent(accountNumber)}`);
  }

  // --- Segment Lookup Values ---

  /**
   * Get lookup values for a segment
   */
  async getSegmentLookupValues(segmentId: string): Promise<SegmentLookupValue[]> {
    return apiService.get<SegmentLookupValue[]>(`${this.baseUrl}/segments/${segmentId}/values`);
  }

  /**
   * Create segment lookup value
   */
  async createSegmentLookupValue(segmentId: string, data: CreateSegmentLookupValueDto): Promise<SegmentLookupValue> {
    return apiService.post<SegmentLookupValue>(`${this.baseUrl}/segments/${segmentId}/values`, data);
  }

  /**
   * Update segment lookup value
   */
  async updateSegmentLookupValue(
    segmentId: string,
    valueId: string,
    data: Partial<CreateSegmentLookupValueDto>
  ): Promise<SegmentLookupValue> {
    return apiService.put<SegmentLookupValue>(`${this.baseUrl}/segments/${segmentId}/values/${valueId}`, data);
  }

  /**
   * Delete segment lookup value
   */
  async deleteSegmentLookupValue(segmentId: string, valueId: string): Promise<void> {
    return apiService.delete(`${this.baseUrl}/segments/${segmentId}/values/${valueId}`);
  }

  // ==========================================
  // JOURNAL ENTRIES
  // ==========================================

  /**
   * Get journal entries with filters
   */
  async getJournalEntries(params: {
    page?: number;
    pageSize?: number;
    filters?: JournalEntryFilters;
  } = {}): Promise<PaginatedResponse<JournalEntry>> {
    const { page = 1, pageSize = 20, filters } = params;
    const queryParams = new URLSearchParams();
    queryParams.append('page', String(page));
    queryParams.append('pageSize', String(pageSize));
    
    if (filters?.periodId) queryParams.append('periodId', filters.periodId);
    if (filters?.status) queryParams.append('status', filters.status);
    if (filters?.journalType) queryParams.append('journalType', filters.journalType);
    if (filters?.from) queryParams.append('from', filters.from);
    if (filters?.to) queryParams.append('to', filters.to);
    if (filters?.search) queryParams.append('search', filters.search);

    return apiService.get<PaginatedResponse<JournalEntry>>(`${this.baseUrl}/journal-entries?${queryParams.toString()}`);
  }

  /**
   * Get journal entry by ID with lines
   */
  async getJournalEntry(id: string): Promise<JournalEntry> {
    return apiService.get<JournalEntry>(`${this.baseUrl}/journal-entries/${id}`);
  }

  /**
   * Create and optionally post journal entry
   */
  async createJournalEntry(data: CreateJournalEntryDto): Promise<JournalEntry> {
    return apiService.post<JournalEntry>(`${this.baseUrl}/journal-entries`, data);
  }

  /**
   * Update draft journal entry
   */
  async updateJournalEntry(id: string, data: Partial<CreateJournalEntryDto>): Promise<JournalEntry> {
    return apiService.put<JournalEntry>(`${this.baseUrl}/journal-entries/${id}`, data);
  }

  /**
   * Delete draft journal entry
   */
  async deleteJournalEntry(id: string): Promise<void> {
    return apiService.delete(`${this.baseUrl}/journal-entries/${id}`);
  }

  /**
   * Post draft journal entry to GL
   */
  async postJournalEntry(id: string): Promise<JournalEntry> {
    return apiService.post<JournalEntry>(`${this.baseUrl}/journal-entries/${id}/post`);
  }

  /**
   * Reverse posted journal entry
   */
  async reverseJournalEntry(id: string, reason: string, reversalDate?: string): Promise<JournalEntry> {
    return apiService.post<JournalEntry>(`${this.baseUrl}/journal-entries/${id}/reverse`, { reason, reversalDate });
  }

  // ==========================================
  // FINANCIAL REPORTS
  // ==========================================

  /**
   * Generate Trial Balance
   */
  async getTrialBalance(request: TrialBalanceRequestDto): Promise<TrialBalanceReportDto> {
    const params = new URLSearchParams();
    if (request.periodId) params.append('periodId', request.periodId);
    if (request.asOfDate) params.append('asOfDate', request.asOfDate);
    if (request.classification) params.append('classification', request.classification);
    if (request.includeZeroBalances !== undefined) params.append('includeZeroBalances', String(request.includeZeroBalances));
    if (request.accountTypes) request.accountTypes.forEach(t => params.append('accountTypes', t));
    
    return apiService.get<TrialBalanceReportDto>(`${this.baseUrl}/statements/trial-balance?${params.toString()}`);
  }

  /**
   * Generate Income Statement
   */
  async getIncomeStatement(request: IncomeStatementRequestDto): Promise<any> {
    const params = new URLSearchParams();
    if (request.periodId) params.append('periodId', request.periodId);
    if (request.startDate) params.append('startDate', request.startDate);
    if (request.endDate) params.append('endDate', request.endDate);
    if (request.classification) params.append('classification', request.classification);
    if (request.comparePeriodId) params.append('comparePeriodId', request.comparePeriodId);
    if (request.includeBudget !== undefined) params.append('includeBudget', String(request.includeBudget));
    
    return apiService.get(`${this.baseUrl}/statements/income-statement?${params.toString()}`);
  }

  /**
   * Generate Balance Sheet
   */
  async getBalanceSheet(request: BalanceSheetRequestDto): Promise<any> {
    const params = new URLSearchParams();
    params.append('asOfDate', request.asOfDate);
    if (request.classification) params.append('classification', request.classification);
    if (request.compareAsOfDate) params.append('compareAsOfDate', request.compareAsOfDate);
    
    return apiService.get(`${this.baseUrl}/statements/balance-sheet?${params.toString()}`);
  }

  /**
   * Generate Cash Flow Statement
   */
  async getCashFlowStatement(request: CashFlowStatementRequestDto): Promise<any> {
    const params = new URLSearchParams();
    if (request.periodId) params.append('periodId', request.periodId);
    if (request.startDate) params.append('startDate', request.startDate);
    if (request.endDate) params.append('endDate', request.endDate);
    if (request.classification) params.append('classification', request.classification);
    
    return apiService.get(`${this.baseUrl}/statements/cash-flow?${params.toString()}`);
  }

  /**
   * Generate Multi-Currency Detail Report
   */
  async getMultiCurrencyDetailReport(request: MultiCurrencyDetailRequestDto): Promise<any> {
    const params = new URLSearchParams();
    if (request.accountId) params.append('accountId', request.accountId);
    if (request.currencyCode) params.append('currencyCode', request.currencyCode);
    if (request.asOfDate) params.append('asOfDate', request.asOfDate);
    
    return apiService.get(`${this.baseUrl}/statements/multi-currency-detail?${params.toString()}`);
  }

  // ==========================================
  // CURRENCY REVALUATION
  // ==========================================

  /**
   * Run currency revaluation
   */
  async runRevaluation(request: RevaluationRequestDto): Promise<CurrencyRevaluationPostingResultDto> {
    return apiService.post<CurrencyRevaluationPostingResultDto>(`${this.baseUrl}/revaluation`, request);
  }

  /**
   * Preview revaluation (no posting)
   */
  async previewRevaluation(request: Omit<RevaluationRequestDto, 'previewOnly'>): Promise<CurrencyRevaluationPreviewDto> {
    return apiService.post<CurrencyRevaluationPreviewDto>(`${this.baseUrl}/revaluation/preview`, {
      ...request,
      previewOnly: true,
    });
  }

  async getRevaluationHistory(startDate: string, endDate: string, currencyCode?: string): Promise<FxRevaluationBatchSummaryDto[]> {
    const params = new URLSearchParams({ startDate, endDate });
    if (currencyCode) params.set('currencyCode', currencyCode);
    return apiService.get<FxRevaluationBatchSummaryDto[]>(`${this.baseUrl}/revaluation/history?${params.toString()}`);
  }

  async reverseRevaluation(batchId: string, reversalDate: string, reason: string): Promise<{ id: string; status: string; reversalJournalEntryId?: string; reversedAt?: string }> {
    return apiService.post(`${this.baseUrl}/revaluation/${batchId}/reverse`, { reversalDate, reason });
  }

  // ==========================================
  // PHASE 2+ PLACEHOLDER METHODS
  // ==========================================

  // TODO: Phase 2 - Cash Management & Payables
  // - Bank Account Management
  // - Bank Reconciliation
  // - Treasury Operations
  // - Accounts Payable

  // TODO: Phase 3 - Receivables & Tax
  // - Customer Management
  // - Invoice Generation
  // - Payment Receipt
  // - Tax Management

  // TODO: Phase 4 - Inventory, Budget & Fixed Assets
  // - Inventory Management
  // - Budget Management
  // - Fixed Assets
}

// Export singleton instance
export const financeService = new FinanceService();

// Export class for testing
export { FinanceService };
