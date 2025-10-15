'use client';

import { ReactNode } from 'react';
import { Sidebar } from './sidebar';
import { Header } from './header';
import { SessionTimeoutProvider } from '../../contexts/session-timeout-context';
import { authService } from '../../services/auth';
import { useAuth } from '../../hooks/use-auth';

interface DebugLayoutProps {
  children: ReactNode;
}

export function DebugLayout({ children }: DebugLayoutProps) {
  const { user, isAuthenticated } = useAuth();
  const isAuthenticatedClient = typeof window !== 'undefined' ? authService.isAuthenticated() : false;
  
  console.log('DebugLayout rendering with:', {
    user,
    isAuthenticated,
    isAuthenticatedClient,
    hasWindow: typeof window !== 'undefined'
  });
  
  return (
    <SessionTimeoutProvider enabled={isAuthenticatedClient}>
      <div className="min-h-screen bg-gradient-to-br from-slate-50 via-blue-50/30 to-indigo-50/30 dark:from-slate-950 dark:via-slate-900 dark:to-slate-900">
        
        {/* Debug Info Panel */}
        <div className="fixed top-0 right-0 z-[9999] bg-red-500 text-white p-2 text-xs">
          Debug: Auth={isAuthenticated.toString()}, User={user?.username || 'none'}
        </div>
        
        <div className="flex h-screen">
          {/* Sidebar with debug styling */}
          <div className="bg-red-200 border-4 border-red-500">
            <Sidebar />
          </div>

          {/* Main Content */}
          <div className="flex flex-1 flex-col overflow-hidden bg-blue-200">
            {/* Header with debug styling */}
            <div className="bg-green-200 border-4 border-green-500">
              <Header />
            </div>

            {/* Page Content */}
            <main className="flex-1 overflow-y-auto bg-yellow-200">
              <div className="container mx-auto p-6 space-y-6 border-4 border-yellow-500">
                <div className="bg-white p-4 border-2 border-purple-500">
                  <h1 className="text-lg font-bold">Debug Info:</h1>
                  <ul className="text-sm">
                    <li>User: {user?.username || 'Not loaded'}</li>
                    <li>Authenticated: {isAuthenticated.toString()}</li>
                    <li>Client Auth: {isAuthenticatedClient.toString()}</li>
                    <li>Has Window: {(typeof window !== 'undefined').toString()}</li>
                  </ul>
                </div>
                {children}
              </div>
            </main>
          </div>
        </div>
      </div>
    </SessionTimeoutProvider>
  );
}