import { apiService } from '../api.service';
import type {
  CalibrationDistribution,
  CycleNudgeResult,
  EmployeePerformanceTrend,
  HRCycleDashboard,
} from '@/types/hr/analytics';

/**
 * api/HRCycleDashboard — the whole HR view of a running cycle in one call.
 *
 * ⚠ `getActiveCycleId` answers with the all-zero GUID rather than 404 when no cycle is open;
 * `EMPTY_GUID` below is the test for that. Everything else is HR-only (403 otherwise).
 */
export const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

class HRCycleDashboardService {
  private readonly baseUrl = '/HRCycleDashboard';

  /** The most recently touched Open/InProgress cycle, or `EMPTY_GUID` when none is running. */
  getActiveCycleId(): Promise<string> {
    return apiService.get<string>(`${this.baseUrl}/active-cycle`);
  }

  get(cycleId: string): Promise<HRCycleDashboard> {
    return apiService.get<HRCycleDashboard>(`${this.baseUrl}/${cycleId}`);
  }

  /**
   * Reminds whoever owes one appraisal's current step — the appraisee, the peers with forms
   * still open, or the manager.
   *
   * 422 when the step is not one an individual can be nudged about: a finished appraisal, or one
   * waiting on calibration or on HR itself. Show the message; it names the step.
   */
  nudge(cycleId: string, appraisalId: string): Promise<CycleNudgeResult> {
    return apiService.post<CycleNudgeResult>(
      `${this.baseUrl}/${cycleId}/appraisals/${appraisalId}/nudge`,
    );
  }
}

/**
 * api/PerformanceAnalytics — score analytics over finalised appraisals.
 *
 * ⚠ Both reads are derived from `overallScore`, which is only set at HR sign-off. Early in a
 * cycle they are legitimately empty; that is not an error state.
 */
class PerformanceAnalyticsService {
  private readonly baseUrl = '/PerformanceAnalytics';

  /** Rating spread for a cycle — the calibration leniency check. HR only. */
  getCycleRatingDistribution(cycleId: string): Promise<CalibrationDistribution> {
    return apiService.get<CalibrationDistribution>(
      `${this.baseUrl}/cycle/${cycleId}/rating-distribution`,
    );
  }

  /** One employee's score history. Readable by HR, the employee, and their line manager. */
  getEmployeeTrend(employeeId: string): Promise<EmployeePerformanceTrend> {
    return apiService.get<EmployeePerformanceTrend>(
      `${this.baseUrl}/employee/${employeeId}/trend`,
    );
  }

  /** The signed-in employee's own history. 400 when the account has no employee record. */
  getMyTrend(): Promise<EmployeePerformanceTrend> {
    return apiService.get<EmployeePerformanceTrend>(`${this.baseUrl}/employee/me/trend`);
  }
}

export const hrCycleDashboardService = new HRCycleDashboardService();
export const performanceAnalyticsService = new PerformanceAnalyticsService();
