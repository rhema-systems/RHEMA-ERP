import { apiService } from '../api.service';
import type {
  ShePermitToWork,
  ShePermitToWorkSummary,
  ShePermitToWorkCreateRequest,
  ShePermitToWorkUpdateRequest,
  ShePermitToWorkApproveRequest,
  ShePermitToWorkSuspendRequest,
  ShePermitToWorkCloseRequest,
  ShePermitToWorkWorker,
  ShePermitToWorkWorkerCreateRequest,
  ShePermitToWorkWorkerUpdateRequest,
  ShePermitToWorkExtension,
  ShePermitToWorkExtensionCreateRequest,
  ShePermitToWorkDocument,
  ShePermitToWorkDocumentCreateRequest,
  ShePermitType,
  ShePermitStatus,
} from '@/types/hr/safety-permits';

/**
 * Permit to Work: the authorisation lifecycle, authorised workers, extensions and documents.
 * Backend route: api/safety/permits. HR-gated throughout.
 *
 * Approval is refused (422) unless Draft/PendingApproval AND the mandatory safety sections are
 * complete (FR-PTW-002); suspension needs Active; resumption needs Suspended; closing needs
 * Active/Suspended; extensions need Active; a Completed/Cancelled permit refuses edits.
 * Expiry IS automatic — the reminder engine expires permits past their window (hourly)
 * and warns as the end approaches; `getExpiring` remains the queue view.
 */
class SafetyPermitService {
  private readonly baseUrl = '/safety/permits';

  // ── Queries ────────────────────────────────────────────────────────────────

  getAll(): Promise<ShePermitToWorkSummary[]> {
    return apiService.get<ShePermitToWorkSummary[]>(this.baseUrl);
  }

  getById(id: string): Promise<ShePermitToWork> {
    return apiService.get<ShePermitToWork>(`${this.baseUrl}/${id}`);
  }

  getByNumber(permitNumber: string): Promise<ShePermitToWork | null> {
    return apiService.get<ShePermitToWork | null>(
      `${this.baseUrl}/number/${encodeURIComponent(permitNumber)}`,
    );
  }

  getByStatus(status: ShePermitStatus): Promise<ShePermitToWorkSummary[]> {
    return apiService.get<ShePermitToWorkSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getByType(type: ShePermitType): Promise<ShePermitToWorkSummary[]> {
    return apiService.get<ShePermitToWorkSummary[]>(`${this.baseUrl}/type/${type}`);
  }

  getActive(): Promise<ShePermitToWorkSummary[]> {
    return apiService.get<ShePermitToWorkSummary[]>(`${this.baseUrl}/active`);
  }

  getByContractor(contractorId: string): Promise<ShePermitToWorkSummary[]> {
    return apiService.get<ShePermitToWorkSummary[]>(`${this.baseUrl}/contractor/${contractorId}`);
  }

  getByRequestor(requestedById: string): Promise<ShePermitToWorkSummary[]> {
    return apiService.get<ShePermitToWorkSummary[]>(`${this.baseUrl}/requestor/${requestedById}`);
  }

  getExpiring(daysAhead = 1): Promise<ShePermitToWorkSummary[]> {
    return apiService.get<ShePermitToWorkSummary[]>(`${this.baseUrl}/expiring`, { daysAhead });
  }

  getSuspended(): Promise<ShePermitToWorkSummary[]> {
    return apiService.get<ShePermitToWorkSummary[]>(`${this.baseUrl}/suspended`);
  }

  // ── CRUD + lifecycle ───────────────────────────────────────────────────────

  create(data: ShePermitToWorkCreateRequest): Promise<ShePermitToWork> {
    return apiService.post<ShePermitToWork>(this.baseUrl, data);
  }

  update(id: string, data: ShePermitToWorkUpdateRequest): Promise<ShePermitToWork> {
    return apiService.put<ShePermitToWork>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Refused (422) until the mandatory safety sections are complete (FR-PTW-002). */
  approve(id: string, data: ShePermitToWorkApproveRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/approve`, data);
  }

  suspend(id: string, data: ShePermitToWorkSuspendRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/suspend`, data);
  }

  resume(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/resume`, {});
  }

  close(id: string, data: ShePermitToWorkCloseRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/close`, data);
  }

  // ── Authorised workers ─────────────────────────────────────────────────────

  addWorker(
    permitId: string,
    data: ShePermitToWorkWorkerCreateRequest,
  ): Promise<ShePermitToWorkWorker> {
    return apiService.post<ShePermitToWorkWorker>(`${this.baseUrl}/${permitId}/workers`, data);
  }

  updateWorker(
    workerId: string,
    data: ShePermitToWorkWorkerUpdateRequest,
  ): Promise<ShePermitToWorkWorker> {
    return apiService.put<ShePermitToWorkWorker>(`${this.baseUrl}/workers/${workerId}`, data);
  }

  removeWorker(workerId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/workers/${workerId}`);
  }

  // ── Extensions ─────────────────────────────────────────────────────────────

  addExtension(
    permitId: string,
    data: ShePermitToWorkExtensionCreateRequest,
  ): Promise<ShePermitToWorkExtension> {
    return apiService.post<ShePermitToWorkExtension>(`${this.baseUrl}/${permitId}/extensions`, data);
  }

  // ── Documents ──────────────────────────────────────────────────────────────

  addDocument(
    permitId: string,
    data: ShePermitToWorkDocumentCreateRequest,
  ): Promise<ShePermitToWorkDocument> {
    return apiService.post<ShePermitToWorkDocument>(`${this.baseUrl}/${permitId}/documents`, data);
  }

  removeDocument(documentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/documents/${documentId}`);
  }
}

export const safetyPermitService = new SafetyPermitService();
export default safetyPermitService;
