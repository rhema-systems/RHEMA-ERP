import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  CreateDevelopmentObjective,
  CreateDevelopmentPlan,
  CreateDevelopmentPlanFeedback,
  DevelopmentObjective,
  DevelopmentPlan,
  DevelopmentPlanFeedback,
  DevelopmentPlanStatus,
  UpdateDevelopmentObjective,
  UpdateDevelopmentPlan,
  UpdateObjectiveProgress,
} from '@/types/hr/development';

/**
 * api/DevelopmentPlans — employee development plans and their objectives.
 *
 * Reads are scoped by who is asking: `getMine` and `getMyTeam` take the employee from the token,
 * and everything keyed on an id is refused (403) unless the caller is HR, the employee, or that
 * employee's line manager. `getPaged` is HR's org-wide view and is refused for everyone else.
 *
 * Business rules come back as 422 with a readable `message` — a completed plan cannot be edited,
 * a shared plan cannot go back to draft.
 */
class DevelopmentPlanService {
  private readonly baseUrl = '/DevelopmentPlans';

  /** HR's org-wide list. 403 for anyone else. */
  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<DevelopmentPlan>> {
    return apiService.get<PagedResult<DevelopmentPlan>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<DevelopmentPlan> {
    return apiService.get<DevelopmentPlan>(`${this.baseUrl}/${id}`);
  }

  /** The signed-in employee's own plans. `[]` when the account has no employee link. */
  getMine(): Promise<DevelopmentPlan[]> {
    return apiService.get<DevelopmentPlan[]>(`${this.baseUrl}/mine`);
  }

  /** Plans belonging to the signed-in manager's direct reports. */
  getMyTeam(): Promise<DevelopmentPlan[]> {
    return apiService.get<DevelopmentPlan[]>(`${this.baseUrl}/my-team`);
  }

  getByEmployee(employeeId: string): Promise<DevelopmentPlan[]> {
    return apiService.get<DevelopmentPlan[]>(`${this.baseUrl}/by-employee/${employeeId}`);
  }

  /** 404 when the employee has no active plan — that is an answer, not an error. */
  getActive(employeeId: string, cycleId?: string): Promise<DevelopmentPlan> {
    return apiService.get<DevelopmentPlan>(
      `${this.baseUrl}/active/${employeeId}`,
      cycleId ? { cycleId } : undefined,
    );
  }

  create(data: CreateDevelopmentPlan): Promise<DevelopmentPlan> {
    return apiService.post<DevelopmentPlan>(this.baseUrl, data);
  }

  update(id: string, data: UpdateDevelopmentPlan): Promise<DevelopmentPlan> {
    return apiService.put<DevelopmentPlan>(`${this.baseUrl}/${id}`, data);
  }

  delete(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Activating is what tells the employee the plan is theirs to work on. */
  updateStatus(id: string, status: DevelopmentPlanStatus): Promise<void> {
    return apiService.patch<void>(`${this.baseUrl}/${id}/status`, { status });
  }

  // ── Objectives ──────────────────────────────────────────────────────────────

  getObjectives(planId: string): Promise<DevelopmentObjective[]> {
    return apiService.get<DevelopmentObjective[]>(`${this.baseUrl}/${planId}/objectives`);
  }

  addObjective(planId: string, data: CreateDevelopmentObjective): Promise<DevelopmentObjective> {
    return apiService.post<DevelopmentObjective>(`${this.baseUrl}/${planId}/objectives`, data);
  }

  updateObjective(
    planId: string,
    objectiveId: string,
    data: UpdateDevelopmentObjective,
  ): Promise<DevelopmentObjective> {
    return apiService.put<DevelopmentObjective>(
      `${this.baseUrl}/${planId}/objectives/${objectiveId}`,
      data,
    );
  }

  deleteObjective(planId: string, objectiveId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${planId}/objectives/${objectiveId}`);
  }

  updateObjectiveProgress(
    planId: string,
    objectiveId: string,
    data: UpdateObjectiveProgress,
  ): Promise<DevelopmentObjective> {
    return apiService.patch<DevelopmentObjective>(
      `${this.baseUrl}/${planId}/objectives/${objectiveId}/progress`,
      data,
    );
  }
}

/**
 * api/DevelopmentPlanFeedback — the manager's running commentary on a plan.
 *
 * The author is taken from the token, so `managerId` on the create payload is ignored. Adding
 * feedback notifies the employee; only its author (or HR) can withdraw it.
 */
class DevelopmentPlanFeedbackService {
  private readonly baseUrl = '/DevelopmentPlanFeedback';

  getByPlan(planId: string): Promise<DevelopmentPlanFeedback[]> {
    return apiService.get<DevelopmentPlanFeedback[]>(`${this.baseUrl}/by-plan/${planId}`);
  }

  add(data: CreateDevelopmentPlanFeedback): Promise<DevelopmentPlanFeedback> {
    return apiService.post<DevelopmentPlanFeedback>(this.baseUrl, data);
  }

  delete(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const developmentPlanService = new DevelopmentPlanService();
export const developmentPlanFeedbackService = new DevelopmentPlanFeedbackService();
