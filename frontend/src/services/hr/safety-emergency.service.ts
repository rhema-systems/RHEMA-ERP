import { apiService } from '../api.service';
import type {
  EmergencyPlan,
  EmergencyPlanSummary,
  EmergencyPlanCreateRequest,
  EmergencyPlanUpdateRequest,
  SheAssemblyPoint,
  SheAssemblyPointCreateRequest,
  SheAssemblyPointUpdateRequest,
  EmergencyContact,
  EmergencyContactCreateRequest,
  EmergencyContactUpdateRequest,
  EmergencyDrill,
  EmergencyDrillCreateRequest,
  EmergencyDrillUpdateRequest,
  EmergencyResponseTeamMember,
  EmergencyResponseTeamMemberCreateRequest,
  EmergencyResponseTeamMemberUpdateRequest,
  SheEmergencyType,
} from '@/types/hr/safety-equipment';

/**
 * Emergency preparedness: plans with their assembly points, contact trees, drills and response
 * teams. Backend route: api/safety/emergency. HR-gated throughout.
 *
 * Plan and drill numbers are user-assigned codes — a duplicate is refused (422), as is adding
 * an employee twice to the same plan's response team. Due-for-review and expiring-certificates
 * are queries the screens poll — no reminder fires until the slice-13 job engine.
 */
class SafetyEmergencyService {
  private readonly baseUrl = '/safety/emergency';

  // ── Plans ──────────────────────────────────────────────────────────────────

  getPlans(): Promise<EmergencyPlanSummary[]> {
    return apiService.get<EmergencyPlanSummary[]>(`${this.baseUrl}/plans`);
  }

  getPlan(id: string): Promise<EmergencyPlan> {
    return apiService.get<EmergencyPlan>(`${this.baseUrl}/plans/${id}`);
  }

  getPlanByNumber(planNumber: string): Promise<EmergencyPlan | null> {
    return apiService.get<EmergencyPlan | null>(
      `${this.baseUrl}/plans/number/${encodeURIComponent(planNumber)}`,
    );
  }

  getPlansByType(type: SheEmergencyType): Promise<EmergencyPlanSummary[]> {
    return apiService.get<EmergencyPlanSummary[]>(`${this.baseUrl}/plans/type/${type}`);
  }

  getActivePlans(): Promise<EmergencyPlanSummary[]> {
    return apiService.get<EmergencyPlanSummary[]>(`${this.baseUrl}/plans/active`);
  }

  getPlansDueForReview(daysAhead = 30): Promise<EmergencyPlanSummary[]> {
    return apiService.get<EmergencyPlanSummary[]>(`${this.baseUrl}/plans/due-for-review`, {
      daysAhead,
    });
  }

  createPlan(data: EmergencyPlanCreateRequest): Promise<EmergencyPlan> {
    return apiService.post<EmergencyPlan>(`${this.baseUrl}/plans`, data);
  }

  updatePlan(id: string, data: EmergencyPlanUpdateRequest): Promise<EmergencyPlan> {
    return apiService.put<EmergencyPlan>(`${this.baseUrl}/plans/${id}`, data);
  }

  removePlan(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/plans/${id}`);
  }

  // ── Assembly points ────────────────────────────────────────────────────────

  addAssemblyPoint(
    planId: string,
    data: SheAssemblyPointCreateRequest,
  ): Promise<SheAssemblyPoint> {
    return apiService.post<SheAssemblyPoint>(`${this.baseUrl}/plans/${planId}/assembly-points`, data);
  }

  updateAssemblyPoint(
    assemblyPointId: string,
    data: SheAssemblyPointUpdateRequest,
  ): Promise<SheAssemblyPoint> {
    return apiService.put<SheAssemblyPoint>(
      `${this.baseUrl}/assembly-points/${assemblyPointId}`,
      data,
    );
  }

  removeAssemblyPoint(assemblyPointId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/assembly-points/${assemblyPointId}`);
  }

  // ── Emergency contacts ─────────────────────────────────────────────────────

  addContact(planId: string, data: EmergencyContactCreateRequest): Promise<EmergencyContact> {
    return apiService.post<EmergencyContact>(`${this.baseUrl}/plans/${planId}/contacts`, data);
  }

  updateContact(contactId: string, data: EmergencyContactUpdateRequest): Promise<EmergencyContact> {
    return apiService.put<EmergencyContact>(`${this.baseUrl}/contacts/${contactId}`, data);
  }

  removeContact(contactId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/contacts/${contactId}`);
  }

  // ── Drills ─────────────────────────────────────────────────────────────────

  getDrillsForPlan(planId: string): Promise<EmergencyDrill[]> {
    return apiService.get<EmergencyDrill[]>(`${this.baseUrl}/plans/${planId}/drills`);
  }

  getUpcomingDrills(daysAhead = 30): Promise<EmergencyDrill[]> {
    return apiService.get<EmergencyDrill[]>(`${this.baseUrl}/drills/upcoming`, { daysAhead });
  }

  addDrill(planId: string, data: EmergencyDrillCreateRequest): Promise<EmergencyDrill> {
    return apiService.post<EmergencyDrill>(`${this.baseUrl}/plans/${planId}/drills`, data);
  }

  updateDrill(drillId: string, data: EmergencyDrillUpdateRequest): Promise<EmergencyDrill> {
    return apiService.put<EmergencyDrill>(`${this.baseUrl}/drills/${drillId}`, data);
  }

  removeDrill(drillId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/drills/${drillId}`);
  }

  // ── Response team ──────────────────────────────────────────────────────────

  getExpiringTeamCertificates(daysAhead = 30): Promise<EmergencyResponseTeamMember[]> {
    return apiService.get<EmergencyResponseTeamMember[]>(
      `${this.baseUrl}/team/expiring-certificates`,
      { daysAhead },
    );
  }

  getTeamMembershipsByEmployee(employeeId: string): Promise<EmergencyResponseTeamMember[]> {
    return apiService.get<EmergencyResponseTeamMember[]>(
      `${this.baseUrl}/team/by-employee/${employeeId}`,
    );
  }

  addTeamMember(
    planId: string,
    data: EmergencyResponseTeamMemberCreateRequest,
  ): Promise<EmergencyResponseTeamMember> {
    return apiService.post<EmergencyResponseTeamMember>(`${this.baseUrl}/plans/${planId}/team`, data);
  }

  updateTeamMember(
    teamMemberId: string,
    data: EmergencyResponseTeamMemberUpdateRequest,
  ): Promise<EmergencyResponseTeamMember> {
    return apiService.put<EmergencyResponseTeamMember>(`${this.baseUrl}/team/${teamMemberId}`, data);
  }

  removeTeamMember(teamMemberId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/team/${teamMemberId}`);
  }
}

export const safetyEmergencyService = new SafetyEmergencyService();
export default safetyEmergencyService;
