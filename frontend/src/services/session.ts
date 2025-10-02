import { apiService } from './api.service';

export interface UserSession {
  sessionId: string;
  ipAddress: string;
  userAgent: string;
  deviceType: string;
  browser: string;
  operatingSystem: string;
  location: string;
  loginTime: string;
  lastActivityTime: string;
  sessionDuration: string;
  isCurrentSession: boolean;
}

export interface SessionTerminationResponse {
  message: string;
  sessionId: string;
}

export class SessionService {
  /**
   * Get the current user's active sessions
   */
  async getMyActiveSessions(): Promise<UserSession[]> {
    try {
      const sessions = await apiService.request<UserSession[]>('/session/my-sessions');
      return sessions;
    } catch (error: any) {
      console.error('Error fetching user sessions:', error);
      throw new Error(error.message || 'Failed to load your active sessions');
    }
  }

  /**
   * Terminate one of the current user's sessions
   */
  async terminateMySession(sessionId: string): Promise<SessionTerminationResponse> {
    try {
      const response = await apiService.request<SessionTerminationResponse>(
        `/session/my-sessions/${sessionId}/terminate`,
        {
          method: 'POST'
        }
      );
      return response;
    } catch (error: any) {
      console.error(`Error terminating session ${sessionId}:`, error);
      throw new Error(error.message || 'Failed to terminate the session');
    }
  }

  /**
   * Format session duration for display
   */
  formatSessionDuration(duration: string): string {
    try {
      // Parse duration string (format: "HH:mm:ss.fffffff")
      const parts = duration.split(':');
      if (parts.length >= 3) {
        const hours = parseInt(parts[0]);
        const minutes = parseInt(parts[1]);
        const seconds = parseInt(parts[2].split('.')[0]);
        
        if (hours > 0) {
          return `${hours}h ${minutes}m`;
        } else if (minutes > 0) {
          return `${minutes}m ${seconds}s`;
        } else {
          return `${seconds}s`;
        }
      }
      return duration;
    } catch {
      return duration;
    }
  }

  /**
   * Get a user-friendly device description
   */
  getDeviceDescription(session: UserSession): string {
    const parts: string[] = [];
    
    if (session.browser) {
      parts.push(session.browser);
    }
    
    if (session.operatingSystem) {
      parts.push(`on ${session.operatingSystem}`);
    }
    
    if (session.deviceType) {
      parts.push(`(${session.deviceType})`);
    }
    
    return parts.length > 0 ? parts.join(' ') : 'Unknown Device';
  }

  /**
   * Format date for display
   */
  formatDateTime(dateString: string): string {
    try {
      const date = new Date(dateString);
      const now = new Date();
      const diffMs = now.getTime() - date.getTime();
      const diffMinutes = Math.floor(diffMs / (1000 * 60));
      const diffHours = Math.floor(diffMinutes / 60);
      const diffDays = Math.floor(diffHours / 24);
      
      if (diffMinutes < 1) {
        return 'Just now';
      } else if (diffMinutes < 60) {
        return `${diffMinutes} minute${diffMinutes === 1 ? '' : 's'} ago`;
      } else if (diffHours < 24) {
        return `${diffHours} hour${diffHours === 1 ? '' : 's'} ago`;
      } else if (diffDays < 7) {
        return `${diffDays} day${diffDays === 1 ? '' : 's'} ago`;
      } else {
        return date.toLocaleDateString();
      }
    } catch {
      return dateString;
    }
  }

  /**
   * Get location display text
   */
  getLocationDisplay(location: string): string {
    if (!location || location === 'Unknown' || location.trim() === '') {
      return 'Unknown Location';
    }
    return location;
  }
}

export const sessionService = new SessionService();
export default sessionService;