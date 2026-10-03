'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import Link from 'next/link';
import { 
  Building2,
  Settings, 
  ChevronDown,
} from 'lucide-react';

import { Button } from '../ui/button';
import { GlobalSearch } from './GlobalSearch';
import { cn, getInitials } from '../../lib/utils';
import { useAuth } from '../../hooks/use-auth';
import { useTenant } from '../../contexts/TenantContext';
import { ClientOnly } from '../ClientOnly';
import HeaderNotificationBell from '../notifications/HeaderNotificationBell';
import { AccountSidebar } from './AccountSidebar';
import { FontSizeToggle } from './FontSizeToggle';
import { settingsNavigationItems } from './sidebar';
import { hasAnyAccessibleSettings } from '../settings/settings-access';

interface HeaderProps {
  className?: string;
  accountSidebarContainer?: HTMLElement | null;
}

export function Header({ className, accountSidebarContainer }: HeaderProps) {
  const accountButtonRef = useRef<HTMLButtonElement>(null);
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const [mounted, setMounted] = useState(false);
  const { user, logout, isLoggingOut, hasAnyRole, hasAnyPermission } = useAuth();
  const { currentTenant, currentTenantCode, setCurrentTenantCode } = useTenant();

  // Sync tenant code when user logs in
  useEffect(() => {
    if (user && typeof window !== 'undefined') {
      const storedTenantCode = localStorage.getItem('currentTenantCode');
      if (storedTenantCode && storedTenantCode !== currentTenantCode) {
        setCurrentTenantCode(storedTenantCode);
      }
    }
  }, [user, currentTenantCode, setCurrentTenantCode]);

  useEffect(() => {
    setMounted(true);
  }, []);

  const closeAccountSidebar = useCallback(() => {
    setIsUserMenuOpen(false);
    accountButtonRef.current?.focus();
  }, []);

  const handleLogout = () => {
    logout('logout');
    closeAccountSidebar();
  };

  // Local storage and a warm query cache are unavailable to the server. Keep
  // the first browser render identical, then reveal the authenticated header.
  const visibleUser = mounted ? user : undefined;
  const userDisplayName = [visibleUser?.firstName, visibleUser?.lastName].filter(Boolean).join(' ').trim() || visibleUser?.username || 'User';
  const canOpenSettings = mounted && hasAnyAccessibleSettings(settingsNavigationItems, {
    hasAnyRole,
    hasAnyPermission,
  });

  return (
    <>
    <header className={cn(
      'sticky top-0 z-50 w-full border-b border-blue-100/80 bg-white/82 shadow-[0_8px_30px_-24px_rgba(37,99,235,0.5)] backdrop-blur-xl transition-all supports-[backdrop-filter]:bg-white/72 dark:border-neutral-700/70 dark:bg-neutral-900/88',
      className
    )}>
      <div className="flex min-h-16 w-full min-w-0 flex-wrap items-center gap-x-3 gap-y-2 px-3 py-2 sm:flex-nowrap sm:px-5">
        {/* Enhanced Search */}
        <div className="order-last flex min-w-0 basis-full items-center space-x-4 sm:order-none sm:basis-auto sm:flex-1">
          <GlobalSearch />
        </div>

        <div className="ml-auto flex shrink-0 items-center gap-3">
          {/* Current Tenant */}
          <ClientOnly>
            {currentTenant && (
              <div className="hidden h-10 items-center rounded-xl border border-slate-200/80 bg-white px-3 shadow-sm dark:border-neutral-700 dark:bg-neutral-800/90 lg:flex">
                <div className="flex items-center gap-2.5">
                  <span className="relative flex h-7 w-7 items-center justify-center rounded-lg bg-blue-50 text-blue-700 dark:bg-blue-950/60 dark:text-blue-300">
                    <Building2 className="h-3.5 w-3.5" />
                    <i className="absolute -right-0.5 -top-0.5 h-2 w-2 rounded-full border-2 border-white bg-emerald-500 dark:border-neutral-800" />
                  </span>
                  <div className="max-w-40 text-sm leading-tight">
                    <span className="block truncate text-[0.72rem] font-semibold text-slate-800 dark:text-slate-200">{currentTenant.name}</span>
                    <span className="block text-[0.58rem] uppercase tracking-wider text-slate-400">{currentTenant.code}</span>
                  </div>
                  <ChevronDown className="h-3.5 w-3.5 text-slate-400" />
                </div>
              </div>
            )}
          </ClientOnly>

          {/* Actions */}
          <div className="flex items-center gap-1.5">
          {/* Quick Reports Access - HIDDEN */}
          {/* <div className="hidden lg:flex items-center space-x-2">
            <Button
              variant="ghost"
              size="sm"
              className="h-9 px-3 rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800"
              onClick={() => window.location.href = '/reports'}
            >
              <BarChart3 className="h-4 w-4 mr-2 text-slate-600 dark:text-slate-400" />
              <span className="text-sm text-slate-700 dark:text-slate-300">Reports</span>
            </Button>
            
            {/* Admin Quick Access - only show for admin users */}
            {/* <ClientOnly>
              {user?.roles?.some(role => ['admin', 'SuperAdmin', 'TenantAdmin'].includes(role)) && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-9 px-3 rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800"
                  onClick={() => window.location.href = '/administration/reports'}
                >
                  <Shield className="h-4 w-4 mr-2 text-slate-600 dark:text-slate-400" />
                  <span className="text-sm text-slate-700 dark:text-slate-300">Admin</span>
                </Button>
              )}
            </ClientOnly>
          </div> */}

          {/* Notifications */}
          <FontSizeToggle />
          <HeaderNotificationBell />

          {/* Full settings catalogue — gated on the client-side user, so it renders only after mount */}
          <ClientOnly>
            {canOpenSettings && (
              <Button
                asChild
                variant="ghost"
                size="sm"
                className="h-9 w-9 rounded-full border border-transparent p-0 hover:border-slate-200 hover:bg-slate-50 dark:hover:border-neutral-700 dark:hover:bg-neutral-800"
              >
                <Link href="/settings" aria-label="Open settings" title="Settings">
                  <Settings className="h-4 w-4 text-slate-600 dark:text-slate-300" />
                </Link>
              </Button>
            )}
          </ClientOnly>

          {/* User Menu */}
          <div className="relative">
            <Button
              ref={accountButtonRef}
              variant="ghost"
              size="sm"
              onClick={() => isUserMenuOpen ? closeAccountSidebar() : setIsUserMenuOpen(true)}
              aria-label="Open account sidebar"
              aria-expanded={isUserMenuOpen}
              aria-controls={isUserMenuOpen ? 'account-sidebar' : undefined}
              className="flex h-10 items-center space-x-0 rounded-xl border border-slate-200/80 bg-white px-1.5 shadow-sm hover:bg-slate-50 dark:border-neutral-700 dark:bg-neutral-800 dark:hover:bg-neutral-700 sm:space-x-2 sm:px-2.5"
            >
              {/* Avatar */}
              <div className="flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-br from-blue-600 to-indigo-600 text-xs font-semibold text-white shadow-sm ring-2 ring-blue-100 dark:ring-blue-950">
                {getInitials(userDisplayName) || visibleUser?.username?.[0]?.toUpperCase() || 'U'}
              </div>
              
              {/* User Info */}
              <div className="hidden max-w-36 text-left sm:block">
                <div className="truncate text-[0.72rem] font-semibold leading-tight text-slate-900 dark:text-white">
                  {visibleUser?.firstName && visibleUser?.lastName
                    ? `${visibleUser.firstName} ${visibleUser.lastName}`
                    : visibleUser?.username || 'User'}
                </div>
                <div className="mt-0.5 truncate text-[0.58rem] leading-tight text-slate-400">{visibleUser?.roles?.[0] || 'Team member'}</div>
              </div>

              <ChevronDown className={cn(
                'hidden h-3 w-3 text-slate-500 transition-transform duration-200 sm:block',
                isUserMenuOpen && 'rotate-180'
              )} />
            </Button>

          </div>
          </div>
        </div>
      </div>

    </header>
    <AccountSidebar
      open={isUserMenuOpen}
      container={accountSidebarContainer}
      user={user}
      currentTenant={currentTenant}
      isLoggingOut={isLoggingOut}
      showSettingsLink={canOpenSettings}
      onClose={closeAccountSidebar}
      onLogout={handleLogout}
    />
    </>
  );
}
