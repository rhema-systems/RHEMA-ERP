import { apiService } from '../api.service';
import type {
  StaffPromotionDetail,
  StaffTransferDetail,
  StaffDemotionDetail,
  StaffSecondmentDetail,
  StaffActingAppointment,
  CreateStaffActingAppointmentRequest,
  UpdateStaffActingAppointmentRequest,
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

  /**
   * `HR.Movements.Admin`. Removes the detail recorded against this movement.
   *
   * ⚠ Re-adding a detail afterwards works: the server clears the tombstone first, because
   * `IX_StaffPromotions_MovementId` is unique and counts soft-deleted rows, so a plain insert would have
   * violated it and the movement could never have carried a promotion detail again.
   */
  deletePromotion(id: string): Promise<void> {
    return apiService.delete<void>(`/staff-promotions/${id}`);
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

  /**
   * `HR.Movements.Admin`. Removes the detail recorded against this movement.
   *
   * ⚠ Re-adding a detail afterwards works: the server clears the tombstone first, because
   * `IX_StaffTransfers_MovementId` is unique and counts soft-deleted rows, so a plain insert would have
   * violated it and the movement could never have carried a transfer detail again.
   */
  deleteTransfer(id: string): Promise<void> {
    return apiService.delete<void>(`/staff-transfers/${id}`);
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

  /**
   * `HR.Movements.Admin`. Removes the detail recorded against this movement.
   *
   * ⚠ Re-adding a detail afterwards works: the server clears the tombstone first, because
   * `IX_StaffDemotions_MovementId` is unique and counts soft-deleted rows, so a plain insert would have
   * violated it and the movement could never have carried a demotion detail again.
   */
  deleteDemotion(id: string): Promise<void> {
    return apiService.delete<void>(`/staff-demotions/${id}`);
  }

  /**
   * The employee's own acceptance of, or appeal against, a demotion notice.
   *
   * ⚠ **Only the demoted employee.** The endpoint holds no permission policy beyond
   * `InternalOnly` — deliberately, so an ordinary employee can reach it — and the service refuses
   * anyone else: an appeal filed in someone's name by someone else is worse than no appeal. Which
   * is why this is called from `/me/movements` and not from the HR desk, whose `pending-appeals`
   * queue could otherwise never fill.
   */
  respondToDemotion(id: string, response: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`/staff-demotions/${id}/respond`, { response });
  }

  /** Demotions still awaiting the employee's answer, inside the appeal window. */
  getPendingAppeals(): Promise<StaffDemotionDetail[]> {
    return apiService.get<StaffDemotionDetail[]>('/staff-demotions/pending-appeals');
  }

  /**
   * Demotions the employee has answered.
   *
   * ⚠ The opposite of `getPendingAppeals`, not a filter of it: answering REMOVES a demotion from
   * that list, so before this endpoint existed a filed appeal appeared in no list at all and HR
   * found it only by opening the movement (ledger D-37).
   */
  getFiledAppeals(): Promise<StaffDemotionDetail[]> {
    return apiService.get<StaffDemotionDetail[]>('/staff-demotions/filed-appeals');
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
   * `HR.Movements.Admin`. Removes the detail recorded against this movement.
   *
   * ⚠ Re-adding a detail afterwards works: the server clears the tombstone first, because
   * `IX_StaffSecondments_MovementId` is unique and counts soft-deleted rows, so a plain insert would have
   * violated it and the movement could never have carried a secondment detail again.
   */
  deleteSecondment(id: string): Promise<void> {
    return apiService.delete<void>(`/staff-secondments/${id}`);
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

  /**
   * Corrects an appointment's end date, allowance and status.
   *
   * ⚠ Refused once the appointment is `Completed` — completing it is what fixes its terms in
   * place, and re-opening one is not an edit.
   *
   * ⚠ `allowanceCalculation` is one of the fields the closure ledger's section E lists as settable
   * by no form; it is on this payload for that reason.
   */
  update(id: string, request: UpdateStaffActingAppointmentRequest): Promise<StaffActingAppointment> {
    return apiService.put<StaffActingAppointment>(`${this.baseUrl}/${id}`, { id, ...request });
  }

  /** Refused while the appointment is still active — complete or cancel it first. */
  delete(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const movementSubtypeService = new MovementSubtypeService();
export const actingAppointmentService = new ActingAppointmentService();
