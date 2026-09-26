import { apiService } from '../api.service';
import type {
  LeaveType,
  LeaveTypeDetail,
  CreateLeaveTypeRequest,
  UpdateLeaveTypeRequest,
  LeaveSubType,
  LeaveSubTypeRequest,
  LeaveCategoryAllocation,
  LeaveCategoryAllocationRequest,
  LeaveTypeEligibility,
  CreateLeaveTypeEligibilityRequest,
  LeaveAccrualPolicy,
  LeaveAccrualPolicyRequest,
} from '@/types/hr/leave';

/**
 * Leave type setup. Backend route: api/hr/leave-types.
 *
 * The four nested collections read from under their parent
 * (`GET {leaveTypeId}/sub-types`) but write to the controller root
 * (`POST sub-types`, with leaveTypeId in the body) — that asymmetry is the
 * controller's, mirrored here rather than hidden.
 *
 * Sub-types, allocations and accrual policies reuse their create DTO for updates;
 * eligibility rules have no update endpoint and are added/removed only.
 */
class LeaveTypeService {
  private readonly baseUrl = '/hr/leave-types';

  getAll(activeOnly = true): Promise<LeaveType[]> {
    return apiService.get<LeaveType[]>(this.baseUrl, { activeOnly });
  }

  getById(id: string): Promise<LeaveType> {
    return apiService.get<LeaveType>(`${this.baseUrl}/${id}`);
  }

  /** Leave type with its sub-types, allocations, eligibility and accrual policies. */
  getDetail(id: string): Promise<LeaveTypeDetail> {
    return apiService.get<LeaveTypeDetail>(`${this.baseUrl}/${id}/detail`);
  }

  create(data: CreateLeaveTypeRequest): Promise<LeaveType> {
    return apiService.post<LeaveType>(this.baseUrl, data);
  }

  /**
   * Retire a leave type.
   *
   * ⚠ **The only way out.** This controller has no delete at all — `DELETE hr/leave-types/{id}`
   * answers 405 — which is correct, because a type is referenced by every request ever made against
   * it. Until this method existed a leave type could be created and never retired, so a type added
   * in error stayed in the picker for good (ledger: LeaveTypes, BUILD).
   */
  deactivate(id: string) {
    return apiService.patch<void>(`${this.baseUrl}/${id}/deactivate`, {});
  }

  update(id: string, data: UpdateLeaveTypeRequest): Promise<LeaveType> {
    return apiService.put<LeaveType>(`${this.baseUrl}/${id}`, data);
  }

  // ── Sub-types ─────────────────────────────────────────────────────────────────

  /**
   * Sub-types of a leave type. `activeOnly` defaults to false so the rulebook tab keeps showing
   * retired rows; anything that offers a choice passes true (closure plan L-34).
   */
  getSubTypes(leaveTypeId: string, activeOnly = false): Promise<LeaveSubType[]> {
    const q = activeOnly ? '?activeOnly=true' : '';
    return apiService.get<LeaveSubType[]>(`${this.baseUrl}/${leaveTypeId}/sub-types${q}`);
  }

  createSubType(data: LeaveSubTypeRequest): Promise<LeaveSubType> {
    return apiService.post<LeaveSubType>(`${this.baseUrl}/sub-types`, data);
  }

  updateSubType(id: string, data: LeaveSubTypeRequest): Promise<LeaveSubType> {
    return apiService.put<LeaveSubType>(`${this.baseUrl}/sub-types/${id}`, data);
  }

  removeSubType(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/sub-types/${id}`);
  }

  // ── Category allocations ──────────────────────────────────────────────────────

  getAllocations(leaveTypeId: string): Promise<LeaveCategoryAllocation[]> {
    return apiService.get<LeaveCategoryAllocation[]>(`${this.baseUrl}/${leaveTypeId}/allocations`);
  }

  createAllocation(data: LeaveCategoryAllocationRequest): Promise<LeaveCategoryAllocation> {
    return apiService.post<LeaveCategoryAllocation>(`${this.baseUrl}/allocations`, data);
  }

  updateAllocation(
    id: string,
    data: LeaveCategoryAllocationRequest,
  ): Promise<LeaveCategoryAllocation> {
    return apiService.put<LeaveCategoryAllocation>(`${this.baseUrl}/allocations/${id}`, data);
  }

  removeAllocation(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/allocations/${id}`);
  }

  // ── Eligibility rules (create + delete only) ──────────────────────────────────

  getEligibilityRules(leaveTypeId: string): Promise<LeaveTypeEligibility[]> {
    return apiService.get<LeaveTypeEligibility[]>(`${this.baseUrl}/${leaveTypeId}/eligibility`);
  }

  createEligibilityRule(
    data: CreateLeaveTypeEligibilityRequest,
  ): Promise<LeaveTypeEligibility> {
    return apiService.post<LeaveTypeEligibility>(`${this.baseUrl}/eligibility`, data);
  }

  removeEligibilityRule(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/eligibility/${id}`);
  }

  // ── Accrual policies ──────────────────────────────────────────────────────────

  getAccrualPolicies(leaveTypeId: string): Promise<LeaveAccrualPolicy[]> {
    return apiService.get<LeaveAccrualPolicy[]>(`${this.baseUrl}/${leaveTypeId}/accrual-policies`);
  }

  createAccrualPolicy(data: LeaveAccrualPolicyRequest): Promise<LeaveAccrualPolicy> {
    return apiService.post<LeaveAccrualPolicy>(`${this.baseUrl}/accrual-policies`, data);
  }

  updateAccrualPolicy(id: string, data: LeaveAccrualPolicyRequest): Promise<LeaveAccrualPolicy> {
    return apiService.put<LeaveAccrualPolicy>(`${this.baseUrl}/accrual-policies/${id}`, data);
  }

  removeAccrualPolicy(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/accrual-policies/${id}`);
  }
}

export const leaveTypeService = new LeaveTypeService();
