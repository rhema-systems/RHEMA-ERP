import { apiService } from '../api.service';
import type {
  StaffPromotionDetail,
  StaffTransferDetail,
  StaffDemotionDetail,
  StaffSecondmentDetail,
  StaffActingAppointment,
  CreateStaffActingAppointmentRequest,
  StaffActingStatus,
} from '@/types/hr/movement-subtypes';

/**
 * Subtype detail for a movement, and the standalone acting appointment register.
 *
 * A detail row is one-to-one with its movement and must match the movement's type: the server
 * refuses a promotion detail on a demotion, and refuses a second detail row on a movement that
 * already has one. The grade-band counts on promotion and demotion are computed server-side from
 * the movement's salary grades and ignored if sent.
 */
class MovementSubtypeService {
  // ── Promotion ──────────────────────────────────────────────────────────────

  getPromotionByMovement(movementId: string): Promise<StaffPromotionDetail | null> {
    return apiService.get<StaffPromotionDetail | null>(`/staff-promotions/movement/${movementId}`);
  }

  createPromotion(request: Record<string, unknown>): Promise<StaffPromotionDetail> {
    return apiService.post<StaffPromotionDetail>('/staff-promotions', request);
  }

  updatePromotion(id: string, request: Record<string, unknown>): Promise<StaffPromotionDetail> {
    return apiService.put<StaffPromotionDetail>(`/staff-promotions/${id}`, { id, ...request });
  }

  // ── Transfer ───────────────────────────────────────────────────────────────

  getTransferByMovement(movementId: string): Promise<StaffTransferDetail | null> {
    return apiService.get<StaffTransferDetail | null>(`/staff-transfers/movement/${movementId}`);
  }

  createTransfer(request: Record<string, unknown>): Promise<StaffTransferDetail> {
    return apiService.post<StaffTransferDetail>('/staff-transfers', request);
  }

  updateTransfer(id: string, request: Record<string, unknown>): Promise<StaffTransferDetail> {
    return apiService.put<StaffTransferDetail>(`/staff-transfers/${id}`, { id, ...request });
  }

  getInterCompanyTransfers(): Promise<StaffTransferDetail[]> {
    return apiService.get<StaffTransferDetail[]>('/staff-transfers/inter-company');
  }

  getRelocationTransfers(): Promise<StaffTransferDetail[]> {
    return apiService.get<StaffTransferDetail[]>('/staff-transfers/relocation');
  }

  // ── Demotion ───────────────────────────────────────────────────────────────

  getDemotionByMovement(movementId: string): Promise<StaffDemotionDetail | null> {
    return apiService.get<StaffDemotionDetail | null>(`/staff-demotions/movement/${movementId}`);
  }

  createDemotion(request: Record<string, unknown>): Promise<StaffDemotionDetail> {
    return apiService.post<StaffDemotionDetail>('/staff-demotions', request);
  }

  updateDemotion(id: string, request: Record<string, unknown>): Promise<StaffDemotionDetail> {
    return apiService.put<StaffDemotionDetail>(`/staff-demotions/${id}`, { id, ...request });
  }

  getPendingAppeals(): Promise<StaffDemotionDetail[]> {
    return apiService.get<StaffDemotionDetail[]>('/staff-demotions/pending-appeals');
  }

  // ── Secondment ─────────────────────────────────────────────────────────────

  getSecondmentByMovement(movementId: string): Promise<StaffSecondmentDetail | null> {
    return apiService.get<StaffSecondmentDetail | null>(`/staff-secondments/movement/${movementId}`);
  }

  createSecondment(request: Record<string, unknown>): Promise<StaffSecondmentDetail> {
    return apiService.post<StaffSecondmentDetail>('/staff-secondments', request);
  }

  updateSecondment(id: string, request: Record<string, unknown>): Promise<StaffSecondmentDetail> {
    return apiService.put<StaffSecondmentDetail>(`/staff-secondments/${id}`, { id, ...request });
  }

  /**
   * Extends a secondment. Refused unless extension is permitted, the new date is later than the
   * current one, and the months asked for actually reach that date — the cap is checked against the
   * months, so the two have to agree or the cap means nothing.
   */
  extendSecondment(id: string, extensionMonths: number, newEndDate: string, reason?: string): Promise<StaffSecondmentDetail> {
    return apiService.post<StaffSecondmentDetail>(`/staff-secondments/${id}/extend`, {
      secondmentId: id,
      extensionMonths,
      newEndDate,
      reason,
    });
  }

  getEndingSoon(daysAhead = 30): Promise<StaffSecondmentDetail[]> {
    return apiService.get<StaffSecondmentDetail[]>('/staff-secondments/ending-soon', { daysAhead });
  }
}

/**
 * Acting appointments. Standalone: an acting appointment may but need not come from a movement.
 * One employee cannot hold two overlapping active appointments — each would independently qualify
 * them for an acting allowance.
 */
class ActingAppointmentService {
  private readonly baseUrl = '/staff-acting-appointments';

  getAll(): Promise<StaffActingAppointment[]> {
    return apiService.get<StaffActingAppointment[]>(`${this.baseUrl}/all`);
  }

  getById(id: string): Promise<StaffActingAppointment> {
    return apiService.get<StaffActingAppointment>(`${this.baseUrl}/${id}/details`);
  }

  getActive(): Promise<StaffActingAppointment[]> {
    return apiService.get<StaffActingAppointment[]>(`${this.baseUrl}/active`);
  }

  getExpiring(daysAhead = 14): Promise<StaffActingAppointment[]> {
    return apiService.get<StaffActingAppointment[]>(`${this.baseUrl}/expiring`, { daysAhead });
  }

  getByStatus(status: StaffActingStatus): Promise<StaffActingAppointment[]> {
    return apiService.get<StaffActingAppointment[]>(`${this.baseUrl}/status/${status}`);
  }

  getByEmployee(employeeId: string): Promise<StaffActingAppointment[]> {
    return apiService.get<StaffActingAppointment[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getConvertedToPermanent(): Promise<StaffActingAppointment[]> {
    return apiService.get<StaffActingAppointment[]>(`${this.baseUrl}/converted-to-permanent`);
  }

  create(request: CreateStaffActingAppointmentRequest): Promise<StaffActingAppointment> {
    return apiService.post<StaffActingAppointment>(this.baseUrl, request);
  }

  complete(id: string, notes?: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/complete`, { appointmentId: id, notes });
  }

  extend(id: string, newEndDate: string, notes?: string): Promise<StaffActingAppointment> {
    return apiService.post<StaffActingAppointment>(`${this.baseUrl}/${id}/extend`, {
      appointmentId: id,
      newEndDate,
      notes,
    });
  }

  /** Requires a promotion movement for the same employee — the conversion is that promotion. */
  convertToPermanent(id: string, conversionMovementId: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/convert`, {
      appointmentId: id,
      conversionMovementId,
    });
  }

  /** Refused while the appointment is still active — complete or cancel it first. */
  delete(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const movementSubtypeService = new MovementSubtypeService();
export const actingAppointmentService = new ActingAppointmentService();
