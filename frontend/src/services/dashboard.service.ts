import { DashboardData, DashboardMetrics, RecentActivity, OnlineUser, SystemStatus, Notification } from './signalr.service';
import { apiRequest } from './api.service';

export interface DashboardApiService {
  getDashboardData(): Promise<DashboardData>;
  getDashboardMetrics(): Promise<DashboardMetrics>;
  getRecentActivities(): Promise<RecentActivity[]>;
  getOnlineUsers(): Promise<OnlineUser[]>;
  getSystemStatus(): Promise<SystemStatus>;
  broadcastDashboardUpdate(): Promise<{ message: string }>;
  sendTestNotification(notification: Partial<Notification>): Promise<{ message: string }>;
}

class DashboardService implements DashboardApiService {
  private readonly baseUrl = '/dashboard';

  async getDashboardData(): Promise<DashboardData> {
    const response = await apiRequest<DashboardData>({
      url: this.baseUrl,
      method: 'GET',
    });

    return response;
  }

  async getDashboardMetrics(): Promise<DashboardMetrics> {
    const response = await apiRequest<DashboardMetrics>({
      url: `${this.baseUrl}/metrics`,
      method: 'GET',
    });

    return response;
  }

  async getRecentActivities(): Promise<RecentActivity[]> {
    const response = await apiRequest<RecentActivity[]>({
      url: `${this.baseUrl}/activities`,
      method: 'GET',
    });

    return response;
  }

  async getOnlineUsers(): Promise<OnlineUser[]> {
    const response = await apiRequest<OnlineUser[]>({
      url: `${this.baseUrl}/online-users`,
      method: 'GET',
    });

    return response;
  }

  async getSystemStatus(): Promise<SystemStatus> {
    const response = await apiRequest<SystemStatus>({
      url: `${this.baseUrl}/system-status`,
      method: 'GET',
    });

    return response;
  }

  async broadcastDashboardUpdate(): Promise<{ message: string }> {
    const response = await apiRequest<{ message: string }>({
      url: `${this.baseUrl}/broadcast-update`,
      method: 'POST',
    });

    return response;
  }

  async sendTestNotification(notification: Partial<Notification>): Promise<{ message: string }> {
    const response = await apiRequest<{ message: string }>({
      url: `${this.baseUrl}/send-test-notification`,
      method: 'POST',
      data: notification,
    });

    return response;
  }
}

// Export singleton instance
export const dashboardService = new DashboardService();
export default dashboardService;