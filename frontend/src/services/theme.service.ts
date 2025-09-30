import { apiService } from './api.service';

export interface UserThemePreferences {
  theme: 'dark' | 'light' | 'system';
  colorScheme?: string;
  fontSize?: 'sm' | 'md' | 'lg';
  compactMode?: boolean;
  reducedMotion?: boolean;
  highContrast?: boolean;
  lastUpdated?: string;
}

export interface ThemeApiService {
  getUserPreferences(): Promise<UserThemePreferences>;
  updateUserPreferences(preferences: Partial<UserThemePreferences>): Promise<UserThemePreferences>;
  resetToDefaults(): Promise<UserThemePreferences>;
}

class ThemeService implements ThemeApiService {
  private readonly baseUrl = '/user/preferences';

  async getUserPreferences(): Promise<UserThemePreferences> {
    try {
      const response = await apiService.request<{ themePreferences: UserThemePreferences }>(`${this.baseUrl}/theme`, {
        method: 'GET',
      });
      
      return response.themePreferences || this.getDefaultPreferences();
    } catch (error) {
      console.warn('Failed to fetch theme preferences from server:', error);
      return this.getDefaultPreferences();
    }
  }

  async updateUserPreferences(preferences: Partial<UserThemePreferences>): Promise<UserThemePreferences> {
    try {
      const response = await apiService.request<{ themePreferences: UserThemePreferences }>(`${this.baseUrl}/theme`, {
        method: 'PUT',
        body: JSON.stringify({ themePreferences: preferences }),
      });

      return response.themePreferences;
    } catch (error) {
      console.error('Failed to update theme preferences:', error);
      throw error;
    }
  }

  async resetToDefaults(): Promise<UserThemePreferences> {
    try {
      const response = await apiService.request<{ themePreferences: UserThemePreferences }>(`${this.baseUrl}/theme/reset`, {
        method: 'POST',
      });

      return response.themePreferences;
    } catch (error) {
      console.error('Failed to reset theme preferences:', error);
      throw error;
    }
  }

  private getDefaultPreferences(): UserThemePreferences {
    return {
      theme: 'system',
      colorScheme: 'blue',
      fontSize: 'md',
      compactMode: false,
      reducedMotion: false,
      highContrast: false,
    };
  }

  // Local storage helpers for offline support
  getLocalPreferences(): UserThemePreferences | null {
    try {
      const stored = localStorage.getItem('erp-theme-preferences');
      return stored ? JSON.parse(stored) : null;
    } catch (error) {
      console.warn('Failed to read theme preferences from localStorage:', error);
      return null;
    }
  }

  setLocalPreferences(preferences: UserThemePreferences): void {
    try {
      localStorage.setItem('erp-theme-preferences', JSON.stringify(preferences));
    } catch (error) {
      console.warn('Failed to save theme preferences to localStorage:', error);
    }
  }

  clearLocalPreferences(): void {
    try {
      localStorage.removeItem('erp-theme-preferences');
    } catch (error) {
      console.warn('Failed to clear theme preferences from localStorage:', error);
    }
  }

  // Sync preferences between local and server
  async syncPreferences(): Promise<UserThemePreferences> {
    try {
      const serverPreferences = await this.getUserPreferences();
      const localPreferences = this.getLocalPreferences();

      // If local preferences are newer, sync them to server
      if (localPreferences && localPreferences.lastUpdated) {
        const localTime = new Date(localPreferences.lastUpdated);
        const serverTime = serverPreferences.lastUpdated 
          ? new Date(serverPreferences.lastUpdated) 
          : new Date(0);

        if (localTime > serverTime) {
          return await this.updateUserPreferences(localPreferences);
        }
      }

      // Use server preferences and update local
      this.setLocalPreferences(serverPreferences);
      return serverPreferences;
    } catch (error) {
      console.warn('Failed to sync theme preferences:', error);
      // Fall back to local preferences if available
      return this.getLocalPreferences() || this.getDefaultPreferences();
    }
  }
}

// Export singleton instance
export const themeService = new ThemeService();
export default themeService;