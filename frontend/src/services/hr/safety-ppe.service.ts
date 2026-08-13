import { apiService } from '../api.service';
import type {
  PpeType,
  PpeTypeCreateRequest,
  PpeTypeUpdateRequest,
  PpeInventory,
  PpeInventoryCreateRequest,
  PpeInventoryUpdateRequest,
  PpeInventoryRestockRequest,
  PpeIssuance,
  PpeIssuanceCreateRequest,
  PpeIssuanceReturnRequest,
  JobRolePpeRequirement,
  JobRolePpeRequirementCreateRequest,
  JobRolePpeRequirementUpdateRequest,
} from '@/types/hr/safety-ppe';

/**
 * PPE management: the type catalogue, stock inventory with restock, issuance to employees and
 * the job-role requirement matrix. Backend route: api/safety/ppe. HR-gated throughout except
 * `getMyIssuances`, which any authenticated employee may call — it reads the token's employee,
 * never a parameter.
 *
 * A duplicate type code, inventory item code or job-role requirement pair is refused (422);
 * returning an already-returned issuance is refused (422). No reorder/expiry alerts fire yet —
 * `getBelowReorderLevel` is a query the screens poll, not a job (slice-13 engine).
 */
class SafetyPpeService {
  private readonly baseUrl = '/safety/ppe';

  // ── Types (catalogue) ──────────────────────────────────────────────────────

  getTypes(activeOnly = false): Promise<PpeType[]> {
    return apiService.get<PpeType[]>(`${this.baseUrl}/types`, activeOnly ? { activeOnly } : undefined);
  }

  getType(id: string): Promise<PpeType> {
    return apiService.get<PpeType>(`${this.baseUrl}/types/${id}`);
  }

  createType(data: PpeTypeCreateRequest): Promise<PpeType> {
    return apiService.post<PpeType>(`${this.baseUrl}/types`, data);
  }

  updateType(id: string, data: PpeTypeUpdateRequest): Promise<PpeType> {
    return apiService.put<PpeType>(`${this.baseUrl}/types/${id}`, data);
  }

  removeType(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/types/${id}`);
  }

  // ── Inventory ──────────────────────────────────────────────────────────────

  getInventory(): Promise<PpeInventory[]> {
    return apiService.get<PpeInventory[]>(`${this.baseUrl}/inventory`);
  }

  getInventoryItem(id: string): Promise<PpeInventory> {
    return apiService.get<PpeInventory>(`${this.baseUrl}/inventory/${id}`);
  }

  getInventoryByType(ppeTypeId: string): Promise<PpeInventory[]> {
    return apiService.get<PpeInventory[]>(`${this.baseUrl}/inventory/by-type/${ppeTypeId}`);
  }

  getBelowReorderLevel(): Promise<PpeInventory[]> {
    return apiService.get<PpeInventory[]>(`${this.baseUrl}/inventory/below-reorder`);
  }

  createInventory(data: PpeInventoryCreateRequest): Promise<PpeInventory> {
    return apiService.post<PpeInventory>(`${this.baseUrl}/inventory`, data);
  }

  updateInventory(id: string, data: PpeInventoryUpdateRequest): Promise<PpeInventory> {
    return apiService.put<PpeInventory>(`${this.baseUrl}/inventory/${id}`, data);
  }

  restock(id: string, data: PpeInventoryRestockRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/inventory/${id}/restock`, data);
  }

  removeInventory(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/inventory/${id}`);
  }

  // ── Issuance ───────────────────────────────────────────────────────────────

  /** The caller's own issuance record — open to any authenticated employee. */
  getMyIssuances(): Promise<PpeIssuance[]> {
    return apiService.get<PpeIssuance[]>(`${this.baseUrl}/issuances/mine`);
  }

  getIssuancesByEmployee(employeeId: string): Promise<PpeIssuance[]> {
    return apiService.get<PpeIssuance[]>(`${this.baseUrl}/issuances/by-employee/${employeeId}`);
  }

  getOutstandingIssuances(): Promise<PpeIssuance[]> {
    return apiService.get<PpeIssuance[]>(`${this.baseUrl}/issuances/outstanding`);
  }

  getOverdueReturns(): Promise<PpeIssuance[]> {
    return apiService.get<PpeIssuance[]>(`${this.baseUrl}/issuances/overdue-returns`);
  }

  issue(data: PpeIssuanceCreateRequest): Promise<PpeIssuance> {
    return apiService.post<PpeIssuance>(`${this.baseUrl}/issuances`, data);
  }

  /** Refused (422) when the issuance has already been returned. */
  recordReturn(id: string, data: PpeIssuanceReturnRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/issuances/${id}/return`, data);
  }

  // ── Job-role requirement matrix ────────────────────────────────────────────

  getRequirements(): Promise<JobRolePpeRequirement[]> {
    return apiService.get<JobRolePpeRequirement[]>(`${this.baseUrl}/requirements`);
  }

  getRequirementsByJobRole(jobRoleCode: string): Promise<JobRolePpeRequirement[]> {
    return apiService.get<JobRolePpeRequirement[]>(
      `${this.baseUrl}/requirements/by-role/${encodeURIComponent(jobRoleCode)}`,
    );
  }

  addRequirement(data: JobRolePpeRequirementCreateRequest): Promise<JobRolePpeRequirement> {
    return apiService.post<JobRolePpeRequirement>(`${this.baseUrl}/requirements`, data);
  }

  updateRequirement(
    id: string,
    data: JobRolePpeRequirementUpdateRequest,
  ): Promise<JobRolePpeRequirement> {
    return apiService.put<JobRolePpeRequirement>(`${this.baseUrl}/requirements/${id}`, data);
  }

  removeRequirement(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/requirements/${id}`);
  }
}

export const safetyPpeService = new SafetyPpeService();
export default safetyPpeService;
