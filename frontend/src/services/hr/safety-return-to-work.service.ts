import { apiService } from '../api.service';
import type {
  SheReturnToWorkPlan,
  SheReturnToWorkPlanSummary,
  SheReturnToWorkPlanCreateRequest,
  SheReturnToWorkPlanUpdateRequest,
  SheReturnToWorkStatus,
  SheReturnToWorkPhase,
  SheReturnToWorkPhaseCreateRequest,
  SheReturnToWorkPhaseUpdateRequest,
  SheReturnToWorkReview,
  SheReturnToWorkReviewCreateRequest,
} from '@/types/hr/safety-health';

/**
 * Return-to-work plans after injury or illness, with phased duties and periodic reviews.
 * Backend route: api/safety/return-to-work.
 *
 * ⚠ Gated on the HR.Medical.* policies — plans carry medical restrictions and clearance
 * notes. Duplicate plan numbers are refused (422); phase and review numbers are
 * server-assigned sequences (client values are ignored). Reviews are add-only.
 */
class SafetyReturnToWorkService {
  private readonly baseUrl = '/safety/return-to-work';

  getAll(): Promise<SheReturnToWorkPlanSummary[]> {
    return apiService.get<SheReturnToWorkPlanSummary[]>(this.baseUrl);
  }

  getById(id: string): Promise<SheReturnToWorkPlan> {
    return apiService.get<SheReturnToWorkPlan>(`${this.baseUrl}/${id}`);
  }

  getByNumber(planNumber: string): Promise<SheReturnToWorkPlan | null> {
    return apiService.get<SheReturnToWorkPlan | null>(
      `${this.baseUrl}/number/${encodeURIComponent(planNumber)}`,
    );
  }

  getByEmployee(employeeId: string): Promise<SheReturnToWorkPlanSummary[]> {
    return apiService.get<SheReturnToWorkPlanSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getByStatus(status: SheReturnToWorkStatus): Promise<SheReturnToWorkPlanSummary[]> {
    return apiService.get<SheReturnToWorkPlanSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getByIncident(safetyIncidentId: string): Promise<SheReturnToWorkPlanSummary[]> {
    return apiService.get<SheReturnToWorkPlanSummary[]>(
      `${this.baseUrl}/incident/${safetyIncidentId}`,
    );
  }

  getActive(): Promise<SheReturnToWorkPlanSummary[]> {
    return apiService.get<SheReturnToWorkPlanSummary[]>(`${this.baseUrl}/active`);
  }

  create(data: SheReturnToWorkPlanCreateRequest): Promise<SheReturnToWorkPlan> {
    return apiService.post<SheReturnToWorkPlan>(this.baseUrl, data);
  }

  update(id: string, data: SheReturnToWorkPlanUpdateRequest): Promise<SheReturnToWorkPlan> {
    return apiService.put<SheReturnToWorkPlan>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Phases (numbers are server-assigned) ───────────────────────────────────

  addPhase(planId: string, data: SheReturnToWorkPhaseCreateRequest): Promise<SheReturnToWorkPhase> {
    return apiService.post<SheReturnToWorkPhase>(`${this.baseUrl}/${planId}/phases`, data);
  }

  updatePhase(
    phaseId: string,
    data: SheReturnToWorkPhaseUpdateRequest,
  ): Promise<SheReturnToWorkPhase> {
    return apiService.put<SheReturnToWorkPhase>(`${this.baseUrl}/phases/${phaseId}`, data);
  }

  removePhase(phaseId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/phases/${phaseId}`);
  }

  // ── Reviews (add-only) ─────────────────────────────────────────────────────

  getReviews(planId: string): Promise<SheReturnToWorkReview[]> {
    return apiService.get<SheReturnToWorkReview[]>(`${this.baseUrl}/${planId}/reviews`);
  }

  addReview(
    planId: string,
    data: SheReturnToWorkReviewCreateRequest,
  ): Promise<SheReturnToWorkReview> {
    return apiService.post<SheReturnToWorkReview>(`${this.baseUrl}/${planId}/reviews`, data);
  }
}

export const safetyReturnToWorkService = new SafetyReturnToWorkService();
export default safetyReturnToWorkService;
