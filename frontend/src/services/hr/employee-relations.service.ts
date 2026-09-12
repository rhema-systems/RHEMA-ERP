import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  Grievance,
  GrievanceSummary,
  GrievanceDocument,
  GrievanceEscalationLevel,
  EmployeeRelationsLinkSource,
  ErLinkedCase,
  ErRegisterQuery,
  FileGrievanceRequest,
  OpenErCaseRequest,
  RespondToGrievanceRequest,
  AssignGrievanceStepRequest,
  EscalateGrievanceRequest,
  WithdrawGrievanceRequest,
  AddGrievancePartyRequest,
  RemoveGrievancePartyRequest,
  RecordHrInterpretationRequest,
  OpenInvestigationRequest,
  UpdateInvestigationRequest,
  CompleteInvestigationRequest,
  ResolveGrievanceRequest,
  CloseGrievanceRequest,
  ScheduleConferenceRequest,
  UpdateConferenceRequest,
  HoldConferenceRequest,
  CancelConferenceRequest,
  AddConferenceAttendeeRequest,
  AcceptAgreementRequest,
  LinkErCaseSourceRequest,
  GrievanceDocumentScope,
} from '@/types/hr/employee-relations';

/**
 * Employee-relations cases — HR area 9c. Backend route: `api/hr/employee-relations`.
 *
 * ⚠ **The permission split is the mirror image of the disciplinary case, and the UI must respect it
 * rather than discover it.** A case is raised ABOUT an employee, so it is HR's; a grievance is
 * raised BY one, so **filing, escalating and withdrawing are the employee's and HR cannot do them at
 * all** — there is no employee id on any of those payloads to allow it. What HR owns is answering,
 * assigning, the parties, the artefacts and the register. Do not add a "raise on behalf of"
 * affordance: the server has no way to honour it, and `openCase` refuses `caseType: 'Grievance'`
 * for exactly that reason.
 *
 * ⚠ **`api/grievances` is a deprecated alias for the same controller** and is dropped in slice 12.
 * Every method here uses the new route; nothing new should be written against the old one.
 */
class EmployeeRelationsService {
  private readonly baseUrl = '/hr/employee-relations';

  // ── The register ───────────────────────────────────────────────────────────

  /**
   * The paged register — the desk's main read.
   *
   * ⚠ The server clamps `pageSize` to 200, and a caller who treats one
   * capped page as the whole set gets a truncated answer that looks complete: that is exactly the
   * defect slice 8 found, where a harness compared 200 rows against a set of 227.
   */
  getRegister(query: ErRegisterQuery = {}): Promise<PagedResult<GrievanceSummary>> {
    return apiService.get<PagedResult<GrievanceSummary>>(`${this.baseUrl}/register`, query as Record<string, unknown>);
  }



  // ── The employee's own surface ─────────────────────────────────────────────

  /** The caller's own cases — token-derived, with no id-bearing equivalent. */
  getMine(): Promise<GrievanceSummary[]> {
    return apiService.get<GrievanceSummary[]>(`${this.baseUrl}/mine`);
  }

  /**
   * Cases this caller has been asked to answer. Open to non-HR by design: whoever answers at the
   * supervisor or HOD rung is not in HR, and the register refuses them.
   */
  getAwaitingMyResponse(): Promise<GrievanceSummary[]> {
    return apiService.get<GrievanceSummary[]>(`${this.baseUrl}/awaiting-my-response`);
  }

  /**
   * The case file. Refused to anyone but the griever, HR, or someone named on a step.
   *
   * ⚠ Two fields are redacted per reader rather than by endpoint: `links` is empty for everyone but
   * HR, and each conference's `notes` is null unless you are HR or that conference's chair (with
   * `notesRedacted` telling you which). A screen must not read an absent value as an empty one.
   */
  getById(id: string): Promise<Grievance> {
    return apiService.get<Grievance>(`${this.baseUrl}/${id}`);
  }

  // ── Opening a case ─────────────────────────────────────────────────────────

  /** The griever's own act. No employee id: the caller is the griever. */
  file(payload: FileGrievanceRequest): Promise<Grievance> {
    return apiService.post<Grievance>(this.baseUrl, payload);
  }

  /**
   * HR opens a mediation, welfare matter or union consultation ABOUT somebody.
   *
   * Pass `source` + `sourceRecordId` together to open it straight from a safety incident, PIP or
   * disciplinary case — one call, so a refused link means no case rather than an orphan.
   */
  openCase(payload: OpenErCaseRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/cases`, payload);
  }

  // ── The griever's actions. HR cannot call these at all. ────────────────────

  escalate(id: string, payload: EscalateGrievanceRequest = {}): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/escalate`, payload);
  }

  withdraw(id: string, payload: WithdrawGrievanceRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/withdraw`, payload);
  }

  /** The employee accepting the signed agreement. Only they can — not HR, not a representative. */
  acceptAgreement(id: string, payload: AcceptAgreementRequest = {}): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/agreement/accept`, payload);
  }

  // ── Answering the ladder ───────────────────────────────────────────────────

  /** HR names who should answer at the current rung — how a supervisor or HOD is brought in. */
  assign(id: string, payload: AssignGrievanceStepRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/assign`, payload);
  }

  /** HR, or whoever the step names. Refused to the griever — you cannot answer your own. */
  respond(id: string, payload: RespondToGrievanceRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/respond`, payload);
  }

  // ── Parties ────────────────────────────────────────────────────────────────

  addParty(id: string, payload: AddGrievancePartyRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/parties`, payload);
  }

  /** Stands a party down. Not a delete — the case file must still read correctly afterwards. */
  removeParty(id: string, partyId: string, payload: RemoveGrievancePartyRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/parties/${partyId}/remove`, payload);
  }

  // ── FR-HR-181's artefacts ──────────────────────────────────────────────────

  /** HR's reading of the merits. Amendable while open, frozen once the case is terminal. */
  recordHrInterpretation(id: string, payload: RecordHrInterpretationRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/hr-interpretation`, payload);
  }

  openInvestigation(id: string, payload: OpenInvestigationRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/investigation`, payload);
  }

  updateInvestigation(id: string, payload: UpdateInvestigationRequest): Promise<Grievance> {
    return apiService.put<Grievance>(`${this.baseUrl}/${id}/investigation`, payload);
  }

  /** ⚠ Refused without findings: an investigation reported complete with nothing in it is worse
   * than one still open, because the rungs above it will rely on it. */
  completeInvestigation(id: string, payload: CompleteInvestigationRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/investigation/complete`, payload);
  }

  /** The decision. Frozen on write — the one change it accepts is filling in a missing outcome. */
  resolve(id: string, payload: ResolveGrievanceRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/resolve`, payload);
  }

  /** ⚠ Only for a case that reached the Board and was answered without being resolved. */
  close(id: string, payload: CloseGrievanceRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/close`, payload);
  }

  // ── Documents ──────────────────────────────────────────────────────────────

  /**
   * Uploads a document through the controlled gate: virus scan, central-DMS registration, then the
   * row. All three or none.
   */
  uploadDocument(
    id: string,
    file: File,
    options: {
      scope?: GrievanceDocumentScope;
      stepId?: string;
      conferenceId?: string;
      description?: string;
      agreementSignedDate?: string;
    } = {},
  ): Promise<GrievanceDocument> {
    const form = new FormData();
    form.append('file', file);
    if (options.scope) form.append('scope', options.scope);
    if (options.stepId) form.append('stepId', options.stepId);
    if (options.conferenceId) form.append('conferenceId', options.conferenceId);
    if (options.description) form.append('description', options.description);
    if (options.agreementSignedDate) form.append('agreementSignedDate', options.agreementSignedDate);
    return apiService.post<GrievanceDocument>(`${this.baseUrl}/${id}/documents`, form);
  }

  /**
   * ⚠ The ONLY route to the file, and it needs the bearer token.
   *
   * `GrievanceDocument.filePath` points outside the web root and can never be used as an href —
   * rendering it as a link produces a 404 in development and a broken download in production.
   */
  downloadDocument(documentId: string): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/documents/${documentId}/download`);
  }

  deleteDocument(id: string, documentId: string): Promise<Grievance> {
    return apiService.delete<Grievance>(`${this.baseUrl}/${id}/documents/${documentId}`);
  }

  // ── Conferences ────────────────────────────────────────────────────────────

  scheduleConference(id: string, payload: ScheduleConferenceRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/conferences`, payload);
  }

  updateConference(id: string, conferenceId: string, payload: UpdateConferenceRequest): Promise<Grievance> {
    return apiService.put<Grievance>(`${this.baseUrl}/${id}/conferences/${conferenceId}`, payload);
  }

  /** ⚠ `outcome` is required and at least 20 characters. The server stamps the held date itself. */
  holdConference(id: string, conferenceId: string, payload: HoldConferenceRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/conferences/${conferenceId}/hold`, payload);
  }

  cancelConference(id: string, conferenceId: string, payload: CancelConferenceRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/conferences/${conferenceId}/cancel`, payload);
  }

  addConferenceAttendee(id: string, conferenceId: string, payload: AddConferenceAttendeeRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/conferences/${conferenceId}/attendees`, payload);
  }

  removeConferenceAttendee(id: string, conferenceId: string, attendeeId: string): Promise<Grievance> {
    return apiService.delete<Grievance>(`${this.baseUrl}/${id}/conferences/${conferenceId}/attendees/${attendeeId}`);
  }

  // ── Cross-links (slice 9) ──────────────────────────────────────────────────

  /**
   * ⚠ Refused unless the source record is about somebody already on the case — the primary party or
   * an active party. When it refuses, the message says to add the person as a party first, and that
   * is genuinely the fix rather than a workaround.
   */
  linkSource(id: string, payload: LinkErCaseSourceRequest): Promise<Grievance> {
    return apiService.post<Grievance>(`${this.baseUrl}/${id}/links`, payload);
  }

  /** Changing a link is unlink-then-link: re-pointing in place is refused, on purpose. */
  unlinkSource(id: string, source: EmployeeRelationsLinkSource): Promise<Grievance> {
    return apiService.delete<Grievance>(`${this.baseUrl}/${id}/links/${source}`);
  }

  /** The reverse read, for a "this record has employee-relations cases" panel on a source screen. */
  getCasesForSource(source: EmployeeRelationsLinkSource, recordId: string): Promise<ErLinkedCase[]> {
    return apiService.get<ErLinkedCase[]>(`${this.baseUrl}/by-source/${source}/${recordId}`);
  }
}

export const employeeRelationsService = new EmployeeRelationsService();
