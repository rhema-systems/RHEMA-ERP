import { apiService } from '../api.service';
import type {
  TrainingPlan,
  TrainingPlanSummary,
  TrainingPlanRequest,
  TrainingPlanItem,
  TrainingPlanItemCreateRequest,
  TrainingPlanItemUpdateRequest,
  TrainingPlanBudgetLine,
  TrainingPlanBudgetLineCreateRequest,
  TrainingPlanBudgetLineUpdateRequest,
} from '@/types/hr/training';

/**
 * CRUD + submit/approve workflow for training plans, plus their items and budget-lines
 * sub-resources. Backend route: api/training-plans.
 */
class TrainingPlanService {
  private readonly baseUrl = '/training-plans';

  getAll(): Promise<TrainingPlanSummary[]> {
    return apiService.get<TrainingPlanSummary[]>(this.baseUrl);
  }

  getById(id: string): Promise<TrainingPlan> {
    return apiService.get<TrainingPlan>(`${this.baseUrl}/${id}`);
  }

  getByYear(year: number): Promise<TrainingPlanSummary[]> {
    return apiService.get<TrainingPlanSummary[]>(`${this.baseUrl}/year/${year}`);
  }

  getByStatus(status: string): Promise<TrainingPlanSummary[]> {
    return apiService.get<TrainingPlanSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  create(data: TrainingPlanRequest): Promise<TrainingPlan> {
    return apiService.post<TrainingPlan>(this.baseUrl, data);
  }

  update(id: string, data: TrainingPlanRequest): Promise<TrainingPlan> {
    return apiService.put<TrainingPlan>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Workflow ──────────────────────────────────────────────────────────────

  submit(id: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/submit`, {});
  }

  approve(id: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${id}/approve`, { planId: id });
  }

  // ── Items ─────────────────────────────────────────────────────────────────

  getItems(planId: string): Promise<TrainingPlanItem[]> {
    return apiService.get<TrainingPlanItem[]>(`${this.baseUrl}/${planId}/items`);
  }

  addItem(planId: string, data: Omit<TrainingPlanItemCreateRequest, 'planId'>): Promise<TrainingPlanItem> {
    return apiService.post<TrainingPlanItem>(`${this.baseUrl}/${planId}/items`, { ...data, planId });
  }

  updateItem(itemId: string, data: TrainingPlanItemUpdateRequest): Promise<TrainingPlanItem> {
    return apiService.put<TrainingPlanItem>(`${this.baseUrl}/items/${itemId}`, { id: itemId, ...data });
  }

  removeItem(itemId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/items/${itemId}`);
  }

  // ── Budget lines ──────────────────────────────────────────────────────────

  getBudgetLines(planId: string): Promise<TrainingPlanBudgetLine[]> {
    return apiService.get<TrainingPlanBudgetLine[]>(`${this.baseUrl}/${planId}/budget-lines`);
  }

  addBudgetLine(
    planId: string,
    data: Omit<TrainingPlanBudgetLineCreateRequest, 'planId'>,
  ): Promise<TrainingPlanBudgetLine> {
    return apiService.post<TrainingPlanBudgetLine>(`${this.baseUrl}/${planId}/budget-lines`, {
      ...data,
      planId,
    });
  }

  updateBudgetLine(lineId: string, data: TrainingPlanBudgetLineUpdateRequest): Promise<TrainingPlanBudgetLine> {
    return apiService.put<TrainingPlanBudgetLine>(`${this.baseUrl}/budget-lines/${lineId}`, {
      id: lineId,
      ...data,
    });
  }

  removeBudgetLine(lineId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/budget-lines/${lineId}`);
  }
}

export const trainingPlanService = new TrainingPlanService();
