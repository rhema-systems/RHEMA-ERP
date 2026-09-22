import { apiService } from '../api.service';
import type {
  OnboardingPlan,
  OnboardingPlanDetail,
  OnboardingPlanSummary,
  OnboardingPlanCreateRequest,
  OnboardingPlanUpdateRequest,
  OnboardingStatus,
  OnboardingPlanTemplate,
  OnboardingPlanTemplateDetail,
  OnboardingPlanTemplateSummary,
  OnboardingPlanTemplateCreateRequest,
  OnboardingPlanTemplateUpdateRequest,
  OnboardingTaskTemplate,
  OnboardingTaskTemplateCreateRequest,
  OnboardingTaskTemplateUpdateRequest,
  OnboardingTask,
  OnboardingTaskStatus,
  OnboardingTaskCreateRequest,
  OnboardingTaskUpdateRequest,
  CompleteOnboardingTaskRequest,
  VerifyOnboardingTaskRequest,
  OnboardingTaskComment,
  OnboardingTaskCommentCreateRequest,
  OnboardingAsset,
  OnboardingAssetProvisionStatus,
  OnboardingAssetCreateRequest,
  OnboardingAssetUpdateRequest,
} from '@/types/hr/onboarding';
import type {
  HrAudienceTargetType,
  OnboardingPlanTemplateAudience,
  OnboardingTemplateApplicability,
} from '@/types/hr/orientation';

/**
 * Onboarding plan templates. Backend route: api/onboarding-plan-templates. HR-only.
 *
 * ⚠ There is no by-position lookup. Templates carry no link to a position, so the endpoint that
 * claimed to filter by one was removed rather than left returning everything. Pick from `getAll()`,
 * or fall back to `getDefault()`.
 */
class OnboardingPlanTemplateService {
  private readonly baseUrl = '/onboarding-plan-templates';

  getById(id: string): Promise<OnboardingPlanTemplate> {
    return apiService.get<OnboardingPlanTemplate>(`${this.baseUrl}/${id}`);
  }

  /** Active templates only — a deactivated template is not offered for new plans. */
  getAll(): Promise<OnboardingPlanTemplateSummary[]> {
    return apiService.get<OnboardingPlanTemplateSummary[]>(`${this.baseUrl}/all`);
  }

  getWithTasks(id: string): Promise<OnboardingPlanTemplateDetail> {
    return apiService.get<OnboardingPlanTemplateDetail>(`${this.baseUrl}/${id}/with-tasks`);
  }

  getDefault(): Promise<OnboardingPlanTemplate | null> {
    return apiService.get<OnboardingPlanTemplate | null>(`${this.baseUrl}/default`);
  }

  /** Marking one default clears the flag on whichever template held it. */
  create(data: OnboardingPlanTemplateCreateRequest): Promise<OnboardingPlanTemplate> {
    return apiService.post<OnboardingPlanTemplate>(this.baseUrl, data);
  }

  update(id: string, data: OnboardingPlanTemplateUpdateRequest): Promise<OnboardingPlanTemplate> {
    return apiService.put<OnboardingPlanTemplate>(`${this.baseUrl}/${id}`, data);
  }

  /** Refused with 422 for the default template — assign a new default first. */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  getTaskTemplates(planTemplateId: string): Promise<OnboardingTaskTemplate[]> {
    return apiService.get<OnboardingTaskTemplate[]>(
      `${this.baseUrl}/${planTemplateId}/task-templates`,
    );
  }

  addTaskTemplate(
    planTemplateId: string,
    data: OnboardingTaskTemplateCreateRequest,
  ): Promise<OnboardingTaskTemplate> {
    return apiService.post<OnboardingTaskTemplate>(
      `${this.baseUrl}/${planTemplateId}/task-templates`,
      data,
    );
  }

  updateTaskTemplate(
    taskTemplateId: string,
    data: OnboardingTaskTemplateUpdateRequest,
  ): Promise<OnboardingTaskTemplate> {
    return apiService.put<OnboardingTaskTemplate>(
      `${this.baseUrl}/task-templates/${taskTemplateId}`,
      data,
    );
  }

  removeTaskTemplate(taskTemplateId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/task-templates/${taskTemplateId}`);
  }

  // ── Who a template is for (round 4, lane I4) ──────────────────────────────

  getAudiences(templateId: string): Promise<OnboardingPlanTemplateAudience[]> {
    return apiService.get<OnboardingPlanTemplateAudience[]>(`${this.baseUrl}/${templateId}/audiences`);
  }

  addAudience(
    templateId: string,
    data: { targetType: HrAudienceTargetType; targetEntityId?: string | null; isInclusive: boolean },
  ): Promise<OnboardingPlanTemplateAudience> {
    return apiService.post<OnboardingPlanTemplateAudience>(`${this.baseUrl}/${templateId}/audiences`, data);
  }

  removeAudience(templateId: string, audienceId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${templateId}/audiences/${audienceId}`);
  }

  /** Which template a placement (or an employee's current placement) would get, and why. */
  getApplicable(query: {
    employeeId?: string;
    positionId?: string;
    organizationUnitId?: string;
    organizationLevelId?: string;
    locationId?: string;
  }): Promise<OnboardingTemplateApplicability> {
    const qs = new URLSearchParams(
      Object.entries(query).filter(([, v]) => !!v) as [string, string][],
    ).toString();
    return apiService.get<OnboardingTemplateApplicability>(`${this.baseUrl}/applicable${qs ? `?${qs}` : ''}`);
  }
}

/**
 * Onboarding plans, tasks and provisioned assets. Backend route: api/onboarding-plans. HR-only —
 * a new hire's own view and an assignee's task queue belong to the employee self-service portal.
 */
class OnboardingPlanService {
  private readonly baseUrl = '/onboarding-plans';

  // ── Plans ─────────────────────────────────────────────────────────────────

  getById(id: string): Promise<OnboardingPlan> {
    return apiService.get<OnboardingPlan>(`${this.baseUrl}/${id}`);
  }

  /** The employee's in-progress plan, else their most recent. Null when they have none. */
  getByEmployee(employeeId: string): Promise<OnboardingPlan | null> {
    return apiService.get<OnboardingPlan | null>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getWithDetails(id: string): Promise<OnboardingPlanDetail> {
    return apiService.get<OnboardingPlanDetail>(`${this.baseUrl}/${id}/details`);
  }

  getByStatus(status: OnboardingStatus): Promise<OnboardingPlanSummary[]> {
    return apiService.get<OnboardingPlanSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getOverdueTasksCount(planId: string): Promise<number> {
    return apiService.get<number>(`${this.baseUrl}/${planId}/overdue-tasks/count`);
  }

  /**
   * Supplying `templatePlanId` instantiates the template's tasks onto the new plan, each due at
   * startDate + its dueDaysFromStartDate. The tasks are copied at creation, so later edits to the
   * template do not rewrite a plan already in flight.
   */
  create(data: OnboardingPlanCreateRequest): Promise<OnboardingPlan> {
    return apiService.post<OnboardingPlan>(this.baseUrl, data);
  }

  /** Refused with 422 once the plan is Completed. */
  update(id: string, data: OnboardingPlanUpdateRequest): Promise<OnboardingPlan> {
    return apiService.put<OnboardingPlan>(`${this.baseUrl}/${id}`, data);
  }

  start(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/start`);
  }

  complete(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/complete`);
  }

  // ── Tasks ─────────────────────────────────────────────────────────────────

  getTasks(planId: string): Promise<OnboardingTask[]> {
    return apiService.get<OnboardingTask[]>(`${this.baseUrl}/${planId}/tasks`);
  }

  getTasksByStatus(planId: string, status: OnboardingTaskStatus): Promise<OnboardingTask[]> {
    return apiService.get<OnboardingTask[]>(`${this.baseUrl}/${planId}/tasks/status/${status}`);
  }

  getAllTasksByStatus(status: OnboardingTaskStatus): Promise<OnboardingTask[]> {
    return apiService.get<OnboardingTask[]>(`${this.baseUrl}/tasks/status/${status}`);
  }

  getOverdueTasks(): Promise<OnboardingTask[]> {
    return apiService.get<OnboardingTask[]>(`${this.baseUrl}/tasks/overdue`);
  }

  getTasksByAssignee(employeeId: string): Promise<OnboardingTask[]> {
    return apiService.get<OnboardingTask[]>(`${this.baseUrl}/tasks/assignee/${employeeId}`);
  }

  addTask(planId: string, data: OnboardingTaskCreateRequest): Promise<OnboardingTask> {
    return apiService.post<OnboardingTask>(`${this.baseUrl}/${planId}/tasks`, data);
  }

  updateTask(taskId: string, data: OnboardingTaskUpdateRequest): Promise<OnboardingTask> {
    return apiService.put<OnboardingTask>(`${this.baseUrl}/tasks/${taskId}`, data);
  }

  /**
   * A task flagged `requiresVerification` lands in `PendingVerification`, not `Completed` — check the
   * returned status rather than assuming the task is finished.
   */
  completeTask(taskId: string, data: CompleteOnboardingTaskRequest): Promise<OnboardingTask> {
    return apiService.post<OnboardingTask>(`${this.baseUrl}/tasks/${taskId}/complete`, data);
  }

  /** Refused with 422 if the task is not yet complete, or if the verifier completed it themselves. */
  verifyTask(taskId: string, data: VerifyOnboardingTaskRequest): Promise<OnboardingTask> {
    return apiService.post<OnboardingTask>(`${this.baseUrl}/tasks/${taskId}/verify`, data);
  }

  // ── Task comments ─────────────────────────────────────────────────────────

  getTaskComments(taskId: string): Promise<OnboardingTaskComment[]> {
    return apiService.get<OnboardingTaskComment[]>(`${this.baseUrl}/tasks/${taskId}/comments`);
  }

  addTaskComment(
    taskId: string,
    data: OnboardingTaskCommentCreateRequest,
  ): Promise<OnboardingTaskComment> {
    return apiService.post<OnboardingTaskComment>(`${this.baseUrl}/tasks/${taskId}/comments`, data);
  }

  // ── Assets ────────────────────────────────────────────────────────────────

  getAssets(planId: string): Promise<OnboardingAsset[]> {
    return apiService.get<OnboardingAsset[]>(`${this.baseUrl}/${planId}/assets`);
  }

  getAssetsByStatus(
    planId: string,
    status: OnboardingAssetProvisionStatus,
  ): Promise<OnboardingAsset[]> {
    return apiService.get<OnboardingAsset[]>(`${this.baseUrl}/${planId}/assets/status/${status}`);
  }

  getAllAssetsByStatus(status: OnboardingAssetProvisionStatus): Promise<OnboardingAsset[]> {
    return apiService.get<OnboardingAsset[]>(`${this.baseUrl}/assets/status/${status}`);
  }

  addAsset(planId: string, data: OnboardingAssetCreateRequest): Promise<OnboardingAsset> {
    return apiService.post<OnboardingAsset>(`${this.baseUrl}/${planId}/assets`, data);
  }

  updateAsset(assetId: string, data: OnboardingAssetUpdateRequest): Promise<OnboardingAsset> {
    return apiService.put<OnboardingAsset>(`${this.baseUrl}/assets/${assetId}`, data);
  }
}

export const onboardingPlanTemplateService = new OnboardingPlanTemplateService();
export const onboardingPlanService = new OnboardingPlanService();
