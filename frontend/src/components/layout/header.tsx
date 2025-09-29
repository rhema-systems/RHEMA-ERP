'use client';

import { useState, useEffect } from 'react';
import { 
  Bell, 
  Search, 
  Settings, 
  User, 
  LogOut, 
  Moon, 
  Sun,
  ChevronDown,
  Clock 
} from 'lucide-react';

import { Button } from '../ui/button';
import { Input } from '../ui/input';
import { cn, getInitials } from '../../lib/utils';
import { useAuth } from '../../hooks/use-auth';
import { useTenant } from '../../contexts/TenantContext';
import { useOptionalSessionTimeoutContext } from '../../contexts/session-timeout-context';
import { ClientOnly } from '../ClientOnly';

interface HeaderProps {
  className?: string;
}

export function Header({ className }: HeaderProps) {
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const [isDarkMode, setIsDarkMode] = useState(false);
  const [mounted, setMounted] = useState(false);
  const { user, logout, isLoggingOut } = useAuth();
  const { currentTenant, currentTenantCode, setCurrentTenantCode } = useTenant();
  const sessionTimeout = useOptionalSessionTimeoutContext();

  // Sync tenant code when user logs in
  useEffect(() => {
    if (user && typeof window !== 'undefined') {
      const storedTenantCode = localStorage.getItem('currentTenantCode');
      if (storedTenantCode && storedTenantCode !== currentTenantCode) {
        setCurrentTenantCode(storedTenantCode);
      }
    }
  }, [user, currentTenantCode, setCurrentTenantCode]);

  // Debug logging
  useEffect(() => {
    if (currentTenant) {
      console.log('Header: Displaying tenant:', currentTenant.name);
    }
  }, [currentTenant]);

  useEffect(() => {
    setMounted(true);
  }, []);

  const toggleDarkMode = () => {
    if (!mounted) return;
    setIsDarkMode(!isDarkMode);
    // In a real app, this would update the theme context
    document.documentElement.classList.toggle('dark');
  };

  const handleLogout = () => {
    logout();
    setIsUserMenuOpen(false);
  };

  return (
    <header className={cn(
      'sticky top-0 z-50 w-full border-b border-slate-200/50 dark:border-slate-800/50 bg-white/95 dark:bg-slate-900/95 backdrop-blur supports-[backdrop-filter]:bg-white/60 dark:supports-[backdrop-filter]:bg-slate-900/60',
      className
    )}>
      <div className="container flex h-16 items-center justify-between px-6">
        {/* Search */}
        <div className="flex flex-1 items-center space-x-4">
          <div className="relative w-full max-w-sm">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500 dark:text-slate-400" />
            <Input
              placeholder="Search anything..."
              className="pl-10 bg-slate-50/50 dark:bg-slate-800/50 border-slate-200/50 dark:border-slate-700/50 focus:bg-white dark:focus:bg-slate-800"
            />
          </div>
        </div>

        {/* Current Tenant */}
        <ClientOnly>
          {currentTenant && (
            <div className="hidden md:flex items-center px-3 py-1.5 bg-slate-50/80 dark:bg-slate-800/80 rounded-xl border border-slate-200/50 dark:border-slate-700/50">
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
        <div className="flex items-center space-x-3">
          {/* Theme Toggle */}
          <Button
            variant="ghost"
            size="sm"
            onClick={toggleDarkMode}
            className="h-9 w-9 p-0 rounded-xl"
          >
            {isDarkMode ? (
              <Sun className="h-4 w-4 text-slate-600 dark:text-slate-400" />
            ) : (
              <Moon className="h-4 w-4 text-slate-600 dark:text-slate-400" />
            )}
          </Button>

          {/* Notifications */}
          <Button
            variant="ghost"
            size="sm"
            className="relative h-9 w-9 p-0 rounded-xl"
          >
            <Bell className="h-4 w-4 text-slate-600 dark:text-slate-400" />
            <span className="absolute -top-1 -right-1 h-3 w-3 bg-red-500 rounded-full border-2 border-white dark:border-slate-900" />
          </Button>

          {/* Session Status */}
          <ClientOnly>
            {sessionTimeout && (
              <div className="hidden sm:flex items-center px-3 py-1.5 bg-slate-50/80 dark:bg-slate-800/80 rounded-xl border border-slate-200/50 dark:border-slate-700/50">
                <Clock className="h-3 w-3 text-slate-500 mr-2" />
                <span className="text-xs font-medium text-slate-600 dark:text-slate-400">
                  Session: {Math.ceil((sessionTimeout.sessionState.sessionTimeoutMinutes || 30) - 
                  ((Date.now() - sessionTimeout.sessionState.lastActivity) / (1000 * 60)))}m
                </span>
              </div>
            )}
          </ClientOnly>

          {/* User Menu */}
          <div className="relative">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setIsUserMenuOpen(!isUserMenuOpen)}
              className="flex items-center space-x-2 h-9 px-3 rounded-xl bg-slate-50 dark:bg-slate-800 hover:bg-slate-100 dark:hover:bg-slate-700"
            >
              {/* Avatar */}
              <div className="flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-r from-blue-600 to-indigo-600 text-xs font-semibold text-white">
                {getInitials(user?.firstName, user?.lastName) || user?.username?.[0]?.toUpperCase() || 'U'}
              </div>
              
              {/* User Info */}
              <div className="hidden sm:block text-left">
                <div className="text-sm font-medium text-slate-900 dark:text-white">
                  {user?.firstName && user?.lastName 
                    ? `${user.firstName} ${user.lastName}` 
                    : user?.username || 'User'}
                </div>
              </div>

              <ChevronDown className={cn(
                'h-3 w-3 text-slate-500 transition-transform duration-200',
                isUserMenuOpen && 'rotate-180'
              )} />
            </Button>

            {/* Dropdown Menu */}
            {isUserMenuOpen && (
              <div className="absolute right-0 top-full mt-2 w-64 rounded-2xl border border-slate-200/50 dark:border-slate-800/50 bg-white/95 dark:bg-slate-900/95 backdrop-blur-xl shadow-xl ring-1 ring-black/5 dark:ring-white/5">
                {/* User Info */}
                <div className="p-4 border-b border-slate-100 dark:border-slate-800">
                  <div className="flex items-center space-x-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-r from-blue-600 to-indigo-600 text-sm font-semibold text-white">
                      {getInitials(user?.firstName, user?.lastName) || user?.username?.[0]?.toUpperCase() || 'U'}
                    </div>
                    <div>
                      <div className="text-sm font-medium text-slate-900 dark:text-white">
                        {user?.firstName && user?.lastName 
                          ? `${user.firstName} ${user.lastName}` 
                          : user?.username || 'User'}
                      </div>
                      <div className="text-xs text-slate-500 dark:text-slate-400">
                        {user?.email || 'user@example.com'}
                      </div>
                      <div className="mt-1">
                        {user?.roles?.map((role, index) => (
                          <span
                            key={role}
                            className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-blue-100 dark:bg-blue-900/30 text-blue-800 dark:text-blue-300 mr-1"
                          >
                            {role}
                          </span>
                        ))}
                      </div>
                    </div>
                  </div>
                </div>

                {/* Menu Items */}
                <div className="p-2">
                  <Button
                    variant="ghost"
                    className="w-full justify-start h-10 px-3 text-sm font-medium rounded-xl"
                    onClick={() => setIsUserMenuOpen(false)}
                  >
                    <User className="h-4 w-4 mr-3 text-slate-500" />
                    Profile Settings
                  </Button>
                  
                  <Button
                    variant="ghost"
                    className="w-full justify-start h-10 px-3 text-sm font-medium rounded-xl"
                    onClick={() => setIsUserMenuOpen(false)}
                  >
                    <Settings className="h-4 w-4 mr-3 text-slate-500" />
                    Account Settings
                  </Button>
                </div>

                <div className="border-t border-slate-100 dark:border-slate-800 p-2">
                  <Button
                    variant="ghost"
                    className="w-full justify-start h-10 px-3 text-sm font-medium rounded-xl text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20"
                    onClick={handleLogout}
                    disabled={isLoggingOut}
                  >
                    <LogOut className="h-4 w-4 mr-3" />
                    {isLoggingOut ? 'Signing Out...' : 'Sign Out'}
                  </Button>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Click outside to close menu */}
      {isUserMenuOpen && (
        <div
          className="fixed inset-0 z-40"
          onClick={() => setIsUserMenuOpen(false)}
        />
      )}
    </header>
  );
}