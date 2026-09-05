import { apiService } from '../api.service';
import type {
  SheWasteType,
  SheWasteTypeCreateRequest,
  SheWasteTypeUpdateRequest,
  SheWasteClassification,
  SheWasteDisposalRecord,
  SheWasteDisposalRecordSummary,
  SheWasteDisposalRecordCreateRequest,
  SheWasteDisposalRecordUpdateRequest,
} from '@/types/hr/safety-environment';

/**
 * Waste management (FR-SHE-060–062): the waste-type catalogue and disposal records.
 * Backend route: api/safety/waste.
 *
 * The disposal-certificate gate is live: a record for a manifest-requiring waste type is
 * refused (422) until both the manifest number and the certificate document are recorded —
 * on create and update. Record numbers are server-assigned when left blank (WD-YYYY-NNNN).
 * There is no unfiltered record list — registers read by date range.
 */
class SafetyWasteService {
  private readonly baseUrl = '/safety/waste';

  // ── Waste types ────────────────────────────────────────────────────────────

  getTypes(activeOnly = false): Promise<SheWasteType[]> {
    return apiService.get<SheWasteType[]>(`${this.baseUrl}/types`, activeOnly ? { activeOnly } : undefined);
  }

  getTypesByClassification(classification: SheWasteClassification): Promise<SheWasteType[]> {
    return apiService.get<SheWasteType[]>(`${this.baseUrl}/types/classification/${classification}`);
  }

  createType(data: SheWasteTypeCreateRequest): Promise<SheWasteType> {
    return apiService.post<SheWasteType>(`${this.baseUrl}/types`, data);
  }

  updateType(id: string, data: SheWasteTypeUpdateRequest): Promise<SheWasteType> {
    return apiService.put<SheWasteType>(`${this.baseUrl}/types/${id}`, data);
  }

  removeType(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/types/${id}`);
  }

  // ── Disposal records ───────────────────────────────────────────────────────

  getRecord(id: string): Promise<SheWasteDisposalRecord> {
    return apiService.get<SheWasteDisposalRecord>(`${this.baseUrl}/records/${id}`);
  }

  getRecordsByWasteType(wasteTypeId: string): Promise<SheWasteDisposalRecordSummary[]> {
    return apiService.get<SheWasteDisposalRecordSummary[]>(
      `${this.baseUrl}/records/by-waste-type/${wasteTypeId}`,
    );
  }

  getRecordsByDateRange(from: string, to: string): Promise<SheWasteDisposalRecordSummary[]> {
    return apiService.get<SheWasteDisposalRecordSummary[]>(`${this.baseUrl}/records/date-range`, {
      from,
      to,
    });
  }

  getRecordsByContractor(contractorId: string): Promise<SheWasteDisposalRecordSummary[]> {
    return apiService.get<SheWasteDisposalRecordSummary[]>(
      `${this.baseUrl}/records/by-contractor/${contractorId}`,
    );
  }

  /** Refused (422) without manifest + certificate when the waste type requires a manifest. */
  createRecord(data: SheWasteDisposalRecordCreateRequest): Promise<SheWasteDisposalRecord> {
    return apiService.post<SheWasteDisposalRecord>(`${this.baseUrl}/records`, data);
  }

  /** Refused (422) without manifest + certificate when the waste type requires a manifest. */
  updateRecord(id: string, data: SheWasteDisposalRecordUpdateRequest): Promise<SheWasteDisposalRecord> {
    return apiService.put<SheWasteDisposalRecord>(`${this.baseUrl}/records/${id}`, data);
  }

  removeRecord(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/records/${id}`);
  }
}

export const safetyWasteService = new SafetyWasteService();
export default safetyWasteService;
