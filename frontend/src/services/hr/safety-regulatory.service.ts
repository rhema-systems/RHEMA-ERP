import { apiService } from '../api.service';
import type { SheRegulatoryDomain } from '@/types/hr/safety';
import type {
  SheComplianceStatus,
  SheRegulatoryObligation,
  SheRegulatoryObligationSummary,
  SheRegulatoryObligationCreateRequest,
  SheRegulatoryObligationUpdateRequest,
  SheRegulatoryComplianceEvidence,
  SheRegulatoryComplianceEvidenceCreateRequest,
} from '@/types/hr/safety-governance';

/**
 * Regulatory compliance obligations and evidence (FR-SHE-180–182, FR-SHE-084).
 * Backend route: api/safety/regulatory. Regulatory BODIES are reference data
 * (safety-reference.service). GNFS liaison is modelled here: GNFS the body +
 * FireSafety-domain obligations + evidence rows per inspection/certificate.
 *
 * Obligation codes are user-entered, unique per tenant (duplicate → 422), and
 * immutable. Due-date alerts ride the reminder engine's statutory 180/90/60/30/14/7 ladder;
 * due-for-review is the work queue.
 */
class SafetyRegulatoryService {
  private readonly baseUrl = '/safety/regulatory';

  getAllObligations(): Promise<SheRegulatoryObligationSummary[]> {
    return apiService.get<SheRegulatoryObligationSummary[]>(`${this.baseUrl}/obligations`);
  }

  getObligation(id: string): Promise<SheRegulatoryObligation> {
    return apiService.get<SheRegulatoryObligation>(`${this.baseUrl}/obligations/${id}`);
  }

  getObligationByCode(code: string): Promise<SheRegulatoryObligation | null> {
    return apiService.get<SheRegulatoryObligation | null>(
      `${this.baseUrl}/obligations/code/${encodeURIComponent(code)}`,
    );
  }

  getObligationsByDomain(domain: SheRegulatoryDomain): Promise<SheRegulatoryObligationSummary[]> {
    return apiService.get<SheRegulatoryObligationSummary[]>(
      `${this.baseUrl}/obligations/domain/${domain}`,
    );
  }

  getObligationsByStatus(status: SheComplianceStatus): Promise<SheRegulatoryObligationSummary[]> {
    return apiService.get<SheRegulatoryObligationSummary[]>(
      `${this.baseUrl}/obligations/status/${status}`,
    );
  }

  getObligationsByOwner(ownerId: string): Promise<SheRegulatoryObligationSummary[]> {
    return apiService.get<SheRegulatoryObligationSummary[]>(
      `${this.baseUrl}/obligations/owner/${ownerId}`,
    );
  }

  getObligationsDueForReview(daysAhead = 30): Promise<SheRegulatoryObligationSummary[]> {
    return apiService.get<SheRegulatoryObligationSummary[]>(
      `${this.baseUrl}/obligations/due-for-review`,
      { daysAhead },
    );
  }

  getNonCompliantObligations(): Promise<SheRegulatoryObligationSummary[]> {
    return apiService.get<SheRegulatoryObligationSummary[]>(
      `${this.baseUrl}/obligations/non-compliant`,
    );
  }

  /** Refused (422) when the obligation code is already taken. */
  createObligation(data: SheRegulatoryObligationCreateRequest): Promise<SheRegulatoryObligation> {
    return apiService.post<SheRegulatoryObligation>(`${this.baseUrl}/obligations`, data);
  }

  updateObligation(
    id: string,
    data: SheRegulatoryObligationUpdateRequest,
  ): Promise<SheRegulatoryObligation> {
    return apiService.put<SheRegulatoryObligation>(`${this.baseUrl}/obligations/${id}`, data);
  }

  removeObligation(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/obligations/${id}`);
  }

  // ── Evidence ───────────────────────────────────────────────────────────────

  addEvidence(
    obligationId: string,
    data: SheRegulatoryComplianceEvidenceCreateRequest,
  ): Promise<SheRegulatoryComplianceEvidence> {
    return apiService.post<SheRegulatoryComplianceEvidence>(
      `${this.baseUrl}/obligations/${obligationId}/evidence`,
      data,
    );
  }

  removeEvidence(evidenceId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/evidence/${evidenceId}`);
  }
}

export const safetyRegulatoryService = new SafetyRegulatoryService();
export default safetyRegulatoryService;
