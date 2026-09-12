import { apiService } from '../api.service';
import type {
  TrainingDashboard,
  TrainingAnalytics,
  EmployeeTrainingSummary,
} from '@/types/hr/training-analytics';

/**
 * Training dashboards and analytics. Backend route: api/training-dashboard.
 *
 * Note that `mine` and `employee/{id}` return the same shape but are not interchangeable: the first
 * is token-derived self-service, the second is HR-only because a training record carries compliance
 * standing and certificate history.
 */
class TrainingAnalyticsService {
  private readonly baseUrl = '/training-dashboard';

  getDashboard(year?: number): Promise<TrainingDashboard> {
    return apiService.get<TrainingDashboard>(this.baseUrl, year ? { year } : undefined);
  }

  getAnalytics(year?: number): Promise<TrainingAnalytics> {
    return apiService.get<TrainingAnalytics>(`${this.baseUrl}/analytics`, year ? { year } : undefined);
  }

  /** The caller's own training record. */
  getMySummary(): Promise<EmployeeTrainingSummary> {
    return apiService.get<EmployeeTrainingSummary>(`${this.baseUrl}/mine`);
  }

  /** Someone else's — HR only; a 403 here is the rule working, not a bug. */
  getEmployeeSummary(employeeId: string): Promise<EmployeeTrainingSummary> {
    return apiService.get<EmployeeTrainingSummary>(`${this.baseUrl}/employee/${employeeId}`);
  }
}

export const trainingAnalyticsService = new TrainingAnalyticsService();
