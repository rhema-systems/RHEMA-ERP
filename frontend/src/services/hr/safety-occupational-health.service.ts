import { apiService } from '../api.service';
import type {
  SheHealthSurveillance,
  SheHealthSurveillanceSummary,
  SheHealthSurveillanceCreateRequest,
  SheHealthSurveillanceUpdateRequest,
  SheHealthSurveillanceType,
  SheHealthSurveillanceResult,
  SheFirstAidStation,
  SheFirstAidStationCreateRequest,
  SheFirstAidStationUpdateRequest,
  SheWellnessProgram,
  SheWellnessProgramCreateRequest,
  SheWellnessProgramUpdateRequest,
  SheWellnessProgramStatus,
  SheWellnessProgramType,
} from '@/types/hr/safety-health';

/**
 * Occupational health (FR-SHE-140–143): health surveillance, first-aid stations and wellness
 * programs. Backend route: api/safety/occupational-health.
 *
 * ⚠ Gated on the HR.Medical.* policies, not the SHE HR-role gate — surveillance carries
 * examination results and work restrictions. A 403 here means the user lacks medical
 * permissions, not HR ones. Duplicate surveillance numbers / station codes / program codes
 * are refused (422). Due queues are polled — nothing alerts until the slice-13 job engine.
 */
class SafetyOccupationalHealthService {
  private readonly baseUrl = '/safety/occupational-health';

  // ── Health surveillance ────────────────────────────────────────────────────

  getSurveillance(): Promise<SheHealthSurveillanceSummary[]> {
    return apiService.get<SheHealthSurveillanceSummary[]>(`${this.baseUrl}/surveillance`);
  }

  getSurveillanceById(id: string): Promise<SheHealthSurveillance> {
    return apiService.get<SheHealthSurveillance>(`${this.baseUrl}/surveillance/${id}`);
  }

  getSurveillanceByEmployee(employeeId: string): Promise<SheHealthSurveillanceSummary[]> {
    return apiService.get<SheHealthSurveillanceSummary[]>(
      `${this.baseUrl}/surveillance/by-employee/${employeeId}`,
    );
  }

  getSurveillanceByType(type: SheHealthSurveillanceType): Promise<SheHealthSurveillanceSummary[]> {
    return apiService.get<SheHealthSurveillanceSummary[]>(`${this.baseUrl}/surveillance/type/${type}`);
  }

  getSurveillanceByResult(
    result: SheHealthSurveillanceResult,
  ): Promise<SheHealthSurveillanceSummary[]> {
    return apiService.get<SheHealthSurveillanceSummary[]>(
      `${this.baseUrl}/surveillance/result/${result}`,
    );
  }

  getSurveillanceDueForExamination(daysAhead = 30): Promise<SheHealthSurveillanceSummary[]> {
    return apiService.get<SheHealthSurveillanceSummary[]>(
      `${this.baseUrl}/surveillance/due-for-examination`,
      { daysAhead },
    );
  }

  getSurveillanceWithRestrictions(): Promise<SheHealthSurveillanceSummary[]> {
    return apiService.get<SheHealthSurveillanceSummary[]>(
      `${this.baseUrl}/surveillance/with-restrictions`,
    );
  }

  createSurveillance(data: SheHealthSurveillanceCreateRequest): Promise<SheHealthSurveillance> {
    return apiService.post<SheHealthSurveillance>(`${this.baseUrl}/surveillance`, data);
  }

  updateSurveillance(
    id: string,
    data: SheHealthSurveillanceUpdateRequest,
  ): Promise<SheHealthSurveillance> {
    return apiService.put<SheHealthSurveillance>(`${this.baseUrl}/surveillance/${id}`, data);
  }

  removeSurveillance(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/surveillance/${id}`);
  }

  // ── First-aid stations ─────────────────────────────────────────────────────

  getFirstAidStations(activeOnly = false): Promise<SheFirstAidStation[]> {
    return apiService.get<SheFirstAidStation[]>(
      `${this.baseUrl}/first-aid-stations`,
      activeOnly ? { activeOnly } : undefined,
    );
  }

  getFirstAidStation(id: string): Promise<SheFirstAidStation> {
    return apiService.get<SheFirstAidStation>(`${this.baseUrl}/first-aid-stations/${id}`);
  }

  getFirstAidStationsByLocation(locationId: string): Promise<SheFirstAidStation[]> {
    return apiService.get<SheFirstAidStation[]>(
      `${this.baseUrl}/first-aid-stations/by-location/${locationId}`,
    );
  }

  getFirstAidStationsDueForInspection(daysAhead = 30): Promise<SheFirstAidStation[]> {
    return apiService.get<SheFirstAidStation[]>(
      `${this.baseUrl}/first-aid-stations/due-for-inspection`,
      { daysAhead },
    );
  }

  getUnderStockedStations(): Promise<SheFirstAidStation[]> {
    return apiService.get<SheFirstAidStation[]>(`${this.baseUrl}/first-aid-stations/under-stocked`);
  }

  createFirstAidStation(data: SheFirstAidStationCreateRequest): Promise<SheFirstAidStation> {
    return apiService.post<SheFirstAidStation>(`${this.baseUrl}/first-aid-stations`, data);
  }

  updateFirstAidStation(
    id: string,
    data: SheFirstAidStationUpdateRequest,
  ): Promise<SheFirstAidStation> {
    return apiService.put<SheFirstAidStation>(`${this.baseUrl}/first-aid-stations/${id}`, data);
  }

  removeFirstAidStation(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/first-aid-stations/${id}`);
  }

  // ── Wellness programs ──────────────────────────────────────────────────────

  getWellnessPrograms(activeOnly = false): Promise<SheWellnessProgram[]> {
    return apiService.get<SheWellnessProgram[]>(
      `${this.baseUrl}/wellness-programs`,
      activeOnly ? { activeOnly } : undefined,
    );
  }

  getWellnessProgram(id: string): Promise<SheWellnessProgram> {
    return apiService.get<SheWellnessProgram>(`${this.baseUrl}/wellness-programs/${id}`);
  }

  getWellnessProgramsByStatus(status: SheWellnessProgramStatus): Promise<SheWellnessProgram[]> {
    return apiService.get<SheWellnessProgram[]>(`${this.baseUrl}/wellness-programs/status/${status}`);
  }

  getWellnessProgramsByType(type: SheWellnessProgramType): Promise<SheWellnessProgram[]> {
    return apiService.get<SheWellnessProgram[]>(`${this.baseUrl}/wellness-programs/type/${type}`);
  }

  createWellnessProgram(data: SheWellnessProgramCreateRequest): Promise<SheWellnessProgram> {
    return apiService.post<SheWellnessProgram>(`${this.baseUrl}/wellness-programs`, data);
  }

  updateWellnessProgram(
    id: string,
    data: SheWellnessProgramUpdateRequest,
  ): Promise<SheWellnessProgram> {
    return apiService.put<SheWellnessProgram>(`${this.baseUrl}/wellness-programs/${id}`, data);
  }

  removeWellnessProgram(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/wellness-programs/${id}`);
  }
}

export const safetyOccupationalHealthService = new SafetyOccupationalHealthService();
export default safetyOccupationalHealthService;
