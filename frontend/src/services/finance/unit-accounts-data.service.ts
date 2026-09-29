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
    BudgetVariance,
    RatioCalculationResult,
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
    UpdateAllocationRuleDto,
    RunAllocationDto,
    AllocationRunResult,
    AllocationRunBatch,
    AllocationRunBatchStatus,
    CreateAllocationRunBatchDto,
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
        const endpoint = filters?.isActive === true
            ? '/finance/unit-types/active'
            : '/finance/unit-types';
        const unitTypes = await apiService.get<UnitType[]>(endpoint);
        return filters?.isActive === undefined
            ? unitTypes
            : unitTypes.filter((unitType) => unitType.isActive === filters.isActive);
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

    async setUnitTypeActive(id: string, isActive: boolean): Promise<void> {
        return apiService.patch(`/finance/unit-types/${id}/${isActive ? 'activate' : 'deactivate'}`, {});
    }

    // ===== UNIT ACCOUNTS =====

    async getUnitAccounts(filters?: {
        unitTypeId?: string;
        isActive?: boolean;
        isPostingAccount?: boolean;
    }): Promise<UnitAccount[]> {
        let accounts: UnitAccount[];

        if (filters?.unitTypeId) {
            accounts = await apiService.get<UnitAccount[]>(`/finance/unit-accounts/by-type/${filters.unitTypeId}`);
        } else if (filters?.isPostingAccount) {
            accounts = await apiService.get<UnitAccount[]>('/finance/unit-accounts/posting');
        } else {
            accounts = await apiService.get<UnitAccount[]>('/finance/unit-accounts');
        }

        // The backend exposes purpose-built routes rather than query filters here.
        // Keep the remaining filters client-side so callers get a stable service contract.
        return accounts.filter((account) => {
            const matchesPosting =
                filters?.isPostingAccount === undefined || account.isPostingAccount === filters.isPostingAccount;
            const matchesActive =
                filters?.isActive === undefined || account.isActive === filters.isActive;
            return matchesPosting && matchesActive;
        });
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
        fiscalPeriodId?: string;
    }): Promise<UnitJournalEntry[]> {
        const queryParams = new URLSearchParams();
        if (filters?.startDate) queryParams.append('startDate', filters.startDate);
        if (filters?.endDate) queryParams.append('endDate', filters.endDate);
        if (filters?.status) queryParams.append('status', filters.status);
        if (filters?.fiscalPeriodId) queryParams.append('fiscalPeriodId', filters.fiscalPeriodId);

        const basePath = queryParams.toString()
            ? '/finance/unit-journal-entries/filter'
            : '/finance/unit-journal-entries';
        const endpoint = `${basePath}${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<UnitJournalEntry[]>(endpoint);
    }

    async getUnitJournalEntryById(id: string): Promise<UnitJournalEntry> {
        return apiService.get<UnitJournalEntry>(`/finance/unit-journal-entries/${id}`);
    }

    async getPendingUnitJournalApprovals(): Promise<UnitJournalEntry[]> {
        return apiService.get<UnitJournalEntry[]>('/finance/unit-journal-entries/pending-approval');
    }

    async createUnitJournalEntry(dto: CreateUnitJournalEntryDto): Promise<UnitJournalEntry> {
        return apiService.post<UnitJournalEntry>('/finance/unit-journal-entries', dto);
    }

    async postUnitJournalEntry(id: string): Promise<UnitJournalEntry> {
        return apiService.post<UnitJournalEntry>(`/finance/unit-journal-entries/${id}/post`, {});
    }

    async submitUnitJournalEntry(id: string): Promise<UnitJournalEntry> {
        return apiService.post<UnitJournalEntry>(`/finance/unit-journal-entries/${id}/submit`, {});
    }

    async approveUnitJournalEntry(id: string): Promise<UnitJournalEntry> {
        return apiService.post<UnitJournalEntry>(`/finance/unit-journal-entries/${id}/approve`, {});
    }

    async rejectUnitJournalEntry(id: string, reason: string): Promise<UnitJournalEntry> {
        return apiService.post<UnitJournalEntry>(`/finance/unit-journal-entries/${id}/reject`, { reason });
    }

    async reverseUnitJournalEntry(id: string, reason: string): Promise<UnitJournalEntry> {
        return apiService.post<UnitJournalEntry>(`/finance/unit-journal-entries/${id}/reverse`, { reason });
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
        if (!filters?.unitAccountId) {
            throw new Error('unitAccountId is required when loading unit account balances.');
        }

        const queryParams = new URLSearchParams();
        if (filters.fiscalYearId) queryParams.append('fiscalYearId', filters.fiscalYearId);

        const endpoint = `/finance/unit-accounts/${filters.unitAccountId}/balances${queryParams.toString() ? `?${queryParams}` : ''}`;
        const balances = await apiService.get<UnitAccountBalance[]>(endpoint);

        return filters.fiscalPeriodId
            ? balances.filter((balance) => balance.fiscalPeriodId === filters.fiscalPeriodId)
            : balances;
    }

    // ===== RATIO DEFINITIONS =====

    async getRatioDefinitions(filters?: {
        isActive?: boolean;
    }): Promise<RatioDefinition[]> {
        const endpoint = filters?.isActive === true
            ? '/finance/ratio-definitions/active'
            : '/finance/ratio-definitions';
        const ratios = await apiService.get<RatioDefinition[]>(endpoint);
        return filters?.isActive === undefined
            ? ratios
            : ratios.filter((ratio) => ratio.isActive === filters.isActive);
    }

    async getRatioDefinitionById(id: string): Promise<RatioDefinition> {
        return apiService.get<RatioDefinition>(`/finance/ratio-definitions/${id}`);
    }

    async createRatioDefinition(dto: CreateRatioDefinitionDto): Promise<RatioDefinition> {
        const payload = {
            ...dto,
            numeratorConstantValue: dto.numeratorConstantValue ?? dto.numeratorConstant,
            denominatorConstantValue: dto.denominatorConstantValue ?? dto.denominatorConstant,
            formatPrecision: dto.formatPrecision ?? dto.decimalPlaces ?? 2,
        };

        delete (payload as any).numeratorConstant;
        delete (payload as any).denominatorConstant;
        delete (payload as any).decimalPlaces;

        return apiService.post<RatioDefinition>('/finance/ratio-definitions', payload);
    }

    async updateRatioDefinition(id: string, dto: Partial<RatioDefinition>): Promise<RatioDefinition> {
        return apiService.put<RatioDefinition>(`/finance/ratio-definitions/${id}`, dto);
    }

    async deleteRatioDefinition(id: string): Promise<void> {
        return apiService.delete(`/finance/ratio-definitions/${id}`);
    }

    async setRatioDefinitionActive(id: string, isActive: boolean): Promise<void> {
        return apiService.patch(`/finance/ratio-definitions/${id}/${isActive ? 'activate' : 'deactivate'}`, {});
    }

    async calculateRatio(id: string, fiscalPeriodId: string): Promise<RatioCalculationResult> {
        return apiService.get<RatioCalculationResult>(
            `/finance/ratio-definitions/${id}/calculate?fiscalPeriodId=${encodeURIComponent(fiscalPeriodId)}`
        );
    }

    async calculateRatioForRange(id: string, startDate: string, endDate: string): Promise<RatioCalculationResult> {
        const query = new URLSearchParams({ startDate, endDate });
        return apiService.get<RatioCalculationResult>(`/finance/ratio-definitions/${id}/calculate-range?${query}`);
    }

    // ===== UNIT ACCOUNT BUDGETS =====

    async getUnitAccountBudgets(filters?: {
        unitAccountId?: string;
        fiscalYearId?: string;
    }): Promise<UnitAccountBudget[]> {
        const endpoint = filters?.unitAccountId
            ? `/finance/unit-budgets/by-account/${filters.unitAccountId}`
            : '/finance/unit-budgets';
        const budgets = await apiService.get<UnitAccountBudget[]>(endpoint);
        return filters?.fiscalYearId
            ? budgets.filter((budget) => budget.fiscalYearId === filters.fiscalYearId)
            : budgets;
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

    async getBudgetVariances(fiscalPeriodId?: string): Promise<BudgetVariance[]> {
        const query = fiscalPeriodId ? `?periodId=${encodeURIComponent(fiscalPeriodId)}` : '';
        return apiService.get<BudgetVariance[]>(`/finance/unit-budgets/variances${query}`);
    }

    // ===== ALLOCATION RULES =====
    // Backend routes live under /finance/allocations/rules (AllocationController);
    // rule execution is exposed as "run".

    async getAllocationRules(filters?: {
        isActive?: boolean;
        allocationType?: AllocationType;
    }): Promise<AllocationRule[]> {
        const endpoint = filters?.isActive === true
            ? '/finance/allocations/rules/active'
            : '/finance/allocations/rules';
        const rules = await apiService.get<AllocationRule[]>(endpoint);
        return rules.filter((rule) => {
            const matchesActive = filters?.isActive === undefined || rule.isActive === filters.isActive;
            const matchesType = filters?.allocationType === undefined || rule.allocationType === filters.allocationType;
            return matchesActive && matchesType;
        });
    }

    async getAllocationRuleById(id: string): Promise<AllocationRule> {
        return apiService.get<AllocationRule>(`/finance/allocations/rules/${id}`);
    }

    async createAllocationRule(dto: CreateAllocationRuleDto): Promise<AllocationRule> {
        return apiService.post<AllocationRule>('/finance/allocations/rules', dto);
    }

    async updateAllocationRule(id: string, dto: UpdateAllocationRuleDto): Promise<AllocationRule> {
        return apiService.put<AllocationRule>(`/finance/allocations/rules/${id}`, dto);
    }

    async deleteAllocationRule(id: string): Promise<void> {
        return apiService.delete(`/finance/allocations/rules/${id}`);
    }

    async runAllocationRule(id: string, dto: Omit<RunAllocationDto, 'allocationRuleId'>): Promise<AllocationRunResult> {
        return apiService.post<AllocationRunResult>(`/finance/allocations/rules/${id}/run`, {
            ...dto,
            allocationRuleId: id,
        });
    }

    async executeAllocationRule(id: string, dto: Omit<RunAllocationDto, 'allocationRuleId'>): Promise<AllocationRunResult> {
        return this.runAllocationRule(id, dto);
    }

    async getAllocationRunBatches(filters?: {
        status?: AllocationRunBatchStatus;
    }): Promise<AllocationRunBatch[]> {
        const queryParams = new URLSearchParams();
        if (filters?.status) queryParams.append('status', filters.status);

        const endpoint = `/finance/allocations/runs${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<AllocationRunBatch[]>(endpoint);
    }

    async getAllocationRunBatchById(id: string): Promise<AllocationRunBatch> {
        return apiService.get<AllocationRunBatch>(`/finance/allocations/runs/${id}`);
    }

    async createAllocationRunBatch(
        ruleId: string,
        dto: Omit<CreateAllocationRunBatchDto, 'allocationRuleId'>
    ): Promise<AllocationRunBatch> {
        return apiService.post<AllocationRunBatch>(`/finance/allocations/rules/${ruleId}/runs`, {
            ...dto,
            allocationRuleId: ruleId,
        });
    }

    async submitAllocationRunBatch(id: string, comment?: string): Promise<AllocationRunBatch> {
        return apiService.post<AllocationRunBatch>(`/finance/allocations/runs/${id}/submit`, { comment });
    }

    async approveAllocationRunBatch(id: string, comment?: string): Promise<AllocationRunBatch> {
        return apiService.post<AllocationRunBatch>(`/finance/allocations/runs/${id}/approve`, { comment });
    }

    async rejectAllocationRunBatch(id: string, reason: string): Promise<AllocationRunBatch> {
        return apiService.post<AllocationRunBatch>(`/finance/allocations/runs/${id}/reject`, { reason });
    }

    async postAllocationRunBatch(id: string): Promise<AllocationRunBatch> {
        return apiService.post<AllocationRunBatch>(`/finance/allocations/runs/${id}/post`, {});
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
