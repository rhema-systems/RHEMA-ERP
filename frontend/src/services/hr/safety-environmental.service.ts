import { apiService } from '../api.service';
import type {
  SheEnvironmentalIncident,
  SheEnvironmentalIncidentSummary,
  SheEnvironmentalIncidentCreateRequest,
  SheEnvironmentalIncidentUpdateRequest,
  SheEnvironmentalIncidentCloseRequest,
  SheEnvironmentalIncidentStatus,
  SheEnvironmentalIncidentType,
  SheEnvironmentalMonitoringRecord,
  SheEnvironmentalMonitoringRecordCreateRequest,
  SheEnvironmentalMonitoringRecordUpdateRequest,
  SheEnvironmentalMonitoringType,
} from '@/types/hr/safety-environment';

/**
 * Environmental data capture (FR-SHE-050–072): incidents (spills, exceedances, contamination)
 * and monitoring readings. Backend route: api/safety/environmental.
 *
 * Closed incidents refuse edits and re-closes; closing goes through the close endpoint.
 * Numbers are server-assigned when left blank (ENV-/EM-YYYY-NNNN). Monitoring exceedance
 * flags are computed server-side. EPA reporting is a hand-kept flag — nothing submits
 * anything automatically. There is no unfiltered list — registers read by date range.
 */
class SafetyEnvironmentalService {
  private readonly baseUrl = '/safety/environmental';

  // ── Incidents ──────────────────────────────────────────────────────────────

  getIncident(id: string): Promise<SheEnvironmentalIncident> {
    return apiService.get<SheEnvironmentalIncident>(`${this.baseUrl}/incidents/${id}`);
  }

  getIncidentByNumber(incidentNumber: string): Promise<SheEnvironmentalIncident | null> {
    return apiService.get<SheEnvironmentalIncident | null>(
      `${this.baseUrl}/incidents/number/${encodeURIComponent(incidentNumber)}`,
    );
  }

  getIncidentsByStatus(
    status: SheEnvironmentalIncidentStatus,
  ): Promise<SheEnvironmentalIncidentSummary[]> {
    return apiService.get<SheEnvironmentalIncidentSummary[]>(
      `${this.baseUrl}/incidents/status/${status}`,
    );
  }

  getIncidentsByType(
    type: SheEnvironmentalIncidentType,
  ): Promise<SheEnvironmentalIncidentSummary[]> {
    return apiService.get<SheEnvironmentalIncidentSummary[]>(`${this.baseUrl}/incidents/type/${type}`);
  }

  getIncidentsByDateRange(from: string, to: string): Promise<SheEnvironmentalIncidentSummary[]> {
    return apiService.get<SheEnvironmentalIncidentSummary[]>(`${this.baseUrl}/incidents/date-range`, {
      from,
      to,
    });
  }

  getOpenIncidents(): Promise<SheEnvironmentalIncidentSummary[]> {
    return apiService.get<SheEnvironmentalIncidentSummary[]>(`${this.baseUrl}/incidents/open`);
  }

  getIncidentsReportedToEpa(): Promise<SheEnvironmentalIncidentSummary[]> {
    return apiService.get<SheEnvironmentalIncidentSummary[]>(
      `${this.baseUrl}/incidents/reported-to-epa`,
    );
  }

  createIncident(data: SheEnvironmentalIncidentCreateRequest): Promise<SheEnvironmentalIncident> {
    return apiService.post<SheEnvironmentalIncident>(`${this.baseUrl}/incidents`, data);
  }

  /** Refused (422) on a closed incident, or when status is set to Closed here. */
  updateIncident(
    id: string,
    data: SheEnvironmentalIncidentUpdateRequest,
  ): Promise<SheEnvironmentalIncident> {
    return apiService.put<SheEnvironmentalIncident>(`${this.baseUrl}/incidents/${id}`, data);
  }

  /** Refused (422) when the incident is already closed. */
  closeIncident(id: string, data: SheEnvironmentalIncidentCloseRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/incidents/${id}/close`, data);
  }

  removeIncident(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/incidents/${id}`);
  }

  // ── Monitoring ─────────────────────────────────────────────────────────────

  getMonitoringRecord(id: string): Promise<SheEnvironmentalMonitoringRecord> {
    return apiService.get<SheEnvironmentalMonitoringRecord>(`${this.baseUrl}/monitoring/${id}`);
  }

  getMonitoringByType(
    type: SheEnvironmentalMonitoringType,
  ): Promise<SheEnvironmentalMonitoringRecord[]> {
    return apiService.get<SheEnvironmentalMonitoringRecord[]>(
      `${this.baseUrl}/monitoring/type/${type}`,
    );
  }

  getMonitoringByDateRange(from: string, to: string): Promise<SheEnvironmentalMonitoringRecord[]> {
    return apiService.get<SheEnvironmentalMonitoringRecord[]>(`${this.baseUrl}/monitoring/date-range`, {
      from,
      to,
    });
  }

  getMonitoringByLocation(locationId: string): Promise<SheEnvironmentalMonitoringRecord[]> {
    return apiService.get<SheEnvironmentalMonitoringRecord[]>(
      `${this.baseUrl}/monitoring/location/${locationId}`,
    );
  }

  getExceedances(): Promise<SheEnvironmentalMonitoringRecord[]> {
    return apiService.get<SheEnvironmentalMonitoringRecord[]>(`${this.baseUrl}/monitoring/exceedances`);
  }

  createMonitoringRecord(
    data: SheEnvironmentalMonitoringRecordCreateRequest,
  ): Promise<SheEnvironmentalMonitoringRecord> {
    return apiService.post<SheEnvironmentalMonitoringRecord>(`${this.baseUrl}/monitoring`, data);
  }

  updateMonitoringRecord(
    id: string,
    data: SheEnvironmentalMonitoringRecordUpdateRequest,
  ): Promise<SheEnvironmentalMonitoringRecord> {
    return apiService.put<SheEnvironmentalMonitoringRecord>(`${this.baseUrl}/monitoring/${id}`, data);
  }

  removeMonitoringRecord(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/monitoring/${id}`);
  }
}

export const safetyEnvironmentalService = new SafetyEnvironmentalService();
export default safetyEnvironmentalService;
