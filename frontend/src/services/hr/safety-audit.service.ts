import { apiService } from '../api.service';
import type {
  SheAudit,
  SheAuditSummary,
  SheAuditStatus,
  SheAuditCreateRequest,
  SheAuditUpdateRequest,
  SheAuditFinding,
  SheAuditFindingCreateRequest,
  SheAuditFindingUpdateRequest,
  SheAuditFindingAction,
  SheAuditFindingActionCreateRequest,
  SheAuditFindingActionUpdateRequest,
  SheAuditTeamMember,
} from '@/types/hr/safety-audits';

/**
 * SHE audit management (FRD §12): planning → execution → findings → CAPA →
 * verification → closure. Backend route: api/safety/audits. HR-only.
 *
 * The server refuses (422): starting a non-Planned audit, issuing a report off an
 * InProgress audit, closing before every finding is closed, recording findings
 * outside InProgress, verifying an unresolved finding, closing an unverified
 * finding or one with open actions, and deleting an executed audit.
 */
class SafetyAuditService {
  private readonly baseUrl = '/safety/audits';

  getAll(status?: SheAuditStatus, year?: number): Promise<SheAuditSummary[]> {
    const params = new URLSearchParams();
    if (status) params.set('status', status);
    if (year != null) params.set('year', String(year));
    const query = params.toString();
    return apiService.get<SheAuditSummary[]>(query ? `${this.baseUrl}?${query}` : this.baseUrl);
  }

  getUpcoming(daysAhead = 30): Promise<SheAuditSummary[]> {
    return apiService.get<SheAuditSummary[]>(`${this.baseUrl}/upcoming?daysAhead=${daysAhead}`);
  }

  getById(id: string): Promise<SheAudit> {
    return apiService.get<SheAudit>(`${this.baseUrl}/${id}`);
  }

  create(data: SheAuditCreateRequest): Promise<SheAudit> {
    return apiService.post<SheAudit>(this.baseUrl, data);
  }

  update(id: string, data: SheAuditUpdateRequest): Promise<SheAudit> {
    return apiService.put<SheAudit>(`${this.baseUrl}/${id}`, data);
  }

  start(id: string): Promise<SheAudit> {
    return apiService.post<SheAudit>(`${this.baseUrl}/${id}/start`, { auditId: id });
  }

  issueReport(id: string, summary: string, reportDocumentPath?: string | null): Promise<SheAudit> {
    return apiService.post<SheAudit>(`${this.baseUrl}/${id}/issue-report`, {
      auditId: id,
      summary,
      reportDocumentPath: reportDocumentPath ?? null,
    });
  }

  close(id: string, closedById: string, closureNotes?: string | null): Promise<SheAudit> {
    return apiService.post<SheAudit>(`${this.baseUrl}/${id}/close`, {
      auditId: id,
      closedById,
      closureNotes: closureNotes ?? null,
    });
  }

  cancel(id: string, reason: string): Promise<SheAudit> {
    return apiService.post<SheAudit>(`${this.baseUrl}/${id}/cancel`, { auditId: id, reason });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  addTeamMember(auditId: string, employeeId: string, role: string): Promise<SheAuditTeamMember> {
    return apiService.post<SheAuditTeamMember>(`${this.baseUrl}/${auditId}/team`, {
      auditId,
      employeeId,
      role,
    });
  }

  removeTeamMember(teamMemberId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/team/${teamMemberId}`);
  }

  addFinding(data: SheAuditFindingCreateRequest): Promise<SheAuditFinding> {
    return apiService.post<SheAuditFinding>(`${this.baseUrl}/${data.auditId}/findings`, data);
  }

  updateFinding(data: SheAuditFindingUpdateRequest): Promise<SheAuditFinding> {
    return apiService.put<SheAuditFinding>(`${this.baseUrl}/findings/${data.id}`, data);
  }

  verifyFinding(findingId: string, verifiedById: string, verificationNotes?: string | null): Promise<SheAuditFinding> {
    return apiService.post<SheAuditFinding>(`${this.baseUrl}/findings/${findingId}/verify`, {
      findingId,
      verifiedById,
      verificationNotes: verificationNotes ?? null,
    });
  }

  closeFinding(findingId: string): Promise<SheAuditFinding> {
    return apiService.post<SheAuditFinding>(`${this.baseUrl}/findings/${findingId}/close`, {});
  }

  addFindingAction(data: SheAuditFindingActionCreateRequest): Promise<SheAuditFindingAction> {
    return apiService.post<SheAuditFindingAction>(
      `${this.baseUrl}/findings/${data.findingId}/actions`,
      data,
    );
  }

  updateFindingAction(data: SheAuditFindingActionUpdateRequest): Promise<SheAuditFindingAction> {
    return apiService.put<SheAuditFindingAction>(`${this.baseUrl}/finding-actions/${data.id}`, data);
  }
}

export const safetyAuditService = new SafetyAuditService();
export default safetyAuditService;
