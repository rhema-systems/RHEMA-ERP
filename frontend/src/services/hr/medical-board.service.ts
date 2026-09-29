import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  MedicalBoard,
  MedicalBoardCase,
  MedicalBoardDocument,
  MedicalBoardKind,
  MedicalBoardMember,
  MedicalBoardPurpose,
  MedicalBoardSitting,
  MedicalBoardStatus,
  RequestMedicalBoardRequest,
  AddMedicalBoardCaseRequest,
  AddMedicalBoardMemberRequest,
  RecordMedicalBoardSittingRequest,
  ConcludeMedicalBoardCaseRequest,
  AssessIncapacityRequest,
  RecordCompensationRequest,
  IncapacityScheduleItem,
  SaveIncapacityScheduleItemRequest,
} from '@/types/hr/medical-board';

export interface MedicalBoardListParams {
  /** Boards with a case about this employee. */
  employeeId?: string;
  status?: MedicalBoardStatus;
  /** Boards with a case asked this question. */
  purpose?: MedicalBoardPurpose;
  kind?: MedicalBoardKind;
  from?: string;
  to?: string;
  search?: string;
  pageNumber?: number;
  pageSize?: number;
}

/**
 * Medical boards. Backend route: `api/hr/medical-boards`.
 *
 * ⚠ There is deliberately no method here for acting on a finding. Leave and separation READ the
 * case about their employee — through `leaveService.linkMedicalBoard` and
 * `separationService.linkMedicalBoard` — and neither writes back.
 */
class MedicalBoardService {
  private readonly baseUrl = '/hr/medical-boards';

  list(params: MedicalBoardListParams = {}): Promise<PagedResult<MedicalBoard>> {
    const q = new URLSearchParams();
    if (params.employeeId) q.set('employeeId', params.employeeId);
    if (params.status) q.set('status', params.status);
    if (params.purpose) q.set('purpose', params.purpose);
    if (params.kind) q.set('kind', params.kind);
    if (params.from) q.set('from', params.from);
    if (params.to) q.set('to', params.to);
    if (params.search) q.set('search', params.search);
    q.set('pageNumber', String(params.pageNumber ?? 1));
    q.set('pageSize', String(params.pageSize ?? 25));
    return apiService.get<PagedResult<MedicalBoard>>(`${this.baseUrl}?${q.toString()}`);
  }

  getById(id: string): Promise<MedicalBoard> {
    return apiService.get<MedicalBoard>(`${this.baseUrl}/${id}`);
  }

  /** Asks for a board, with its first case. It has no members yet. */
  request(data: RequestMedicalBoardRequest): Promise<MedicalBoard> {
    return apiService.post<MedicalBoard>(this.baseUrl, data);
  }

  /** Another employee before the board (lane K-II-a). */
  addCase(boardId: string, data: AddMedicalBoardCaseRequest): Promise<MedicalBoardCase> {
    return apiService.post<MedicalBoardCase>(`${this.baseUrl}/${boardId}/cases`, data);
  }

  /**
   * A case decided, at a sitting whose attendance holds the quorum.
   *
   * ⚠ Cannot be undone, and it is the only state leave and separation act on. The board reports by
   * itself when its last open case closes.
   */
  concludeCase(boardId: string, caseId: string, data: ConcludeMedicalBoardCaseRequest): Promise<MedicalBoard> {
    return apiService.put<MedicalBoard>(`${this.baseUrl}/${boardId}/cases/${caseId}/conclude`, data);
  }

  /** A case taken off the board without a finding. A reason is required. */
  withdrawCase(boardId: string, caseId: string, reason: string): Promise<MedicalBoard> {
    return apiService.put<MedicalBoard>(`${this.baseUrl}/${boardId}/cases/${caseId}/withdraw`, reason);
  }

  /** ⚠ Refused once the board has reported — its membership is part of what its findings mean. */
  addMember(boardId: string, data: AddMedicalBoardMemberRequest): Promise<MedicalBoardMember> {
    return apiService.post<MedicalBoardMember>(`${this.baseUrl}/${boardId}/members`, data);
  }

  removeMember(boardId: string, memberId: string): Promise<unknown> {
    return apiService.delete<unknown>(`${this.baseUrl}/${boardId}/members/${memberId}`);
  }

  /** ⚠ Refused without members, or without an open case. */
  convene(boardId: string): Promise<MedicalBoard> {
    return apiService.put<MedicalBoard>(`${this.baseUrl}/${boardId}/convene`, {});
  }

  recordSitting(boardId: string, data: RecordMedicalBoardSittingRequest): Promise<MedicalBoardSitting> {
    return apiService.post<MedicalBoardSitting>(`${this.baseUrl}/${boardId}/sittings`, data);
  }

  /** Replaces who was present. ⚠ Refused once a case has been decided at the sitting. */
  setSittingAttendance(boardId: string, sittingId: string, memberIds: string[]): Promise<MedicalBoardSitting> {
    return apiService.put<MedicalBoardSitting>(
      `${this.baseUrl}/${boardId}/sittings/${sittingId}/attendance`,
      { memberIds },
    );
  }

  // ── Incapacity and compensation (lane K-II-b; PNDCL 187) ──────────────────

  /** The whole assessment, replacing the last. ⚠ Refused once the labour officer's amount is recorded. */
  assessIncapacity(boardId: string, caseId: string, data: AssessIncapacityRequest): Promise<MedicalBoard> {
    return apiService.put<MedicalBoard>(`${this.baseUrl}/${boardId}/cases/${caseId}/incapacity`, data);
  }

  /** The labour officer's notice (s.35) and any agreement (s.15) — the whole record. */
  recordCompensation(boardId: string, caseId: string, data: RecordCompensationRequest): Promise<MedicalBoard> {
    return apiService.put<MedicalBoard>(`${this.baseUrl}/${boardId}/cases/${caseId}/compensation`, data);
  }

  /** ⚠ `null` is dropped by `apiService.put`; the endpoint reads no body as "clear". */
  linkSafetyIncident(boardId: string, caseId: string, incidentId: string | null): Promise<MedicalBoard> {
    return apiService.put<MedicalBoard>(`${this.baseUrl}/${boardId}/cases/${caseId}/safety-incident`, incidentId);
  }

  getSchedule(includeInactive = false): Promise<IncapacityScheduleItem[]> {
    return apiService.get<IncapacityScheduleItem[]>(
      `${this.baseUrl}/incapacity-schedule?includeInactive=${includeInactive}`,
    );
  }

  /** Medical admin: adds only PNDCL 187's rows not already there. */
  loadDefaultSchedule(): Promise<IncapacityScheduleItem[]> {
    return apiService.post<IncapacityScheduleItem[]>(`${this.baseUrl}/incapacity-schedule/load-defaults`, {});
  }

  addScheduleItem(data: SaveIncapacityScheduleItemRequest): Promise<IncapacityScheduleItem> {
    return apiService.post<IncapacityScheduleItem>(`${this.baseUrl}/incapacity-schedule`, data);
  }

  /** ⚠ Assessments already recorded keep the percentage they used. */
  updateScheduleItem(itemId: string, data: SaveIncapacityScheduleItemRequest): Promise<IncapacityScheduleItem> {
    return apiService.put<IncapacityScheduleItem>(`${this.baseUrl}/incapacity-schedule/${itemId}`, data);
  }

  /**
   * Stops a board that has not reported. ⚠ One action, two words (lane K5): a Requested board's
   * request is cancelled; a Convened board is dissolved. Its open cases are withdrawn with the reason.
   */
  cancel(boardId: string, reason: string): Promise<MedicalBoard> {
    return apiService.put<MedicalBoard>(`${this.baseUrl}/${boardId}/cancel`, reason);
  }

  // ── Documents (round 5, lane K4) ──────────────────────────────────────────
  // Multipart through the controlled-upload gate (scan-mandatory: a missing scanner is a 422 with a
  // reason, not a stored file). Served only by the gated download — never link to the route.

  getDocuments(boardId: string): Promise<MedicalBoardDocument[]> {
    return apiService.get<MedicalBoardDocument[]>(`${this.baseUrl}/${boardId}/documents`);
  }

  /** Allowed at any status — the signed report usually arrives after the board has concluded. */
  uploadDocument(
    boardId: string,
    file: File,
    description?: string | null,
    caseId?: string | null,
  ): Promise<MedicalBoardDocument> {
    return hrDocumentService.upload<MedicalBoardDocument>(`${this.baseUrl}/${boardId}/documents`, file, {
      description: description ?? undefined,
      caseId: caseId ?? undefined,
    });
  }

  downloadDocument(boardId: string, document: MedicalBoardDocument): Promise<void> {
    return hrDocumentService.download(
      `${this.baseUrl}/${boardId}/documents/${document.id}/download`,
      document.fileName,
    );
  }

  /** ⚠ Refused once the board has reported or been stopped — its papers are then part of the record. */
  removeDocument(boardId: string, documentId: string): Promise<unknown> {
    return apiService.delete<unknown>(`${this.baseUrl}/${boardId}/documents/${documentId}`);
  }
}

export const medicalBoardService = new MedicalBoardService();
