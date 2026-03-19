/**
 * Budget Data Service
 * Service layer for budgeting module API calls
 */

import type {
    BudgetScenario,
    BudgetReturn,
    BudgetEntry,
    CreateBudgetScenarioDto,
    UpdateBudgetScenarioDto,
    CreateBudgetReturnDto,
    UpdateBudgetReturnDto,
    SubmitBudgetReturnDto,
    ApproveBudgetReturnDto,
    RejectBudgetReturnDto,
    BulkSaveBudgetEntriesDto,
    BudgetSummaryDto
} from '@/types/budget';

import { apiService } from '@/services/api.service';

// =============================================================================
// BUDGET DATA SERVICE
// =============================================================================

class BudgetDataService {
    // ===== BUDGET SCENARIOS =====

    async getScenarios(): Promise<BudgetScenario[]> {
        return apiService.get<BudgetScenario[]>('/finance/budget/scenarios');
    }

    async getScenarioById(id: string): Promise<BudgetScenario> {
        return apiService.get<BudgetScenario>(`/finance/budget/scenarios/${id}`);
    }

    async createScenario(dto: CreateBudgetScenarioDto): Promise<BudgetScenario> {
        return apiService.post<BudgetScenario>('/finance/budget/scenarios', dto);
    }

    async updateScenario(id: string, dto: UpdateBudgetScenarioDto): Promise<BudgetScenario> {
        return apiService.put<BudgetScenario>(`/finance/budget/scenarios/${id}`, dto);
    }

    async deleteScenario(id: string): Promise<void> {
        return apiService.delete(`/finance/budget/scenarios/${id}`);
    }

    async lockScenario(id: string): Promise<BudgetScenario> {
        return apiService.post<BudgetScenario>(`/finance/budget/scenarios/${id}/lock`, {});
    }

    // ===== BUDGET RETURNS =====

    async getReturns(scenarioId: string): Promise<BudgetReturn[]> {
        return apiService.get<BudgetReturn[]>(`/finance/budget/scenarios/${scenarioId}/returns`);
    }

    async getMyReturns(): Promise<BudgetReturn[]> {
        return apiService.get<BudgetReturn[]>('/finance/budget/returns/my-returns');
    }

    async getReturnById(id: string): Promise<BudgetReturn> {
        return apiService.get<BudgetReturn>(`/finance/budget/returns/${id}`);
    }

    async createReturn(dto: CreateBudgetReturnDto): Promise<BudgetReturn> {
        return apiService.post<BudgetReturn>('/finance/budget/returns', dto);
    }

    async updateReturn(id: string, dto: UpdateBudgetReturnDto): Promise<BudgetReturn> {
        return apiService.put<BudgetReturn>(`/finance/budget/returns/${id}`, dto);
    }

    async submitReturn(dto: SubmitBudgetReturnDto): Promise<BudgetReturn> {
        return apiService.post<BudgetReturn>(`/finance/budget/returns/${dto.returnId}/submit`, dto);
    }

    async approveReturn(dto: ApproveBudgetReturnDto): Promise<BudgetReturn> {
        return apiService.post<BudgetReturn>(`/finance/budget/returns/${dto.returnId}/approve`, dto);
    }

    async rejectReturn(dto: RejectBudgetReturnDto): Promise<BudgetReturn> {
        return apiService.post<BudgetReturn>(`/finance/budget/returns/${dto.returnId}/reject`, dto);
    }

    // ===== BUDGET ENTRIES =====

    async getEntries(returnId: string): Promise<BudgetEntry[]> {
        return apiService.get<BudgetEntry[]>(`/finance/budget/entries/return/${returnId}`);
    }

    async bulkSaveEntries(dto: BulkSaveBudgetEntriesDto): Promise<boolean> {
        await apiService.post('/finance/budget/entries/bulk-save', dto);
        return true;
    }

    // ===== ANALYTICS =====

    async getBudgetSummary(scenarioId: string): Promise<BudgetSummaryDto> {
        return apiService.get<BudgetSummaryDto>(`/finance/budget/analytics/summary/${scenarioId}`);
    }
}

export const budgetDataService = new BudgetDataService();
