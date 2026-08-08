'use client';

import { ReactNode } from 'react';
import { Sidebar } from './sidebar';
import { Header } from './header';
import { SessionTimeoutProvider } from '../../contexts/session-timeout-context';
import { authService } from '../../services/auth';

interface DashboardLayoutProps {
  children: ReactNode;
  defaultSidebarCollapsed?: boolean;
}

export function DashboardLayout({ children, defaultSidebarCollapsed = false }: DashboardLayoutProps) {
  const isAuthenticated = typeof window !== 'undefined' ? authService.isAuthenticated() : false;
  
  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-blue-50/30 to-indigo-50/30 dark:from-[#101010] dark:via-[#151515] dark:to-[#181818]">
      <div className="flex h-screen">
        {/* Sidebar */}
        <Sidebar defaultCollapsed={defaultSidebarCollapsed} />

        {/* Main Content */}
        <div className="flex flex-1 flex-col overflow-hidden">
          {/* Header */}
          <SessionTimeoutProvider enabled={isAuthenticated}>
            <Header />
          </SessionTimeoutProvider>

          {/* Page Content */}
          <main className="flex-1 overflow-y-auto">
            <div className="w-full max-w-none p-6 space-y-6">
              {children}
            </div>
          </main>
        </div>
      </div>
    </div>
  );
}
