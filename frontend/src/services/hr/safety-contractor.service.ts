import { apiService } from '../api.service';
import type {
  SheContractor,
  SheContractorSummary,
  SheContractorCreateRequest,
  SheContractorUpdateRequest,
  SheContractorPreQualifyRequest,
  SheContractorStatus,
  SheContractorInduction,
  SheContractorInductionCreateRequest,
  SheContractorInductionUpdateRequest,
  SheContractorInspection,
  SheContractorInspectionCreateRequest,
  SheContractorInspectionUpdateRequest,
  SheContractorNonCompliance,
  SheContractorNonComplianceCreateRequest,
  SheContractorNonComplianceUpdateRequest,
  SheContractorNonComplianceCloseRequest,
  SheContractorDocument,
  SheContractorDocumentCreateRequest,
  SheContractorDocumentVerifyRequest,
} from '@/types/hr/safety-contractors';

/**
 * Contractor SHE management (FRD §7): the register with pre-qualification, worker inductions,
 * contractor SHE inspections, non-compliance notices and the document file. HR-gated throughout.
 * Backend route: api/safety/contractors.
 *
 * Duplicate contractor codes, inspection numbers and notice numbers are refused (422).
 * Closed notices cannot be edited or re-closed; closing goes through the close endpoint only.
 * Repeat-violation flags are computed server-side. Expiring pre-qualifications/documents are
 * polled queries — no automatic alerts until the slice-13 job engine.
 */
class SafetyContractorService {
  private readonly baseUrl = '/safety/contractors';

  // ── Contractors ────────────────────────────────────────────────────────────

  getAll(): Promise<SheContractorSummary[]> {
    return apiService.get<SheContractorSummary[]>(this.baseUrl);
  }

  getById(id: string): Promise<SheContractor> {
    return apiService.get<SheContractor>(`${this.baseUrl}/${id}`);
  }

  getByCode(contractorCode: string): Promise<SheContractor | null> {
    return apiService.get<SheContractor | null>(
      `${this.baseUrl}/code/${encodeURIComponent(contractorCode)}`,
    );
  }

  getByStatus(status: SheContractorStatus): Promise<SheContractorSummary[]> {
    return apiService.get<SheContractorSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getActive(): Promise<SheContractorSummary[]> {
    return apiService.get<SheContractorSummary[]>(`${this.baseUrl}/active`);
  }

  getExpiringPreQualification(daysAhead = 30): Promise<SheContractorSummary[]> {
    return apiService.get<SheContractorSummary[]>(`${this.baseUrl}/expiring-prequalification`, {
      daysAhead,
    });
  }

  getWithOpenNonCompliances(): Promise<SheContractorSummary[]> {
    return apiService.get<SheContractorSummary[]>(`${this.baseUrl}/with-open-non-compliances`);
  }

  create(data: SheContractorCreateRequest): Promise<SheContractor> {
    return apiService.post<SheContractor>(this.baseUrl, data);
  }

  update(id: string, data: SheContractorUpdateRequest): Promise<SheContractor> {
    return apiService.put<SheContractor>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Records the assessment outcome — 'PendingAssessment' as the outcome is refused (422). */
  preQualify(id: string, data: SheContractorPreQualifyRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/prequalify`, data);
  }

  // ── Inductions ─────────────────────────────────────────────────────────────

  addInduction(
    contractorId: string,
    data: SheContractorInductionCreateRequest,
  ): Promise<SheContractorInduction> {
    return apiService.post<SheContractorInduction>(
      `${this.baseUrl}/${contractorId}/inductions`,
      data,
    );
  }

  updateInduction(
    inductionId: string,
    data: SheContractorInductionUpdateRequest,
  ): Promise<SheContractorInduction> {
    return apiService.put<SheContractorInduction>(
      `${this.baseUrl}/inductions/${inductionId}`,
      data,
    );
  }

  removeInduction(inductionId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/inductions/${inductionId}`);
  }

  // ── Contractor SHE inspections (no delete endpoint — the record is permanent) ──

  getInspections(contractorId: string): Promise<SheContractorInspection[]> {
    return apiService.get<SheContractorInspection[]>(
      `${this.baseUrl}/${contractorId}/inspections`,
    );
  }

  addInspection(
    contractorId: string,
    data: SheContractorInspectionCreateRequest,
  ): Promise<SheContractorInspection> {
    return apiService.post<SheContractorInspection>(
      `${this.baseUrl}/${contractorId}/inspections`,
      data,
    );
  }

  updateInspection(
    inspectionId: string,
    data: SheContractorInspectionUpdateRequest,
  ): Promise<SheContractorInspection> {
    return apiService.put<SheContractorInspection>(
      `${this.baseUrl}/inspections/${inspectionId}`,
      data,
    );
  }

  // ── Non-compliance notices (no delete endpoint — closed, never removed) ─────

  getNonCompliances(contractorId: string): Promise<SheContractorNonCompliance[]> {
    return apiService.get<SheContractorNonCompliance[]>(
      `${this.baseUrl}/${contractorId}/non-compliances`,
    );
  }

  getOpenNonCompliances(): Promise<SheContractorNonCompliance[]> {
    return apiService.get<SheContractorNonCompliance[]>(`${this.baseUrl}/non-compliances/open`);
  }

  getOverdueNonCompliances(): Promise<SheContractorNonCompliance[]> {
    return apiService.get<SheContractorNonCompliance[]>(
      `${this.baseUrl}/non-compliances/overdue`,
    );
  }

  addNonCompliance(
    contractorId: string,
    data: SheContractorNonComplianceCreateRequest,
  ): Promise<SheContractorNonCompliance> {
    return apiService.post<SheContractorNonCompliance>(
      `${this.baseUrl}/${contractorId}/non-compliances`,
      data,
    );
  }

  /** Refused (422) when the notice is closed, or when status is set to Closed here. */
  updateNonCompliance(
    nonComplianceId: string,
    data: SheContractorNonComplianceUpdateRequest,
  ): Promise<SheContractorNonCompliance> {
    return apiService.put<SheContractorNonCompliance>(
      `${this.baseUrl}/non-compliances/${nonComplianceId}`,
      data,
    );
  }

  /** Refused (422) when the notice is already closed. */
  closeNonCompliance(
    nonComplianceId: string,
    data: SheContractorNonComplianceCloseRequest,
  ): Promise<void> {
    return apiService.post<void>(
      `${this.baseUrl}/non-compliances/${nonComplianceId}/close`,
      data,
    );
  }

  // ── Documents ──────────────────────────────────────────────────────────────

  getDocuments(contractorId: string): Promise<SheContractorDocument[]> {
    return apiService.get<SheContractorDocument[]>(`${this.baseUrl}/${contractorId}/documents`);
  }

  getExpiringDocuments(daysAhead = 30): Promise<SheContractorDocument[]> {
    return apiService.get<SheContractorDocument[]>(`${this.baseUrl}/documents/expiring`, {
      daysAhead,
    });
  }

  getUnverifiedDocuments(): Promise<SheContractorDocument[]> {
    return apiService.get<SheContractorDocument[]>(`${this.baseUrl}/documents/unverified`);
  }

  addDocument(
    contractorId: string,
    data: SheContractorDocumentCreateRequest,
  ): Promise<SheContractorDocument> {
    return apiService.post<SheContractorDocument>(
      `${this.baseUrl}/${contractorId}/documents`,
      data,
    );
  }

  /** Refused (422) when the document has already been verified. */
  verifyDocument(documentId: string, data: SheContractorDocumentVerifyRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/documents/${documentId}/verify`, data);
  }

  removeDocument(documentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/documents/${documentId}`);
  }
}

export const safetyContractorService = new SafetyContractorService();
export default safetyContractorService;
