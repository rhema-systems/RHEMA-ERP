import { apiService } from '../api.service';
import type {
  SheIncidentType,
  SheIncidentTypeCreateRequest,
  SheIncidentTypeUpdateRequest,
  SheInjuryType,
  SheInjuryTypeCreateRequest,
  SheInjuryTypeUpdateRequest,
  SheBodyPart,
  SheBodyPartCreateRequest,
  SheBodyPartUpdateRequest,
  SheCorrectiveActionTemplate,
  SheCorrectiveActionTemplateCreateRequest,
  SheCorrectiveActionTemplateUpdateRequest,
  SheRegulatoryBody,
  SheRegulatoryBodyCreateRequest,
  SheRegulatoryBodyUpdateRequest,
} from '@/types/hr/safety';

/**
 * The SHE reference catalogue: incident types, injury types, body parts, corrective-action
 * templates and regulatory bodies. Backend route: api/safety/reference. HR-only end to end
 * (class-level role gate). Deletes are soft; 404s carry the service's own message.
 */
class SafetyReferenceService {
  private readonly baseUrl = '/safety/reference';

  // ── Incident types ─────────────────────────────────────────────────────────

  getIncidentTypes(activeOnly = false): Promise<SheIncidentType[]> {
    return apiService.get<SheIncidentType[]>(`${this.baseUrl}/incident-types`, { activeOnly });
  }

  getReportableIncidentTypes(): Promise<SheIncidentType[]> {
    return apiService.get<SheIncidentType[]>(`${this.baseUrl}/incident-types/reportable`);
  }

  getIncidentType(id: string): Promise<SheIncidentType> {
    return apiService.get<SheIncidentType>(`${this.baseUrl}/incident-types/${id}`);
  }

  createIncidentType(data: SheIncidentTypeCreateRequest): Promise<SheIncidentType> {
    return apiService.post<SheIncidentType>(`${this.baseUrl}/incident-types`, data);
  }

  updateIncidentType(id: string, data: SheIncidentTypeUpdateRequest): Promise<SheIncidentType> {
    return apiService.put<SheIncidentType>(`${this.baseUrl}/incident-types/${id}`, data);
  }

  removeIncidentType(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/incident-types/${id}`);
  }

  // ── Injury types ───────────────────────────────────────────────────────────

  getInjuryTypes(activeOnly = false): Promise<SheInjuryType[]> {
    return apiService.get<SheInjuryType[]>(`${this.baseUrl}/injury-types`, { activeOnly });
  }

  createInjuryType(data: SheInjuryTypeCreateRequest): Promise<SheInjuryType> {
    return apiService.post<SheInjuryType>(`${this.baseUrl}/injury-types`, data);
  }

  updateInjuryType(id: string, data: SheInjuryTypeUpdateRequest): Promise<SheInjuryType> {
    return apiService.put<SheInjuryType>(`${this.baseUrl}/injury-types/${id}`, data);
  }

  removeInjuryType(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/injury-types/${id}`);
  }

  // ── Body parts ─────────────────────────────────────────────────────────────

  getBodyParts(activeOnly = false): Promise<SheBodyPart[]> {
    return apiService.get<SheBodyPart[]>(`${this.baseUrl}/body-parts`, { activeOnly });
  }

  createBodyPart(data: SheBodyPartCreateRequest): Promise<SheBodyPart> {
    return apiService.post<SheBodyPart>(`${this.baseUrl}/body-parts`, data);
  }

  updateBodyPart(id: string, data: SheBodyPartUpdateRequest): Promise<SheBodyPart> {
    return apiService.put<SheBodyPart>(`${this.baseUrl}/body-parts/${id}`, data);
  }

  removeBodyPart(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/body-parts/${id}`);
  }

  // ── Corrective action templates ────────────────────────────────────────────

  getCorrectiveActionTemplates(activeOnly = false): Promise<SheCorrectiveActionTemplate[]> {
    return apiService.get<SheCorrectiveActionTemplate[]>(
      `${this.baseUrl}/corrective-action-templates`,
      { activeOnly },
    );
  }

  createCorrectiveActionTemplate(
    data: SheCorrectiveActionTemplateCreateRequest,
  ): Promise<SheCorrectiveActionTemplate> {
    return apiService.post<SheCorrectiveActionTemplate>(
      `${this.baseUrl}/corrective-action-templates`,
      data,
    );
  }

  updateCorrectiveActionTemplate(
    id: string,
    data: SheCorrectiveActionTemplateUpdateRequest,
  ): Promise<SheCorrectiveActionTemplate> {
    return apiService.put<SheCorrectiveActionTemplate>(
      `${this.baseUrl}/corrective-action-templates/${id}`,
      data,
    );
  }

  removeCorrectiveActionTemplate(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/corrective-action-templates/${id}`);
  }

  // ── Regulatory bodies ──────────────────────────────────────────────────────

  getRegulatoryBodies(activeOnly = false): Promise<SheRegulatoryBody[]> {
    return apiService.get<SheRegulatoryBody[]>(`${this.baseUrl}/regulatory-bodies`, { activeOnly });
  }

  createRegulatoryBody(data: SheRegulatoryBodyCreateRequest): Promise<SheRegulatoryBody> {
    return apiService.post<SheRegulatoryBody>(`${this.baseUrl}/regulatory-bodies`, data);
  }

  updateRegulatoryBody(id: string, data: SheRegulatoryBodyUpdateRequest): Promise<SheRegulatoryBody> {
    return apiService.put<SheRegulatoryBody>(`${this.baseUrl}/regulatory-bodies/${id}`, data);
  }

  removeRegulatoryBody(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/regulatory-bodies/${id}`);
  }
}

export const safetyReferenceService = new SafetyReferenceService();
export default safetyReferenceService;
