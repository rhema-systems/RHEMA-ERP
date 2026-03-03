'use client';

import { useEffect, useState, useCallback, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import { settingsService } from '../services/settings';
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
  const { data: securitySettings, isLoading: isLoadingSettings } = useQuery({
    queryKey: ['securitySettings'],
    queryFn: () => settingsService.getSecuritySettings(),
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

  // Use the actual session timeout from settings, or default if still loading
  const sessionTimeoutMinutes = securitySettings?.sessionTimeoutMinutes || DEFAULT_SESSION_TIMEOUT;
  const shouldInitializeTimers = !isLoadingSettings || securitySettings?.sessionTimeoutMinutes;
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
      console.log('⏳ Waiting for security settings before initializing session timeout...');
      return;
    }

    console.log(`🔄 Activity reset - session timeout in ${sessionTimeoutMinutes} minutes`);

    // Set warning timer (show warning 2 minutes before timeout)
    warningTimeoutRef.current = setTimeout(() => {
      console.log('⚠️ Showing session timeout warning');
      setSessionState(prev => ({
        ...prev,
        showWarning: true,
        remainingSeconds: WARNING_TIME,
      }));

      // Set final timeout timer
      timeoutRef.current = setTimeout(() => {
        console.log('⏰ Session timeout - logging out');
        handleSessionTimeout();
      }, warningTimeMs);

    }, sessionTimeoutMs - warningTimeMs);

  }, [sessionTimeoutMinutes, sessionTimeoutMs, warningTimeMs, shouldInitializeTimers]);

  const handleSessionTimeout = useCallback(async () => {
    console.log('🚪 Session expired - calling backend logout and clearing tokens');
    
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
      console.log('✅ Backend logout successful');
    } catch (error) {
      console.warn('⚠️ Backend logout failed, but continuing with cleanup:', error);
      // Still clear tokens locally even if backend fails
      tokenRefreshService.clearTokens();
    }

    // Redirect to login
    window.location.href = '/login';
  }, []);

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
    if (securitySettings?.sessionTimeoutMinutes && shouldInitializeTimers) {
      console.log(`\u2699\ufe0f Security settings loaded - session timeout: ${securitySettings.sessionTimeoutMinutes} minutes`);
      setSessionState(prev => ({
        ...prev,
        sessionTimeoutMinutes: securitySettings.sessionTimeoutMinutes,
      }));
      
      // Initialize timers with the correct timeout
      resetActivity();
    }
  }, [securitySettings?.sessionTimeoutMinutes, shouldInitializeTimers, resetActivity]);

  // Update session timeout when security settings change (after initial load)
  useEffect(() => {
    if (securitySettings?.sessionTimeoutMinutes && !isLoadingSettings) {
      console.log(`\ud83d\udd04 Security settings updated - new session timeout: ${securitySettings.sessionTimeoutMinutes} minutes`);
      setSessionState(prev => ({
        ...prev,
        sessionTimeoutMinutes: securitySettings.sessionTimeoutMinutes,
      }));
      
      // Reset activity to apply new timeout
      resetActivity();
    }
  }, [securitySettings?.sessionTimeoutMinutes, isLoadingSettings, resetActivity]);

  // Update remaining seconds countdown when warning is shown
  useEffect(() => {
    if (!sessionState.showWarning) return;

    const countdownInterval;
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

    countdownInterval = setInterval(updateCountdown, 1000);

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