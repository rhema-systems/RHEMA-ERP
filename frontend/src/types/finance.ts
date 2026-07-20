// ============================================
// FINANCE MODULE TYPES
// ============================================

// ============================================
// CORE ENUMS
// ============================================

export type AccountType = 'Asset' | 'Liability' | 'Equity' | 'Revenue' | 'Expense';
export type AccountStatus = 'Active' | 'Inactive' | 'Closed';
export type JournalType = 'General' | 'Adjusting' | 'Reversing' | 'Recurring' | 'Opening Balance' | 'Closing' | 'Revaluation' | 'System Generated';
export type PostingStatus = 'Draft' | 'Pending Approval' | 'Approved' | 'Posted' | 'Rejected' | 'Reversed';
export type ExchangeRateType = 'Daily' | 'Average' | 'MonthEnd' | 'QuarterEnd' | 'YearEnd' | 'Budget' | 'Fixed' | 'Spot' | 'Official' | 'Market' | 'Custom';
export type PeriodStatus = 'Future' | 'Open' | 'Closed' | 'Locked';
export type RevaluationFrequency = 'None' | 'Monthly' | 'Quarterly' | 'Annually';
export type SubledgerModule = 'AR' | 'AP';
export type SubledgerAdjustmentType = 'Debit' | 'Credit';
export type SubledgerAdjustmentPurpose = 'StandardAdjustment' | 'OpeningBalance';
export type SubledgerAdjustmentStatus = 'Posted' | 'Reversed';

// ============================================
// CORE ENTITIES
// ============================================

export interface Currency {
    id: string;
    tenantId?: string;
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
    hasTransactionHistory?: boolean;
    createdAt: string;
    updatedAt: string;
}

export interface ExchangeRate {
    id: string;
    tenantId?: string;
    baseCurrencyCode: string;
    targetCurrencyCode: string;
    rate: number;
    currentExchangeRate?: number;
    effectiveDate: string;
    rateType: ExchangeRateType;
    rateSource: string;
    comments?: string;
    isActive: boolean;
    createdAt: string;
    updatedAt: string;
}

export interface FiscalYear {
    id: string;
    tenantId?: string;
    fiscalYearName: string;
    fiscalYearCode: string;
    year: number;
    startDate: string;
    endDate: string;
    fiscalYearType: string;
    numberOfPeriods: number;
    baseCurrency: string;
    status: string;
    isClosed: boolean;
    isLocked: boolean;
    periods?: FiscalPeriod[];
    createdAt: string;
    updatedAt: string;
}

export interface FiscalPeriod {
    id: string;
    tenantId?: string;
    fiscalYearId: string;
    periodNumber: number;
    periodCode: string;
    periodName: string;
    startDate: string;
    endDate: string;
    periodStatus: PeriodStatus;
    status?: PeriodStatus;
    isOpen?: boolean;
    isClosed: boolean;
    isLocked: boolean;
    isGlobalLockSuspended?: boolean;
    isPartiallyLocked?: boolean;
    closedDate?: string;
    closedByUserId?: string;
    closingNotes?: string;
    moduleLocks?: PeriodModuleLock[];
    createdAt: string;
    updatedAt: string;
}

export interface PeriodModuleLock {
    id: string;
    fiscalPeriodId: string;
    moduleDefinitionId: string;
    moduleCode: string;
    moduleName: string;
    isLocked: boolean;
    lockedDate?: string;
    lockedByUserName?: string;
    lockReason?: string;
    unlockedDate?: string;
    unlockedByUserName?: string;
    unlockReason?: string;
    reopenExpiresAtUtc?: string;
    autoRelockedDate?: string;
    isTemporaryReopening?: boolean;
}

export interface ModuleDefinition {
    id: string;
    moduleCode: string;
    moduleName: string;
    description?: string;
    isActive: boolean;
    sortOrder: number;
    checkboxLabel?: string;
}

export interface AccountingBook {
    id: string;
    tenantId: string;
    code: string;
    name: string;
    description?: string;
    purpose: string;
    isActive: boolean;
    isDefault: boolean;
    allowsPosting: boolean;
    isSystemDefined: boolean;
    sortOrder: number;
}

export interface AccountAccountingBook {
    id: string;
    accountId: string;
    accountingBookId: string;
    accountingBookCode: string;
    accountingBookName: string;
    isEnabled: boolean;
    financialStatementLineItem?: string;
}

export interface Account {
    id: string;
    tenantId: string;
    accountCode: string;
    accountNumber: string;
    accountName: string;
    accountType: AccountType;
    accountCategory?: string;
    accountSubCategory?: string;
    description?: string;
    parentAccountId?: string;
    isSegmented: boolean;
    segmentValues?: AccountSegmentValue[];
    currencyCode: string;
    isMultiCurrency: boolean;
    isIFRSClassified: boolean;
    isManagementClassified?: boolean;
    isBaseClassified?: boolean;
    isLocalClassified?: boolean;
    isBaseFrameworkClassified?: boolean;
    isLocalFrameworkClassified?: boolean;
    accountingBooks?: AccountAccountingBook[];
    ifrsLineItem?: string;
    baseLineItem?: string;
    localLineItem?: string;
    allowDirectPosting: boolean;
    isPostingAllowed?: boolean;
    isControlAccount: boolean;
    budgetTrackingEnabled: boolean;
    status: AccountStatus;
    currentBalance?: number;
    currencyLinks?: AccountCurrencyLink[];
    createdAt: string;
    updatedAt: string;
}

export interface AccountSegmentValue {
    id: string;
    accountId: string;
    segmentStructureId: string;
    segmentPosition: number;
    segmentValue: string;
    segmentDescription?: string;
}

export interface AccountCurrencyLink {
    id: string;
    accountId: string;
    linkedCurrencyCode: string;
    revaluationRequired: boolean;
    revaluationFrequency: RevaluationFrequency;
    transactionRateType: string;
    revaluationRateType: string;
    foreignCurrencyBalance: number;
    baseCurrencyBalance: number;
    currentExchangeRate?: number;
    lastRevaluationDate?: string;
    lastRevaluationAdjustment?: number;
    cumulativeRevaluationAdjustment: number;
    isActive: boolean;
    notes?: string;
    createdAt: string;
    updatedAt: string;
}

export interface SegmentStructure {
    id: string;
    segmentName: string;
    segmentCode: string;
    segmentPosition: number;
    segmentLength: number;
    dataType: string;
    separatorCharacter?: string;
    lookupTableRequired: boolean;
    isMandatory: boolean;
    isReportingDimension: boolean;
    isNaturalAccount: boolean;
    isActive: boolean;
    canBeModified?: boolean;
    description?: string;
    lookupValues?: SegmentLookupValue[];
    lookupValueCount?: number;
    lookupValuesCount?: number;
    createdAt: string;
    updatedAt: string;
}

export interface SegmentLookupValue {
    id: string;
    segmentStructureId: string;
    parentLookupValueId?: string;
    segmentValue: string;
    description: string;
    effectiveDate: string;
    expirationDate?: string;
    isActive: boolean;
    displayOrder: number;
    createdAt: string;
    updatedAt: string;
}

export interface ReportingSegmentOption {
    segmentValue: string;
    description: string;
    accountCombinationCount: number;
}

export interface ReportingSegmentOptionsResponse {
    items: ReportingSegmentOption[];
    hasMore: boolean;
}

/**
 * Maps to backend: JournalEntryDto (JournalEntryDtos.cs)
 * JSON serialization: camelCase from PascalCase C# properties
 */
export interface JournalEntry {
    id: string;
    tenantId?: string;
    /** Backend: JournalNumber */
    journalNumber: string;
    /** Alias kept for backward compat with UI components */
    journalEntryNumber: string;
    /** Backend: TransactionDate */
    transactionDate: string;
    /** Alias kept for backward compat */
    entryDate: string;
    description: string;
    reference?: string;
    /** Alias kept for backward compat */
    referenceNumber?: string;
    sourceModule?: string;
    totalDebit: number;
    totalCredit: number;
    /** Alias kept for backward compat */
    totalDebitAmount: number;
    /** Alias kept for backward compat */
    totalCreditAmount: number;
    /** Backend: Status (aliased as PostingStatus) */
    status: string;
    /** Alias kept for backward compat */
    postingStatus: PostingStatus;
    isReversed: boolean;
    reversalJournalId?: string;
    originalJournalId?: string;
    /** Alias kept for backward compat */
    reversalJournalEntryId?: string;
    /** Alias kept for backward compat */
    originalJournalEntryId?: string;
    postedDate?: string;
    /** Alias kept for backward compat */
    postingDate?: string;
    postedByUserId?: string;
    postedByUserName?: string;
    // Approval workflow
    requiresApproval: boolean;
    approvalStatus?: string;
    approvedByUserId?: string;
    approvedDate?: string;
    rejectionReason?: string;
    transactions: AccountTransaction[];
    attachments?: JournalEntryAttachment[];
    createdAt: string;
    createdById?: string;
    /** Alias kept for backward compat */
    createdBy: string;
    updatedAt: string;
    /** Alias kept for backward compat */
    updatedBy: string;
    // Frontend-only fields that may not come from backend
    journalType?: JournalType;
    isBalanced?: boolean;
    isMultiCurrency?: boolean;
    primaryCurrency?: string;
    bookClassification?: string;
    fiscalPeriodId?: string;
    isRevaluationEntry?: boolean;
    revaluationType?: string;
    reversalDate?: string;
    reversalReason?: string;
    reversalType?: string;
    notes?: string;
}

/**
 * Maps to backend: AccountTransactionDto (AccountTransactionDtos.cs)
 */
export interface AccountTransaction {
    id: string;
    accountId: string;
    accountName?: string;
    accountNumber?: string;
    /** Alias kept for backward compat */
    accountCode?: string;
    journalEntryId: string;
    amount: number;
    transactionType: 'Debit' | 'Credit';
    description?: string;
    transactionDate: string;
    reference: string;
    balanceAfter: number;
    // Computed helpers for UI compatibility
    /** debitAmount = amount when transactionType === 'Debit', else 0 */
    debitAmount?: number;
    /** creditAmount = amount when transactionType === 'Credit', else 0 */
    creditAmount?: number;
    lineNumber?: number;
}

/** @deprecated Use AccountTransaction instead */
export type JournalEntryLine = AccountTransaction;

export interface JournalEntryAttachment {
    id: string;
    journalEntryId: string;
    fileId: string;
    fileName: string;
    fileUrl: string;
    contentType: string;
    fileSize?: number;
    uploadedAt: string;
    uploadedBy: string;
}

export interface FinanceJournalAuditLog {
    id: string;
    action: string;
    resource: string;
    resourceId?: string;
    username: string;
    userId: string;
    timestamp: string;
    ipAddress?: string;
    userAgent?: string;
    oldValues?: unknown;
    newValues?: unknown;
}

export interface SubledgerAdjustmentJournal {
    id: string;
    module: SubledgerModule;
    adjustmentNumber: string;
    purpose: SubledgerAdjustmentPurpose;
    customerId?: string;
    customerName?: string;
    supplierId?: string;
    supplierName?: string;
    adjustmentDate: string;
    dueDate?: string;
    adjustmentType: SubledgerAdjustmentType;
    amount: number;
    signedSubledgerAmount: number;
    currencyCode: string;
    exchangeRate: number;
    baseCurrencyAmount: number;
    contraAccountId: string;
    contraAccountNumber?: string;
    contraAccountName?: string;
    journalEntryId?: string;
    journalNumber?: string;
    status: SubledgerAdjustmentStatus;
    reference?: string;
    reason: string;
    notes?: string;
    originalAdjustmentId?: string;
    reversalAdjustmentId?: string;
    reversalReason?: string;
    reversedAt?: string;
    createdAt: string;
    createdBy?: string;
}

export interface CreateSubledgerAdjustmentJournalDto {
    module: SubledgerModule;
    purpose?: SubledgerAdjustmentPurpose;
    customerId?: string;
    supplierId?: string;
    adjustmentDate: string;
    dueDate?: string;
    adjustmentType: SubledgerAdjustmentType;
    amount: number;
    currencyCode?: string;
    exchangeRate: number;
    contraAccountId: string;
    reference?: string;
    reason: string;
    notes?: string;
}

export interface ReverseSubledgerAdjustmentJournalDto {
    reason: string;
    reversalDate?: string;
}

export interface AccountBalance {
    accountId: string;
    balance: number;
    currencyCode: string;
    asOfDate: string;
}

// ============================================
// UTILITY TYPES
// ============================================

export interface PaginatedResponse<T> {
    items: T[];
    total: number;
    page: number;
    pageSize: number;
    totalPages: number;
}

// ============================================
// REQUEST/RESPONSE DTOS
// ============================================

// Settings
export interface FinanceSettings {
    id: string;
    tenantId: string;
    coaType: 'Standard' | 'Segmented';
    coaConfigurationLocked: boolean;
    baseCurrency: string;
    accountSeparator?: string;
    retainedEarningsAccountId?: string;
    unrealizedGainLossAccountId?: string;
    realizedGainLossAccountId?: string;
    suspenseAccountId?: string;
    controlAccountArId?: string;
    controlAccountApId?: string;
    controlAccountInventoryId?: string;
    controlAccountPayrollId?: string;
    controlAccountTaxId?: string;
    controlAccountGRVAccrualId?: string;
    discountAllowedAccountId?: string;
    discountReceivedAccountId?: string;
    migrationClearingAccountId?: string;
    openingBalanceAutoRoutingEnabled?: boolean;
    /** True when posted transactions exist — base currency and control accounts become locked */
    transactionsExist?: boolean;
}

export interface UpdateFinanceSettingsDto {
    coaType?: 'Standard' | 'Segmented';
    baseCurrency?: string;
    accountSeparator?: string;
    retainedEarningsAccountId?: string;
    unrealizedGainLossAccountId?: string;
    realizedGainLossAccountId?: string;
    suspenseAccountId?: string;
    controlAccountArId?: string;
    controlAccountApId?: string;
    controlAccountInventoryId?: string;
    controlAccountPayrollId?: string;
    controlAccountTaxId?: string;
    controlAccountGRVAccrualId?: string;
    discountAllowedAccountId?: string;
    discountReceivedAccountId?: string;
    migrationClearingAccountId?: string;
    openingBalanceAutoRoutingEnabled?: boolean;
}

// Currency
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

export interface UpdateCurrencyDto extends CreateCurrencyDto { }

// Exchange Rate
export interface CreateExchangeRateDto {
    baseCurrencyCode: string;
    targetCurrencyCode: string;
    rate: number;
    effectiveDate: string;
    expiryDate?: string;
    rateType: ExchangeRateType;
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
    from?: string;
    to?: string;
    isActive?: boolean;
}

// Exchange Rate Trends
export interface TrendAnalysisDto {
    date: string;
    sourceCurrency: string;
    targetCurrency: string;
    rate: number;
    previousRate: number | null;
    changeAmount: number | null;
    changePercentage: number | null;
    movingAverage: number | null;
    volatility: number | null;
    minRate: number | null;
    maxRate: number | null;
}

// Fiscal Year
// Period Types matching backend enum
export enum PeriodType {
    Monthly = 1,
    Quarterly = 2,
    Weekly = 3,
    Daily = 4
}

export interface CreateFiscalYearDto {
    fiscalYearName: string;
    fiscalYearCode: string;
    year: number;
    startDate: string;
    endDate: string;
    fiscalYearType: string;
    numberOfPeriods: number;
    baseCurrency: string;
    periodType: PeriodType;
}

// Fiscal Period
export interface PeriodCloseRequestDto {
    fiscalPeriodId: string;
    closingNotes?: string;
    bypassValidation?: boolean;
}

export interface PeriodReopenRequestDto {
    fiscalPeriodId: string;
    reason: string;
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

// Account
export interface AccountFilters {
    accountType?: AccountType;
    status?: AccountStatus;
    classification?: 'IFRS' | 'LOCAL_STATUTORY' | 'MANAGEMENT';
    isMultiCurrency?: boolean;
    parentAccountId?: string;
    search?: string;
    page?: number;
    pageSize?: number;
    take?: number;
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
    isManagementClassified?: boolean;
    isLocalClassified?: boolean;
    isBaseFrameworkClassified?: boolean;
    isLocalFrameworkClassified?: boolean;
    ifrsLineItem?: string;
    baseLineItem?: string;
    localLineItem?: string;
    allowDirectPosting?: boolean;
    isPostingAllowed?: boolean;
    isControlAccount: boolean;
    budgetTrackingEnabled: boolean;
    status: AccountStatus;
}

export interface SegmentValueInput {
    segmentStructureId: string;
    segmentPosition: number;
    segmentValue: string;
    segmentLookupValueId?: string;
}

export interface UpdateAccountDto extends Partial<CreateAccountDto> {
    id: string;
}

// Account Currency Link
export interface AddCurrencyLinkDto {
    accountId: string;
    currencyCode?: string;
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

// Segment
export interface CreateSegmentDto {
    segmentName: string;
    segmentCode: string;
    segmentPosition: number;
    segmentLength: number;
    dataType: string;
    separatorCharacter?: string;
    lookupTableRequired: boolean;
    isMandatory: boolean;
    isReportingDimension: boolean;
    isNaturalAccount: boolean;
    description?: string;
}

export interface UpdateSegmentDto extends CreateSegmentDto {
    id: string;
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

// Journal Entry
export interface JournalEntryFilters {
    periodId?: string;
    status?: PostingStatus;
    journalType?: JournalType;
    sourceModule?: string;
    from?: string;
    to?: string;
    search?: string;
}

export interface CreateJournalEntryDto {
    journalNumber?: string;
    transactionDate: string;
    journalType?: JournalType;
    description?: string;
    reference?: string;
    notes?: string;
    bookClassification?: string;
    fiscalPeriodId?: string;
    sourceModule?: string;
    sourceDocumentId?: string;
    sourceDocumentType?: string;
    transactions: CreateAccountTransactionDto[];
}

export interface CreateAccountTransactionDto {
    accountId: string;
    amount: number;
    transactionType: 'Debit' | 'Credit';
    description?: string;
    reference: string;
    currencyCode?: string;
    foreignAmount?: number;
    exchangeRate?: number;
    lineNumber?: number;
}

// Controlled Opening Balances
export interface CreateOpeningBalanceBatchDto {
    batchNumber?: string;
    sourceReference?: string;
    description?: string;
    openingDate: string;
    fiscalPeriodId: string;
    bookClassification: string;
    idempotencyKey?: string;
    lines: CreateOpeningBalanceLineDto[];
}

export interface CreateOpeningBalanceLineDto {
    accountId: string;
    debitAmount: number;
    creditAmount: number;
    transactionCurrencyCode?: string;
    functionalCurrencyCode?: string;
    exchangeRateId?: string;
    exchangeRateDate?: string;
    segmentString?: string;
    bankAccountId?: string;
    counterpartyType?: string;
    counterpartyId?: string;
    sourceReference?: string;
    notes?: string;
}

export interface UpdateOpeningBalanceBatchDto {
    sourceReference?: string;
    description?: string;
    openingDate: string;
    fiscalPeriodId: string;
    bookClassification: string;
    lines: CreateOpeningBalanceLineDto[];
}

export interface OpeningBalanceBatch {
    id: string;
    tenantId: string;
    batchNumber: string;
    sourceReference?: string;
    description?: string;
    openingDate: string;
    fiscalPeriodId: string;
    fiscalPeriodCode: string;
    bookClassification: string;
    status: string;
    idempotencyKey: string;
    totalDebit: number;
    totalCredit: number;
    difference: number;
    journalEntryId?: string;
    postingEventId?: string;
    workflowInstanceId?: string;
    validatedAt?: string;
    submittedAt?: string;
    approvedAt?: string;
    postedAt?: string;
    failureReason?: string;
    createdAt: string;
    updatedAt?: string;
    lines: OpeningBalanceLine[];
}

export interface OpeningBalanceLine {
    id: string;
    lineNumber: number;
    accountId: string;
    accountCode: string;
    accountName: string;
    debitAmount: number;
    creditAmount: number;
    transactionCurrencyCode: string;
    functionalCurrencyCode: string;
    exchangeRateId?: string;
    exchangeRateDate?: string;
    segmentString?: string;
    bankAccountId?: string;
    counterpartyType?: string;
    counterpartyId?: string;
    sourceReference?: string;
    notes?: string;
}

export interface OpeningBalanceValidationResult {
    batchId: string;
    isValid: boolean;
    totalDebit: number;
    totalCredit: number;
    difference: number;
    errors: string[];
    warnings: string[];
}

export interface OpeningBalanceDiagnostic {
    diagnosticCode: string;
    severity: 'Info' | 'Warning' | 'Error' | string;
    batchId?: string;
    reference?: string;
    message: string;
}

// Reports
export interface FinanceSegmentFilterDto {
    segmentStructureId?: string;
    segmentCode?: string;
    segmentPosition?: number;
    segmentValue: string;
}

export interface TrialBalanceRequestDto {
    asAtDate?: string;
    bookClassification?: string;
    includeZeroBalances?: boolean;
    segmentFilters?: FinanceSegmentFilterDto[];
}

export interface TrialBalanceReportDto {
    companyName: string;
    asAtDate: string;
    bookClassification: string;
    currencyCode: string;
    lines: TrialBalanceLineDto[];
    totalDebits: number;
    totalCredits: number;
    isBalanced: boolean;
    difference: number;
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
}

export interface DetailedLedgerRequestDto {
    startDate: string;
    endDate: string;
    accountIds?: string[];
    bookClassification?: string;
    includeReversed?: boolean;
    includeOpeningBalances?: boolean;
}

export interface DetailedLedgerReportDto {
    companyName: string;
    reportDate: string;
    startDate: string;
    endDate: string;
    bookClassification: string;
    currencyCode: string;
    accounts: DetailedLedgerAccountDto[];
    totalDebits: number;
    totalCredits: number;
}

export interface DetailedLedgerAccountDto {
    accountId: string;
    accountCode: string;
    accountNumber: string;
    accountName: string;
    accountType: string;
    openingBalance: number;
    openingBalanceType: 'Debit' | 'Credit' | '';
    totalDebits: number;
    totalCredits: number;
    closingBalance: number;
    closingBalanceType: 'Debit' | 'Credit' | '';
    lines: DetailedLedgerLineDto[];
}

export interface DetailedLedgerLineDto {
    transactionId: string;
    journalEntryId: string;
    journalEntryNumber: string;
    transactionDate: string;
    lineNumber: number;
    description: string;
    reference: string;
    sourceModule: string;
    postingStatus: string;
    debitAmount: number;
    creditAmount: number;
    runningBalance: number;
    runningBalanceType: 'Debit' | 'Credit' | '';
    currencyCode?: string;
    foreignAmount?: number;
    exchangeRate?: number;
    isReversed: boolean;
}

export interface IncomeStatementRequestDto {
    periodStart: string;
    periodEnd: string;
    bookClassification?: string;
    includeAccountDetails?: boolean;
    segmentFilters?: FinanceSegmentFilterDto[];
}

export interface BalanceSheetRequestDto {
    asAtDate: string;
    bookClassification?: string;
    includeAccountDetails?: boolean;
    segmentFilters?: FinanceSegmentFilterDto[];
}

export interface CashFlowStatementRequestDto {
    periodStart: string;
    periodEnd: string;
    bookClassification?: string;
    includeAccountDetails?: boolean;
    method?: 'Direct' | 'Indirect';
}

export interface MultiCurrencyDetailRequestDto {
    accountId?: string;
    currencyCode?: string;
    startDate: string;
    endDate: string;
    includeRevaluation?: boolean;
}

export interface IncomeStatementReportDto {
    companyName: string;
    periodStart: string;
    periodEnd: string;
    bookClassification: string;
    currencyCode: string;
    sections: IncomeStatementSectionDto[];
    totalRevenue: number;
    totalCostOfSales: number;
    grossProfit: number;
    totalOperatingExpenses: number;
    operatingProfit: number;
    totalOtherIncome: number;
    totalOtherExpenses: number;
    profitBeforeTax: number;
    taxExpense: number;
    netProfit: number;
}

export interface IncomeStatementSectionDto {
    sectionName: string;
    sectionOrder: number;
    lineItems: FinancialStatementLineItemDto[];
    sectionTotal: number;
}

export interface FinancialStatementLineItemDto {
    lineItemName: string;
    amount: number;
    lineOrder: number;
    accountNumbers?: string[];
}

export interface BalanceSheetReportDto {
    companyName: string;
    asAtDate: string;
    bookClassification: string;
    currencyCode: string;
    sections: BalanceSheetSectionDto[];
    totalAssets: number;
    totalLiabilities: number;
    totalEquity: number;
    isBalanced: boolean;
}

export interface BalanceSheetSectionDto {
    sectionName: string;
    sectionOrder: number;
    categories: BalanceSheetCategoryDto[];
    sectionTotal: number;
}

export interface BalanceSheetCategoryDto {
    categoryName: string;
    categoryOrder: number;
    lineItems: FinancialStatementLineItemDto[];
    categoryTotal: number;
}

export interface CashFlowStatementReportDto {
    companyName: string;
    periodStart: string;
    periodEnd: string;
    bookClassification: string;
    currencyCode: string;
    operatingActivities: CashFlowSectionDto;
    investingActivities: CashFlowSectionDto;
    financingActivities: CashFlowSectionDto;
    netCashFromOperating: number;
    netCashFromInvesting: number;
    netCashFromFinancing: number;
    netIncreaseInCash: number;
    cashAtBeginning: number;
    cashAtEnd: number;
    isReconciled: boolean;
}

export interface CashFlowSectionDto {
    sectionName: string;
    sectionOrder: number;
    lineItems: FinancialStatementLineItemDto[];
    sectionTotal: number;
}

export interface MultiCurrencyDetailReportDto {
    companyName: string;
    reportDate: string;
    periodStart: string;
    periodEnd: string;
    accounts: MultiCurrencyAccountDetailDto[];
}

export interface MultiCurrencyAccountDetailDto {
    accountId: string;
    accountNumber: string;
    accountName: string;
    currencyCode: string;
    openingBalanceForeign: number;
    openingBalanceBase: number;
    totalDebitsForeign: number;
    totalCreditsForeign: number;
    totalDebitsBase: number;
    totalCreditsBase: number;
    closingBalanceForeign: number;
    closingBalanceBase: number;
    unrealizedGainLoss: number;
    transactions: MultiCurrencyTransactionDetailDto[];
}

export interface MultiCurrencyTransactionDetailDto {
    transactionDate: string;
    description: string;
    reference: string;
    transactionType: 'Debit' | 'Credit' | string;
    foreignAmount: number;
    exchangeRate: number;
    baseAmount: number;
    runningBalanceForeign: number;
    runningBalanceBase: number;
    isRevaluation: boolean;
}

// Revaluation
export interface RevaluationRequestDto {
    revaluationDate: string;
    revaluationType: string;
    currencyCode?: string;
    unrealizedGainLossAccountId: string;
    previewOnly: boolean;
    notes?: string;
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

// Year End Close
export interface YearEndCloseRequestDto {
    fiscalYearId: string;
    retainedEarningsAccountId: string;
    closingNotes?: string;
}

// ============================================
// ACCOUNT COMBINATION GENERATOR
// ============================================

export interface SegmentSelection {
    segmentStructureId: string;
    selectedLookupValueIds: string[];
}

export interface CombinationRequest {
    segmentSelections: SegmentSelection[];
    accountType: AccountType;
    currencyCode: string;
    includeExistingInPreview: boolean;
    isMultiCurrency?: boolean;
    allowDirectPosting?: boolean;
    budgetTrackingEnabled?: boolean;
}

export interface SegmentValuePreview {
    segmentStructureId: string;
    segmentName: string;
    segmentValue: string;
    segmentDescription: string;
    segmentPosition: number;
}

export type CombinationStatus = 'Valid' | 'Duplicate' | 'Invalid';

export interface AccountCombinationPreview {
    tempId: string;
    accountNumber: string;
    generatedName: string;
    segmentValues: SegmentValuePreview[];
    status: CombinationStatus;
    validationMessage?: string;
    isSelected: boolean; // Frontend only
}

export interface BulkCreateAccountsRequest {
    combinations: AccountCombinationPreview[];
    skipDuplicates: boolean;
}

export interface BulkCreationError {
    accountNumber: string;
    errorMessage: string;
}

export interface BulkCreationResult {
    totalRequested: number;
    successCount: number;
    skipCount: number;
    errorCount: number;
    createdAccountIds: string[];
    errors: BulkCreationError[];
}
