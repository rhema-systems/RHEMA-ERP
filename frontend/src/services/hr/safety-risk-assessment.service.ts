import { apiService } from '../api.service';
import type {
  SheRiskAssessment,
  SheRiskAssessmentSummary,
  SheRiskAssessmentCreateRequest,
  SheRiskAssessmentUpdateRequest,
  SheRiskAssessmentApproveRequest,
  SheRiskAssessmentHazard,
  SheRiskAssessmentHazardCreateRequest,
  SheRiskAssessmentHazardUpdateRequest,
  SheRiskAssessmentAcknowledgement,
  SheRiskAssessmentAcknowledgementCreateRequest,
  SheRiskAssessmentType,
  SheRiskAssessmentStatus,
  MyRiskAcknowledgement,
} from '@/types/hr/safety-hazards';

/**
 * Formal risk assessments (HIRA/JHA/Pre-Task) — hazard lines, approval and acknowledgements.
 * Backend route: api/safety/risk-assessments.
 *
 * `addAcknowledgement` is the ONE action open to every authenticated employee; non-HR callers
 * always sign as themselves regardless of the body. Everything else answers 403 for non-HR.
 * Approval is refused (422) once granted, and for Expired/Superseded/Withdrawn assessments;
 * signing is refused (422) unless the assessment is Approved/Active, and on a repeat sign.
 */
class SafetyRiskAssessmentService {
  private readonly baseUrl = '/safety/risk-assessments';

  // ── Queries ────────────────────────────────────────────────────────────────

  getAll(): Promise<SheRiskAssessmentSummary[]> {
    return apiService.get<SheRiskAssessmentSummary[]>(this.baseUrl);
  }

  getById(id: string): Promise<SheRiskAssessment> {
    return apiService.get<SheRiskAssessment>(`${this.baseUrl}/${id}`);
  }

  getByNumber(assessmentNumber: string): Promise<SheRiskAssessment | null> {
    return apiService.get<SheRiskAssessment | null>(
      `${this.baseUrl}/number/${encodeURIComponent(assessmentNumber)}`,
    );
  }

  getByStatus(status: SheRiskAssessmentStatus): Promise<SheRiskAssessmentSummary[]> {
    return apiService.get<SheRiskAssessmentSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getByType(type: SheRiskAssessmentType): Promise<SheRiskAssessmentSummary[]> {
    return apiService.get<SheRiskAssessmentSummary[]>(`${this.baseUrl}/type/${type}`);
  }

  getByPreparer(preparedById: string): Promise<SheRiskAssessmentSummary[]> {
    return apiService.get<SheRiskAssessmentSummary[]>(`${this.baseUrl}/preparer/${preparedById}`);
  }

  getExpiring(daysAhead = 30): Promise<SheRiskAssessmentSummary[]> {
    return apiService.get<SheRiskAssessmentSummary[]>(`${this.baseUrl}/expiring`, { daysAhead });
  }

  getDueForReview(daysAhead = 30): Promise<SheRiskAssessmentSummary[]> {
    return apiService.get<SheRiskAssessmentSummary[]>(`${this.baseUrl}/due-for-review`, {
      daysAhead,
    });
  }

  /** HR-gated per-employee view — the employee self-service variant is area-25 work. */
  getForAcknowledgement(employeeId: string): Promise<MyRiskAcknowledgement[]> {
    return apiService.get<MyRiskAcknowledgement[]>(
      `${this.baseUrl}/for-acknowledgement/${employeeId}`,
    );
  }

  // ── CRUD + lifecycle ───────────────────────────────────────────────────────

  create(data: SheRiskAssessmentCreateRequest): Promise<SheRiskAssessment> {
    return apiService.post<SheRiskAssessment>(this.baseUrl, data);
  }

  update(id: string, data: SheRiskAssessmentUpdateRequest): Promise<SheRiskAssessment> {
    return apiService.put<SheRiskAssessment>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  approve(id: string, data: SheRiskAssessmentApproveRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/approve`, data);
  }

  // ── Hazard lines ───────────────────────────────────────────────────────────

  addHazard(
    assessmentId: string,
    data: SheRiskAssessmentHazardCreateRequest,
  ): Promise<SheRiskAssessmentHazard> {
    return apiService.post<SheRiskAssessmentHazard>(
      `${this.baseUrl}/${assessmentId}/hazards`,
      data,
    );
  }

  updateHazard(
    hazardLineId: string,
    data: SheRiskAssessmentHazardUpdateRequest,
  ): Promise<SheRiskAssessmentHazard> {
    return apiService.put<SheRiskAssessmentHazard>(`${this.baseUrl}/hazards/${hazardLineId}`, data);
  }

  removeHazard(hazardLineId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/hazards/${hazardLineId}`);
  }

  // ── Acknowledgements ───────────────────────────────────────────────────────

  /** Open to every authenticated employee — non-HR callers always sign as themselves. */
  addAcknowledgement(
    assessmentId: string,
    data: SheRiskAssessmentAcknowledgementCreateRequest,
  ): Promise<SheRiskAssessmentAcknowledgement> {
    return apiService.post<SheRiskAssessmentAcknowledgement>(
      `${this.baseUrl}/${assessmentId}/acknowledgements`,
      data,
    );
  }
}

export const safetyRiskAssessmentService = new SafetyRiskAssessmentService();
export default safetyRiskAssessmentService;
