import { apiService } from './api.service';

export interface TokenRefreshResponse {
  token: string;
  refreshToken: string;
  expiresIn: number;
  user: {
    id: string;
    username: string;
    email: string;
    firstName: string;
    lastName: string;
    roles: string[];
  };
}

class TokenRefreshService {
  private refreshPromise: Promise<TokenRefreshResponse> | null = null;

  /**
   * Refresh the JWT token using the refresh token
   */
  async refreshToken(): Promise<TokenRefreshResponse> {
    // Prevent multiple simultaneous refresh requests
    if (this.refreshPromise) {
      return this.refreshPromise;
    }

    try {
      this.refreshPromise = this.performTokenRefresh();
      const response = await this.refreshPromise;
      
      // Store the new tokens
      localStorage.setItem('authToken', response.token);
      localStorage.setItem('refreshToken', response.refreshToken);
      
      // Update token expiry time
      const expiryTime = Date.now() + (response.expiresIn * 1000);
      localStorage.setItem('tokenExpiry', expiryTime.toString());
      
      console.log('✅ Token refreshed successfully');
      return response;
    } catch (error) {
      console.error('❌ Token refresh failed:', error);
      throw error;
    } finally {
      this.refreshPromise = null;
    }
  }

  private async performTokenRefresh(): Promise<TokenRefreshResponse> {
    const refreshToken = localStorage.getItem('refreshToken');
    
    if (!refreshToken) {
      throw new Error('No refresh token available');
    }

    const baseUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
    const token = localStorage.getItem('authToken');
    
    const response = await fetch(`${baseUrl}/auth/refresh`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ token, refreshToken }),
    });

    if (!response.ok) {
      if (response.status === 401) {
        throw new Error('REFRESH_TOKEN_EXPIRED');
      }
      throw new Error(`Token refresh failed: ${response.statusText}`);
    }

    return response.json();
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
    let refreshTimer: NodeJS.Timeout;

    const scheduleRefresh = () => {
      const timeUntilExpiry = this.getTimeUntilExpiry();
      const refreshTime = Math.max(0, timeUntilExpiry - (5 * 60 * 1000)); // Refresh 5 minutes before expiry

      console.log(`🔄 Token refresh scheduled in ${Math.round(refreshTime / 1000)} seconds`);

      refreshTimer = setTimeout(async () => {
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

    // Only schedule if we have a valid token and refresh token
    if (!this.isTokenExpired(0) && this.hasRefreshToken()) {
      scheduleRefresh();
    }

    // Return cleanup function
    return () => {
      if (refreshTimer) {
        clearTimeout(refreshTimer);
      }
    };
  }
}

export const tokenRefreshService = new TokenRefreshService();