'use client';

import { useEffect, useState, useCallback, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import { settingsService, type SessionRuntimeSettings } from '../services/settings';
import { tokenRefreshService } from '../services/token-refresh.service';
import { authService } from '../services/auth';

export interface SessionTimeoutState {
  isActive: boolean;
  showWarning: boolean;
  remainingSeconds: number;
  sessionTimeoutMinutes: number;
  lastActivity: number;
}

const DEFAULT_SESSION_TIMEOUT = 30; // 30 minutes
const WARNING_TIME = 120; // Show warning 2 minutes (120 seconds) before timeout

export function useSessionTimeout() {
  const [sessionState, setSessionState] = useState<SessionTimeoutState>({
    isActive: true,
    showWarning: false,
    remainingSeconds: WARNING_TIME,
    sessionTimeoutMinutes: DEFAULT_SESSION_TIMEOUT,
    lastActivity: Date.now(),
  });

  const timeoutRef = useRef<NodeJS.Timeout | null>(null);
  const warningTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const activityTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  // Fetch session timeout settings from security settings - only if authenticated
  const { data: sessionRuntimeSettings, isLoading: isLoadingSettings } = useQuery<SessionRuntimeSettings>({
    queryKey: ['sessionRuntimeSettings'],
    queryFn: () => settingsService.getSessionSettings(),
    staleTime: 30 * 60 * 1000,
    gcTime: 60 * 60 * 1000,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    refetchOnMount: false,
    refetchInterval: 30 * 60 * 1000,
    enabled: typeof window !== 'undefined' && authService.isAuthenticated(),
    retry: (failureCount, error: any) => {
      // Don't retry on 401 errors - user is not authenticated
      if (error?.message?.includes('401') || error?.message?.includes('Unauthorized')) {
        return false;
      }
      return failureCount < 2;
    },
  });

  // Use the actual session timeout from settings, or default if still loading
  const sessionTimeoutMinutes = sessionRuntimeSettings?.sessionTimeoutMinutes || DEFAULT_SESSION_TIMEOUT;
  const shouldInitializeTimers = !isLoadingSettings || sessionRuntimeSettings !== undefined;
  const sessionTimeoutMs = sessionTimeoutMinutes * 60 * 1000;
  const warningTimeMs = WARNING_TIME * 1000;

  const resetActivity = useCallback(() => {
    const now = Date.now();
    setSessionState(prev => ({
      ...prev,
      lastActivity: now,
      showWarning: false,
      sessionTimeoutMinutes: sessionTimeoutMinutes, // Update state with current timeout
    }));

    // Clear existing timers
    if (timeoutRef.current) clearTimeout(timeoutRef.current);
    if (warningTimeoutRef.current) clearTimeout(warningTimeoutRef.current);
    if (activityTimeoutRef.current) clearTimeout(activityTimeoutRef.current);

    // Don't initialize timers until we have the actual settings loaded
    if (!shouldInitializeTimers) {
      return;
    }

    // Set warning timer (show warning 2 minutes before timeout)
    warningTimeoutRef.current = setTimeout(() => {
      setSessionState(prev => ({
        ...prev,
        showWarning: true,
        remainingSeconds: WARNING_TIME,
      }));

      // Set final timeout timer
      timeoutRef.current = setTimeout(() => {
        handleSessionTimeout();
      }, warningTimeMs);

    }, sessionTimeoutMs - warningTimeMs);

  }, [sessionTimeoutMinutes, sessionTimeoutMs, warningTimeMs, shouldInitializeTimers]);

  const handleSessionTimeout = useCallback(async () => {
    // Clear all timers
    if (timeoutRef.current) clearTimeout(timeoutRef.current);
    if (warningTimeoutRef.current) clearTimeout(warningTimeoutRef.current);
    if (activityTimeoutRef.current) clearTimeout(activityTimeoutRef.current);

    // Update state immediately
    setSessionState(prev => ({
      ...prev,
      isActive: false,
      showWarning: false,
    }));

    // Call proper logout which handles backend cleanup
    try {
      await authService.logout();
    } catch (error) {
      console.warn('Backend logout failed during session timeout cleanup:', error);
      // Still clear tokens locally even if backend fails
      tokenRefreshService.clearTokens();
    }

    // Redirect to login
    window.location.href = '/login';
  }, []);

  const extendSession = useCallback(async () => {
    try {
      // Try to refresh the token to extend the session
      if (tokenRefreshService.hasRefreshToken()) {
        await tokenRefreshService.refreshToken();
      }
      
      // Reset activity timers
      resetActivity();
    } catch (error) {
      console.error('Failed to extend session:', error);
      handleSessionTimeout();
    }
  }, [resetActivity, handleSessionTimeout]);

  const logout = useCallback(async () => {
    await handleSessionTimeout();
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

    // Initial activity reset
    resetActivity();

    return () => {
      // Cleanup event listeners
      events.forEach(event => {
        document.removeEventListener(event, handleActivity);
      });

      // Clear timers
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
      if (warningTimeoutRef.current) clearTimeout(warningTimeoutRef.current);
      if (activityTimeoutRef.current) clearTimeout(activityTimeoutRef.current);
    };
  }, [handleActivity, resetActivity]);

  // Initialize session timeout when security settings are first loaded
  useEffect(() => {
    // Only initialize if we have settings and haven't initialized yet
    if (sessionRuntimeSettings?.sessionTimeoutMinutes && shouldInitializeTimers) {
      setSessionState(prev => ({
        ...prev,
        sessionTimeoutMinutes: sessionRuntimeSettings.sessionTimeoutMinutes,
      }));
      
      // Initialize timers with the correct timeout
      resetActivity();
    }
  }, [sessionRuntimeSettings?.sessionTimeoutMinutes, shouldInitializeTimers, resetActivity]);

  // Update session timeout when security settings change (after initial load)
  useEffect(() => {
    if (sessionRuntimeSettings?.sessionTimeoutMinutes && !isLoadingSettings) {
      setSessionState(prev => ({
        ...prev,
        sessionTimeoutMinutes: sessionRuntimeSettings.sessionTimeoutMinutes,
      }));
      
      // Reset activity to apply new timeout
      resetActivity();
    }
  }, [sessionRuntimeSettings?.sessionTimeoutMinutes, isLoadingSettings, resetActivity]);

  // Update remaining seconds countdown when warning is shown
  useEffect(() => {
    if (!sessionState.showWarning) return;

    let remainingTime = WARNING_TIME;

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

    const countdownInterval: ReturnType<typeof setInterval> = setInterval(updateCountdown, 1000);

    return () => {
      if (countdownInterval) {
        clearInterval(countdownInterval);
      }
    };
  }, [sessionState.showWarning]);

  return {
    sessionState,
    extendSession,
    logout,
    resetActivity,
  };
}
