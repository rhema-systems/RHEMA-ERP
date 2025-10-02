import { useEffect, useState, useCallback, useRef } from 'react';
import { useTheme } from '@/contexts/ThemeContext';
import { themeService, UserThemePreferences } from '@/services/theme.service';

export interface UseUserThemeOptions {
  autoSync?: boolean;
  fallbackToLocal?: boolean;
}

export function useUserTheme({ 
  autoSync = true,
  fallbackToLocal = true 
}: UseUserThemeOptions = {}) {
  const { theme, setTheme, actualTheme, systemTheme, toggleTheme, isSystemTheme } = useTheme();
  const [preferences, setPreferences] = useState<UserThemePreferences | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isSyncing, setIsSyncing] = useState(false);
  const hasInitialized = useRef(false);

  // Load user preferences on mount (only once)
  useEffect(() => {
    if (hasInitialized.current) return;
    
    const loadPreferences = async () => {
      setIsLoading(true);
      setError(null);

      try {
        let userPreferences: UserThemePreferences;

        if (autoSync) {
          // Try to sync with server
          userPreferences = await themeService.syncPreferences();
        } else {
          // Load from server or fall back to local
          try {
            userPreferences = await themeService.getUserPreferences();
          } catch (err) {
            if (fallbackToLocal) {
              userPreferences = themeService.getLocalPreferences() || {
                theme: 'system',
                colorScheme: 'blue',
                fontSize: 'md',
                compactMode: false,
                reducedMotion: false,
                highContrast: false,
              };
            } else {
              throw err;
            }
          }
        }

        setPreferences(userPreferences);
        
        // Apply theme if it differs from current but don't create loops
        // This will only happen on the initial load
        if (userPreferences.theme !== theme) {
          setTheme(userPreferences.theme);
        }
        
        hasInitialized.current = true;

      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : 'Failed to load theme preferences';
        setError(errorMessage);
        console.error('Error loading theme preferences:', err);
        hasInitialized.current = true; // Mark as initialized even on error
      } finally {
        setIsLoading(false);
      }
    };

    loadPreferences();
  }, [autoSync, fallbackToLocal, theme, setTheme]); // Keep dependencies but use ref to prevent multiple loads

  // Update user preferences
  const updatePreferences = useCallback(async (newPreferences: Partial<UserThemePreferences>) => {
    if (!preferences) return;

    setIsSyncing(true);
    setError(null);

    try {
      const updatedPreferences = {
        ...preferences,
        ...newPreferences,
        lastUpdated: new Date().toISOString(),
      };

      // Update local state immediately for responsive UI
      setPreferences(updatedPreferences);

      // Save to local storage for offline support
      themeService.setLocalPreferences(updatedPreferences);

      // Apply theme change if needed
      if (newPreferences.theme && newPreferences.theme !== theme) {
        setTheme(newPreferences.theme);
      }

      // Sync with server in background
      try {
        const serverPreferences = await themeService.updateUserPreferences(updatedPreferences);
        setPreferences(serverPreferences);
      } catch (serverError) {
        console.warn('Failed to sync preferences with server:', serverError);
        // Keep local changes even if server sync fails
      }

    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to update theme preferences';
      setError(errorMessage);
      console.error('Error updating theme preferences:', err);
      
      // Revert local state on error
      const localPrefs = themeService.getLocalPreferences();
      if (localPrefs) {
        setPreferences(localPrefs);
      }
    } finally {
      setIsSyncing(false);
    }
  }, [preferences, theme, setTheme]);

  // Reset to defaults
  const resetToDefaults = useCallback(async () => {
    setIsSyncing(true);
    setError(null);

    try {
      const defaultPreferences = await themeService.resetToDefaults();
      setPreferences(defaultPreferences);
      setTheme(defaultPreferences.theme);
      themeService.setLocalPreferences(defaultPreferences);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to reset theme preferences';
      setError(errorMessage);
      console.error('Error resetting theme preferences:', err);
    } finally {
      setIsSyncing(false);
    }
  }, [setTheme]);

  // Sync with server manually
  const syncWithServer = useCallback(async () => {
    setIsSyncing(true);
    setError(null);

    try {
      const syncedPreferences = await themeService.syncPreferences();
      setPreferences(syncedPreferences);
      
      if (syncedPreferences.theme !== theme) {
        setTheme(syncedPreferences.theme);
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to sync with server';
      setError(errorMessage);
      console.error('Error syncing with server:', err);
    } finally {
      setIsSyncing(false);
    }
  }, [theme, setTheme]);

  // Update specific preference
  const updateTheme = useCallback((newTheme: 'dark' | 'light' | 'system') => {
    updatePreferences({ theme: newTheme });
  }, [updatePreferences]);

  const updateColorScheme = useCallback((colorScheme: string) => {
    updatePreferences({ colorScheme });
  }, [updatePreferences]);

  const updateFontSize = useCallback((fontSize: 'sm' | 'md' | 'lg') => {
    updatePreferences({ fontSize });
  }, [updatePreferences]);

  const toggleCompactMode = useCallback(() => {
    if (preferences) {
      updatePreferences({ compactMode: !preferences.compactMode });
    }
  }, [preferences, updatePreferences]);

  const toggleReducedMotion = useCallback(() => {
    if (preferences) {
      updatePreferences({ reducedMotion: !preferences.reducedMotion });
    }
  }, [preferences, updatePreferences]);

  const toggleHighContrast = useCallback(() => {
    if (preferences) {
      updatePreferences({ highContrast: !preferences.highContrast });
    }
  }, [preferences, updatePreferences]);

  return {
    // Theme state
    theme,
    actualTheme,
    systemTheme,
    isSystemTheme,
    toggleTheme,

    // User preferences
    preferences,
    isLoading,
    error,
    isSyncing,

    // Preference updates
    updatePreferences,
    updateTheme,
    updateColorScheme,
    updateFontSize,
    toggleCompactMode,
    toggleReducedMotion,
    toggleHighContrast,

    // Actions
    resetToDefaults,
    syncWithServer,

    // Computed values
    isDark: actualTheme === 'dark',
    isLight: actualTheme === 'light',
    isCompact: preferences?.compactMode || false,
    hasReducedMotion: preferences?.reducedMotion || false,
    isHighContrast: preferences?.highContrast || false,
    fontSize: preferences?.fontSize || 'md',
    colorScheme: preferences?.colorScheme || 'blue',
  };
}

// Hook specifically for theme-aware styling
export function useThemeStyles() {
  const { actualTheme, preferences } = useUserTheme();
  
  const getClassName = useCallback((lightClass: string, darkClass: string) => {
    return actualTheme === 'dark' ? darkClass : lightClass;
  }, [actualTheme]);

  const getStyle = useCallback((lightStyle: React.CSSProperties, darkStyle: React.CSSProperties) => {
    return actualTheme === 'dark' ? darkStyle : lightStyle;
  }, [actualTheme]);

  return {
    actualTheme,
    isDark: actualTheme === 'dark',
    isLight: actualTheme === 'light',
    isCompact: preferences?.compactMode || false,
    hasReducedMotion: preferences?.reducedMotion || false,
    isHighContrast: preferences?.highContrast || false,
    fontSize: preferences?.fontSize || 'md',
    colorScheme: preferences?.colorScheme || 'blue',
    getClassName,
    getStyle,
  };
}

export default useUserTheme;