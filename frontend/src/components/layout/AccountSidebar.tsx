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

interface AccountSidebarProps {
  open: boolean;
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
  user,
  currentTenant,
  isLoggingOut = false,
  showSettingsLink = false,
  onClose,
  onLogout,
}: AccountSidebarProps) {
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const { theme, setTheme, actualTheme } = useTheme();
  const displayName = [user?.firstName, user?.lastName].filter(Boolean).join(' ').trim()
    || user?.username
    || 'User';

  useEffect(() => {
    if (!open) return;

    const previousOverflow = document.body.style.overflow;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };

    document.body.style.overflow = 'hidden';
    document.addEventListener('keydown', handleKeyDown);
    closeButtonRef.current?.focus();

    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [onClose, open]);

  if (!open || typeof document === 'undefined') return null;

  return createPortal(
    <div className="fixed inset-0 z-[80]" data-testid="account-sidebar">
      <button
        type="button"
        aria-label="Dismiss account sidebar"
        className="absolute inset-0 cursor-default bg-slate-950/45 backdrop-blur-[1px]"
        onClick={onClose}
      />

      <aside
        role="dialog"
        aria-modal="true"
        aria-labelledby="account-sidebar-title"
        className="absolute inset-y-0 right-0 flex w-[min(420px,calc(100vw-16px))] flex-col overflow-hidden border-l border-slate-200 bg-slate-50 shadow-2xl dark:border-neutral-700 dark:bg-[#151515]"
      >
        <div className="relative bg-gradient-to-br from-blue-600 via-indigo-600 to-violet-600 px-6 pb-7 pt-6 text-white">
          <Button
            ref={closeButtonRef}
            type="button"
            variant="ghost"
            size="sm"
            aria-label="Close account sidebar"
            onClick={onClose}
            className="absolute right-4 top-4 h-9 w-9 rounded-full p-0 text-white hover:bg-white/15 hover:text-white"
          >
            <X className="h-5 w-5" />
          </Button>

          <div className="flex items-center gap-4 pr-10">
            <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full border-4 border-white/25 bg-white/15 text-xl font-semibold shadow-lg">
              {getInitials(displayName) || user?.username?.[0]?.toUpperCase() || 'U'}
            </div>
            <div className="min-w-0">
              <h2 id="account-sidebar-title" className="truncate text-lg font-semibold">
                {displayName}
              </h2>
              <p className="truncate text-sm text-white/85">{user?.email || user?.username}</p>
              {user?.id && <p className="mt-1 truncate text-xs text-white/65">User ID: {user.id}</p>}
            </div>
          </div>

          {currentTenant && (
            <div className="mt-5 flex items-center gap-2 rounded-xl border border-white/25 bg-white/10 px-3 py-2 text-sm backdrop-blur-sm">
              <Building2 className="h-4 w-4 shrink-0" />
              <span className="min-w-0 flex-1 truncate">{currentTenant.name}</span>
              <span className="rounded-md bg-white/15 px-2 py-0.5 text-xs">{currentTenant.code}</span>
            </div>
          )}
        </div>

        <div className="min-h-0 flex-1 space-y-5 overflow-y-auto p-5">
          <section className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm dark:border-neutral-700 dark:bg-[#202020]">
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
                      'flex h-10 items-center justify-center gap-2 rounded-lg text-sm font-medium transition-colors',
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

          <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-neutral-700 dark:bg-[#202020]">
            <div className="border-b border-slate-100 px-4 py-3 dark:border-neutral-700">
              <h3 className="text-sm font-semibold text-slate-950 dark:text-neutral-100">Account</h3>
            </div>
            <nav aria-label="Account shortcuts" className="p-2">
              <Link
                href="/profile"
                onClick={onClose}
                className="flex items-center gap-3 rounded-xl px-3 py-3 text-sm font-medium text-slate-700 transition-colors hover:bg-blue-50 hover:text-blue-700 dark:text-neutral-200 dark:hover:bg-neutral-800 dark:hover:text-white"
              >
                <UserIcon className="h-4 w-4 text-slate-500 dark:text-neutral-400" />
                Profile Settings
              </Link>
              <Link
                href="/account"
                onClick={onClose}
                className="flex items-center gap-3 rounded-xl px-3 py-3 text-sm font-medium text-slate-700 transition-colors hover:bg-blue-50 hover:text-blue-700 dark:text-neutral-200 dark:hover:bg-neutral-800 dark:hover:text-white"
              >
                <Settings className="h-4 w-4 text-slate-500 dark:text-neutral-400" />
                Account Settings
              </Link>
              {showSettingsLink && (
                <Link
                  href="/settings"
                  onClick={onClose}
                  className="flex items-center gap-3 rounded-xl px-3 py-3 text-sm font-medium text-slate-700 transition-colors hover:bg-blue-50 hover:text-blue-700 dark:text-neutral-200 dark:hover:bg-neutral-800 dark:hover:text-white"
                >
                  <Settings className="h-4 w-4 text-slate-500 dark:text-neutral-400" />
                  All Settings
                </Link>
              )}
            </nav>
          </section>

          {!!user?.roles?.length && (
            <section className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm dark:border-neutral-700 dark:bg-[#202020]">
              <div className="mb-3 flex items-center gap-2">
                <ShieldCheck className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                <h3 className="text-sm font-semibold text-slate-950 dark:text-neutral-100">Access roles</h3>
              </div>
              <div className="flex flex-wrap gap-2">
                {user.roles.map(role => (
                  <span
                    key={role}
                    className="rounded-full bg-blue-50 px-2.5 py-1 text-xs font-medium text-blue-700 dark:bg-blue-950/50 dark:text-blue-300"
                  >
                    {role}
                  </span>
                ))}
              </div>
            </section>
          )}
        </div>

        <div className="border-t border-slate-200 bg-white p-4 dark:border-neutral-700 dark:bg-[#1b1b1b]">
          <Button
            type="button"
            variant="ghost"
            onClick={onLogout}
            disabled={isLoggingOut}
            className="h-11 w-full justify-center gap-2 rounded-xl text-red-600 hover:bg-red-50 hover:text-red-700 dark:text-red-400 dark:hover:bg-red-950/30 dark:hover:text-red-300"
          >
            <LogOut className="h-4 w-4" />
            {isLoggingOut ? 'Signing Out...' : 'Sign Out'}
          </Button>
        </div>
      </aside>
    </div>,
    document.body,
  );
}
