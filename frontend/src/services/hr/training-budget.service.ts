import { apiService } from '../api.service';
import type {
  TrainingBudget,
  TrainingBudgetSummary,
  TrainingBudgetCreateRequest,
  TrainingBudgetUpdateRequest,
  TrainingBudgetTransaction,
  TrainingBudgetTransactionCreateRequest,
} from '@/types/hr/training';

/**
 * CRUD + approve workflow for training budgets, plus their spend-transaction sub-resource.
 * Backend route: api/training-budgets.
 */
class TrainingBudgetService {
  private readonly baseUrl = '/training-budgets';

  getAll(): Promise<TrainingBudgetSummary[]> {
    return apiService.get<TrainingBudgetSummary[]>(this.baseUrl);
  }

  getById(id: string): Promise<TrainingBudget> {
    return apiService.get<TrainingBudget>(`${this.baseUrl}/${id}`);
  }

  getByBudgetCode(budgetCode: string): Promise<TrainingBudget | null> {
    return apiService.get<TrainingBudget | null>(`${this.baseUrl}/code/${budgetCode}`);
  }

  getByYear(year: number): Promise<TrainingBudgetSummary[]> {
    return apiService.get<TrainingBudgetSummary[]>(`${this.baseUrl}/year/${year}`);
  }

  getByYearAndQuarter(year: number, quarter: number): Promise<TrainingBudgetSummary[]> {
    return apiService.get<TrainingBudgetSummary[]>(`${this.baseUrl}/year/${year}/quarter/${quarter}`);
  }

  getApproved(): Promise<TrainingBudgetSummary[]> {
    return apiService.get<TrainingBudgetSummary[]>(`${this.baseUrl}/approved`);
  }

  getOverBudget(): Promise<TrainingBudgetSummary[]> {
    return apiService.get<TrainingBudgetSummary[]>(`${this.baseUrl}/over-budget`);
  }

  getByOrganizationUnitId(orgUnitId: string): Promise<TrainingBudgetSummary[]> {
    return apiService.get<TrainingBudgetSummary[]>(`${this.baseUrl}/org-unit/${orgUnitId}`);
  }

  create(data: TrainingBudgetCreateRequest): Promise<TrainingBudget> {
    return apiService.post<TrainingBudget>(this.baseUrl, data);
  }

  update(id: string, data: TrainingBudgetUpdateRequest): Promise<TrainingBudget> {
    return apiService.put<TrainingBudget>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Workflow ──────────────────────────────────────────────────────────────

  approve(id: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/approve`, { budgetId: id });
  }

  // ── Transactions ──────────────────────────────────────────────────────────

  getTransactions(budgetId: string): Promise<TrainingBudgetTransaction[]> {
    return apiService.get<TrainingBudgetTransaction[]>(`${this.baseUrl}/${budgetId}/transactions`);
  }

  getTransactionsByDateRange(
    budgetId: string,
    from: string,
    to: string,
  ): Promise<TrainingBudgetTransaction[]> {
    return apiService.get<TrainingBudgetTransaction[]>(`${this.baseUrl}/${budgetId}/transactions/date-range`, {
      from,
      to,
    });
  }

  recordTransaction(
    budgetId: string,
    data: Omit<TrainingBudgetTransactionCreateRequest, 'budgetId'>,
  ): Promise<TrainingBudgetTransaction> {
    return apiService.post<TrainingBudgetTransaction>(`${this.baseUrl}/${budgetId}/transactions`, {
      ...data,
      budgetId,
    });
  }
}

export const trainingBudgetService = new TrainingBudgetService();
