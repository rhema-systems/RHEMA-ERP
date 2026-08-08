import { apiService } from '../api.service';
import type {
  SalaryGrade,
  SalaryGradeDetail,
  SalaryLevel,
  SalaryNotch,
  SalaryStructureProjectionResult,
} from '@/types/hr/salary';

/**
 * Read access to the salary structure. Backend routes: api/hr/salary-grades,
 * api/hr/salary-levels.
 *
 * Payroll owns this data — it is defined in Administration → HR → Payroll → Grades Setup
 * and mirrored into the HR tables. There are no create/update/delete methods here on
 * purpose: the write endpoints return 409. Reads reconcile the mirror automatically, so
 * a grade added in payroll shows up here without any explicit sync.
 */
class SalaryGradeService {
  private readonly baseUrl = '/hr/salary-grades';
  private readonly levelsUrl = '/hr/salary-levels';

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
}

export const salaryGradeService = new SalaryGradeService();
