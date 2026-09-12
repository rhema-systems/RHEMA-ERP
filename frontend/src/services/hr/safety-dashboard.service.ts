import { apiService } from '../api.service';
import type { SheDashboard } from '@/types/hr/safety';

/**
 * The SHE dashboard — 17 live operational counts computed server-side per tenant.
 * Backend route: api/safety/dashboard. HR-only.
 */
class SafetyDashboardService {
  getDashboard(): Promise<SheDashboard> {
    return apiService.get<SheDashboard>('/safety/dashboard');
  }
}

export const safetyDashboardService = new SafetyDashboardService();
export default safetyDashboardService;
