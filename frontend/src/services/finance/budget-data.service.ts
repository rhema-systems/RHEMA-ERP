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
    BulkSaveBudgetEntriesDto,
    BudgetSummaryDto,
    BudgetAuditEvent,
    AdoptBudgetScenarioDto,
    ConsolidatedBudgetView,
    BudgetScenarioComparison
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

    async getScenariosForYear(fiscalYearId: string): Promise<BudgetScenario[]> {
        return apiService.get<BudgetScenario[]>(`/budget/scenarios/year/${fiscalYearId}`);
    }

    async createScenario(dto: CreateBudgetScenarioDto): Promise<BudgetScenario> {
        return apiService.post<BudgetScenario>('/budget/scenarios', dto);
    }

    async updateScenario(id: string, dto: UpdateBudgetScenarioDto): Promise<BudgetScenario> {
        return apiService.put<BudgetScenario>(`/budget/scenarios/${id}`, { ...dto, id });
    }

    async deleteScenario(id: string): Promise<void> {
        return apiService.delete(`/budget/scenarios/${id}`);
    }

    async openScenario(id: string, rowVersion: string): Promise<BudgetScenario> {
        return apiService.post<BudgetScenario>(`/budget/scenarios/${id}/open`, { rowVersion });
    }

    async submitScenario(id: string, rowVersion: string): Promise<BudgetScenario> {
        return apiService.post<BudgetScenario>(`/budget/scenarios/${id}/submit`, { rowVersion });
    }

    async archiveScenario(id: string, rowVersion: string): Promise<BudgetScenario> {
        return apiService.post<BudgetScenario>(`/budget/scenarios/${id}/archive`, { rowVersion });
    }

    async adoptScenario(id: string, dto: AdoptBudgetScenarioDto): Promise<BudgetScenario> {
        return apiService.post<BudgetScenario>(`/budget/scenarios/${id}/adopt`, dto);
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

    async recallReturn(returnId: string, rowVersion: string, reason?: string): Promise<BudgetReturn> {
        return apiService.post<BudgetReturn>(`/budget/returns/${returnId}/recall`, {
            rowVersion,
            reason,
        });
    }

    // ===== BUDGET ENTRIES =====

    async getEntries(returnId: string): Promise<BudgetEntry[]> {
        return apiService.get<BudgetEntry[]>(`/budget/returns/${returnId}/entries`);
    }

    async bulkSaveEntries(dto: BulkSaveBudgetEntriesDto): Promise<BudgetReturn> {
        return apiService.post<BudgetReturn>('/budget/entries/bulk-save', {
            budgetReturnId: dto.returnId,
            returnRowVersion: dto.returnRowVersion,
            entries: dto.entries,
        });
    }

    async getScenarioAuditHistory(id: string): Promise<BudgetAuditEvent[]> {
        return apiService.get<BudgetAuditEvent[]>(`/budget/scenarios/${id}/audit-history`);
    }

    async getReturnAuditHistory(id: string): Promise<BudgetAuditEvent[]> {
        return apiService.get<BudgetAuditEvent[]>(`/budget/returns/${id}/audit-history`);
    }

    // ===== ANALYTICS =====

    async getBudgetSummary(scenarioId: string): Promise<BudgetSummaryDto> {
        return apiService.get<BudgetSummaryDto>(`/budget/analytics/summary/${scenarioId}`);
    }

    async getConsolidatedView(
        scenarioId: string,
        approvedOnly: boolean
    ): Promise<ConsolidatedBudgetView> {
        return apiService.get<ConsolidatedBudgetView>(
            `/budget/scenarios/${scenarioId}/consolidated?approvedOnly=${approvedOnly}`
        );
    }

    async getActiveBudgetVsActual(fiscalYearId: string): Promise<ConsolidatedBudgetView> {
        return apiService.get<ConsolidatedBudgetView>(
            `/budget/analytics/budget-vs-actual/fiscal-year/${fiscalYearId}`
        );
    }

    async compareScenarios(
        baseScenarioId: string,
        comparisonScenarioId: string
    ): Promise<BudgetScenarioComparison> {
        return apiService.get<BudgetScenarioComparison>(
            `/budget/scenarios/${baseScenarioId}/compare/${comparisonScenarioId}`
        );
    }
}

export const budgetDataService = new BudgetDataService();
