import { apiService } from '../api.service';
import type {
  MedicalPreAuthorizationSummary,
  MedicalPreAuthorizationCreateRequest,
  MedicalReferralSummary,
  MedicalReferralCreateRequest,
  MedicalReferralStatus,
  MedicalAppointmentSummary,
  MedicalAppointmentCreateRequest,
} from '@/types/hr/medical';

/**
 * The clinical surface: pre-authorisations, referrals and appointments.
 * Backend route: api/medical-clinical.
 *
 * These are the three things that happen *before* a claim exists — an insurer's agreement to pay,
 * a hand-off to another clinician, and a booked visit. A claim can point back at a pre-authorisation
 * or a referral, which is why they carry their own numbers.
 *
 * ⚠ Gated on HR.Medical.* like the rest of the module; deletes need Admin.
 */
class MedicalClinicalService {
  private readonly baseUrl = '/medical-clinical';

  // ── Pre-authorisations ─────────────────────────────────────────────────────

  getPreAuthorizations(): Promise<MedicalPreAuthorizationSummary[]> {
    return apiService.get<MedicalPreAuthorizationSummary[]>(`${this.baseUrl}/pre-authorizations`);
  }

  getPendingPreAuthorizations(): Promise<MedicalPreAuthorizationSummary[]> {
    return apiService.get<MedicalPreAuthorizationSummary[]>(
      `${this.baseUrl}/pre-authorizations/pending`,
    );
  }

  getPreAuthorizationsByEmployee(employeeId: string): Promise<MedicalPreAuthorizationSummary[]> {
    return apiService.get<MedicalPreAuthorizationSummary[]>(
      `${this.baseUrl}/employees/${employeeId}/pre-authorizations`,
    );
  }

  createPreAuthorization(
    payload: MedicalPreAuthorizationCreateRequest,
  ): Promise<MedicalPreAuthorizationSummary> {
    return apiService.post<MedicalPreAuthorizationSummary>(
      `${this.baseUrl}/pre-authorizations`,
      payload,
    );
  }

  /**
   * Approves the request. The approver is taken from the token, not the payload — the API
   * overwrites any `approvedBy` sent, so an unlinked account gets a 400 rather than a wrong name.
   */
  approvePreAuthorization(
    id: string,
    authorizedAmount?: number | null,
    expiryDate?: string | null,
    notes?: string | null,
  ): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(
      `${this.baseUrl}/pre-authorizations/${id}/approve`,
      {
        preAuthorizationId: id,
        authorizedAmount: authorizedAmount ?? null,
        expiryDate: expiryDate ?? null,
        notes: notes ?? null,
      },
    );
  }

  rejectPreAuthorization(id: string, rejectionReason: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/pre-authorizations/${id}/reject`, {
      preAuthorizationId: id,
      rejectionReason,
    });
  }

  // ── Referrals ──────────────────────────────────────────────────────────────

  getReferrals(): Promise<MedicalReferralSummary[]> {
    return apiService.get<MedicalReferralSummary[]>(`${this.baseUrl}/referrals`);
  }

  getPendingReferrals(): Promise<MedicalReferralSummary[]> {
    return apiService.get<MedicalReferralSummary[]>(`${this.baseUrl}/referrals/pending`);
  }

  createReferral(payload: MedicalReferralCreateRequest): Promise<MedicalReferralSummary> {
    return apiService.post<MedicalReferralSummary>(`${this.baseUrl}/referrals`, payload);
  }

  updateReferralStatus(
    id: string,
    status: MedicalReferralStatus,
    outcomeSummary?: string | null,
  ): Promise<{ message: string }> {
    return apiService.put<{ message: string }>(`${this.baseUrl}/referrals/${id}/status`, {
      referralId: id,
      status,
      outcomeSummary: outcomeSummary ?? null,
    });
  }

  completeReferral(id: string, outcomeSummary?: string | null): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/referrals/${id}/complete`, {
      referralId: id,
      outcomeSummary: outcomeSummary ?? null,
      completedDate: new Date().toISOString(),
    });
  }

  // ── Appointments ───────────────────────────────────────────────────────────

  getAppointments(): Promise<MedicalAppointmentSummary[]> {
    return apiService.get<MedicalAppointmentSummary[]>(`${this.baseUrl}/appointments`);
  }

  getUpcomingAppointments(daysAhead = 30): Promise<MedicalAppointmentSummary[]> {
    return apiService.get<MedicalAppointmentSummary[]>(`${this.baseUrl}/appointments/upcoming`, {
      daysAhead,
    });
  }

  createAppointment(
    payload: MedicalAppointmentCreateRequest,
  ): Promise<MedicalAppointmentSummary> {
    return apiService.post<MedicalAppointmentSummary>(`${this.baseUrl}/appointments`, payload);
  }

  checkIn(id: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/appointments/${id}/check-in`, {
      appointmentId: id,
      checkInTime: new Date().toISOString(),
    });
  }

  checkOut(id: string, outcomeSummary?: string | null): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/appointments/${id}/check-out`, {
      appointmentId: id,
      checkOutTime: new Date().toISOString(),
      outcomeSummary: outcomeSummary ?? null,
    });
  }

  cancelAppointment(id: string, cancellationReason: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/appointments/${id}/cancel`, {
      appointmentId: id,
      cancellationReason,
    });
  }
}

export const medicalClinicalService = new MedicalClinicalService();
