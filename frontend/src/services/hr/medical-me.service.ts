import { apiService } from '../api.service';

/**
 * The employee's own medical surface (area 25 slice 8).
 *
 * Two backend homes, both self-scoped: `api/medical/me` (coverage + appointments, alongside the
 * claims the older `medicalSelfServiceClaimService` covers) and `api/employee-health/me`
 * (occupational-health file: profile, conditions, allergies, exams, surveillance).
 *
 * The law is the portal-wide one: no method here takes an employee id, ever — the server derives
 * the employee from the JWT. Somebody else's record id is a 404 lookup miss, never a 403.
 * Every shape below was measured live (probe-slice8) — these are the self projections, NOT the
 * HR DTOs: flags like IsFlaggedForReview, server paths and adjudicator notes never appear.
 */

// ── Coverage ────────────────────────────────────────────────────────────────────

export interface MyPolicySummary {
  id: string;
  providerName: string;
  planName: string;
  policyNumber: string;
  startDate: string;
  endDate: string | null;
  remainingLimit: number;
  status: string;
  statusName: string;
  isActive: boolean;
}

export interface MyPolicy {
  id: string;
  providerName: string;
  planName: string;
  benefitTierName: string | null;
  policyNumber: string;
  membershipNumber: string | null;
  startDate: string;
  endDate: string | null;
  annualLimit: number;
  utilizedAmount: number;
  remainingLimit: number;
  utilizationPercentage: number;
  coversDependents: boolean;
  status: string;
  statusName: string;
  isActive: boolean;
  cancellationDate: string | null;
}

export interface MyPolicyDependent {
  id: string;
  dependentName: string;
  relationship: string | null;
  membershipNumber: string | null;
  coverageStartDate: string;
  coverageEndDate: string | null;
  annualLimit: number;
  utilizedAmount: number;
  remainingLimit: number;
  isActive: boolean;
}

// ── Appointments ────────────────────────────────────────────────────────────────

export interface MyAppointmentSummary {
  id: string;
  appointmentNumber: string;
  facilityName: string;
  appointmentDateTime: string;
  status: string;
  statusName: string;
}

export interface MyAppointment extends MyAppointmentSummary {
  isForDependent: boolean;
  dependentName: string | null;
  physicianName: string | null;
  durationMinutes: number | null;
  serviceType: string;
  serviceTypeName: string;
  purpose: string;
  checkInTime: string | null;
  checkOutTime: string | null;
  outcomeSummary: string | null;
  cancellationReason: string | null;
}

class MedicalMeService {
  private readonly baseUrl = '/medical/me';

  getPolicies(): Promise<MyPolicySummary[]> {
    return apiService.get<MyPolicySummary[]>(`${this.baseUrl}/insurance-policies`);
  }

  /** 404 when the employee has no active policy — callers treat that as the empty state. */
  getActivePolicy(): Promise<MyPolicy> {
    return apiService.get<MyPolicy>(`${this.baseUrl}/insurance-policies/active`);
  }

  getPolicy(id: string): Promise<MyPolicy> {
    return apiService.get<MyPolicy>(`${this.baseUrl}/insurance-policies/${id}`);
  }

  getPolicyDependents(policyId: string): Promise<MyPolicyDependent[]> {
    return apiService.get<MyPolicyDependent[]>(
      `${this.baseUrl}/insurance-policies/${policyId}/dependents`,
    );
  }

  getAppointments(): Promise<MyAppointmentSummary[]> {
    return apiService.get<MyAppointmentSummary[]>(`${this.baseUrl}/appointments`);
  }

  getAppointment(id: string): Promise<MyAppointment> {
    return apiService.get<MyAppointment>(`${this.baseUrl}/appointments/${id}`);
  }
}

export const medicalMeService = new MedicalMeService();

// ── Occupational health (employee-health/me) ────────────────────────────────────

export interface MyHealthProfile {
  id: string;
  employeeId: string;
  bloodGroup: string | null;
  heightCm: number | null;
  weightKg: number | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
  emergencyContactRelationship: string | null;
}

export interface MyHealthCondition {
  id: string;
  conditionName: string;
  diagnosedDate: string | null;
  severity: string | null;
  status: string | null;
}

export interface MyAllergy {
  id: string;
  allergen: string;
  severity: string | null;
}

export interface MyMedicalExam {
  id: string;
  examDate: string;
  result: string | null;
  nextExamDueDate: string | null;
}

export interface MySurveillanceSummary {
  id: string;
  surveillanceNumber: string;
  type: string;
  examinationDate: string;
  nextExaminationDate: string | null;
  result: string;
  workRestrictionIssued: boolean;
}

export interface MySurveillanceRecord extends MySurveillanceSummary {
  exposureHazard: string | null;
  healthcareFacilityName: string | null;
  examiningPhysician: string | null;
  findings: string | null;
  recommendations: string | null;
  workRestrictionDetails: string | null;
}

class EmployeeHealthMeService {
  private readonly baseUrl = '/employee-health/me';

  /** 404 while HR has not opened a health profile — callers treat that as the empty state. */
  getProfile(): Promise<MyHealthProfile> {
    return apiService.get<MyHealthProfile>(`${this.baseUrl}/profile`);
  }

  getConditions(): Promise<MyHealthCondition[]> {
    return apiService.get<MyHealthCondition[]>(`${this.baseUrl}/conditions`);
  }

  getAllergies(): Promise<MyAllergy[]> {
    return apiService.get<MyAllergy[]>(`${this.baseUrl}/allergies`);
  }

  getExams(): Promise<MyMedicalExam[]> {
    return apiService.get<MyMedicalExam[]>(`${this.baseUrl}/exams`);
  }

  getSurveillance(): Promise<MySurveillanceSummary[]> {
    return apiService.get<MySurveillanceSummary[]>(`${this.baseUrl}/surveillance`);
  }

  getSurveillanceRecord(id: string): Promise<MySurveillanceRecord> {
    return apiService.get<MySurveillanceRecord>(`${this.baseUrl}/surveillance/${id}`);
  }
}

export const employeeHealthMeService = new EmployeeHealthMeService();
