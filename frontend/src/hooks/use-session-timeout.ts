'use client';

import { useEffect, useState, useCallback, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import { settingsService } from '../services/settings';
import { tokenRefreshService } from '../services/token-refresh.service';
import { authService } from '../services/auth';
import { buildLoginRedirectUrl, getCurrentRelativeUrl } from '../lib/auth-redirect';

export interface SessionTimeoutState {
  isActive: boolean;
  showWarning: boolean;
  remainingSeconds: number;
  sessionTimeoutMinutes: number;
  lastActivity: number;
}

type SessionSettingsDto = Awaited<ReturnType<typeof settingsService.getSessionSettings>>;

const UNINITIALIZED_SESSION_TIMEOUT = 0;
const WARNING_TIME = 120; // Show warning 2 minutes (120 seconds) before timeout
const SESSION_ACTIVITY_STORAGE_KEY = 'erp-session-last-activity';

const readStoredSessionActivity = (): number | null => {
  if (typeof window === 'undefined') {
    return null;
  }

  const rawValue = localStorage.getItem(SESSION_ACTIVITY_STORAGE_KEY);
  if (!rawValue) {
    return null;
  }

  const parsedValue = Number.parseInt(rawValue, 10);
  return Number.isFinite(parsedValue) ? parsedValue : null;
};

export function useSessionTimeout() {
  const [sessionState, setSessionState] = useState<SessionTimeoutState>({
    isActive: true,
    showWarning: false,
    remainingSeconds: WARNING_TIME,
    sessionTimeoutMinutes: UNINITIALIZED_SESSION_TIMEOUT,
    lastActivity: Date.now(),
  });

  const timeoutRef = useRef<NodeJS.Timeout | null>(null);
  const warningTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const activityTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  // Fetch persisted session settings for the authenticated tenant.
  const { data: sessionSettings, isLoading: isLoadingSettings } = useQuery<SessionSettingsDto>({
    queryKey: ['sessionSettings'],
    queryFn: async (): Promise<SessionSettingsDto> => settingsService.getSessionSettings(),
    refetchInterval: 5 * 60 * 1000, // Refetch every 5 minutes to get updated settings
    enabled: typeof window !== 'undefined' && authService.isAuthenticated(),
    retry: (failureCount, error: any) => {
      // Don't retry on 401 errors - user is not authenticated
      if (error?.message?.includes('401') || error?.message?.includes('Unauthorized')) {
        return false;
      }
      return failureCount < 2;
    },
  });

  const resolvedSessionSettings = sessionSettings as SessionSettingsDto | undefined;
  const sessionTimeoutMinutes = resolvedSessionSettings?.sessionTimeoutMinutes ?? UNINITIALIZED_SESSION_TIMEOUT;
  const shouldInitializeTimers = !isLoadingSettings && sessionTimeoutMinutes > 0;
  const sessionTimeoutMs = sessionTimeoutMinutes * 60 * 1000;
  const warningTimeMs = WARNING_TIME * 1000;

  const clearTimers = useCallback(() => {
    if (timeoutRef.current) clearTimeout(timeoutRef.current);
    if (warningTimeoutRef.current) clearTimeout(warningTimeoutRef.current);
    if (activityTimeoutRef.current) clearTimeout(activityTimeoutRef.current);
  }, []);

  const handleSessionTimeout = useCallback(async (preserveRedirect: boolean = true) => {
    console.log('🚪 Session expired - calling backend logout and clearing tokens');
    
    clearTimers();

    // Update state immediately
    setSessionState(prev => ({
      ...prev,
      isActive: false,
      showWarning: false,
    }));

    // Call proper logout which handles backend cleanup
    try {
      await authService.logout();
      console.log('✅ Backend logout successful');
    } catch (error) {
      console.warn('⚠️ Backend logout failed, but continuing with cleanup:', error);
      // Still clear tokens locally even if backend fails
      tokenRefreshService.clearTokens();
    }

    const redirectTarget = preserveRedirect ? getCurrentRelativeUrl() : null;

    // Redirect to login
    window.location.href = buildLoginRedirectUrl(redirectTarget);
  }, [clearTimers]);

  const showWarningAndScheduleTimeout = useCallback((activityTimestamp: number) => {
    const elapsedMs = Math.max(0, Date.now() - activityTimestamp);
    const remainingTimeoutMs = Math.max(0, sessionTimeoutMs - elapsedMs);

    if (remainingTimeoutMs <= 0) {
      void handleSessionTimeout();
      return;
    }

    console.log('⚠️ Showing session timeout warning');
    setSessionState(prev => ({
      ...prev,
      showWarning: true,
      remainingSeconds: Math.max(1, Math.ceil(remainingTimeoutMs / 1000)),
      sessionTimeoutMinutes,
    }));

    timeoutRef.current = setTimeout(() => {
      console.log('⏰ Session timeout - logging out');
      void handleSessionTimeout();
    }, remainingTimeoutMs);
  }, [handleSessionTimeout, sessionTimeoutMinutes, sessionTimeoutMs]);

  const syncActivity = useCallback((activityTimestamp: number, persistToStorage: boolean) => {
    const normalizedTimestamp = Number.isFinite(activityTimestamp) ? activityTimestamp : Date.now();

    setSessionState(prev => ({
      ...prev,
      isActive: true,
      lastActivity: normalizedTimestamp,
      showWarning: false,
      remainingSeconds: WARNING_TIME,
      sessionTimeoutMinutes,
    }));

    clearTimers();

    if (persistToStorage && typeof window !== 'undefined') {
      localStorage.setItem(SESSION_ACTIVITY_STORAGE_KEY, normalizedTimestamp.toString());
    }

    if (!shouldInitializeTimers) {
      console.log('⏳ Waiting for persisted session settings before initializing session timeout...');
      return;
    }

    console.log(`🔄 Activity synchronized - session timeout in ${sessionTimeoutMinutes} minutes`);

    const elapsedMs = Math.max(0, Date.now() - normalizedTimestamp);
    const remainingTimeoutMs = sessionTimeoutMs - elapsedMs;

    if (remainingTimeoutMs <= 0) {
      void handleSessionTimeout();
      return;
    }

    const remainingWarningDelayMs = remainingTimeoutMs - warningTimeMs;

    if (remainingWarningDelayMs <= 0) {
      showWarningAndScheduleTimeout(normalizedTimestamp);
      return;
    }

    warningTimeoutRef.current = setTimeout(() => {
      showWarningAndScheduleTimeout(normalizedTimestamp);
    }, remainingWarningDelayMs);
  }, [
    clearTimers,
    handleSessionTimeout,
    sessionTimeoutMinutes,
    sessionTimeoutMs,
    shouldInitializeTimers,
    showWarningAndScheduleTimeout,
    warningTimeMs,
  ]);

  const resetActivity = useCallback(() => {
    syncActivity(Date.now(), true);
  }, [syncActivity]);

  const extendSession = useCallback(async () => {
    console.log('🔄 Extending session...');
    
    try {
      // Try to refresh the token to extend the session
      if (tokenRefreshService.hasRefreshToken()) {
        await tokenRefreshService.refreshToken();
        console.log('✅ Session extended via token refresh');
      }
      
      // Reset activity timers
      resetActivity();
    } catch (error) {
      console.error('❌ Failed to extend session:', error);
      handleSessionTimeout();
    }
  }, [resetActivity, handleSessionTimeout]);

  const logout = useCallback(async () => {
    console.log('🚪 Manual logout requested');
    await handleSessionTimeout(false);
  }, [handleSessionTimeout]);

  // Activity event handlers
  const handleActivity = useCallback(() => {
    // Throttle activity updates to prevent excessive timer resets
    if (activityTimeoutRef.current) return;
    
    activityTimeoutRef.current = setTimeout(() => {
      resetActivity();
      activityTimeoutRef.current = null;
    }, 1000); // Throttle to once per second
  }, [resetActivity]);

  // Set up activity listeners - only if authenticated
  useEffect(() => {
    // Don't set up activity monitoring if not authenticated
    if (typeof window === 'undefined' || !authService.isAuthenticated()) {
      return;
    }

    const events = [
      'mousedown',
      'mousemove',
      'keypress',
      'scroll',
      'touchstart',
      'click',
    ];

    // Add event listeners with passive option for better performance
    events.forEach(event => {
      document.addEventListener(event, handleActivity, { passive: true });
    });

    const storedActivityTimestamp = readStoredSessionActivity();
    const initialActivityTimestamp = storedActivityTimestamp ?? Date.now();

    syncActivity(initialActivityTimestamp, !storedActivityTimestamp);

    const handleStorageEvent = (event: StorageEvent) => {
      if (event.key === SESSION_ACTIVITY_STORAGE_KEY && event.newValue) {
        const activityTimestamp = Number.parseInt(event.newValue, 10);
        if (Number.isFinite(activityTimestamp) && activityTimestamp > sessionState.lastActivity) {
          syncActivity(activityTimestamp, false);
        }
      }
    };

    window.addEventListener('storage', handleStorageEvent);

    return () => {
      // Cleanup event listeners
      events.forEach(event => {
        document.removeEventListener(event, handleActivity);
      });

      window.removeEventListener('storage', handleStorageEvent);
      clearTimers();
    };
  }, [clearTimers, handleActivity, sessionState.lastActivity, sessionTimeoutMs, syncActivity]);

  // Initialize session timeout when the persisted tenant settings are first loaded.
  useEffect(() => {
    if (resolvedSessionSettings?.sessionTimeoutMinutes && shouldInitializeTimers) {
      console.log(`\u2699\ufe0f Session settings loaded - session timeout: ${resolvedSessionSettings.sessionTimeoutMinutes} minutes`);
      setSessionState(prev => ({
        ...prev,
        sessionTimeoutMinutes: resolvedSessionSettings.sessionTimeoutMinutes,
      }));
      
      // Initialize timers with the correct timeout
      const storedActivityTimestamp = readStoredSessionActivity() ?? Date.now();
      syncActivity(storedActivityTimestamp, false);
    }
  }, [resolvedSessionSettings?.sessionTimeoutMinutes, shouldInitializeTimers, syncActivity]);

  // Update session timeout when the persisted tenant settings change.
  useEffect(() => {
    if (resolvedSessionSettings?.sessionTimeoutMinutes && !isLoadingSettings) {
      console.log(`\ud83d\udd04 Session settings updated - new session timeout: ${resolvedSessionSettings.sessionTimeoutMinutes} minutes`);
      setSessionState(prev => ({
        ...prev,
        sessionTimeoutMinutes: resolvedSessionSettings.sessionTimeoutMinutes,
      }));
      
      // Reset activity to apply new timeout
      const storedActivityTimestamp = readStoredSessionActivity() ?? Date.now();
      syncActivity(storedActivityTimestamp, false);
    }
  }, [resolvedSessionSettings?.sessionTimeoutMinutes, isLoadingSettings, syncActivity]);

  // Update remaining seconds countdown when warning is shown
  useEffect(() => {
    if (!sessionState.showWarning || sessionState.remainingSeconds <= 0) return;

    let remainingTime = sessionState.remainingSeconds;

    const updateCountdown = () => {
      remainingTime -= 1;
      
      setSessionState(prev => ({
        ...prev,
        remainingSeconds: Math.max(0, remainingTime),
      }));

      if (remainingTime <= 0) {
        clearInterval(countdownInterval);
      }
    };

    const countdownInterval = setInterval(updateCountdown, 1000);

    return () => {
      clearInterval(countdownInterval);
    };
  }, [sessionState.remainingSeconds, sessionState.showWarning]);

  return {
    sessionState,
    extendSession,
    logout,
    resetActivity,
  };
}
