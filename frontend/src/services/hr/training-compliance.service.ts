import { apiService } from '../api.service';
import type {
  ComplianceTrainingRequirement,
  ComplianceTrainingRequirementSummary,
  ComplianceRequirementCreate,
  ComplianceRequirementUpdate,
  EmployeeComplianceRecord,
  EmployeeComplianceRecordSummary,
  AssignComplianceRequest,
  ExemptEmployeeComplianceRequest,
} from '@/types/hr/training-compliance';

/**
 * Mandatory-training compliance. Backend route: api/compliance-training.
 *
 * Two halves on one controller: `requirements/*` is the configuration (who must hold what, how
 * often), `records/*` is the per-employee state that goes overdue, gets fulfilled or gets exempted.
 */
class TrainingComplianceService {
  private readonly baseUrl = '/compliance-training';

  // ── Requirements (configuration) ────────────────────────────────────────────
  getRequirements(): Promise<ComplianceTrainingRequirementSummary[]> {
    return apiService.get<ComplianceTrainingRequirementSummary[]>(`${this.baseUrl}/requirements`);
  }

  getActiveRequirements(): Promise<ComplianceTrainingRequirementSummary[]> {
    return apiService.get<ComplianceTrainingRequirementSummary[]>(`${this.baseUrl}/requirements/active`);
  }

  getRequirementById(id: string): Promise<ComplianceTrainingRequirement> {
    return apiService.get<ComplianceTrainingRequirement>(`${this.baseUrl}/requirements/${id}`);
  }

  getRequirementsByProgram(programId: string): Promise<ComplianceTrainingRequirementSummary[]> {
    return apiService.get<ComplianceTrainingRequirementSummary[]>(
      `${this.baseUrl}/requirements/program/${programId}`,
    );
  }

  createRequirement(data: ComplianceRequirementCreate): Promise<ComplianceTrainingRequirement> {
    return apiService.post<ComplianceTrainingRequirement>(`${this.baseUrl}/requirements`, data);
  }

  updateRequirement(id: string, data: ComplianceRequirementUpdate): Promise<ComplianceTrainingRequirement> {
    return apiService.put<ComplianceTrainingRequirement>(`${this.baseUrl}/requirements/${id}`, {
      id,
      ...data,
    });
  }

  removeRequirement(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/requirements/${id}`);
  }

  // ── Records (per-employee state) ────────────────────────────────────────────
  /** The caller's own compliance — "am I compliant?". Token-derived. */
  getMyRecords(): Promise<EmployeeComplianceRecord[]> {
    return apiService.get<EmployeeComplianceRecord[]>(`${this.baseUrl}/records/mine`);
  }

  getRecordsForEmployee(employeeId: string): Promise<EmployeeComplianceRecord[]> {
    return apiService.get<EmployeeComplianceRecord[]>(`${this.baseUrl}/records/employee/${employeeId}`);
  }

  /**
   * ⚠ These three return *summaries*, not full records — the API declared the full DTO but has
   * always returned the narrow one. Typed honestly here so a screen cannot reach for a field the
   * payload does not carry.
   */
  getRecordsForRequirement(requirementId: string): Promise<EmployeeComplianceRecordSummary[]> {
    return apiService.get<EmployeeComplianceRecordSummary[]>(
      `${this.baseUrl}/records/requirement/${requirementId}`,
    );
  }

  getOverdue(): Promise<EmployeeComplianceRecordSummary[]> {
    return apiService.get<EmployeeComplianceRecordSummary[]>(`${this.baseUrl}/records/overdue`);
  }

  getNonCompliant(): Promise<EmployeeComplianceRecordSummary[]> {
    return apiService.get<EmployeeComplianceRecordSummary[]>(`${this.baseUrl}/records/non-compliant`);
  }

  assign(data: AssignComplianceRequest): Promise<EmployeeComplianceRecord> {
    return apiService.post<EmployeeComplianceRecord>(`${this.baseUrl}/records/assign`, data);
  }

  /** Returns the updated record with exemptedByName resolved. */
  exempt(
    recordId: string,
    data: Omit<ExemptEmployeeComplianceRequest, 'recordId'>,
  ): Promise<EmployeeComplianceRecord> {
    return apiService.post<EmployeeComplianceRecord>(`${this.baseUrl}/records/${recordId}/exempt`, {
      recordId,
      ...data,
    });
  }

  /**
   * Marks a requirement satisfied by a specific nomination, and recomputes the next due date from
   * the requirement's recurrence — so the overdue and expiring queues stay correct afterwards.
   */
  fulfill(recordId: string, nominationId: string): Promise<EmployeeComplianceRecord> {
    return apiService.post<EmployeeComplianceRecord>(`${this.baseUrl}/records/${recordId}/fulfill`, {
      nominationId,
    });
  }
}

export const trainingComplianceService = new TrainingComplianceService();
