'use client';

import React, { createContext, useContext, useState, useCallback, useEffect } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import { authService } from '../services/auth';
import { useQueryClient } from '@tanstack/react-query';
import { buildLoginRedirectUrl, getCurrentRelativeUrl } from '../lib/auth-redirect';

interface SessionBlacklistContextType {
  isSessionTerminated: boolean;
  showSessionTerminatedAlert: () => void;
  hideSessionTerminatedAlert: () => void;
}

const SessionBlacklistContext = createContext<SessionBlacklistContextType | undefined>(undefined);

interface SessionTerminatedAlertProps {
  isVisible: boolean;
  onClose: () => void;
}

function SessionTerminatedAlert({ isVisible, onClose }: SessionTerminatedAlertProps) {
  const [countdown, setCountdown] = useState(10);

  useEffect(() => {
    if (!isVisible) {
      setCountdown(10);
      return;
    }

    const timer = setInterval(() => {
      setCountdown((prev) => {
        if (prev <= 1) {
          return 0;
        }
        return prev - 1;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [isVisible]);

  if (!isVisible) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50">
      <div className="bg-white rounded-lg shadow-xl p-6 max-w-md w-full mx-4">
        <div className="flex items-center justify-center w-12 h-12 mx-auto mb-4 bg-red-100 rounded-full">
          <svg
            className="w-6 h-6 text-red-600"
            fill="none"
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth="2"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.732-.833-2.502 0L4.732 18.5c-.77.833.192 2.5 1.732 2.5z" />
          </svg>
        </div>
        
        <div className="text-center">
          <h3 className="text-lg font-semibold text-gray-900 mb-2">
            Session Terminated
          </h3>
          <p className="text-gray-600 mb-6">
            Your session has been terminated because you logged in from another device. 
            <br /><br />
            <strong>What happened?</strong> For security reasons, only one active session is allowed per user. 
            <br /><br />
            You will be automatically redirected to the login page where you can sign in again.
          </p>
          
          <div className="flex flex-col items-center justify-center mb-4">
            <div className="flex items-center justify-center mb-2">
              <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-blue-600"></div>
              <span className="ml-2 text-sm text-gray-500">Redirecting...</span>
            </div>
            <div className="text-center">
              <span className="inline-flex items-center justify-center w-8 h-8 bg-blue-100 text-blue-800 rounded-full text-sm font-semibold">
                {countdown}
              </span>
              <p className="text-xs text-gray-500 mt-1">
                second{countdown !== 1 ? 's' : ''} remaining
              </p>
            </div>
          </div>
          
          <button
            onClick={onClose}
            className="w-full bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 transition-colors"
          >
            Go to Login Now
          </button>
        </div>
      </div>
    </div>
  );
}

interface SessionBlacklistProviderProps {
  children: React.ReactNode;
}

export function SessionBlacklistProvider({ children }: SessionBlacklistProviderProps) {
  const [isSessionTerminated, setIsSessionTerminated] = useState(false);
  const [hasShownAlert, setHasShownAlert] = useState(false);
  const router = useRouter();
  const pathname = usePathname();
  const queryClient = useQueryClient();

  const handleRedirectToLogin = useCallback(() => {
    // Hide alert and reset state
    setIsSessionTerminated(false);
    setHasShownAlert(false);
    
    // Redirect to login
    router.push(buildLoginRedirectUrl(getCurrentRelativeUrl()));
  }, [router]);

  const showSessionTerminatedAlert = useCallback(() => {
    // Prevent multiple alerts from showing
    if (hasShownAlert) {
      return;
    }
    
    setHasShownAlert(true);
    
    // Clear tokens immediately to prevent any further API calls with blacklisted token
    authService.clearTokens();
    queryClient.clear();
    
    // Small delay to allow any in-flight requests to complete
    setTimeout(() => {
      setIsSessionTerminated(true);
      
      // Auto redirect after 10 seconds
      const timer = setTimeout(() => {
        handleRedirectToLogin();
      }, 10000);

      return () => clearTimeout(timer);
    }, 100);
  }, [handleRedirectToLogin, queryClient, hasShownAlert]);

  // Listen for session blacklist events from the API service
  useEffect(() => {
    const handleSessionBlacklisted = (event: CustomEvent) => {
      console.log('Session blacklisted event received:', event.detail);
      
      // Don't show the alert if user is already on the login page
      if (pathname === '/login' || pathname === '/') {
        console.log('Ignoring session blacklist event - user is on login page');
        return;
      }
      
      showSessionTerminatedAlert();
    };

    if (typeof window !== 'undefined') {
      window.addEventListener('session-blacklisted', handleSessionBlacklisted as EventListener);
    }

    return () => {
      if (typeof window !== 'undefined') {
        window.removeEventListener('session-blacklisted', handleSessionBlacklisted as EventListener);
      }
    };
  }, [showSessionTerminatedAlert, pathname]);

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    const handleAuthStorageChange = (event: StorageEvent) => {
      if (!['authToken', 'token', 'refreshToken', 'user'].includes(event.key ?? '')) {
        return;
      }

      if (authService.isAuthenticated()) {
        setIsSessionTerminated(false);
        setHasShownAlert(false);
        return;
      }

      authService.clearTokens();
      queryClient.clear();

      if (pathname !== '/login' && pathname !== '/') {
        router.push(buildLoginRedirectUrl(getCurrentRelativeUrl()));
      }
    };

    window.addEventListener('storage', handleAuthStorageChange);

    return () => {
      window.removeEventListener('storage', handleAuthStorageChange);
    };
  }, [pathname, queryClient, router]);

  const hideSessionTerminatedAlert = useCallback(() => {
    handleRedirectToLogin();
  }, [handleRedirectToLogin]);

  const contextValue: SessionBlacklistContextType = {
    isSessionTerminated,
    showSessionTerminatedAlert,
    hideSessionTerminatedAlert,
  };

  return (
    <SessionBlacklistContext.Provider value={contextValue}>
      {children}
      <SessionTerminatedAlert 
        isVisible={isSessionTerminated} 
        onClose={hideSessionTerminatedAlert}
      />
    </SessionBlacklistContext.Provider>
  );
}

export function useSessionBlacklist() {
  const context = useContext(SessionBlacklistContext);
  if (!context) {
    throw new Error('useSessionBlacklist must be used within a SessionBlacklistProvider');
  }
  return context;
}
