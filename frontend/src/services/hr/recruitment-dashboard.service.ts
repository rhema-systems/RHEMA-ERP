import { apiService } from '../api.service';
import type { RecruitmentDashboard } from '@/types/hr/recruitment-dashboard';

/**
 * api/recruitment-dashboard — one aggregated read for the KPI screen. HR-gated (see the
 * controller's doc comment); the `analytics` endpoint (speed/cost/funnel/source-effectiveness for
 * a given year) exists on the same controller but has no frontend consumer yet.
 */
class RecruitmentDashboardService {
  private readonly baseUrl = '/recruitment-dashboard';

  get(): Promise<RecruitmentDashboard> {
    return apiService.get<RecruitmentDashboard>(this.baseUrl);
  }
}

export const recruitmentDashboardService = new RecruitmentDashboardService();
