import { apiService } from '../api.service';
import type {
  SafetyInspection,
  SafetyInspectionSummary,
  SafetyInspectionCreateRequest,
  SafetyInspectionUpdateRequest,
  SafetyInspectionCloseRequest,
  SafetyInspectionItem,
  SafetyInspectionItemCreateRequest,
  SafetyInspectionItemUpdateRequest,
  SafetyInspectionHazard,
  SafetyInspectionHazardCreateRequest,
  SafetyInspectionHazardUpdateRequest,
  SafetyInspectionHazardAction,
  SafetyInspectionHazardActionCreateRequest,
  SafetyInspectionHazardActionUpdateRequest,
  SafetyInspectionDocument,
  SafetyInspectionDocumentCreateRequest,
  SheInspectionType,
  SheInspectionCategory,
  SheInspectionStatus,
} from '@/types/hr/safety-inspections';

/**
 * Safety inspections: scheduling, checklist findings, hazards discovered on the walk, their
 * corrective actions, documents, and the guarded close-out. Backend route:
 * api/safety/inspections. HR-gated throughout. Closing is refused with 422 while any finding
 * is unresolved or any corrective action is open, and a closed inspection refuses a second close.
 */
class SafetyInspectionService {
  private readonly baseUrl = '/safety/inspections';

  // ── Queries ────────────────────────────────────────────────────────────────

  getAll(): Promise<SafetyInspectionSummary[]> {
    return apiService.get<SafetyInspectionSummary[]>(this.baseUrl);
  }

  getById(id: string): Promise<SafetyInspection> {
    return apiService.get<SafetyInspection>(`${this.baseUrl}/${id}`);
  }

  getByNumber(inspectionNumber: string): Promise<SafetyInspection | null> {
    return apiService.get<SafetyInspection | null>(
      `${this.baseUrl}/number/${encodeURIComponent(inspectionNumber)}`,
    );
  }

  getByStatus(status: SheInspectionStatus): Promise<SafetyInspectionSummary[]> {
    return apiService.get<SafetyInspectionSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getByType(type: SheInspectionType): Promise<SafetyInspectionSummary[]> {
    return apiService.get<SafetyInspectionSummary[]>(`${this.baseUrl}/type/${type}`);
  }

  getByCategory(category: SheInspectionCategory): Promise<SafetyInspectionSummary[]> {
    return apiService.get<SafetyInspectionSummary[]>(`${this.baseUrl}/category/${category}`);
  }

  getByDateRange(from: string, to: string): Promise<SafetyInspectionSummary[]> {
    return apiService.get<SafetyInspectionSummary[]>(`${this.baseUrl}/date-range`, { from, to });
  }

  getByInspector(inspectorId: string): Promise<SafetyInspectionSummary[]> {
    return apiService.get<SafetyInspectionSummary[]>(`${this.baseUrl}/inspector/${inspectorId}`);
  }

  getByLocation(locationId: string): Promise<SafetyInspectionSummary[]> {
    return apiService.get<SafetyInspectionSummary[]>(`${this.baseUrl}/location/${locationId}`);
  }

  getDue(daysAhead = 30): Promise<SafetyInspectionSummary[]> {
    return apiService.get<SafetyInspectionSummary[]>(`${this.baseUrl}/due`, { daysAhead });
  }

  getOpenWithFindings(): Promise<SafetyInspectionSummary[]> {
    return apiService.get<SafetyInspectionSummary[]>(`${this.baseUrl}/open-with-findings`);
  }

  // ── CRUD + lifecycle ───────────────────────────────────────────────────────

  create(data: SafetyInspectionCreateRequest): Promise<SafetyInspection> {
    return apiService.post<SafetyInspection>(this.baseUrl, data);
  }

  update(id: string, data: SafetyInspectionUpdateRequest): Promise<SafetyInspection> {
    return apiService.put<SafetyInspection>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Refused (422) while findings are unresolved or corrective actions open. */
  close(id: string, data: SafetyInspectionCloseRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/close`, data);
  }

  // ── Items (findings) ───────────────────────────────────────────────────────

  addItem(
    inspectionId: string,
    data: SafetyInspectionItemCreateRequest,
  ): Promise<SafetyInspectionItem> {
    return apiService.post<SafetyInspectionItem>(`${this.baseUrl}/${inspectionId}/items`, data);
  }

  updateItem(itemId: string, data: SafetyInspectionItemUpdateRequest): Promise<SafetyInspectionItem> {
    return apiService.put<SafetyInspectionItem>(`${this.baseUrl}/items/${itemId}`, data);
  }

  removeItem(itemId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/items/${itemId}`);
  }

  // ── Discovered hazards ─────────────────────────────────────────────────────

  addHazard(
    inspectionId: string,
    data: SafetyInspectionHazardCreateRequest,
  ): Promise<SafetyInspectionHazard> {
    return apiService.post<SafetyInspectionHazard>(`${this.baseUrl}/${inspectionId}/hazards`, data);
  }

  updateHazard(
    hazardId: string,
    data: SafetyInspectionHazardUpdateRequest,
  ): Promise<SafetyInspectionHazard> {
    return apiService.put<SafetyInspectionHazard>(`${this.baseUrl}/hazards/${hazardId}`, data);
  }

  removeHazard(hazardId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/hazards/${hazardId}`);
  }

  // ── Hazard corrective actions ──────────────────────────────────────────────

  addHazardAction(
    hazardId: string,
    data: SafetyInspectionHazardActionCreateRequest,
  ): Promise<SafetyInspectionHazardAction> {
    return apiService.post<SafetyInspectionHazardAction>(
      `${this.baseUrl}/hazards/${hazardId}/actions`,
      data,
    );
  }

  updateHazardAction(
    actionId: string,
    data: SafetyInspectionHazardActionUpdateRequest,
  ): Promise<SafetyInspectionHazardAction> {
    return apiService.put<SafetyInspectionHazardAction>(
      `${this.baseUrl}/hazard-actions/${actionId}`,
      data,
    );
  }

  removeHazardAction(actionId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/hazard-actions/${actionId}`);
  }

  // ── Documents ──────────────────────────────────────────────────────────────

  addDocument(
    inspectionId: string,
    data: SafetyInspectionDocumentCreateRequest,
  ): Promise<SafetyInspectionDocument> {
    return apiService.post<SafetyInspectionDocument>(
      `${this.baseUrl}/${inspectionId}/documents`,
      data,
    );
  }

  removeDocument(documentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/documents/${documentId}`);
  }
}

export const safetyInspectionService = new SafetyInspectionService();
export default safetyInspectionService;
