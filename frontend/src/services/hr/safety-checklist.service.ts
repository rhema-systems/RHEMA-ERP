import { apiService } from '../api.service';
import type {
  SheInspectionChecklist,
  SheInspectionChecklistCreateRequest,
  SheInspectionChecklistUpdateRequest,
  SheInspectionChecklistItem,
  SheInspectionChecklistItemCreateRequest,
  SheInspectionChecklistItemUpdateRequest,
  SheInspectionType,
} from '@/types/hr/safety-inspections';

/**
 * Inspection checklist templates and their items. Backend route:
 * api/safety/inspection-checklists. HR-gated throughout; a duplicate checklist number within
 * the tenant is refused with 422. Items come back ordered by itemOrder on the detail read.
 */
class SafetyChecklistService {
  private readonly baseUrl = '/safety/inspection-checklists';

  getAll(activeOnly = false): Promise<SheInspectionChecklist[]> {
    return apiService.get<SheInspectionChecklist[]>(
      this.baseUrl,
      activeOnly ? { activeOnly } : undefined,
    );
  }

  getById(id: string): Promise<SheInspectionChecklist> {
    return apiService.get<SheInspectionChecklist>(`${this.baseUrl}/${id}`);
  }

  getByType(type: SheInspectionType): Promise<SheInspectionChecklist[]> {
    return apiService.get<SheInspectionChecklist[]>(`${this.baseUrl}/type/${type}`);
  }

  create(data: SheInspectionChecklistCreateRequest): Promise<SheInspectionChecklist> {
    return apiService.post<SheInspectionChecklist>(this.baseUrl, data);
  }

  update(id: string, data: SheInspectionChecklistUpdateRequest): Promise<SheInspectionChecklist> {
    return apiService.put<SheInspectionChecklist>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Items ──────────────────────────────────────────────────────────────────

  addItem(
    checklistId: string,
    data: SheInspectionChecklistItemCreateRequest,
  ): Promise<SheInspectionChecklistItem> {
    return apiService.post<SheInspectionChecklistItem>(
      `${this.baseUrl}/${checklistId}/items`,
      data,
    );
  }

  updateItem(
    itemId: string,
    data: SheInspectionChecklistItemUpdateRequest,
  ): Promise<SheInspectionChecklistItem> {
    return apiService.put<SheInspectionChecklistItem>(`${this.baseUrl}/items/${itemId}`, data);
  }

  removeItem(itemId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/items/${itemId}`);
  }
}

export const safetyChecklistService = new SafetyChecklistService();
export default safetyChecklistService;
