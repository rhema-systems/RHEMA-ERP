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

// ============================================
// CORE ENTITIES
// ============================================

export interface Currency {
    id: string;
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
    baseCurrencyCode: string;
    targetCurrencyCode: string;
    rate: number;
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
    fiscalYearId: string;
    periodNumber: number;
    periodName: string;
    startDate: string;
    endDate: string;
    status: PeriodStatus;
    isClosed: boolean;
    isLocked: boolean;
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
    isBaseClassified: boolean;
    isLocalClassified: boolean;
    ifrsLineItem?: string;
    baseLineItem?: string;
    localLineItem?: string;
    allowDirectPosting: boolean;
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
    lookupValuesCount?: number;
    createdAt: string;
    updatedAt: string;
}

export interface SegmentLookupValue {
    id: string;
    segmentStructureId: string;
    segmentValue: string;
    description: string;
    effectiveDate: string;
    expirationDate?: string;
    isActive: boolean;
    displayOrder: number;
    createdAt: string;
    updatedAt: string;
}

export interface JournalEntry {
    id: string;
    journalEntryNumber: string;
    journalType: JournalType;
    entryDate: string;
    description: string;
    referenceNumber?: string;
    sourceModule?: string;
    totalDebitAmount: number;
    totalCreditAmount: number;
    isBalanced: boolean;
    isMultiCurrency: boolean;
    primaryCurrency?: string;
    bookClassification: string;
    fiscalPeriodId: string;
    postingStatus: PostingStatus;
    postingDate?: string;
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
    isRevaluationEntry: boolean;
    revaluationType?: string;
    transactions: JournalEntryLine[];
    notes?: string;
    createdAt: string;
    updatedAt: string;
    createdBy: string;
    updatedBy: string;
}

export interface JournalEntryLine {
    id: string;
    journalEntryId: string;
    lineNumber: number;
    accountId: string;
    accountCode: string;
    accountName: string;
    description: string;
    debitAmount: number;
    creditAmount: number;
    transactionCurrency?: string;
    foreignCurrencyAmount?: number;
    exchangeRate?: number;
    isRevaluationEntry: boolean;
    revaluationType?: string;
    createdAt: string;
    updatedAt: string;
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
}

export interface UpdateCurrencyDto extends CreateCurrencyDto { }

// Exchange Rate
export interface CreateExchangeRateDto {
    baseCurrencyCode: string;
    targetCurrencyCode: string;
    rate: number;
    effectiveDate: string;
    rateType: ExchangeRateType;
    rateSource: string;
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
    previousRate: number;
    changeAmount: number;
    changePercentage: number;
    movingAverage: number;
    volatility: number;
    minRate: number;
    maxRate: number;
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
    reopenReason: string;
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

// Reports
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
