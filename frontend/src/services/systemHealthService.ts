import { API_CONFIG, API_ENDPOINTS } from '../config/api';

export interface SystemHealthCheck {
  name: string;
  status: string;
  description?: string;
  durationMilliseconds: number;
  tags: string[];
}

export interface SystemHealthSnapshot {
  status: string;
  observedAtUtc: string;
  durationMilliseconds: number;
  checks: SystemHealthCheck[];
}

class SystemHealthService {
  async getReadiness(): Promise<SystemHealthSnapshot> {
    const response = await fetch(`${API_CONFIG.BASE_URL}${API_ENDPOINTS.HEALTH.READY}`, {
      cache: 'no-store',
      headers: { Accept: 'application/json' },
    });

    const payload = await response.json() as SystemHealthSnapshot;
    if (!response.ok && !payload?.status) {
      throw new Error('System readiness could not be loaded.');
    }
    return payload;
  }
}

export const systemHealthService = new SystemHealthService();
