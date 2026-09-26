import { apiService } from '../api.service';
import type {
  SheInspectionChecklist,
  SheInspectionChecklistCreateRequest,
  SheInspectionChecklistUpdateRequest,
  SheInspectionChecklistItem,
  SheInspectionChecklistItemCreateRequest,
  SheInspectionChecklistItemUpdateRequest,
  SheInspectionChecklistField,
  SheInspectionChecklistFieldCreateRequest,
  SheInspectionChecklistFieldUpdateRequest,
  SheInspectionChecklistSection,
  SheInspectionChecklistSectionCreateRequest,
  SheInspectionChecklistSectionUpdateRequest,
  SheInspectionChecklistOutcome,
  SheInspectionChecklistOutcomeCreateRequest,
  SheInspectionChecklistOutcomeUpdateRequest,
  SheInspectionChecklistSignatory,
  SheInspectionChecklistSignatoryCreateRequest,
  SheInspectionChecklistSignatoryUpdateRequest,
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

  // ── Builder: lifecycle (docs/HR/areas/she/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md §4) ──

  /** Validates the structure and freezes it; refused (422) with the list of problems otherwise. */
  publish(id: string): Promise<SheInspectionChecklist> {
    return apiService.post<SheInspectionChecklist>(`${this.baseUrl}/${id}/publish`);
  }

  retire(id: string): Promise<SheInspectionChecklist> {
    return apiService.post<SheInspectionChecklist>(`${this.baseUrl}/${id}/retire`);
  }

  /** Clones a published / retired template into Draft v+1 under the same number. */
  createNewVersion(id: string): Promise<SheInspectionChecklist> {
    return apiService.post<SheInspectionChecklist>(`${this.baseUrl}/${id}/new-version`);
  }

  // ── Builder: header fields ──

  addField(checklistId: string, data: SheInspectionChecklistFieldCreateRequest): Promise<SheInspectionChecklistField> {
    return apiService.post<SheInspectionChecklistField>(`${this.baseUrl}/${checklistId}/fields`, data);
  }

  updateField(fieldId: string, data: SheInspectionChecklistFieldUpdateRequest): Promise<SheInspectionChecklistField> {
    return apiService.put<SheInspectionChecklistField>(`${this.baseUrl}/fields/${fieldId}`, data);
  }

  removeField(fieldId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/fields/${fieldId}`);
  }

  reorderFields(checklistId: string, orderedIds: string[]): Promise<SheInspectionChecklist> {
    return apiService.put<SheInspectionChecklist>(`${this.baseUrl}/${checklistId}/fields/order`, { orderedIds });
  }

  // ── Builder: sections ──

  addSection(checklistId: string, data: SheInspectionChecklistSectionCreateRequest): Promise<SheInspectionChecklistSection> {
    return apiService.post<SheInspectionChecklistSection>(`${this.baseUrl}/${checklistId}/sections`, data);
  }

  updateSection(sectionId: string, data: SheInspectionChecklistSectionUpdateRequest): Promise<SheInspectionChecklistSection> {
    return apiService.put<SheInspectionChecklistSection>(`${this.baseUrl}/sections/${sectionId}`, data);
  }

  /** Deletes the section and its items. */
  removeSection(sectionId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/sections/${sectionId}`);
  }

  reorderSections(checklistId: string, orderedIds: string[]): Promise<SheInspectionChecklist> {
    return apiService.put<SheInspectionChecklist>(`${this.baseUrl}/${checklistId}/sections/order`, { orderedIds });
  }

  reorderSectionItems(sectionId: string, orderedIds: string[]): Promise<SheInspectionChecklist> {
    return apiService.put<SheInspectionChecklist>(`${this.baseUrl}/sections/${sectionId}/items/order`, { orderedIds });
  }

  // ── Builder: outcomes ──

  addOutcome(checklistId: string, data: SheInspectionChecklistOutcomeCreateRequest): Promise<SheInspectionChecklistOutcome> {
    return apiService.post<SheInspectionChecklistOutcome>(`${this.baseUrl}/${checklistId}/outcomes`, data);
  }

  updateOutcome(outcomeId: string, data: SheInspectionChecklistOutcomeUpdateRequest): Promise<SheInspectionChecklistOutcome> {
    return apiService.put<SheInspectionChecklistOutcome>(`${this.baseUrl}/outcomes/${outcomeId}`, data);
  }

  removeOutcome(outcomeId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/outcomes/${outcomeId}`);
  }

  reorderOutcomes(checklistId: string, orderedIds: string[]): Promise<SheInspectionChecklist> {
    return apiService.put<SheInspectionChecklist>(`${this.baseUrl}/${checklistId}/outcomes/order`, { orderedIds });
  }

  // ── Builder: signatories ──

  addSignatory(checklistId: string, data: SheInspectionChecklistSignatoryCreateRequest): Promise<SheInspectionChecklistSignatory> {
    return apiService.post<SheInspectionChecklistSignatory>(`${this.baseUrl}/${checklistId}/signatories`, data);
  }

  updateSignatory(signatoryId: string, data: SheInspectionChecklistSignatoryUpdateRequest): Promise<SheInspectionChecklistSignatory> {
    return apiService.put<SheInspectionChecklistSignatory>(`${this.baseUrl}/signatories/${signatoryId}`, data);
  }

  removeSignatory(signatoryId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/signatories/${signatoryId}`);
  }

  reorderSignatories(checklistId: string, orderedIds: string[]): Promise<SheInspectionChecklist> {
    return apiService.put<SheInspectionChecklist>(`${this.baseUrl}/${checklistId}/signatories/order`, { orderedIds });
  }
}

export const safetyChecklistService = new SafetyChecklistService();
export default safetyChecklistService;
