'use client';

import { createContext, useContext, ReactNode } from 'react';
import { useSessionTimeout, SessionTimeoutState } from '../hooks/use-session-timeout';
import { SessionTimeoutDialog } from '../components/session/session-timeout-dialog';
import { authService } from '../services/auth';

interface SessionTimeoutContextType {
  sessionState: SessionTimeoutState;
  extendSession: () => Promise<void>;
  logout: () => Promise<void>;
  resetActivity: () => void;
}

const SessionTimeoutContext = createContext<SessionTimeoutContextType | undefined>(undefined);

interface SessionTimeoutProviderProps {
  children: ReactNode;
  enabled?: boolean;
}

export function SessionTimeoutProvider({ children, enabled = true }: SessionTimeoutProviderProps) {
  const isAuthenticated = typeof window !== 'undefined' ? authService.isAuthenticated() : false;
  const { sessionState, extendSession, logout, resetActivity } = useSessionTimeout();

  const contextValue: SessionTimeoutContextType = {
    sessionState,
    extendSession,
    logout,
    resetActivity,
  };

  // Don't enable session timeout if not authenticated or explicitly disabled
  if (!enabled || !isAuthenticated) {
    return <>{children}</>;
  }

  return (
    <SessionTimeoutContext.Provider value={contextValue}>
      {children}
      
      {/* Session timeout warning dialog */}
      <SessionTimeoutDialog
        isOpen={sessionState.showWarning}
        remainingSeconds={sessionState.remainingSeconds}
        onExtendSession={extendSession}
        onLogout={logout}
      />
    </SessionTimeoutContext.Provider>
  );
}

export function useSessionTimeoutContext(): SessionTimeoutContextType {
  const context = useContext(SessionTimeoutContext);
  
  if (context === undefined) {
    throw new Error('useSessionTimeoutContext must be used within a SessionTimeoutProvider');
  }
  
  return context;
}

// Optional hook for components that might not have the context available
export function useOptionalSessionTimeoutContext(): SessionTimeoutContextType | undefined {
  return useContext(SessionTimeoutContext);
}