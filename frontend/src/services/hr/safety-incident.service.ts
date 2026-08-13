import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  SafetyIncident,
  SafetyIncidentSummary,
  SafetyIncidentCreateRequest,
  SafetyIncidentUpdateRequest,
  SheIncidentStatus,
  SheIncidentSeverity,
  AssignInvestigationRequest,
  RecordInvestigationRequest,
  NotifyAuthorityRequest,
  FileClaimRequest,
  ReviewIncidentRequest,
  CloseIncidentRequest,
  SafetyIncidentInvolvedPerson,
  InvolvedPersonCreateRequest,
  InvolvedPersonUpdateRequest,
  SafetyIncidentInjuredBodyPart,
  InjuredBodyPartCreateRequest,
  SafetyIncidentWitness,
  WitnessCreateRequest,
  WitnessUpdateRequest,
  SafetyIncidentInvestigationTeamMember,
  InvestigationTeamMemberCreateRequest,
  SafetyIncidentCorrectiveAction,
  CorrectiveActionCreateRequest,
  CorrectiveActionUpdateRequest,
  VerifyCorrectiveActionRequest,
  SafetyIncidentFollowUp,
  FollowUpCreateRequest,
  SafetyIncidentDocument,
  IncidentDocumentCreateRequest,
} from '@/types/hr/safety-incidents';
import type { SheIncidentCategory } from '@/types/hr/safety';

/**
 * The incident register and investigation lifecycle. Backend route: api/safety/incidents.
 *
 * `create` is the ONE action open to every authenticated employee — non-HR reporters are always
 * recorded as themselves, whatever the body says. Everything else answers 403 for non-HR.
 * Closing is refused with 422 while any corrective action is still open, and closed incidents
 * refuse edits until a review reopens them.
 */
class SafetyIncidentService {
  private readonly baseUrl = '/safety/incidents';

  // ── Queries ────────────────────────────────────────────────────────────────

  getPaged(
    page = 1,
    pageSize = 20,
    status?: SheIncidentStatus,
  ): Promise<PagedResult<SafetyIncidentSummary>> {
    return apiService.get<PagedResult<SafetyIncidentSummary>>(this.baseUrl, {
      page,
      pageSize,
      ...(status ? { status } : {}),
    });
  }

  getById(id: string): Promise<SafetyIncident> {
    return apiService.get<SafetyIncident>(`${this.baseUrl}/${id}`);
  }

  getByNumber(incidentNumber: string): Promise<SafetyIncident | null> {
    return apiService.get<SafetyIncident | null>(
      `${this.baseUrl}/number/${encodeURIComponent(incidentNumber)}`,
    );
  }

  getByStatus(status: SheIncidentStatus): Promise<SafetyIncidentSummary[]> {
    return apiService.get<SafetyIncidentSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getBySeverity(severity: SheIncidentSeverity): Promise<SafetyIncidentSummary[]> {
    return apiService.get<SafetyIncidentSummary[]>(`${this.baseUrl}/severity/${severity}`);
  }

  getByCategory(category: SheIncidentCategory): Promise<SafetyIncidentSummary[]> {
    return apiService.get<SafetyIncidentSummary[]>(`${this.baseUrl}/category/${category}`);
  }

  getByDateRange(from: string, to: string): Promise<SafetyIncidentSummary[]> {
    return apiService.get<SafetyIncidentSummary[]>(`${this.baseUrl}/date-range`, { from, to });
  }

  getByLocation(locationId: string): Promise<SafetyIncidentSummary[]> {
    return apiService.get<SafetyIncidentSummary[]>(`${this.baseUrl}/location/${locationId}`);
  }

  getByInvolvedEmployee(employeeId: string): Promise<SafetyIncidentSummary[]> {
    return apiService.get<SafetyIncidentSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getForEmployee(employeeId: string): Promise<SafetyIncidentSummary[]> {
    return apiService.get<SafetyIncidentSummary[]>(`${this.baseUrl}/for-employee/${employeeId}`);
  }

  getRequiringInvestigation(): Promise<SafetyIncidentSummary[]> {
    return apiService.get<SafetyIncidentSummary[]>(`${this.baseUrl}/requiring-investigation`);
  }

  getOpen(): Promise<SafetyIncidentSummary[]> {
    return apiService.get<SafetyIncidentSummary[]>(`${this.baseUrl}/open`);
  }

  getLostTime(): Promise<SafetyIncidentSummary[]> {
    return apiService.get<SafetyIncidentSummary[]>(`${this.baseUrl}/lost-time`);
  }

  getReportablePending(): Promise<SafetyIncidentSummary[]> {
    return apiService.get<SafetyIncidentSummary[]>(`${this.baseUrl}/reportable-pending`);
  }

  // ── CRUD ───────────────────────────────────────────────────────────────────

  /** Open to every authenticated employee. Auto-populates the incident type's default CAs. */
  create(data: SafetyIncidentCreateRequest): Promise<SafetyIncident> {
    return apiService.post<SafetyIncident>(this.baseUrl, data);
  }

  update(id: string, data: SafetyIncidentUpdateRequest): Promise<SafetyIncident> {
    return apiService.put<SafetyIncident>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Workflow ───────────────────────────────────────────────────────────────

  assignInvestigation(id: string, data: AssignInvestigationRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/assign-investigation`, data);
  }

  recordInvestigation(id: string, data: RecordInvestigationRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/record-investigation`, data);
  }

  notifyAuthority(id: string, data: NotifyAuthorityRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/notify-authority`, data);
  }

  fileClaim(id: string, data: FileClaimRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/file-claim`, data);
  }

  review(id: string, data: ReviewIncidentRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/review`, data);
  }

  /** Refused with 422 while any corrective action is still open. */
  close(id: string, data: CloseIncidentRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/close`, data);
  }

  // ── Involved persons ───────────────────────────────────────────────────────

  addInvolvedPerson(
    incidentId: string,
    data: InvolvedPersonCreateRequest,
  ): Promise<SafetyIncidentInvolvedPerson> {
    return apiService.post<SafetyIncidentInvolvedPerson>(
      `${this.baseUrl}/${incidentId}/involved-persons`,
      data,
    );
  }

  updateInvolvedPerson(
    personId: string,
    data: InvolvedPersonUpdateRequest,
  ): Promise<SafetyIncidentInvolvedPerson> {
    return apiService.put<SafetyIncidentInvolvedPerson>(
      `${this.baseUrl}/involved-persons/${personId}`,
      data,
    );
  }

  removeInvolvedPerson(personId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/involved-persons/${personId}`);
  }

  addInjuredBodyPart(
    personId: string,
    data: InjuredBodyPartCreateRequest,
  ): Promise<SafetyIncidentInjuredBodyPart> {
    return apiService.post<SafetyIncidentInjuredBodyPart>(
      `${this.baseUrl}/involved-persons/${personId}/body-parts`,
      data,
    );
  }

  removeInjuredBodyPart(bodyPartId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/body-parts/${bodyPartId}`);
  }

  // ── Witnesses ──────────────────────────────────────────────────────────────

  addWitness(incidentId: string, data: WitnessCreateRequest): Promise<SafetyIncidentWitness> {
    return apiService.post<SafetyIncidentWitness>(`${this.baseUrl}/${incidentId}/witnesses`, data);
  }

  updateWitness(witnessId: string, data: WitnessUpdateRequest): Promise<SafetyIncidentWitness> {
    return apiService.put<SafetyIncidentWitness>(`${this.baseUrl}/witnesses/${witnessId}`, data);
  }

  removeWitness(witnessId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/witnesses/${witnessId}`);
  }

  // ── Investigation team ─────────────────────────────────────────────────────

  addInvestigationTeamMember(
    incidentId: string,
    data: InvestigationTeamMemberCreateRequest,
  ): Promise<SafetyIncidentInvestigationTeamMember> {
    return apiService.post<SafetyIncidentInvestigationTeamMember>(
      `${this.baseUrl}/${incidentId}/investigation-team`,
      data,
    );
  }

  removeInvestigationTeamMember(memberId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/investigation-team/${memberId}`);
  }

  // ── Corrective actions ─────────────────────────────────────────────────────

  getCorrectiveActions(incidentId: string): Promise<SafetyIncidentCorrectiveAction[]> {
    return apiService.get<SafetyIncidentCorrectiveAction[]>(
      `${this.baseUrl}/${incidentId}/corrective-actions`,
    );
  }

  getOverdueCorrectiveActions(): Promise<SafetyIncidentCorrectiveAction[]> {
    return apiService.get<SafetyIncidentCorrectiveAction[]>(
      `${this.baseUrl}/corrective-actions/overdue`,
    );
  }

  getCorrectiveActionsByResponsible(employeeId: string): Promise<SafetyIncidentCorrectiveAction[]> {
    return apiService.get<SafetyIncidentCorrectiveAction[]>(
      `${this.baseUrl}/corrective-actions/by-responsible/${employeeId}`,
    );
  }

  addCorrectiveAction(
    incidentId: string,
    data: CorrectiveActionCreateRequest,
  ): Promise<SafetyIncidentCorrectiveAction> {
    return apiService.post<SafetyIncidentCorrectiveAction>(
      `${this.baseUrl}/${incidentId}/corrective-actions`,
      data,
    );
  }

  updateCorrectiveAction(
    actionId: string,
    data: CorrectiveActionUpdateRequest,
  ): Promise<SafetyIncidentCorrectiveAction> {
    return apiService.put<SafetyIncidentCorrectiveAction>(
      `${this.baseUrl}/corrective-actions/${actionId}`,
      data,
    );
  }

  verifyCorrectiveAction(actionId: string, data: VerifyCorrectiveActionRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/corrective-actions/${actionId}/verify`, data);
  }

  removeCorrectiveAction(actionId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/corrective-actions/${actionId}`);
  }

  // ── Follow-ups & documents ─────────────────────────────────────────────────

  addFollowUp(incidentId: string, data: FollowUpCreateRequest): Promise<SafetyIncidentFollowUp> {
    return apiService.post<SafetyIncidentFollowUp>(`${this.baseUrl}/${incidentId}/follow-ups`, data);
  }

  addDocument(
    incidentId: string,
    data: IncidentDocumentCreateRequest,
  ): Promise<SafetyIncidentDocument> {
    return apiService.post<SafetyIncidentDocument>(`${this.baseUrl}/${incidentId}/documents`, data);
  }

  removeDocument(documentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/documents/${documentId}`);
  }
}

export const safetyIncidentService = new SafetyIncidentService();
export default safetyIncidentService;
