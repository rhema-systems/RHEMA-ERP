import { apiService } from '../api.service';
import type {
  CreateSalaryGradeRequest,
  CreateSalaryLevelRequest,
  CreateSalaryNotchRequest,
  SalaryGrade,
  SalaryGradeDetail,
  SalaryLevel,
  SalaryNotch,
  SalaryStructureProjectionResult,
  UpdateSalaryGradeRequest,
  UpdateSalaryLevelRequest,
  UpdateSalaryNotchRequest,
} from '@/types/hr/salary';

/**
 * The salary structure. Backend routes: api/hr/salary-grades, api/hr/salary-levels,
 * api/hr/salary-notches.
 *
 * Reads always work and, while Payroll is the structure source, reconcile the mirror on the way
 * — a grade added in payroll shows up here without any explicit sync. The writes below are live
 * only while the structure source is HR (a policy setting, lane G); with Payroll as the source
 * every one of them answers 409 pointing at payroll's setup screen. The Salary Structure screen
 * reads the setting and does not offer them otherwise.
 */
class SalaryGradeService {
  private readonly baseUrl = '/hr/salary-grades';
  private readonly levelsUrl = '/hr/salary-levels';
  private readonly notchesUrl = '/hr/salary-notches';

  getAll(includeInactive = true): Promise<SalaryGrade[]> {
    return apiService.get<SalaryGrade[]>(this.baseUrl, { includeInactive });
  }

  /** Grades available for assignment — what pickers should use. */
  getActive(): Promise<SalaryGrade[]> {
    return this.getAll(false);
  }

  /** A grade with its levels (and, per level, its notches). */
  getById(gradeId: string): Promise<SalaryGradeDetail> {
    return apiService.get<SalaryGradeDetail>(`${this.baseUrl}/${gradeId}`);
  }

  getLevels(gradeId: string, includeInactive = false): Promise<SalaryLevel[]> {
    return apiService.get<SalaryLevel[]>(`${this.baseUrl}/${gradeId}/levels`, { includeInactive });
  }

  getNotches(levelId: string, includeInactive = false): Promise<SalaryNotch[]> {
    return apiService.get<SalaryNotch[]>(`${this.levelsUrl}/${levelId}/notches`, {
      includeInactive,
    });
  }

  /**
   * Forces a re-projection from payroll. Reads already reconcile on their own; this backs
   * an explicit "refresh from payroll" action and backfills a tenant whose mirror has
   * never been built.
   */
  syncFromPayroll(): Promise<SalaryStructureProjectionResult> {
    return apiService.post<SalaryStructureProjectionResult>(`${this.baseUrl}/sync`);
  }

  // ── Writes (HR-mastered tenants only) ──────────────────────────────────────

  createGrade(data: CreateSalaryGradeRequest): Promise<SalaryGrade> {
    return apiService.post<SalaryGrade>(this.baseUrl, data);
  }

  updateGrade(id: string, data: UpdateSalaryGradeRequest): Promise<SalaryGrade> {
    return apiService.put<SalaryGrade>(`${this.baseUrl}/${id}`, data);
  }

  setGradeActive(id: string, isActive: boolean): Promise<SalaryGrade> {
    return apiService.put<SalaryGrade>(`${this.baseUrl}/${id}/active?isActive=${isActive}`, {});
  }

  deleteGrade(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  createLevel(data: CreateSalaryLevelRequest): Promise<SalaryLevel> {
    return apiService.post<SalaryLevel>(this.levelsUrl, data);
  }

  updateLevel(id: string, data: UpdateSalaryLevelRequest): Promise<SalaryLevel> {
    return apiService.put<SalaryLevel>(`${this.levelsUrl}/${id}`, data);
  }

  setLevelActive(id: string, isActive: boolean): Promise<SalaryLevel> {
    return apiService.put<SalaryLevel>(`${this.levelsUrl}/${id}/active?isActive=${isActive}`, {});
  }

  deleteLevel(id: string): Promise<void> {
    return apiService.delete<void>(`${this.levelsUrl}/${id}`);
  }

  createNotch(data: CreateSalaryNotchRequest): Promise<SalaryNotch> {
    return apiService.post<SalaryNotch>(this.notchesUrl, data);
  }

  updateNotch(id: string, data: UpdateSalaryNotchRequest): Promise<SalaryNotch> {
    return apiService.put<SalaryNotch>(`${this.notchesUrl}/${id}`, data);
  }

  setNotchActive(id: string, isActive: boolean): Promise<SalaryNotch> {
    return apiService.put<SalaryNotch>(`${this.notchesUrl}/${id}/active?isActive=${isActive}`, {});
  }

  deleteNotch(id: string): Promise<void> {
    return apiService.delete<void>(`${this.notchesUrl}/${id}`);
  }
}

export const salaryGradeService = new SalaryGradeService();
