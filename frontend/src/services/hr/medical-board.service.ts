import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  MedicalBoard,
  MedicalBoardDocument,
  MedicalBoardMember,
  MedicalBoardPurpose,
  MedicalBoardSitting,
  MedicalBoardStatus,
  RequestMedicalBoardRequest,
  AddMedicalBoardMemberRequest,
  RecordMedicalBoardSittingRequest,
  ConcludeMedicalBoardRequest,
} from '@/types/hr/medical-board';

export interface MedicalBoardListParams {
  employeeId?: string;
  status?: MedicalBoardStatus;
  purpose?: MedicalBoardPurpose;
  from?: string;
  to?: string;
  search?: string;
  pageNumber?: number;
  pageSize?: number;
}

/**
 * Medical boards. Backend route: `api/hr/medical-boards`.
 *
 * ⚠ There is deliberately no method here for acting on a board. Leave and separation READ one —
 * leave through `leaveService.linkMedicalBoard` — and neither writes back. A board records what a
 * panel decided; what anybody does about it belongs to the module that acts.
 */
class MedicalBoardService {
  private readonly baseUrl = '/hr/medical-boards';

  list(params: MedicalBoardListParams = {}): Promise<PagedResult<MedicalBoard>> {
    const q = new URLSearchParams();
    if (params.employeeId) q.set('employeeId', params.employeeId);
    if (params.status) q.set('status', params.status);
    if (params.purpose) q.set('purpose', params.purpose);
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

  /** Asks for a board. It has no members and no finding yet. */
  request(data: RequestMedicalBoardRequest): Promise<MedicalBoard> {
    return apiService.post<MedicalBoard>(this.baseUrl, data);
  }

  /** ⚠ Refused once the board has reported — its membership is part of what its finding means. */
  addMember(boardId: string, data: AddMedicalBoardMemberRequest): Promise<MedicalBoardMember> {
    return apiService.post<MedicalBoardMember>(`${this.baseUrl}/${boardId}/members`, data);
  }

  removeMember(boardId: string, memberId: string): Promise<unknown> {
    return apiService.delete<unknown>(`${this.baseUrl}/${boardId}/members/${memberId}`);
  }

  /** ⚠ Refused without members: a board is its panel, and one with nobody on it cannot sit. */
  convene(boardId: string): Promise<MedicalBoard> {
    return apiService.put<MedicalBoard>(`${this.baseUrl}/${boardId}/convene`, {});
  }

  recordSitting(boardId: string, data: RecordMedicalBoardSittingRequest): Promise<MedicalBoardSitting> {
    return apiService.post<MedicalBoardSitting>(`${this.baseUrl}/${boardId}/sittings`, data);
  }

  /**
   * The board reports.
   *
   * ⚠ Cannot be undone, and it is the only status leave and separation act on. A board must have
   * sat at least once first — one that never met cannot have reached a finding.
   */
  conclude(boardId: string, data: ConcludeMedicalBoardRequest): Promise<MedicalBoard> {
    return apiService.put<MedicalBoard>(`${this.baseUrl}/${boardId}/conclude`, data);
  }

  /**
   * Stops a board that has not reported. ⚠ One action, two words (lane K5): a Requested board's
   * request is cancelled; a Convened board is dissolved. The answer's `wasDissolved` says which.
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
  uploadDocument(boardId: string, file: File, description?: string | null): Promise<MedicalBoardDocument> {
    return hrDocumentService.upload<MedicalBoardDocument>(`${this.baseUrl}/${boardId}/documents`, file, {
      description: description ?? undefined,
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
