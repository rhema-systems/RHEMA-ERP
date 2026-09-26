import { apiService } from '../api.service';
import type { RecruitmentDashboard } from '@/types/hr/recruitment-dashboard';
import type { RecruitmentAnalytics } from '@/types/hr/recruitment-analytics';

/**
 * api/recruitment-dashboard — the recruitment module's two aggregate reads, both HR-gated (see the
 * controller's doc comment). They answer different questions and are not interchangeable: `get()`
 * is the operational "what needs doing today" board, `getAnalytics()` is the closed-year record of
 * how recruitment performed.
 */
class RecruitmentDashboardService {
  private readonly baseUrl = '/recruitment-dashboard';

  get(): Promise<RecruitmentDashboard> {
    return apiService.get<RecruitmentDashboard>(this.baseUrl);
  }

  /**
   * Speed, cost, funnel, source effectiveness, offer outcomes, vacancy ageing and recruiter load
   * for one year. Omitting `year` gives the server's current year rather than an all-time total.
   */
  getAnalytics(year?: number): Promise<RecruitmentAnalytics> {
    return apiService.get<RecruitmentAnalytics>(
      `${this.baseUrl}/analytics`,
      year ? { year } : undefined,
    );
  }
}

export const recruitmentDashboardService = new RecruitmentDashboardService();
