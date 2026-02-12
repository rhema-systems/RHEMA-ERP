/**
 * Budgeting Module Types
 */

export type BudgetStatus = 'Draft' | 'Open' | 'Approved' | 'Locked' | 'Archived';
export type BudgetReturnStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected';

export interface BudgetScenario {
    id: string;
    name: string;
    description?: string;
    fiscalYearId: string;
    baseCurrencyCode: string;
    isActive: boolean;
    status: BudgetStatus;
    lockedDate?: string;
    lockedByUserId?: string;
    createdAt: string;
    updatedAt?: string;
    createdBy?: string;
    updatedBy?: string;
    tenantId: string;
}

export interface BudgetReturn {
    id: string;
    budgetScenarioId: string;
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

    // Virtual properties for display
    segmentValueName?: string;
    assignedToUserName?: string;
    approverUserName?: string;
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
}

export interface UpdateBudgetScenarioDto {
    name?: string;
    description?: string;
    isActive?: boolean;
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
    approverUserId?: string;
    notes?: string;
}

export interface BudgetEntryDto {
    id?: string; // Optional for new entries
    budgetReturnId: string;
    accountId: string;
    fiscalPeriodId: string;
    currencyCode: string;
    exchangeRate: number;
    amount: number;
}

export interface SubmitBudgetReturnDto {
    returnId: string;
    notes?: string;
}

export interface ApproveBudgetReturnDto {
    returnId: string;
    notes?: string;
}

export interface RejectBudgetReturnDto {
    returnId: string;
    reason: string;
}

export interface BulkSaveBudgetEntriesDto {
    returnId: string;
    entries: BudgetEntryDto[];
}

export interface BudgetSummaryDto {
    scenarioId: string;
    totalRevenue: number;
    totalExpense: number;
    netIncome: number;
    currencyCode: string;
}
