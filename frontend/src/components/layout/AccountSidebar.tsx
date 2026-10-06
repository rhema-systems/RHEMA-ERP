'use client';

import { useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import Link from 'next/link';
import {
  Building2,
  Check,
  LogOut,
  Monitor,
  Moon,
  Info,
  Settings,
  ShieldCheck,
  Sun,
  User as UserIcon,
  X,
} from 'lucide-react';

import type { Tenant, User } from '../../types';
import { useTheme } from '../../contexts/ThemeContext';
import { cn, getInitials } from '../../lib/utils';
import { Button } from '../ui/button';
import { EnvironmentBadge } from '../environment/EnvironmentBadge';
import { useApplicationEnvironment } from '../../contexts/ApplicationEnvironmentContext';

interface AccountSidebarProps {
  open: boolean;
  container?: HTMLElement | null;
  user?: User;
  currentTenant?: Tenant | null;
  isLoggingOut?: boolean;
  showSettingsLink?: boolean;
  onClose: () => void;
  onLogout: () => void;
}

const modes = [
  { value: 'light' as const, label: 'Day', icon: Sun },
  { value: 'dark' as const, label: 'Night', icon: Moon },
  { value: 'system' as const, label: 'Auto', icon: Monitor },
];

export function AccountSidebar({
  open,
  container,
  user,
  currentTenant,
  isLoggingOut = false,
  showSettingsLink = false,
  onClose,
  onLogout,
}: AccountSidebarProps) {
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const { theme, setTheme, actualTheme } = useTheme();
  const { environment } = useApplicationEnvironment();
  const displayName = [user?.firstName, user?.lastName].filter(Boolean).join(' ').trim()
    || user?.username
    || 'User';

  useEffect(() => {
    if (!open) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !event.defaultPrevented) onClose();
    };

    document.addEventListener('keydown', handleKeyDown);
    closeButtonRef.current?.focus();

    return () => {
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [onClose, open]);

  if (!open || typeof document === 'undefined') return null;

  const panel = (
      <aside
        id="account-sidebar"
        data-testid="account-sidebar"
        aria-labelledby="account-sidebar-title"
        className="order-first flex h-[min(36rem,60dvh)] min-h-0 w-full shrink-0 flex-col overflow-x-hidden overflow-y-auto border-b border-slate-200 bg-slate-50 dark:border-neutral-700 dark:bg-[#151515] lg:order-last lg:h-full lg:w-80 lg:border-b-0 lg:border-l"
      >
        <div className="relative shrink-0 bg-gradient-to-br from-blue-600 via-indigo-600 to-violet-600 p-4 text-white">
          <Button
            ref={closeButtonRef}
            type="button"
            variant="ghost"
            size="sm"
            aria-label="Close account sidebar"
            onClick={onClose}
            className="absolute right-2 top-2 h-8 w-8 rounded-full p-0 text-white hover:bg-white/15 hover:text-white"
          >
            <X className="h-5 w-5" />
          </Button>

          <div className="flex items-center gap-3 pr-7">
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full border-2 border-white/25 bg-white/15 text-base font-semibold">
              {getInitials(displayName) || user?.username?.[0]?.toUpperCase() || 'U'}
            </div>
            <div className="min-w-0">
              <h2 id="account-sidebar-title" className="truncate text-sm font-semibold" title={displayName}>
                {displayName}
              </h2>
              <p className="truncate text-xs text-white/85" title={user?.email || user?.username}>{user?.email || user?.username}</p>
              {user?.id && <p className="mt-1 truncate text-[11px] text-white/65" title={user.id}>User ID: {user.id}</p>}
            </div>
          </div>

          {currentTenant && (
            <div className="mt-3 flex items-center gap-2 rounded-lg border border-white/25 bg-white/10 px-2.5 py-2 text-xs">
              <Building2 className="h-4 w-4 shrink-0" />
              <span className="min-w-0 flex-1 truncate" title={currentTenant.name}>{currentTenant.name}</span>
              <span className="max-w-20 truncate rounded-md bg-white/15 px-2 py-0.5 text-xs" title={currentTenant.code}>{currentTenant.code}</span>
            </div>
          )}
        </div>

        <div className="min-h-0 flex-1 space-y-3 overflow-y-auto overscroll-contain p-3">
          <section className="rounded-xl border border-slate-200 bg-white p-3 shadow-sm dark:border-neutral-700 dark:bg-[#202020]">
            <div className="mb-3 flex items-center justify-between gap-3">
              <div>
                <h3 className="text-sm font-semibold text-slate-950 dark:text-neutral-100">Appearance</h3>
                <p className="text-xs text-slate-500 dark:text-neutral-400">
                  {actualTheme === 'dark' ? 'Dark charcoal theme is active' : 'Light theme is active'}
                </p>
              </div>
              <span className="rounded-full bg-slate-100 px-2.5 py-1 text-[11px] font-medium capitalize text-slate-600 dark:bg-neutral-800 dark:text-neutral-300">
                {theme}
              </span>
            </div>

            <div
              role="radiogroup"
              aria-label="Theme mode"
              className="grid grid-cols-3 gap-1 rounded-xl border border-slate-200 bg-slate-50 p-1 dark:border-neutral-600 dark:bg-[#171717]"
            >
              {modes.map(mode => {
                const Icon = mode.icon;
                const selected = theme === mode.value;
                return (
                  <button
                    key={mode.value}
                    type="button"
                    role="radio"
                    aria-checked={selected}
                    onClick={() => setTheme(mode.value)}
                    className={cn(
                      'flex h-9 items-center justify-center gap-1 rounded-lg text-xs font-medium transition-colors',
                      selected
                        ? 'bg-blue-600 text-white shadow-sm'
                        : 'text-slate-600 hover:bg-white hover:text-slate-950 dark:text-neutral-300 dark:hover:bg-neutral-800 dark:hover:text-white',
                    )}
                  >
                    <Icon className="h-4 w-4" />
                    {mode.label}
                    {selected && <Check className="h-3.5 w-3.5" />}
                  </button>
                );
              })}
            </div>
          </section>

          <section className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm dark:border-neutral-700 dark:bg-[#202020]">
            <div className="border-b border-slate-100 px-3 py-2.5 dark:border-neutral-700">
              <h3 className="text-sm font-semibold text-slate-950 dark:text-neutral-100">Account</h3>
            </div>
            <nav aria-label="Account shortcuts" className="p-2">
              <Link
                href="/profile"
                onClick={onClose}
                className="flex items-center gap-2 rounded-lg px-2 py-2.5 text-sm font-medium text-slate-700 transition-colors hover:bg-blue-50 hover:text-blue-700 dark:text-neutral-200 dark:hover:bg-neutral-800 dark:hover:text-white"
              >
                <UserIcon className="h-4 w-4 text-slate-500 dark:text-neutral-400" />
                Profile Settings
              </Link>
              <Link
                href="/account"
                onClick={onClose}
                className="flex items-center gap-2 rounded-lg px-2 py-2.5 text-sm font-medium text-slate-700 transition-colors hover:bg-blue-50 hover:text-blue-700 dark:text-neutral-200 dark:hover:bg-neutral-800 dark:hover:text-white"
              >
                <Settings className="h-4 w-4 text-slate-500 dark:text-neutral-400" />
                Account Settings
              </Link>
              {showSettingsLink && (
                <Link
                  href="/settings"
                  onClick={onClose}
                  className="flex items-center gap-2 rounded-lg px-2 py-2.5 text-sm font-medium text-slate-700 transition-colors hover:bg-blue-50 hover:text-blue-700 dark:text-neutral-200 dark:hover:bg-neutral-800 dark:hover:text-white"
                >
                  <Settings className="h-4 w-4 text-slate-500 dark:text-neutral-400" />
                  All Settings
                </Link>
              )}
            </nav>
          </section>

          {!!user?.roles?.length && (
            <section className="rounded-xl border border-slate-200 bg-white p-3 shadow-sm dark:border-neutral-700 dark:bg-[#202020]">
              <div className="mb-3 flex items-center gap-2">
                <ShieldCheck className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                <h3 className="text-sm font-semibold text-slate-950 dark:text-neutral-100">Access roles</h3>
              </div>
              <div className="flex flex-wrap gap-2">
                {user.roles.map(role => (
                  <span
                    key={role}
                    className="max-w-full break-words rounded-full bg-blue-50 px-2.5 py-1 text-xs font-medium text-blue-700 dark:bg-blue-950/50 dark:text-blue-300"
                  >
                    {role}
                  </span>
                ))}
              </div>
            </section>
          )}

          <section className="rounded-xl border border-slate-200 bg-white p-3 shadow-sm dark:border-neutral-700 dark:bg-[#202020]">
            <div className="mb-3 flex items-center gap-2">
              <Info className="h-4 w-4 text-blue-600 dark:text-blue-400" />
              <h3 className="text-sm font-semibold text-slate-950 dark:text-neutral-100">System context</h3>
            </div>
            <dl className="space-y-2 text-xs">
              <div className="flex items-center justify-between gap-3">
                <dt className="text-slate-500 dark:text-neutral-400">Environment</dt>
                <dd><EnvironmentBadge compact /></dd>
              </div>
              <div className="flex items-center justify-between gap-3">
                <dt className="text-slate-500 dark:text-neutral-400">Version</dt>
                <dd className="font-medium text-slate-800 dark:text-neutral-200">{environment.applicationVersion}</dd>
              </div>
              {environment.buildId && (
                <div className="flex items-center justify-between gap-3">
                  <dt className="text-slate-500 dark:text-neutral-400">Build</dt>
                  <dd className="font-mono font-medium text-slate-800 dark:text-neutral-200">{environment.buildId}</dd>
                </div>
              )}
            </dl>
            <p className="mt-3 text-[11px] leading-4 text-slate-500 dark:text-neutral-400">
              {environment.message}
            </p>
            {showSettingsLink && (
              <Link
                href="/administration/system"
                onClick={onClose}
                className="mt-3 inline-flex text-xs font-semibold text-blue-600 hover:text-blue-700 dark:text-blue-400 dark:hover:text-blue-300"
              >
                View system information
              </Link>
            )}
          </section>
        </div>

        <div className="shrink-0 border-t border-slate-200 bg-white p-3 dark:border-neutral-700 dark:bg-[#1b1b1b]">
          <Button
            type="button"
            variant="ghost"
            onClick={onLogout}
            disabled={isLoggingOut}
            className="h-10 w-full justify-center gap-2 rounded-lg text-red-600 hover:bg-red-50 hover:text-red-700 dark:text-red-400 dark:hover:bg-red-950/30 dark:hover:text-red-300"
          >
            <LogOut className="h-4 w-4" />
            {isLoggingOut ? 'Signing Out...' : 'Sign Out'}
          </Button>
        </div>
      </aside>
  );

  return container ? createPortal(panel, container) : panel;
}
