// ============================================
// UNIT ACCOUNTS MODULE TYPES
// ============================================

// ============================================
// CORE ENUMS
// ============================================

export type UnitJournalEntryStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Posted' | 'Reversed' | 'ReadyToPost';
export type AllocationRunBatchStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Posted' | 'Cancelled' | 'ReadyToPost';
export type NumeratorDenominatorType = 'FinancialAccount' | 'UnitAccount' | 'Constant';
export type RatioResultFormat = 'Decimal' | 'Percentage' | 'Currency';

// ============================================
// CORE ENTITIES
// ============================================

export interface UnitType {
    id: string;
    code: string;
    name: string;
    description?: string;
    decimalPlaces: number;
    isActive: boolean;
    createdAt: string;
    createdBy: string;
    modifiedAt?: string;
    modifiedBy?: string;
}

export interface UnitAccount {
    id: string;
    accountNumber: string;
    name: string;
    description?: string;
    unitTypeId: string;
    unitType?: UnitType;
    unitTypeCode?: string;
    unitTypeName?: string;
    parentAccountId?: string;
    parentAccount?: UnitAccount;
    parentAccountNumber?: string;
    accountLevel: number;
    isPostingAccount: boolean;
    isActive: boolean;
    currentBalance?: number;
    children?: UnitAccount[];
    createdAt: string;
    createdBy: string;
    modifiedAt?: string;
    modifiedBy?: string;
}

export interface UnitJournalEntry {
    approvalRequired?: boolean;
    workflowInstanceId?: string;
    id: string;
    entryNumber: string;
    entryDate: string;
    fiscalYearId?: string;
    fiscalPeriodId?: string;
    fiscalPeriodName?: string;
    description?: string;
    status: UnitJournalEntryStatus;
    sourceDocument?: string;
    lineCount?: number;
    lines?: UnitJournalEntryLine[];
    approvedAt?: string;
    approvedBy?: string;
    postedAt?: string;
    postedBy?: string;
    rejectionReason?: string;
    createdAt: string;
    createdBy: string;
    modifiedAt?: string;
    modifiedBy?: string;
}

export interface UnitJournalEntryLine {
    id: string;
    unitJournalEntryId?: string;
    lineNumber: number;
    unitAccountId: string;
    unitAccount?: UnitAccount;
    unitAccountNumber?: string;
    unitAccountName?: string;
    quantity: number;
    description?: string;
}

export interface UnitAccountBalance {
    id: string;
    unitAccountId: string;
    unitAccount?: UnitAccount;
    fiscalYearId: string;
    fiscalPeriodId: string;
    openingBalance: number;
    periodActivity: number;
    closingBalance: number;
}

export interface RatioDefinition {
    id: string;
    code: string;
    name: string;
    description?: string;
    ratioType?: string;
    numeratorType: NumeratorDenominatorType;
    numeratorAccountId?: string;
    numeratorUnitAccountId?: string;
    numeratorConstantValue?: number;
    /** @deprecated Use numeratorConstantValue. */
    numeratorConstant?: number;
    denominatorType: NumeratorDenominatorType;
    denominatorAccountId?: string;
    denominatorUnitAccountId?: string;
    denominatorConstantValue?: number;
    /** @deprecated Use denominatorConstantValue. */
    denominatorConstant?: number;
    resultFormat: RatioResultFormat;
    formatPrecision?: number;
    /** @deprecated Use formatPrecision. */
    decimalPlaces?: number;
    isActive: boolean;
    createdAt: string;
    createdBy: string;
    modifiedAt?: string;
    modifiedBy?: string;
}

// ============================================
// REQUEST/RESPONSE DTOS
// ============================================

// Unit Type
export interface CreateUnitTypeDto {
    code: string;
    name: string;
    description?: string;
    decimalPlaces: number;
}

export interface UpdateUnitTypeDto {
    name?: string;
    description?: string;
    decimalPlaces?: number;
    isActive?: boolean;
}

// Unit Account
export interface CreateUnitAccountDto {
    accountNumber: string;
    name: string;
    description?: string;
    unitTypeId: string;
    parentAccountId?: string;
    isPostingAccount: boolean;
}

export interface UpdateUnitAccountDto {
    name?: string;
    description?: string;
    isPostingAccount?: boolean;
}

export interface UnitAccountHierarchyDto {
    id: string;
    accountNumber: string;
    name: string;
    unitTypeName: string;
    accountLevel: number;
    isPostingAccount: boolean;
    isActive: boolean;
    currentBalance: number;
    children: UnitAccountHierarchyDto[];
}

// Unit Journal Entry
export interface CreateUnitJournalEntryDto {
    entryDate: string;
    description?: string;
    sourceDocument?: string;
    fiscalPeriodId?: string;
    lines: CreateUnitJournalEntryLineDto[];
}

export interface CreateUnitJournalEntryLineDto {
    unitAccountId: string;
    quantity: number;
    description?: string;
}

export interface UnitJournalEntryFilters {
    status?: UnitJournalEntryStatus;
    fromDate?: string;
    toDate?: string;
    accountId?: string;
    periodId?: string;
    search?: string;
}

// Ratio Definition
export interface CreateRatioDefinitionDto {
    code: string;
    name: string;
    description?: string;
    ratioType?: string;
    numeratorType: NumeratorDenominatorType;
    numeratorAccountId?: string;
    numeratorUnitAccountId?: string;
    numeratorConstantValue?: number;
    /** @deprecated Use numeratorConstantValue. */
    numeratorConstant?: number;
    denominatorType: NumeratorDenominatorType;
    denominatorAccountId?: string;
    denominatorUnitAccountId?: string;
    denominatorConstantValue?: number;
    /** @deprecated Use denominatorConstantValue. */
    denominatorConstant?: number;
    resultFormat: RatioResultFormat;
    formatPrecision?: number;
    /** @deprecated Use formatPrecision. */
    decimalPlaces?: number;
}

export interface UpdateRatioDefinitionDto {
    name?: string;
    description?: string;
}

export interface RatioCalculationRequest {
    ratioId: string;
    periodId?: string;
    startDate?: string;
    endDate?: string;
}

export interface RatioCalculationResult {
    ratioId: string;
    ratioCode: string;
    ratioName: string;
    fiscalPeriodId?: string;
    periodName?: string;
    numerator?: number;
    denominator?: number;
    /** @deprecated Use numerator. */
    numeratorValue: number;
    /** @deprecated Use denominator. */
    denominatorValue: number;
    result: number;
    formattedResult?: string;
    /** @deprecated Use formattedResult. */
    resultFormatted: string;
    calculatedAt: string;
}

export interface RatioTrendResult {
    ratioId: string;
    ratioCode: string;
    ratioName: string;
    dataPoints: {
        periodId: string;
        periodName: string;
        result: number | null;
        resultFormatted: string;
    }[];
}

// ============================================
// BUDGET ENTITIES & DTOS
// ============================================

export interface UnitAccountBudget {
    id: string;
    unitAccountId: string;
    unitAccount?: UnitAccount;
    fiscalYearId: string;
    fiscalPeriodId: string;
    periodName?: string;
    budgetQuantity: number;
    notes?: string;
    budgetVersion: string;
    isActive: boolean;
    createdAt: string;
    createdBy: string;
}

export interface BudgetVariance {
    unitAccountId: string;
    accountNumber: string;
    accountName: string;
    unitTypeCode: string;
    periodName: string;
    budgetQuantity: number;
    actualQuantity: number;
    variance: number;
    variancePercent: number;
    isFavorable: boolean;
}

export interface CreateBudgetDto {
    unitAccountId: string;
    fiscalYearId: string;
    fiscalPeriodId: string;
    budgetQuantity: number;
    notes?: string;
    budgetVersion?: string;
}

export interface UpdateBudgetDto {
    budgetQuantity?: number;
    notes?: string;
    isActive?: boolean;
}

// ============================================
// ALLOCATION ENTITIES & DTOS
// ============================================

export type AllocationType = 'UnitAccountBased' | 'FixedPercentage' | 'EqualDistribution';

export interface AllocationRule {
    id: string;
    code: string;
    name: string;
    description?: string;
    sourceAccountId: string;
    sourceAccountNumber?: string;
    sourceAccountName?: string;
    allocationType: AllocationType;
    driverUnitAccountId?: string;
    driverUnitAccountNumber?: string;
    driverUnitAccountName?: string;
    isActive: boolean;
    autoReverse: boolean;
    lastRunDate?: string;
    targets: AllocationTarget[];
    createdAt: string;
    createdBy: string;
}

export interface AllocationTarget {
    id: string;
    allocationRuleId?: string;
    targetAccountId: string;
    targetAccountNumber?: string;
    targetAccountName?: string;
    fixedPercentage?: number;
    targetDriverUnitAccountId?: string;
    targetDriverUnitAccountNumber?: string;
    costCenterCode?: string;
}

export interface CreateAllocationRuleDto {
    code: string;
    name: string;
    description?: string;
    sourceAccountId: string;
    allocationType: AllocationType;
    driverUnitAccountId?: string;
    autoReverse: boolean;
    targets: CreateAllocationTargetDto[];
}

export interface UpdateAllocationRuleDto {
    name: string;
    description?: string;
    sourceAccountId: string;
    allocationType: AllocationType;
    driverUnitAccountId?: string;
    isActive: boolean;
    autoReverse: boolean;
    targets: CreateAllocationTargetDto[];
}

export interface CreateAllocationTargetDto {
    targetAccountId: string;
    fixedPercentage?: number;
    targetDriverUnitAccountId?: string;
    costCenterCode?: string;
}

export interface AllocationRunResult {
    allocationRuleId: string;
    ruleCode: string;
    ruleName: string;
    runDate: string;
    journalEntryId?: string;
    journalEntryNumber?: string;
    totalAllocated: number;
    lines: AllocationLineResult[];
}

export interface RunAllocationDto {
    allocationRuleId: string;
    fiscalPeriodId: string;
    allocationDate: string;
    description?: string;
}

export interface AllocationLineResult {
    targetAccountId: string;
    targetAccountNumber: string;
    targetAccountName: string;
    allocationBasis: number;
    allocationPercent: number;
    allocatedAmount: number;
}

export interface AllocationRunBatch {
    approvalRequired?: boolean;
    id: string;
    batchNumber: string;
    allocationRuleId: string;
    ruleCode: string;
    ruleName: string;
    fiscalPeriodId: string;
    periodCode: string;
    periodName: string;
    allocationDate: string;
    description?: string;
    status: AllocationRunBatchStatus;
    sourceAccountId: string;
    sourceAccountNumber: string;
    sourceAccountName: string;
    sourcePeriodBalance: number;
    totalAllocated: number;
    allocationType: AllocationType;
    bookClassification: string;
    functionalCurrencyCode: string;
    workflowInstanceId?: string;
    journalEntryId?: string;
    journalEntryNumber?: string;
    submittedAt?: string;
    submittedByName?: string;
    approvedAt?: string;
    approvedByName?: string;
    postedAt?: string;
    postedByName?: string;
    rejectionReason?: string;
    createdAt: string;
    lines: AllocationRunBatchLine[];
}

export interface AllocationRunBatchLine {
    id: string;
    lineNumber: number;
    targetAccountId: string;
    targetAccountNumber: string;
    targetAccountName: string;
    targetDriverUnitAccountId?: string;
    targetDriverUnitAccountNumber?: string;
    targetDriverUnitAccountName?: string;
    allocationBasis: number;
    allocationPercent: number;
    allocatedAmount: number;
    costCenterCode?: string;
}

export interface CreateAllocationRunBatchDto {
    allocationRuleId: string;
    fiscalPeriodId: string;
    allocationDate: string;
    description?: string;
}
