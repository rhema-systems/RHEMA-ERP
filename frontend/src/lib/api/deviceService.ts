import { apiService } from '../../services/api.service';

// Types matching the backend DTOs
export interface DeviceInfo {
  type: string;
  os: string;
  browser: string;
  model: string;
}

export interface LocationInfo {
  city: string;
  country: string;
  ip: string;
  coordinates?: {
    latitude: number;
    longitude: number;
  };
}

export interface SessionInfo {
  sessionId: string;
  startTime: string;
  lastActivity: string;
  isActive: boolean;
  duration: number; // in minutes
}

export interface SecurityInfo {
  isTrusted: boolean;
  riskScore: number;
  flags: string[];
  twoFactorEnabled: boolean;
}

export interface DeviceSession {
  id: string;
  userId: string;
  userName: string;
  userEmail: string;
  deviceInfo: DeviceInfo;
  location: LocationInfo;
  session: SessionInfo;
  security: SecurityInfo;
}

export interface DeviceSessionStats {
  totalSessions: number;
  activeSessions: number;
  uniqueDevices: number;
  uniqueLocations: number;
  lastLogin?: string;
  recentLocations: string[];
  recentDevices: string[];
}

export interface MyDevicesResponse {
  activeSessions: DeviceSession[];
  recentSessions: DeviceSession[];
  stats: DeviceSessionStats;
}

export interface SuspiciousActivity {
  id: string;
  deviceId: string;
  timestamp: string;
  type: string;
  description: string;
  severity: 'low' | 'medium' | 'high' | 'critical';
}

export interface TerminateSessionRequest {
  reason?: string;
}

export interface UpdateDeviceTrustRequest {
  deviceId: string;
  isTrusted: boolean;
}

export class DeviceService {
  /**
   * Get current user's devices and sessions
   */
  async getMyDevices(): Promise<MyDevicesResponse> {
    const response = await apiService.request<MyDevicesResponse>('/device/my-devices');
    return response;
  }

  /**
   * Get session details by ID
   */
  async getSessionDetails(sessionId: string): Promise<DeviceSession> {
    const response = await apiService.request<DeviceSession>(`/device/sessions/${sessionId}`);
    return response;
  }

  /**
   * Terminate a specific session
   */
  async terminateSession(sessionId: string, reason?: string): Promise<{ message: string; sessionId: string }> {
    const request: TerminateSessionRequest = reason ? { reason } : {};
    const response = await apiService.request<{ message: string; sessionId: string }>(
      `/device/sessions/${sessionId}/terminate`,
      {
        method: 'POST',
        body: JSON.stringify(request)
      }
    );
    return response;
  }

  /**
   * Terminate all other sessions except the current one
   */
  async terminateAllOtherSessions(reason?: string): Promise<{ message: string; terminatedCount: number }> {
    const request: TerminateSessionRequest = reason ? { reason } : {};
    const response = await apiService.request<{ message: string; terminatedCount: number }>(
      '/device/terminate-all-others',
      {
        method: 'POST',
        body: JSON.stringify(request)
      }
    );
    return response;
  }

  /**
   * Update device trust status
   */
  async updateDeviceTrust(deviceId: string, isTrusted: boolean): Promise<{ message: string }> {
    const request: UpdateDeviceTrustRequest = { deviceId, isTrusted };
    const response = await apiService.request<{ message: string }>('/device/trust', {
      method: 'POST',
      body: JSON.stringify(request)
    });
    return response;
  }

  /**
   * Get suspicious activity
   */
  async getSuspiciousActivity(days: number = 30): Promise<SuspiciousActivity[]> {
    const response = await apiService.request<SuspiciousActivity[]>(`/device/suspicious-activity?days=${days}`);
    return response;
  }

  /**
   * Get session statistics
   */
  async getSessionStats(days: number = 30): Promise<DeviceSessionStats> {
    const response = await apiService.request<DeviceSessionStats>(`/device/stats?days=${days}`);
    return response;
  }
}

// Export a singleton instance
export const deviceService = new DeviceService();