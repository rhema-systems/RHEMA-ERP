'use client';

import { useState, useEffect } from 'react';
import { 
  Bell, 
  Search, 
  Settings, 
  User, 
  LogOut, 
  ChevronDown,
  Clock,
  BarChart3,
  Shield,
  Plus,
  Zap,
  FileText
} from 'lucide-react';

import { Button } from '../ui/button';
import { Input } from '../ui/input';
import { cn, getInitials } from '../../lib/utils';
import { useAuth } from '../../hooks/use-auth';
import { useTenant } from '../../contexts/TenantContext';
import { useOptionalSessionTimeoutContext } from '../../contexts/session-timeout-context';
import { ClientOnly } from '../ClientOnly';
import { AnimatedThemeToggle } from '../ui/ThemeToggle';
import { useTheme } from '../../contexts/ThemeContext';

interface HeaderProps {
  className?: string;
}

export function Header({ className }: HeaderProps) {
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [mounted, setMounted] = useState(false);
  const { user, logout, isLoggingOut } = useAuth();
  const { currentTenant, currentTenantCode, setCurrentTenantCode } = useTenant();
  const sessionTimeout = useOptionalSessionTimeoutContext();
  const { actualTheme, toggleTheme } = useTheme();

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
        {/* Enhanced Search */}
        <div className="flex flex-1 items-center space-x-4">
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
              className="pl-10 bg-slate-50/50 dark:bg-slate-800/50 border-slate-200/50 dark:border-slate-700/50 focus:bg-white dark:focus:bg-slate-800"
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

          {/* Theme Toggle (uses ThemeContext) */}
          <AnimatedThemeToggle size="sm" showLabel={false} />

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

                {/* Menu Items - Reports and Admin HIDDEN */}
                <div className="p-2">
                  {/* <Button
                    variant="ghost"
                    className="w-full justify-start h-10 px-3 text-sm font-medium rounded-xl"
                    onClick={() => {
                      setIsUserMenuOpen(false);
                      window.location.href = '/reports';
                    }}
                  >
                    <BarChart3 className="h-4 w-4 mr-3 text-slate-500" />
                    My Reports
                  </Button> */}
                  
                  {/* Admin Functions - only show for admin users */}
                  {/* <ClientOnly>
                    {user?.roles?.some(role => ['admin', 'SuperAdmin', 'TenantAdmin'].includes(role)) && (
                      <>
                        <Button
                          variant="ghost"
                          className="w-full justify-start h-10 px-3 text-sm font-medium rounded-xl"
                          onClick={() => {
                            setIsUserMenuOpen(false);
                            window.location.href = '/administration/reports';
                          }}
                        >
                          <Shield className="h-4 w-4 mr-3 text-slate-500" />
                          Reports Administration
                        </Button>
                        
                        <Button
                          variant="ghost"
                          className="w-full justify-start h-10 px-3 text-sm font-medium rounded-xl"
                          onClick={() => {
                            setIsUserMenuOpen(false);
                            window.location.href = '/administration';
                          }}
                        >
                          <Settings className="h-4 w-4 mr-3 text-slate-500" />
                          Administration
                        </Button>
                      </>
                    )}
                  </ClientOnly> */}
                  
                  <div className="border-t border-slate-100 dark:border-slate-800 my-2" />
                  
                  <Button
                    variant="ghost"
                    className="w-full justify-start h-10 px-3 text-sm font-medium rounded-xl"
                    onClick={() => {
                      setIsUserMenuOpen(false);
                      window.location.href = '/profile';
                    }}
                  >
                    <User className="h-4 w-4 mr-3 text-slate-500" />
                    Profile Settings
                  </Button>
                  
                  <Button
                    variant="ghost"
                    className="w-full justify-start h-10 px-3 text-sm font-medium rounded-xl"
                    onClick={() => {
                      setIsUserMenuOpen(false);
                      window.location.href = '/account';
                    }}
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

      {/* Click outside to close menus */}
      {(isUserMenuOpen || isSearchOpen) && (
        <div
          className="fixed inset-0 z-40"
          onClick={() => {
            setIsUserMenuOpen(false);
            setIsSearchOpen(false);
          }}
        />
      )}
    </header>
  );
}