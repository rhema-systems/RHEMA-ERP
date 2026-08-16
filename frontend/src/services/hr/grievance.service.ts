import { apiService } from '../api.service';
import type {
  Grievance,
  GrievanceSummary,
  GrievanceStatus,
  GrievanceEscalationLevel,
  FileGrievanceRequest,
  RespondToGrievanceRequest,
  AssignGrievanceStepRequest,
  EscalateGrievanceRequest,
  WithdrawGrievanceRequest,
} from '@/types/hr/grievance';

/**
 * Employee grievances. Backend route: `api/grievances`.
 *
 * The permissions are the mirror image of the disciplinary case, and the UI must respect it rather
 * than discover it. A case is raised ABOUT an employee, so it is HR's; a grievance is raised BY one,
 * so **filing, escalating and withdrawing are the employee's and HR cannot do them at all** — there
 * is no employee id on any of those payloads to allow it. HR owns answering, assigning and the
 * register. Do not add a "raise on behalf of" affordance; the server has no way to honour it.
 */
class GrievanceService {
  private readonly baseUrl = '/grievances';

  // ── HR's register ──────────────────────────────────────────────────────────

  getAll(): Promise<GrievanceSummary[]> {
    return apiService.get<GrievanceSummary[]>(this.baseUrl);
  }

  getByStatus(status: GrievanceStatus): Promise<GrievanceSummary[]> {
    return apiService.get<GrievanceSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  /** Where the ladder is stuck — grievances whose current rung has not answered. */
  getAwaitingResponse(level?: GrievanceEscalationLevel): Promise<GrievanceSummary[]> {
    return apiService.get<GrievanceSummary[]>(`${this.baseUrl}/awaiting-response`, level ? { level } : {});
  }

  // ── The employee's own surface ─────────────────────────────────────────────

  /** The caller's own grievances — token-derived, with no id-bearing equivalent. */
  getMine(): Promise<GrievanceSummary[]> {
    return apiService.get<GrievanceSummary[]>(`${this.baseUrl}/mine`);
  }

  /**
   * Grievances this caller has been asked to answer. Open to non-HR by design: whoever answers at
   * the supervisor or HOD rung is not in HR, and the register refuses them.
   */
  getAwaitingMyResponse(): Promise<GrievanceSummary[]> {
    return apiService.get<GrievanceSummary[]>(`${this.baseUrl}/awaiting-my-response`);
  }

  /** Refused to anyone but the griever, HR, or someone named on a step. */
  getById(id: string): Promise<Grievance> {
    return apiService.get<Grievance>(`${this.baseUrl}/${id}`);
  }

  // ── The griever's actions ──────────────────────────────────────────────────

  file(payload: FileGrievanceRequest): Promise<Grievance> {
    return apiService.post<Grievance>(this.baseUrl, payload);
  }

  /** The griever saying the answer did not settle it. Refused to everyone else, HR included. */
  escalate(id: string, payload: EscalateGrievanceRequest = {}): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/escalate`, payload);
  }

  withdraw(id: string, payload: WithdrawGrievanceRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/withdraw`, payload);
  }

  // ── Answering ──────────────────────────────────────────────────────────────

  /** HR names who should answer at the current rung — how a supervisor or HOD is brought in. */
  assign(id: string, payload: AssignGrievanceStepRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/assign`, payload);
  }

  /** HR, or whoever the step names. Refused to the griever — you cannot answer your own. */
  respond(id: string, payload: RespondToGrievanceRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/respond`, payload);
  }
}

export const grievanceService = new GrievanceService();
