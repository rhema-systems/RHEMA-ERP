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
import { financeDataService } from '@/services/finance/finance-data.service';
import type { FiscalYear } from '@/types/finance';

// =============================================================================
// BUDGET DATA SERVICE
// =============================================================================

class BudgetDataService {
    // ===== BUDGET SCENARIOS =====

    async getScenarios(fiscalYears?: FiscalYear[]): Promise<BudgetScenario[]> {
        const years = fiscalYears ?? (await financeDataService.getFiscalYears());
        const scenarioGroups = await Promise.all(
            years.map((fy) => apiService.get<BudgetScenario[]>(`/budget/scenarios/year/${fy.id}`))
        );
        return scenarioGroups.flat();
    }

    async getScenarioById(id: string): Promise<BudgetScenario> {
        return apiService.get<BudgetScenario>(`/budget/scenarios/${id}`);
    }

    async createScenario(dto: CreateBudgetScenarioDto): Promise<BudgetScenario> {
        return apiService.post<BudgetScenario>('/budget/scenarios', dto);
    }

    async updateScenario(id: string, dto: UpdateBudgetScenarioDto): Promise<BudgetScenario> {
        return apiService.put<BudgetScenario>(`/budget/scenarios/${id}`, dto);
    }

    async deleteScenario(id: string): Promise<void> {
        return apiService.delete(`/budget/scenarios/${id}`);
    }

    async lockScenario(id: string): Promise<BudgetScenario> {
        return apiService.post<BudgetScenario>(`/budget/scenarios/${id}/lock`, {});
    }

    // ===== BUDGET RETURNS =====

    async getReturns(scenarioId: string): Promise<BudgetReturn[]> {
        return apiService.get<BudgetReturn[]>(`/budget/scenarios/${scenarioId}/returns`);
    }

    async getMyReturns(): Promise<BudgetReturn[]> {
        return apiService.get<BudgetReturn[]>('/budget/returns/my-returns');
    }

    async getReturnById(id: string): Promise<BudgetReturn> {
        return apiService.get<BudgetReturn>(`/budget/returns/${id}`);
    }

    async createReturn(dto: CreateBudgetReturnDto): Promise<BudgetReturn> {
        return apiService.post<BudgetReturn>('/budget/returns', dto);
    }

    async updateReturn(id: string, dto: UpdateBudgetReturnDto): Promise<BudgetReturn> {
        return apiService.put<BudgetReturn>(`/budget/returns/${id}`, dto);
    }

    async submitReturn(dto: SubmitBudgetReturnDto): Promise<BudgetReturn> {
        return apiService.post<BudgetReturn>(`/budget/returns/${dto.returnId}/submit`, dto);
    }

    async approveReturn(dto: ApproveBudgetReturnDto): Promise<BudgetReturn> {
        return apiService.post<BudgetReturn>(`/budget/returns/${dto.returnId}/approve`, dto);
    }

    async rejectReturn(dto: RejectBudgetReturnDto): Promise<BudgetReturn> {
        return apiService.post<BudgetReturn>(`/budget/returns/${dto.returnId}/reject`, dto);
    }

    // ===== BUDGET ENTRIES =====

    async getEntries(returnId: string): Promise<BudgetEntry[]> {
        return apiService.get<BudgetEntry[]>(`/budget/returns/${returnId}/entries`);
    }

    async bulkSaveEntries(dto: BulkSaveBudgetEntriesDto): Promise<boolean> {
        await apiService.post('/budget/entries/bulk-save', dto);
        return true;
    }

    // ===== ANALYTICS =====

    async getBudgetSummary(scenarioId: string): Promise<BudgetSummaryDto> {
        return apiService.get<BudgetSummaryDto>(`/budget/analytics/summary/${scenarioId}`);
    }
}

export const budgetDataService = new BudgetDataService();
