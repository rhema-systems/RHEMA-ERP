import { apiService } from '../api.service';
import type {
  SafetyEquipment,
  SafetyEquipmentSummary,
  SafetyEquipmentCreateRequest,
  SafetyEquipmentUpdateRequest,
  SafetyEquipmentInspection,
  SafetyEquipmentInspectionCreateRequest,
  SafetyEquipmentInspectionUpdateRequest,
  SafetyEquipmentInspectionAction,
  SafetyEquipmentInspectionActionCreateRequest,
  SafetyEquipmentInspectionActionUpdateRequest,
  SafetyEquipmentMaintenance,
  SafetyEquipmentMaintenanceCreateRequest,
  SafetyEquipmentMaintenanceUpdateRequest,
  SheSafetyEquipmentStatus,
  SheSafetyEquipmentType,
} from '@/types/hr/safety-equipment';

/**
 * The safety-equipment register: extinguishers, AEDs, detectors and the rest, with their
 * inspection history, corrective actions and maintenance records.
 * Backend route: api/safety/equipment. HR-gated throughout.
 *
 * The equipment number is assigned server-side (SEQ-YYYY-NNNN). Due/expiring reads are queries
 * the screens poll — no reminder fires until the slice-13 job engine.
 */
class SafetyEquipmentService {
  private readonly baseUrl = '/safety/equipment';

  // ── Queries ────────────────────────────────────────────────────────────────

  getAll(): Promise<SafetyEquipmentSummary[]> {
    return apiService.get<SafetyEquipmentSummary[]>(this.baseUrl);
  }

  getById(id: string): Promise<SafetyEquipment> {
    return apiService.get<SafetyEquipment>(`${this.baseUrl}/${id}`);
  }

  getByNumber(equipmentNumber: string): Promise<SafetyEquipment | null> {
    return apiService.get<SafetyEquipment | null>(
      `${this.baseUrl}/number/${encodeURIComponent(equipmentNumber)}`,
    );
  }

  getByStatus(status: SheSafetyEquipmentStatus): Promise<SafetyEquipmentSummary[]> {
    return apiService.get<SafetyEquipmentSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getByType(type: SheSafetyEquipmentType): Promise<SafetyEquipmentSummary[]> {
    return apiService.get<SafetyEquipmentSummary[]>(`${this.baseUrl}/type/${type}`);
  }

  getByLocation(locationId: string): Promise<SafetyEquipmentSummary[]> {
    return apiService.get<SafetyEquipmentSummary[]>(`${this.baseUrl}/location/${locationId}`);
  }

  getDueForInspection(daysAhead = 30): Promise<SafetyEquipmentSummary[]> {
    return apiService.get<SafetyEquipmentSummary[]>(`${this.baseUrl}/due-for-inspection`, {
      daysAhead,
    });
  }

  getDueForMaintenance(daysAhead = 30): Promise<SafetyEquipmentSummary[]> {
    return apiService.get<SafetyEquipmentSummary[]>(`${this.baseUrl}/due-for-maintenance`, {
      daysAhead,
    });
  }

  getExpiringCertification(daysAhead = 30): Promise<SafetyEquipmentSummary[]> {
    return apiService.get<SafetyEquipmentSummary[]>(`${this.baseUrl}/expiring-certification`, {
      daysAhead,
    });
  }

  getOutOfService(): Promise<SafetyEquipmentSummary[]> {
    return apiService.get<SafetyEquipmentSummary[]>(`${this.baseUrl}/out-of-service`);
  }

  // ── CRUD ───────────────────────────────────────────────────────────────────

  create(data: SafetyEquipmentCreateRequest): Promise<SafetyEquipment> {
    return apiService.post<SafetyEquipment>(this.baseUrl, data);
  }

  update(id: string, data: SafetyEquipmentUpdateRequest): Promise<SafetyEquipment> {
    return apiService.put<SafetyEquipment>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Inspections ────────────────────────────────────────────────────────────

  getInspections(equipmentId: string): Promise<SafetyEquipmentInspection[]> {
    return apiService.get<SafetyEquipmentInspection[]>(`${this.baseUrl}/${equipmentId}/inspections`);
  }

  addInspection(
    equipmentId: string,
    data: SafetyEquipmentInspectionCreateRequest,
  ): Promise<SafetyEquipmentInspection> {
    return apiService.post<SafetyEquipmentInspection>(
      `${this.baseUrl}/${equipmentId}/inspections`,
      data,
    );
  }

  updateInspection(
    inspectionId: string,
    data: SafetyEquipmentInspectionUpdateRequest,
  ): Promise<SafetyEquipmentInspection> {
    return apiService.put<SafetyEquipmentInspection>(
      `${this.baseUrl}/inspections/${inspectionId}`,
      data,
    );
  }

  addInspectionAction(
    inspectionId: string,
    data: SafetyEquipmentInspectionActionCreateRequest,
  ): Promise<SafetyEquipmentInspectionAction> {
    return apiService.post<SafetyEquipmentInspectionAction>(
      `${this.baseUrl}/inspections/${inspectionId}/actions`,
      data,
    );
  }

  updateInspectionAction(
    actionId: string,
    data: SafetyEquipmentInspectionActionUpdateRequest,
  ): Promise<SafetyEquipmentInspectionAction> {
    return apiService.put<SafetyEquipmentInspectionAction>(
      `${this.baseUrl}/inspection-actions/${actionId}`,
      data,
    );
  }

  removeInspectionAction(actionId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/inspection-actions/${actionId}`);
  }

  // ── Maintenance ────────────────────────────────────────────────────────────

  addMaintenance(
    equipmentId: string,
    data: SafetyEquipmentMaintenanceCreateRequest,
  ): Promise<SafetyEquipmentMaintenance> {
    return apiService.post<SafetyEquipmentMaintenance>(
      `${this.baseUrl}/${equipmentId}/maintenance`,
      data,
    );
  }

  updateMaintenance(
    maintenanceId: string,
    data: SafetyEquipmentMaintenanceUpdateRequest,
  ): Promise<SafetyEquipmentMaintenance> {
    return apiService.put<SafetyEquipmentMaintenance>(
      `${this.baseUrl}/maintenance/${maintenanceId}`,
      data,
    );
  }

  removeMaintenance(maintenanceId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/maintenance/${maintenanceId}`);
  }
}

export const safetyEquipmentService = new SafetyEquipmentService();
export default safetyEquipmentService;
