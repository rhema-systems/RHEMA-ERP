'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import Link from 'next/link';
import { 
  Search, 
  Settings, 
  ChevronDown,
} from 'lucide-react';

import { Button } from '../ui/button';
import { Input } from '../ui/input';
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
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
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
    logout();
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
      'sticky top-0 z-50 w-full border-b border-slate-200/50 bg-white/95 backdrop-blur supports-[backdrop-filter]:bg-white/60 dark:border-neutral-700/70 dark:bg-neutral-900/95 dark:supports-[backdrop-filter]:bg-neutral-900/80',
      className
    )}>
      <div className="flex min-h-16 w-full min-w-0 flex-wrap items-center gap-x-3 gap-y-2 px-3 py-2 sm:flex-nowrap sm:px-6">
        {/* Enhanced Search */}
        <div className="order-last flex min-w-0 basis-full items-center space-x-4 sm:order-none sm:basis-auto sm:flex-1">
          <div className="relative w-full max-w-sm">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500 dark:text-slate-400" />
            <Input
              placeholder="Search modules and functions..."
              value={searchQuery}
              onChange={(e) => {
                setSearchQuery(e.target.value);
                setIsSearchOpen(e.target.value.length > 0);
              }}
              onFocus={() => setIsSearchOpen(searchQuery.length > 0)}
              className="border-slate-200/50 bg-slate-50/50 pl-10 focus:bg-white dark:border-neutral-700 dark:bg-neutral-800/80 dark:focus:bg-neutral-800"
            />
            
            {/* Search Suggestions - HIDDEN */}
            {/* {isSearchOpen && searchQuery && (
              <div className="absolute top-full left-0 right-0 mt-2 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl shadow-lg z-50 max-h-80 overflow-y-auto">
                {/* Quick Actions */}
                {/* <div className="p-2 border-b border-slate-100 dark:border-slate-800">
                  <div className="text-xs font-medium text-slate-500 dark:text-slate-400 px-3 py-1">Quick Actions</div>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="w-full justify-start h-9 px-3 text-sm rounded-lg"
                    onClick={() => {
                      setIsSearchOpen(false);
                      setSearchQuery('');
                      window.location.href = '/reports';
                    }}
                  >
                    <BarChart3 className="h-4 w-4 mr-3 text-slate-500" />
                    Browse All Reports
                  </Button>
                  
                  {user?.roles?.some(role => ['admin', 'SuperAdmin', 'TenantAdmin'].includes(role)) && (
                    <Button
                      variant="ghost"
                      size="sm"
                      className="w-full justify-start h-9 px-3 text-sm rounded-lg"
                      onClick={() => {
                        setIsSearchOpen(false);
                        setSearchQuery('');
                        window.location.href = '/administration/reports';
                      }}
                    >
                      <Plus className="h-4 w-4 mr-3 text-slate-500" />
                      Create New Report
                    </Button>
                  )}
                </div>
                
                {/* Report Modules */}
                {/* <div className="p-2">
                  <div className="text-xs font-medium text-slate-500 dark:text-slate-400 px-3 py-1">Report Modules</div>
                  {['Financial', 'Sales', 'HR', 'Inventory', 'Operations'].filter(module => 
                    module.toLowerCase().includes(searchQuery.toLowerCase())
                  ).map((module) => (
                    <Button
                      key={module}
                      variant="ghost"
                      size="sm"
                      className="w-full justify-start h-9 px-3 text-sm rounded-lg"
                      onClick={() => {
                        setIsSearchOpen(false);
                        setSearchQuery('');
                        window.location.href = `/reports?module=${module.toLowerCase()}`;
                      }}
                    >
                      <FileText className="h-4 w-4 mr-3 text-slate-500" />
                      {module} Reports
                    </Button>
                  ))}
                </div>
              </div>
            )} */}
          </div>
        </div>

        <div className="ml-auto flex shrink-0 items-center gap-3">
          {/* Current Tenant */}
          <ClientOnly>
            {currentTenant && (
              <div className="hidden items-center rounded-xl border border-slate-200/50 bg-slate-50/80 px-3 py-1.5 dark:border-neutral-700 dark:bg-neutral-800/80 lg:flex">
                <div className="flex items-center space-x-2">
                  <div className="w-2 h-2 bg-green-500 rounded-full animate-pulse" />
                  <div className="text-sm">
                    <span className="font-medium text-slate-700 dark:text-slate-300">{currentTenant.name}</span>
                    <span className="text-xs text-slate-500 dark:text-slate-400 ml-1">({currentTenant.code})</span>
                  </div>
                </div>
              </div>
            )}
          </ClientOnly>

          {/* Actions */}
          <div className="flex items-center space-x-1 sm:space-x-3">
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
                className="h-9 w-9 rounded-xl p-0 hover:bg-slate-100 dark:hover:bg-neutral-800"
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
              className="flex h-9 items-center space-x-0 rounded-xl bg-slate-50 px-1.5 hover:bg-slate-100 dark:bg-neutral-800 dark:hover:bg-neutral-700 sm:space-x-2 sm:px-3"
            >
              {/* Avatar */}
              <div className="flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-r from-blue-600 to-indigo-600 text-xs font-semibold text-white">
                {getInitials(userDisplayName) || visibleUser?.username?.[0]?.toUpperCase() || 'U'}
              </div>
              
              {/* User Info */}
              <div className="hidden sm:block text-left">
                <div className="text-sm font-medium text-slate-900 dark:text-white">
                  {visibleUser?.firstName && visibleUser?.lastName
                    ? `${visibleUser.firstName} ${visibleUser.lastName}`
                    : visibleUser?.username || 'User'}
                </div>
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

      {/* Click outside to close menus */}
      {isSearchOpen && (
        <div
          className="fixed inset-0 z-40"
          onClick={() => {
            setIsSearchOpen(false);
          }}
        />
      )}
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
