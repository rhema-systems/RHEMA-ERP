'use client';

import { ReactNode, useState } from 'react';
import { Sidebar } from './sidebar';
import { Header } from './header';
import { SessionTimeoutProvider } from '../../contexts/session-timeout-context';
import { authService } from '../../services/auth';
import { EnvironmentBanner } from '../environment/EnvironmentBanner';

interface DashboardLayoutProps {
  children: ReactNode;
  defaultSidebarCollapsed?: boolean;
}

export function DashboardLayout({ children, defaultSidebarCollapsed = false }: DashboardLayoutProps) {
  const [accountSidebarContainer, setAccountSidebarContainer] = useState<HTMLDivElement | null>(null);
  const isAuthenticated = typeof window !== 'undefined' ? authService.isAuthenticated() : false;
  
  return (
    <div
      data-interface-style="immersive"
      className="flex h-screen flex-col bg-[radial-gradient(circle_at_top_right,rgba(96,165,250,0.12),transparent_34%),linear-gradient(145deg,#f4f8ff,#f8fafc_45%,#effaf7)] transition-colors dark:bg-[radial-gradient(circle_at_top_right,rgba(37,99,235,0.14),transparent_34%),linear-gradient(145deg,#101010,#151515_48%,#111b18)]"
    >
      <EnvironmentBanner />
      <div className="flex min-h-0 flex-1">
        {/* Sidebar */}
        <Sidebar defaultCollapsed={defaultSidebarCollapsed} />

        {/* Main Content */}
        <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
          {/* Header */}
          <SessionTimeoutProvider enabled={isAuthenticated}>
            <Header accountSidebarContainer={accountSidebarContainer} />
          </SessionTimeoutProvider>

          {/* Page Content */}
          <div className="flex min-h-0 min-w-0 flex-1 flex-col lg:flex-row">
            <main className="min-h-0 min-w-0 flex-1 overflow-auto">
              <div className="w-full max-w-none p-3 sm:p-4 lg:p-[18px]">
                {children}
              </div>
            </main>
            <div ref={setAccountSidebarContainer} className="contents" />
          </div>
        </div>
      </div>
    </div>
  );
}
