'use client';

import { useEffect, useState, useCallback, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import { settingsService, type SessionRuntimeSettings } from '../services/settings';
import { tokenRefreshService } from '../services/token-refresh.service';
import { authService } from '../services/auth';
import { browserSessionCoordinator } from '../services/browser-session-coordinator';

export interface SessionTimeoutState {
  isActive: boolean;
  showWarning: boolean;
  remainingSeconds: number;
  sessionTimeoutMinutes: number;
  lastActivity: number;
}

const DEFAULT_SESSION_TIMEOUT = 30;
const WARNING_TIME = 120;
const ACTIVITY_BROADCAST_THROTTLE_MS = 15_000;

export interface SessionTiming {
  expired: boolean;
  showWarning: boolean;
  remainingSeconds: number;
}

export const calculateSessionTiming = (
  lastActivity: number,
  now: number,
  timeoutMs: number,
  warningSeconds = WARNING_TIME,
): SessionTiming => {
  const remainingMs = Math.max(0, lastActivity + timeoutMs - now);
  return {
    expired: remainingMs <= 0,
    showWarning: remainingMs > 0 && remainingMs <= warningSeconds * 1000,
    remainingSeconds: Math.max(0, Math.ceil(remainingMs / 1000)),
  };
};

export function useSessionTimeout() {
  const initialActivity = Date.now();
  const [sessionState, setSessionState] = useState<SessionTimeoutState>({
    isActive: true,
    showWarning: false,
    remainingSeconds: WARNING_TIME,
    sessionTimeoutMinutes: DEFAULT_SESSION_TIMEOUT,
    lastActivity: initialActivity,
  });

  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const lastActivityBroadcastRef = useRef(0);
  const logoutInProgressRef = useRef(false);

  const { data: sessionRuntimeSettings } = useQuery<SessionRuntimeSettings>({
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
      if (error?.message?.includes('401') || error?.message?.includes('Unauthorized')) return false;
      return failureCount < 2;
    },
  });

  const sessionTimeoutMinutes = sessionRuntimeSettings?.sessionTimeoutMinutes || DEFAULT_SESSION_TIMEOUT;
  const sessionTimeoutMs = sessionTimeoutMinutes * 60 * 1000;

  const clearTimer = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  }, []);

  const terminateSession = useCallback(async (reason: 'logout' | 'session-expired') => {
    if (logoutInProgressRef.current || typeof window === 'undefined') return;
    logoutInProgressRef.current = true;
    clearTimer();
    const sharedActivity = browserSessionCoordinator.getLastActivity(0);
    setSessionState(previous => ({
      ...previous,
      isActive: false,
      showWarning: false,
      remainingSeconds: 0,
      lastActivity: sharedActivity,
      sessionTimeoutMinutes,
    }));

    try {
      await authService.logout(reason);
    } catch (error) {
      console.warn('Backend logout failed during session cleanup:', error);
      tokenRefreshService.clearTokens();
      browserSessionCoordinator.publish(reason);
    }

    window.location.assign('/login');
  }, [clearTimer, sessionTimeoutMinutes]);

  const expireSession = useCallback(async () => {
    if (logoutInProgressRef.current || typeof window === 'undefined') return;

    // Re-read the shared timestamp immediately before logout. A background tab may have resumed
    // after its timer was throttled while another ERP tab continued to receive user activity.
    const sharedActivity = browserSessionCoordinator.getLastActivity(0);
    if (Date.now() < sharedActivity + sessionTimeoutMs) return;

    await terminateSession('session-expired');
  }, [sessionTimeoutMs, terminateSession]);

  const reconcileSession = useCallback((now = Date.now()) => {
    if (typeof window === 'undefined' || !authService.isAuthenticated()) return;

    clearTimer();
    const lastActivity = browserSessionCoordinator.getLastActivity(now);
    const timing = calculateSessionTiming(lastActivity, now, sessionTimeoutMs);

    setSessionState(previous => ({
      ...previous,
      isActive: !timing.expired,
      showWarning: timing.showWarning,
      remainingSeconds: timing.remainingSeconds,
      sessionTimeoutMinutes,
      lastActivity,
    }));

    if (timing.expired) {
      void expireSession();
      return;
    }

    // Timers only wake the calculation. Correctness comes from comparing timestamps whenever the
    // tab runs again, receives a cross-tab event, or becomes visible.
    const nextDelay = timing.showWarning
      ? Math.min(1000, timing.remainingSeconds * 1000)
      : Math.max(1, timing.remainingSeconds * 1000 - WARNING_TIME * 1000);
    timerRef.current = setTimeout(() => reconcileSession(), nextDelay);
  }, [clearTimer, expireSession, sessionTimeoutMinutes, sessionTimeoutMs]);

  const resetActivity = useCallback(() => {
    if (typeof window === 'undefined' || !authService.isAuthenticated()) return;
    const now = Date.now();
    const sharedActivity = browserSessionCoordinator.recordActivity(now);
    lastActivityBroadcastRef.current = sharedActivity;
    reconcileSession(now);
  }, [reconcileSession]);

  const handleActivity = useCallback(() => {
    const now = Date.now();
    if (now - lastActivityBroadcastRef.current < ACTIVITY_BROADCAST_THROTTLE_MS) return;
    resetActivity();
  }, [resetActivity]);

  const extendSession = useCallback(async () => {
    try {
      if (tokenRefreshService.hasRefreshToken()) {
        await tokenRefreshService.refreshToken();
      }
      resetActivity();
    } catch (error) {
      console.error('Failed to extend session:', error);
      await terminateSession('session-expired');
    }
  }, [resetActivity, terminateSession]);

  const logout = useCallback(async () => {
    await terminateSession('logout');
  }, [terminateSession]);

  useEffect(() => {
    if (typeof window === 'undefined' || !authService.isAuthenticated()) return;

    const events = ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart', 'click'];
    events.forEach(event => document.addEventListener(event, handleActivity, { passive: true }));

    const unsubscribe = browserSessionCoordinator.subscribe(event => {
      if (event.type === 'activity' || event.type === 'login' || event.type === 'token-refreshed') {
        logoutInProgressRef.current = false;
        reconcileSession();
        return;
      }

      clearTimer();
      setSessionState(previous => ({
        ...previous,
        isActive: false,
        showWarning: false,
        remainingSeconds: 0,
      }));
      if (window.location.pathname !== '/login') window.location.assign('/login');
    });

    const revalidateVisibleSession = () => {
      if (document.visibilityState === 'visible') reconcileSession();
    };
    document.addEventListener('visibilitychange', revalidateVisibleSession);
    window.addEventListener('focus', revalidateVisibleSession);

    // Mounting or reloading an authenticated ERP page is legitimate browser-session activity.
    resetActivity();
    const stopAutoRefresh = tokenRefreshService.setupAutoRefresh(
      () => reconcileSession(),
      () => void terminateSession('session-expired'),
    );

    return () => {
      events.forEach(event => document.removeEventListener(event, handleActivity));
      document.removeEventListener('visibilitychange', revalidateVisibleSession);
      window.removeEventListener('focus', revalidateVisibleSession);
      unsubscribe();
      stopAutoRefresh();
      clearTimer();
    };
  }, [clearTimer, handleActivity, reconcileSession, resetActivity, terminateSession]);

  useEffect(() => {
    reconcileSession();
  }, [sessionTimeoutMinutes, reconcileSession]);

  return { sessionState, extendSession, logout, resetActivity };
}
