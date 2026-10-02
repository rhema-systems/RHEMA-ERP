import { apiService } from './api.service';
import type { LoginResponse } from './api.service';

class TokenRefreshService {
  /**
   * Refresh the JWT token using the refresh token
   */
  async refreshToken(): Promise<LoginResponse> {
    // apiService owns both the in-tab promise and the cross-tab refresh lock. Keeping every
    // refresh entry point on that path prevents a second, independently racing implementation.
    return apiService.refreshToken();
  }

  /**
   * Check if the current token is expired or about to expire
   */
  isTokenExpired(bufferMinutes: number = 5): boolean {
    const tokenExpiry = localStorage.getItem('tokenExpiry');
    if (!tokenExpiry) return true;

    const expiryTime = parseInt(tokenExpiry, 10);
    const bufferTime = bufferMinutes * 60 * 1000; // Convert to milliseconds
    const currentTime = Date.now();

    return currentTime >= (expiryTime - bufferTime);
  }

  /**
   * Get time until token expires in milliseconds
   */
  getTimeUntilExpiry(): number {
    const tokenExpiry = localStorage.getItem('tokenExpiry');
    if (!tokenExpiry) return 0;

    const expiryTime = parseInt(tokenExpiry, 10);
    const currentTime = Date.now();

    return Math.max(0, expiryTime - currentTime);
  }

  /**
   * Clear all tokens from storage
   */
  clearTokens(): void {
    localStorage.removeItem('authToken');
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('tokenExpiry');
  }

  /**
   * Check if refresh token exists
   */
  hasRefreshToken(): boolean {
    return !!localStorage.getItem('refreshToken');
  }

  /**
   * Setup automatic token refresh before expiry
   */
  setupAutoRefresh(onTokenRefreshed?: () => void, onRefreshFailed?: () => void): () => void {
    let refreshTimer: ReturnType<typeof setTimeout> | undefined;
    let stopped = false;

    const scheduleRefresh = () => {
      if (stopped) return;
      if (refreshTimer) clearTimeout(refreshTimer);
      const scheduledExpiry = localStorage.getItem('tokenExpiry');
      const timeUntilExpiry = this.getTimeUntilExpiry();
      const refreshTime = Math.max(0, timeUntilExpiry - (5 * 60 * 1000)); // Refresh 5 minutes before expiry

      refreshTimer = setTimeout(async () => {
        if (stopped) return;
        if (localStorage.getItem('tokenExpiry') !== scheduledExpiry) {
          scheduleRefresh();
          return;
        }

        // A hidden tab may be heavily throttled. It never decides validity by elapsed timer ticks;
        // it rechecks the stored expiry when visible, while a visible tab owns proactive refresh.
        if (document.visibilityState !== 'visible') {
          refreshTimer = setTimeout(scheduleRefresh, 60_000);
          return;
        }

        try {
          await this.refreshToken();
          onTokenRefreshed?.();
          scheduleRefresh(); // Schedule next refresh
        } catch (error) {
          console.error('Auto token refresh failed:', error);
          onRefreshFailed?.();
        }
      }, refreshTime);
    };

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible' && this.hasRefreshToken()) scheduleRefresh();
    };

    // Only schedule if we have a valid token and refresh token
    if (!this.isTokenExpired(0) && this.hasRefreshToken()) {
      scheduleRefresh();
    }
    document.addEventListener('visibilitychange', handleVisibilityChange);

    // Return cleanup function
    return () => {
      stopped = true;
      if (refreshTimer) {
        clearTimeout(refreshTimer);
      }
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    };
  }
}

export const tokenRefreshService = new TokenRefreshService();
