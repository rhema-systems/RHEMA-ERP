/**
 * Budgeting Module Types
 */

export type BudgetStatus = 'Draft' | 'Collecting' | 'InReview' | 'Approved' | 'Superseded' | 'Archived';
export type BudgetReturnStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected';

export interface BudgetControlDimension {
    financeDimensionDefinitionId: string;
    dimensionCode: string;
    dimensionName: string;
    displayOrder: number;
}

export interface BudgetDimensionAssignmentInput {
    financeDimensionDefinitionId: string;
    financeDimensionValueId: string;
}

export interface BudgetDimensionAssignment extends BudgetDimensionAssignmentInput {
    dimensionCode: string;
    dimensionName: string;
    valueCode: string;
    valueName: string;
}

export interface BudgetScenario {
    id: string;
    name: string;
    description?: string;
    versionType: 'Original' | 'Virement' | 'Supplementary' | string;
    versionNumber: number;
    parentScenarioId?: string;
    fiscalYearId: string;
    baseCurrencyCode: string;
    isActive: boolean;
    status: BudgetStatus;
    lockedDate?: string;
    lockedByUserId?: string;
    lockedByUserName?: string;
    adoptedAt?: string;
    adoptionEffectiveDate?: string;
    adoptedByUserId?: string;
    adoptedByUserName?: string;
    adoptionReason?: string;
    supersededAt?: string;
    supersededByUserId?: string;
    supersededByUserName?: string;
    supersessionReason?: string;
    createdAt: string;
    updatedAt?: string;
    createdBy?: string;
    updatedBy?: string;
    tenantId: string;
    rowVersion: string;
    controlDimensions: BudgetControlDimension[];
}

export interface BudgetReturn {
    id: string;
    budgetScenarioId: string;
    budgetScenarioName?: string;
    segmentValueId?: string;
    assignedToUserId?: string;
    approverUserId?: string;
    status: BudgetReturnStatus;
    notes?: string;
    rejectionReason?: string;
    submittedDate?: string;
    approvedDate?: string;
    createdAt: string;
    updatedAt?: string;
    createdBy?: string;
    updatedBy?: string;
    tenantId: string;
    rowVersion: string;
    totalAmountBase: number;

    // Virtual properties for display
    segmentValueName?: string;
    assignedToUserName?: string;
    approverUserName?: string;
}

export interface BudgetAssignee {
    id: string;
    displayName: string;
    email: string;
}

export interface BudgetEntry {
    id: string;
    budgetReturnId: string;
    accountId: string;
    fiscalPeriodId: string;
    currencyCode: string;
    exchangeRate: number;
    amount: number;
    amountBase: number;
    notes?: string;
    createdAt: string;
    updatedAt?: string;
    tenantId: string;
    rowVersion: string;
    financeDimensionSetId?: string;
    dimensionCombinationHash?: string;
    dimensionAssignments: BudgetDimensionAssignment[];

    // Virtual/Display properties
    accountCode?: string;
    accountName?: string;
    periodName?: string;
}

// DTOs

export interface CreateBudgetScenarioDto {
    name: string;
    description?: string;
    fiscalYearId: string;
    baseCurrencyCode: string;
    controlDimensionDefinitionIds: string[];
}

export interface UpdateBudgetScenarioDto {
    name: string;
    description?: string;
    isActive?: boolean;
    rowVersion: string;
    controlDimensionDefinitionIds?: string[];
}

export interface CreateBudgetReturnDto {
    budgetScenarioId: string;
    segmentValueId?: string;
    assignedToUserId?: string;
    approverUserId?: string;
    notes?: string;
}

export interface UpdateBudgetReturnDto {
    assignedToUserId?: string;
    clearAssignedToUser?: boolean;
    approverUserId?: string;
    clearApproverUser?: boolean;
    notes?: string;
    rowVersion: string;
}

export interface BudgetEntryDto {
    id?: string; // Optional for new entries
    budgetReturnId: string;
    accountId: string;
    fiscalPeriodId: string;
    currencyCode: string;
    exchangeRate: number;
    amount: number;
    rowVersion?: string;
    financeDimensionSetId?: string;
    dimensionCombinationHash?: string;
    dimensionAssignments: BudgetDimensionAssignmentInput[];
}

export interface SubmitBudgetReturnDto {
    returnId: string;
    rowVersion: string;
}

export interface BulkSaveBudgetEntriesDto {
    returnId: string;
    returnRowVersion: string;
    entries: BudgetEntryDto[];
}

export interface BudgetAuditEvent {
    id: string;
    action: string;
    username: string;
    timestamp: string;
    oldValues?: string;
    newValues?: string;
}

export interface BudgetSummaryDto {
    scenarioId: string;
    totalRevenue: number;
    totalExpense: number;
    netIncome: number;
    currencyCode: string;
}

export interface AdoptBudgetScenarioDto {
    rowVersion: string;
    effectiveDate: string;
    reason: string;
}

export interface BudgetValidationIssue {
    severity: 'Error' | 'Warning' | string;
    code: string;
    message: string;
    returnId?: string;
}

export interface BudgetReportPeriod {
    fiscalPeriodId: string;
    periodCode: string;
    periodName: string;
    periodNumber: number;
}

export interface BudgetReportContribution {
    budgetReturnId: string;
    segmentValueId?: string;
    segmentCode: string;
    segmentName: string;
    returnStatus: BudgetReturnStatus | string;
    budgetAmount: number;
}

export interface BudgetReportLine {
    accountId: string;
    accountCode: string;
    accountName: string;
    accountType: string;
    fiscalPeriodId: string;
    periodCode: string;
    periodName: string;
    periodNumber: number;
    budgetAmount: number;
    actualAmount: number;
    varianceAmount: number;
    variancePercent?: number;
    favorability: 'Favorable' | 'Unfavorable' | 'OnBudget' | string;
    contributions: BudgetReportContribution[];
}

export interface BudgetUnitSummary {
    budgetReturnId: string;
    segmentValueId?: string;
    segmentCode: string;
    segmentName: string;
    returnStatus: BudgetReturnStatus | string;
    assignedToUserName: string;
    budgetAmount: number;
}

export interface ConsolidatedBudgetView {
    scenarioId: string;
    scenarioName: string;
    fiscalYearId: string;
    fiscalYearName: string;
    scenarioStatus: BudgetStatus;
    isOfficial: boolean;
    approvedOnly: boolean;
    currencyCode: string;
    bookClassification: string;
    totalReturnCount: number;
    includedReturnCount: number;
    approvedReturnCount: number;
    draftReturnCount: number;
    submittedReturnCount: number;
    rejectedReturnCount: number;
    readyForSubmission: boolean;
    totalRevenueBudget: number;
    totalExpenseBudget: number;
    netBudget: number;
    totalRevenueActual: number;
    totalExpenseActual: number;
    netActual: number;
    periods: BudgetReportPeriod[];
    lines: BudgetReportLine[];
    units: BudgetUnitSummary[];
    validationIssues: BudgetValidationIssue[];
}

export interface BudgetScenarioComparisonLine {
    accountId: string;
    accountCode: string;
    accountName: string;
    accountType: string;
    fiscalPeriodId: string;
    periodCode: string;
    periodName: string;
    periodNumber: number;
    baseAmount: number;
    comparisonAmount: number;
    differenceAmount: number;
    differencePercent?: number;
}

export interface BudgetScenarioComparison {
    baseScenarioId: string;
    baseScenarioName: string;
    comparisonScenarioId: string;
    comparisonScenarioName: string;
    fiscalYearId: string;
    currencyCode: string;
    baseTotal: number;
    comparisonTotal: number;
    differenceTotal: number;
    lines: BudgetScenarioComparisonLine[];
}

export type BudgetRevisionType = 'Virement' | 'Supplementary';
export type BudgetRevisionStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Applied';

export interface BudgetRevisionLineInput {
    segmentValueId?: string;
    accountId: string;
    fiscalPeriodId: string;
    adjustmentAmountBase: number;
    notes?: string;
}

export interface CreateBudgetRevisionDto {
    sourceScenarioId: string;
    revisionType: BudgetRevisionType;
    effectiveDate: string;
    boardResolutionReference: string;
    boardResolutionDate: string;
    justification: string;
    lines: BudgetRevisionLineInput[];
}

export interface BudgetRevisionLine extends BudgetRevisionLineInput {
    id: string;
    segmentCode: string;
    segmentName: string;
    accountCode: string;
    accountName: string;
    periodCode: string;
    periodName: string;
    currentAmountBase: number;
    revisedAmountBase: number;
}

export interface BudgetRevision {
    id: string;
    revisionNumber: string;
    revisionType: BudgetRevisionType;
    sourceScenarioId: string;
    sourceScenarioName: string;
    fiscalYearId: string;
    fiscalYearName: string;
    resultScenarioId?: string;
    resultScenarioName?: string;
    effectiveDate: string;
    boardResolutionReference: string;
    boardResolutionDate: string;
    justification: string;
    status: BudgetRevisionStatus;
    increaseAmountBase: number;
    reductionAmountBase: number;
    netChangeAmountBase: number;
    submittedAt?: string;
    approvedAt?: string;
    appliedAt?: string;
    rejectionReason?: string;
    createdAt: string;
    rowVersion: string;
    lines: BudgetRevisionLine[];
}
