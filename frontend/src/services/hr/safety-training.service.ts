import { apiService } from '../api.service';
import type {
  SheTrainingPlan,
  SheTrainingPlanCreateRequest,
  SheTrainingPlanUpdateRequest,
  SheTrainingPlanStatus,
  SheTrainingProgram,
  SheTrainingProgramSummary,
  SheTrainingProgramCreateRequest,
  SheTrainingProgramUpdateRequest,
  SheTrainingProgramEvaluateRequest,
  SheTrainingStatus,
  SheTrainingCategory,
  SheTrainingAttendance,
  SheTrainingAttendanceCreateRequest,
  SheTrainingAttendanceUpdateRequest,
} from '@/types/hr/safety-training';

/**
 * SHE training & awareness (FR-SHE-120–122): annual/quarterly plans, delivery programs
 * (toolbox talks to certified courses) and the attendance register — which covers
 * NON-employees too. This is the SHE record, deliberately separate from the corporate
 * Training module (area 7) per the recorded FR-SHE-121 boundary.
 * Backend route: api/safety/training.
 *
 * Plan numbers / program codes are user-entered, unique (422 on duplicate), immutable.
 * Certificate renewal notices fire automatically (reminder engine); expiring-certificates is the work queue.
 */
class SafetyTrainingService {
  private readonly baseUrl = '/safety/training';

  // ── Plans ──────────────────────────────────────────────────────────────────

  getPlan(id: string): Promise<SheTrainingPlan> {
    return apiService.get<SheTrainingPlan>(`${this.baseUrl}/plans/${id}`);
  }

  getPlansByYear(year: number): Promise<SheTrainingPlan[]> {
    return apiService.get<SheTrainingPlan[]>(`${this.baseUrl}/plans/year/${year}`);
  }

  getPlansByStatus(status: SheTrainingPlanStatus): Promise<SheTrainingPlan[]> {
    return apiService.get<SheTrainingPlan[]>(`${this.baseUrl}/plans/status/${status}`);
  }

  /** Refused (422) when the plan number is already taken. */
  createPlan(data: SheTrainingPlanCreateRequest): Promise<SheTrainingPlan> {
    return apiService.post<SheTrainingPlan>(`${this.baseUrl}/plans`, data);
  }

  /** Setting status Approved without an approver is refused (422). */
  updatePlan(id: string, data: SheTrainingPlanUpdateRequest): Promise<SheTrainingPlan> {
    return apiService.put<SheTrainingPlan>(`${this.baseUrl}/plans/${id}`, data);
  }

  removePlan(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/plans/${id}`);
  }

  // ── Programs ───────────────────────────────────────────────────────────────

  getProgram(id: string): Promise<SheTrainingProgram> {
    return apiService.get<SheTrainingProgram>(`${this.baseUrl}/programs/${id}`);
  }

  getProgramsByPlan(planId: string): Promise<SheTrainingProgramSummary[]> {
    return apiService.get<SheTrainingProgramSummary[]>(`${this.baseUrl}/plans/${planId}/programs`);
  }

  getProgramsByStatus(status: SheTrainingStatus): Promise<SheTrainingProgramSummary[]> {
    return apiService.get<SheTrainingProgramSummary[]>(`${this.baseUrl}/programs/status/${status}`);
  }

  getProgramsByCategory(category: SheTrainingCategory): Promise<SheTrainingProgramSummary[]> {
    return apiService.get<SheTrainingProgramSummary[]>(
      `${this.baseUrl}/programs/category/${category}`,
    );
  }

  getUpcomingPrograms(daysAhead = 30): Promise<SheTrainingProgramSummary[]> {
    return apiService.get<SheTrainingProgramSummary[]>(`${this.baseUrl}/programs/upcoming`, {
      daysAhead,
    });
  }

  /** Refused (422) when the program code is already taken. */
  createProgram(data: SheTrainingProgramCreateRequest): Promise<SheTrainingProgram> {
    return apiService.post<SheTrainingProgram>(`${this.baseUrl}/programs`, data);
  }

  updateProgram(id: string, data: SheTrainingProgramUpdateRequest): Promise<SheTrainingProgram> {
    return apiService.put<SheTrainingProgram>(`${this.baseUrl}/programs/${id}`, data);
  }

  removeProgram(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/programs/${id}`);
  }

  /** One-shot — an already-evaluated program refuses a second evaluation (422). */
  evaluateProgram(id: string, data: SheTrainingProgramEvaluateRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/programs/${id}/evaluate`, data);
  }

  // ── Attendance ─────────────────────────────────────────────────────────────

  getAttendances(programId: string): Promise<SheTrainingAttendance[]> {
    return apiService.get<SheTrainingAttendance[]>(
      `${this.baseUrl}/programs/${programId}/attendances`,
    );
  }

  getAttendancesByEmployee(employeeId: string): Promise<SheTrainingAttendance[]> {
    return apiService.get<SheTrainingAttendance[]>(
      `${this.baseUrl}/attendances/by-employee/${employeeId}`,
    );
  }

  getExpiringCertificates(daysAhead = 30): Promise<SheTrainingAttendance[]> {
    return apiService.get<SheTrainingAttendance[]>(
      `${this.baseUrl}/attendances/expiring-certificates`,
      { daysAhead },
    );
  }

  /** Employee rows: repeat sign-in refused (422); name comes from the employee record. */
  addAttendance(
    programId: string,
    data: SheTrainingAttendanceCreateRequest,
  ): Promise<SheTrainingAttendance> {
    return apiService.post<SheTrainingAttendance>(
      `${this.baseUrl}/programs/${programId}/attendances`,
      data,
    );
  }

  updateAttendance(
    attendanceId: string,
    data: SheTrainingAttendanceUpdateRequest,
  ): Promise<SheTrainingAttendance> {
    return apiService.put<SheTrainingAttendance>(
      `${this.baseUrl}/attendances/${attendanceId}`,
      data,
    );
  }

  removeAttendance(attendanceId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/attendances/${attendanceId}`);
  }
}

export const safetyTrainingService = new SafetyTrainingService();
export default safetyTrainingService;
