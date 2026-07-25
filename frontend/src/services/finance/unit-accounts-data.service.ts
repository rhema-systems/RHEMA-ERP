/**
 * Unit Accounts Data Service
 * Service layer for unit accounts module API calls
 */

import type {
    UnitType,
    UnitAccount,
    UnitJournalEntry,
    UnitAccountBalance,
    RatioDefinition,
    UnitAccountBudget,
    AllocationRule,
    AllocationTarget,
    CreateUnitTypeDto,
    CreateUnitAccountDto,
    UpdateUnitAccountDto,
    CreateUnitJournalEntryDto,
    CreateRatioDefinitionDto,
    CreateBudgetDto,
    UpdateBudgetDto,
    CreateAllocationRuleDto,
    AllocationType,
} from '@/types/unit-accounts';
import { apiService } from '@/services/api.service';

// =============================================================================
// UNIT ACCOUNTS DATA SERVICE
// =============================================================================

class UnitAccountsDataService {
    // ===== UNIT TYPES =====

    async getUnitTypes(filters?: {
        isActive?: boolean;
    }): Promise<UnitType[]> {
        const queryParams = new URLSearchParams();
        if (filters?.isActive !== undefined) queryParams.append('isActive', String(filters.isActive));

        const endpoint = `/finance/unit-types${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<UnitType[]>(endpoint);
    }

    async getUnitTypeById(id: string): Promise<UnitType> {
        return apiService.get<UnitType>(`/finance/unit-types/${id}`);
    }

    async createUnitType(dto: CreateUnitTypeDto): Promise<UnitType> {
        return apiService.post<UnitType>('/finance/unit-types', dto);
    }

    async updateUnitType(id: string, dto: Partial<UnitType>): Promise<UnitType> {
        return apiService.put<UnitType>(`/finance/unit-types/${id}`, dto);
    }

    async deleteUnitType(id: string): Promise<void> {
        return apiService.delete(`/finance/unit-types/${id}`);
    }

    // ===== UNIT ACCOUNTS =====

    async getUnitAccounts(filters?: {
        unitTypeId?: string;
        isActive?: boolean;
        isPostingAccount?: boolean;
    }): Promise<UnitAccount[]> {
        const queryParams = new URLSearchParams();
        if (filters?.unitTypeId) queryParams.append('unitTypeId', filters.unitTypeId);
        if (filters?.isActive !== undefined) queryParams.append('isActive', String(filters.isActive));
        if (filters?.isPostingAccount !== undefined) queryParams.append('isPostingAccount', String(filters.isPostingAccount));

        const endpoint = `/finance/unit-accounts${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<UnitAccount[]>(endpoint);
    }

    async getUnitAccountById(id: string): Promise<UnitAccount> {
        return apiService.get<UnitAccount>(`/finance/unit-accounts/${id}`);
    }

    async createUnitAccount(dto: CreateUnitAccountDto): Promise<UnitAccount> {
        return apiService.post<UnitAccount>('/finance/unit-accounts', dto);
    }

    async updateUnitAccount(id: string, dto: UpdateUnitAccountDto): Promise<UnitAccount> {
        return apiService.put<UnitAccount>(`/finance/unit-accounts/${id}`, dto);
    }

    async deleteUnitAccount(id: string): Promise<void> {
        return apiService.delete(`/finance/unit-accounts/${id}`);
    }

    // ===== UNIT JOURNAL ENTRIES =====

    async getUnitJournalEntries(filters?: {
        startDate?: string;
        endDate?: string;
        status?: string;
    }): Promise<UnitJournalEntry[]> {
        const queryParams = new URLSearchParams();
        if (filters?.startDate) queryParams.append('startDate', filters.startDate);
        if (filters?.endDate) queryParams.append('endDate', filters.endDate);
        if (filters?.status) queryParams.append('status', filters.status);

        const endpoint = `/finance/unit-journal-entries${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<UnitJournalEntry[]>(endpoint);
    }

    async getUnitJournalEntryById(id: string): Promise<UnitJournalEntry> {
        return apiService.get<UnitJournalEntry>(`/finance/unit-journal-entries/${id}`);
    }

    async createUnitJournalEntry(dto: CreateUnitJournalEntryDto): Promise<UnitJournalEntry> {
        return apiService.post<UnitJournalEntry>('/finance/unit-journal-entries', dto);
    }

    async postUnitJournalEntry(id: string): Promise<UnitJournalEntry> {
        return apiService.post<UnitJournalEntry>(`/finance/unit-journal-entries/${id}/post`, {});
    }

    async deleteUnitJournalEntry(id: string): Promise<void> {
        return apiService.delete(`/finance/unit-journal-entries/${id}`);
    }

    async getNextEntryNumber(): Promise<string> {
        const response = await apiService.get<{ number: string }>('/finance/unit-journal-entries/next-number');
        return response.number;
    }

    // ===== UNIT ACCOUNT BALANCES =====

    async getUnitAccountBalances(filters?: {
        unitAccountId?: string;
        fiscalYearId?: string;
        fiscalPeriodId?: string;
    }): Promise<UnitAccountBalance[]> {
        const queryParams = new URLSearchParams();
        if (filters?.unitAccountId) queryParams.append('unitAccountId', filters.unitAccountId);
        if (filters?.fiscalYearId) queryParams.append('fiscalYearId', filters.fiscalYearId);
        if (filters?.fiscalPeriodId) queryParams.append('fiscalPeriodId', filters.fiscalPeriodId);

        const endpoint = `/finance/unit-account-balances${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<UnitAccountBalance[]>(endpoint);
    }

    // ===== RATIO DEFINITIONS =====

    async getRatioDefinitions(filters?: {
        isActive?: boolean;
    }): Promise<RatioDefinition[]> {
        const queryParams = new URLSearchParams();
        if (filters?.isActive !== undefined) queryParams.append('isActive', String(filters.isActive));

        const endpoint = `/finance/ratio-definitions${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<RatioDefinition[]>(endpoint);
    }

    async getRatioDefinitionById(id: string): Promise<RatioDefinition> {
        return apiService.get<RatioDefinition>(`/finance/ratio-definitions/${id}`);
    }

    async createRatioDefinition(dto: CreateRatioDefinitionDto): Promise<RatioDefinition> {
        return apiService.post<RatioDefinition>('/finance/ratio-definitions', dto);
    }

    async updateRatioDefinition(id: string, dto: Partial<RatioDefinition>): Promise<RatioDefinition> {
        return apiService.put<RatioDefinition>(`/finance/ratio-definitions/${id}`, dto);
    }

    async deleteRatioDefinition(id: string): Promise<void> {
        return apiService.delete(`/finance/ratio-definitions/${id}`);
    }

    // ===== UNIT ACCOUNT BUDGETS =====

    async getUnitAccountBudgets(filters?: {
        unitAccountId?: string;
        fiscalYearId?: string;
    }): Promise<UnitAccountBudget[]> {
        const queryParams = new URLSearchParams();
        if (filters?.unitAccountId) queryParams.append('unitAccountId', filters.unitAccountId);
        if (filters?.fiscalYearId) queryParams.append('fiscalYearId', filters.fiscalYearId);

        const endpoint = `/finance/unit-budgets${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<UnitAccountBudget[]>(endpoint);
    }

    async getUnitAccountBudgetById(id: string): Promise<UnitAccountBudget> {
        return apiService.get<UnitAccountBudget>(`/finance/unit-budgets/${id}`);
    }

    async createBudget(dto: CreateBudgetDto): Promise<UnitAccountBudget> {
        return apiService.post<UnitAccountBudget>('/finance/unit-budgets', dto);
    }

    async updateBudget(id: string, dto: UpdateBudgetDto): Promise<UnitAccountBudget> {
        return apiService.put<UnitAccountBudget>(`/finance/unit-budgets/${id}`, dto);
    }

    async deleteBudget(id: string): Promise<void> {
        return apiService.delete(`/finance/unit-budgets/${id}`);
    }

    // ===== ALLOCATION RULES =====
    // Backend routes live under /finance/allocations/rules (AllocationController);
    // rule execution is exposed as "run".

    async getAllocationRules(filters?: {
        isActive?: boolean;
        allocationType?: AllocationType;
    }): Promise<AllocationRule[]> {
        const queryParams = new URLSearchParams();
        if (filters?.isActive !== undefined) queryParams.append('isActive', String(filters.isActive));
        if (filters?.allocationType) queryParams.append('allocationType', filters.allocationType);

        const endpoint = `/finance/allocations/rules${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<AllocationRule[]>(endpoint);
    }

    async getAllocationRuleById(id: string): Promise<AllocationRule> {
        return apiService.get<AllocationRule>(`/finance/allocations/rules/${id}`);
    }

    async createAllocationRule(dto: CreateAllocationRuleDto): Promise<AllocationRule> {
        return apiService.post<AllocationRule>('/finance/allocations/rules', dto);
    }

    async updateAllocationRule(id: string, dto: Partial<AllocationRule>): Promise<AllocationRule> {
        return apiService.put<AllocationRule>(`/finance/allocations/rules/${id}`, dto);
    }

    async deleteAllocationRule(id: string): Promise<void> {
        return apiService.delete(`/finance/allocations/rules/${id}`);
    }

    async executeAllocationRule(id: string): Promise<void> {
        return apiService.post<void>(`/finance/allocations/rules/${id}/run`, {});
    }

    // ===== ALLOCATION TARGETS =====

    async getAllocationTargets(ruleId: string): Promise<AllocationTarget[]> {
        return apiService.get<AllocationTarget[]>(`/finance/allocations/rules/${ruleId}/targets`);
    }

    /** Replaces the rule's target distribution (validated as part of the rule contract). */
    async updateAllocationTargets(
        ruleId: string,
        targets: {
            targetAccountId: string;
            fixedPercentage?: number;
            targetDriverUnitAccountId?: string;
            costCenterCode?: string;
        }[]
    ): Promise<AllocationTarget[]> {
        return apiService.put<AllocationTarget[]>(`/finance/allocations/rules/${ruleId}/targets`, targets);
    }
}

// Export singleton instance
export const unitAccountsDataService = new UnitAccountsDataService();
