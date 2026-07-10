'use client';

import { Suspense, useEffect, useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { ClipboardCheck, ClipboardList, Cloud, Download, Eye, EyeOff, Loader2, LogOut, RefreshCw, Truck, Wrench } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { clearMobileSession, hasValidMobileSession, rememberMobileSession } from '@/lib/mobile-session';
import { PWAManager } from '@/lib/pwa';
import authService from '@/services/auth';
import { TenantService } from '@/services/tenant';
import type { User } from '@/types';

const tenantService = new TenantService();
const MOBILE_FLASH_KEY = 'erp.mobile.flash.v1';
const mobileInputClass = 'bg-white text-slate-950 placeholder:text-slate-400 [color-scheme:light]';

type LoginForm = {
  username: string;
  password: string;
  tenantCode: string;
};

function getStoredUser(): User | null {
  try {
    return authService.getStoredUser();
  } catch {
    return null;
  }
}

function isIosLikeDevice() {
  if (typeof window === 'undefined') return false;

  const userAgent = window.navigator.userAgent || '';
  return /iPad|iPhone|iPod/.test(userAgent)
    || (window.navigator.platform === 'MacIntel' && window.navigator.maxTouchPoints > 1);
}

function isInstalledPwa() {
  if (typeof window === 'undefined') return false;

  const standaloneNavigator = window.navigator as Navigator & { standalone?: boolean };
  return window.matchMedia('(display-mode: standalone)').matches || standaloneNavigator.standalone === true;
}

function MobileHomeContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const redirectTarget = searchParams.get('redirect') || '';
  const [user, setUser] = useState<User | null>(null);
  const [form, setForm] = useState<LoginForm>({ username: '', password: '', tenantCode: '' });
  const [showPassword, setShowPassword] = useState(false);
  const [loggingIn, setLoggingIn] = useState(false);
  const [installing, setInstalling] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const navigateMobile = (href: string) => {
    if (!window.navigator.onLine) {
      window.location.assign(href);
      return;
    }

    router.push(href);
  };

  useEffect(() => {
    if (authService.isAuthenticated() && hasValidMobileSession()) {
      setUser(getStoredUser());
    } else if (authService.isAuthenticated()) {
      clearMobileSession();
      authService.clearTokens();
    }

    const flash = localStorage.getItem(MOBILE_FLASH_KEY);
    if (flash) {
      setMessage(flash);
      localStorage.removeItem(MOBILE_FLASH_KEY);
    } else if (searchParams.get('sent') === '1') {
      setMessage('Inspection sent successfully.');
    } else if (searchParams.get('queued') === '1') {
      setMessage('Inspection saved and will send when the phone is online.');
    }
  }, []);

  const displayName = useMemo(() => {
    if (!user) return '';
    return [user.firstName, user.lastName].filter(Boolean).join(' ') || user.username || user.email;
  }, [user]);

  const login = async () => {
    setLoggingIn(true);
    setError(null);
    setMessage(null);

    try {
      const response = await authService.login({
        username: form.username.trim(),
        password: form.password,
        tenantCode: form.tenantCode.trim() || undefined,
        rememberMe: true,
      });

      if (response.requiresTwoFactor) {
        setError('This mobile entry currently requires standard password login. Complete two-factor login in the main app first.');
        return;
      }

      const tenants = response.user?.accessibleTenants || [];
      const alreadyScoped = !!response.user?.currentTenantCode;
      const selectedTenantCode = form.tenantCode.trim() || tenants.find((tenant) => tenant.isDefault)?.tenantCode || tenants[0]?.tenantCode;

      if (!alreadyScoped && selectedTenantCode) {
        await tenantService.selectTenant(selectedTenantCode, false);
      }

      const stored = getStoredUser() || response.user || null;
      rememberMobileSession();
      setUser(stored);
      setMessage('Ready');
      if (redirectTarget.startsWith('/mobile/')) {
        router.push(redirectTarget);
      }
    } catch (loginError) {
      setError(loginError instanceof Error ? loginError.message : 'Login failed.');
    } finally {
      setLoggingIn(false);
    }
  };

  const logout = async () => {
    clearMobileSession();
    await authService.logout();
    setUser(null);
    setMessage(null);
    setError(null);
  };

  const installApp = async () => {
    setInstalling(true);
    setError(null);
    setMessage(null);

    try {
      if (isInstalledPwa()) {
        setMessage('This mobile app is already installed on this phone.');
        return;
      }

      const isLocal = ['localhost', '127.0.0.1', '::1'].includes(window.location.hostname);
      if (!window.isSecureContext && !isLocal) {
        setError('Install requires HTTPS. Once the IP/domain is on HTTPS, open this page again and tap Install App.');
        return;
      }

      if (isIosLikeDevice()) {
        setMessage('On iPhone/iPad, open this page in Safari, tap Share, then choose Add to Home Screen. Apple does not allow this button to install directly.');
        return;
      }

      const outcome = await PWAManager.getInstance().requestInstall();
      if (outcome === 'accepted') {
        setMessage('Mobile app installed. You can open it from your phone home screen.');
      } else if (outcome === 'dismissed') {
        setMessage('Installation was cancelled. Tap Install App when you are ready to try again.');
      } else {
        setMessage('This browser did not offer an install prompt. In Chrome or Edge, open the browser menu and choose Install app or Add to Home screen.');
      }
    } catch {
      setMessage('Use your browser menu and choose Add to Home Screen.');
    } finally {
      setInstalling(false);
    }
  };

  if (!user) {
    return (
      <main className="light min-h-screen bg-gradient-to-b from-emerald-50 via-white to-slate-100 px-4 py-6 text-slate-950" style={{ colorScheme: 'light' }}>
        <div className="mx-auto flex min-h-[calc(100vh-3rem)] max-w-md flex-col justify-center">
          <div className="mb-8 text-center">
            <div className="mx-auto mb-5 flex h-20 w-20 items-center justify-center rounded-3xl bg-gradient-to-br from-emerald-600 to-cyan-600 text-white shadow-xl shadow-emerald-200">
              <div className="relative flex h-14 w-14 items-center justify-center rounded-2xl bg-white/15">
                <Truck className="h-9 w-9" />
                <span className="absolute -bottom-2 -right-2 flex h-8 w-8 items-center justify-center rounded-full border-2 border-white bg-amber-400 text-slate-900 shadow-sm">
                  <Wrench className="h-4 w-4" />
                </span>
              </div>
            </div>
            <h1 className="text-3xl font-semibold tracking-normal">Fleet Maintenance</h1>
            <p className="mt-2 text-sm text-slate-600">Scan assets, inspect equipment, and sync findings</p>
          </div>

          <Card className="rounded-2xl border-emerald-100 bg-white text-slate-950 shadow-xl shadow-slate-200/70">
            <CardContent className="space-y-4 p-5">
              <div className="space-y-2">
                <Label htmlFor="mobile-username" className="text-slate-700">Username</Label>
                <Input
                  id="mobile-username"
                  autoComplete="username"
                  className={mobileInputClass}
                  value={form.username}
                  onChange={(event) => setForm((current) => ({ ...current, username: event.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mobile-password" className="text-slate-700">Password</Label>
                <div className="relative">
                  <Input
                    id="mobile-password"
                    type={showPassword ? 'text' : 'password'}
                    autoComplete="current-password"
                    className={`${mobileInputClass} pr-12`}
                    value={form.password}
                    onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
                    onKeyDown={(event) => {
                      if (event.key === 'Enter' && form.username && form.password) void login();
                    }}
                  />
                  <button
                    type="button"
                    aria-label={showPassword ? 'Hide password' : 'Show password'}
                    className="absolute inset-y-0 right-0 flex w-12 items-center justify-center rounded-r-md text-slate-500 hover:text-slate-900"
                    onClick={() => setShowPassword((current) => !current)}
                  >
                    {showPassword ? <EyeOff className="h-5 w-5" /> : <Eye className="h-5 w-5" />}
                  </button>
                </div>
              </div>
              {error ? <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div> : null}
              {message ? <div className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-700">{message}</div> : null}

              <Button className="h-12 w-full" onClick={login} disabled={loggingIn || !form.username.trim() || !form.password}>
                {loggingIn ? <Loader2 className="mr-2 h-5 w-5 animate-spin" /> : null}
                Login
              </Button>

              <Button type="button" variant="outline" className="h-11 w-full border-emerald-200 bg-emerald-50 text-emerald-800 hover:bg-emerald-100" onClick={() => void installApp()} disabled={installing}>
                {installing ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Download className="mr-2 h-4 w-4" />}
                Install app on this phone
              </Button>
            </CardContent>
          </Card>
        </div>
      </main>
    );
  }

  return (
    <main className="light min-h-screen bg-gradient-to-b from-emerald-50 via-white to-slate-100 px-4 py-5 text-slate-950" style={{ colorScheme: 'light' }}>
      <div className="mx-auto max-w-md">
        <div className="mb-5 flex items-start justify-between gap-3">
          <div>
            <p className="text-sm text-slate-600">Signed in as</p>
            <h1 className="text-2xl font-semibold tracking-normal">{displayName}</h1>
            <p className="text-sm text-slate-500">{user.currentTenantName || 'Mobile workspace'}</p>
          </div>
          <Button variant="outline" size="icon" className="border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-slate-50" onClick={() => void logout()}>
            <LogOut className="h-4 w-4" />
          </Button>
        </div>

        {message ? (
          <div className="mb-4 rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-800 shadow-sm">
            {message}
          </div>
        ) : null}
        {error ? (
          <div className="mb-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800 shadow-sm">
            {error}
          </div>
        ) : null}

        <div className="grid grid-cols-1 gap-3">
          <button
            type="button"
            onClick={() => void installApp()}
            disabled={installing}
            className="flex min-h-20 items-center gap-4 rounded-lg border border-slate-200 bg-white p-4 text-left text-slate-900 shadow-sm disabled:opacity-70"
          >
            <span className="flex h-11 w-11 items-center justify-center rounded-md bg-emerald-50 text-emerald-700">
              {installing ? <Loader2 className="h-6 w-6 animate-spin" /> : <Download className="h-6 w-6" />}
            </span>
            <span>
              <span className="block text-lg font-semibold">Install App</span>
              <span className="block text-sm text-slate-500">Open from your phone home screen</span>
            </span>
          </button>

          <button
            type="button"
            onClick={() => navigateMobile('/mobile/fleet/inspection?scan=1')}
            className="flex min-h-28 items-center gap-4 rounded-lg border border-emerald-200 bg-emerald-600 p-5 text-left text-white shadow-lg shadow-emerald-200"
          >
            <span className="flex h-12 w-12 items-center justify-center rounded-md bg-white/15 text-white">
              <ClipboardCheck className="h-7 w-7" />
            </span>
            <span>
              <span className="block text-xl font-semibold">Asset Inspection</span>
              <span className="block text-sm text-emerald-50">Scan QR and submit checklist</span>
            </span>
          </button>

          <button
            type="button"
            onClick={() => navigateMobile('/mobile/fleet/inspection?sync=1')}
            className="flex min-h-24 items-center gap-4 rounded-lg border border-slate-200 bg-white p-5 text-left text-slate-900 shadow-sm"
          >
            <span className="flex h-11 w-11 items-center justify-center rounded-md bg-cyan-50 text-cyan-700">
              <RefreshCw className="h-6 w-6" />
            </span>
            <span>
              <span className="block text-lg font-semibold">Sync Checklists</span>
              <span className="block text-sm text-slate-500">Download sheets for offline use</span>
            </span>
          </button>

          <button
            type="button"
            onClick={() => navigateMobile('/mobile/fleet/inspection?pending=1')}
            className="flex min-h-24 items-center gap-4 rounded-lg border border-slate-200 bg-white p-5 text-left text-slate-900 shadow-sm"
          >
            <span className="flex h-11 w-11 items-center justify-center rounded-md bg-blue-50 text-blue-700">
              <Cloud className="h-6 w-6" />
            </span>
            <span>
              <span className="block text-lg font-semibold">Pending Uploads</span>
              <span className="block text-sm text-slate-500">Send saved inspections to server</span>
            </span>
          </button>

          <button
            type="button"
            onClick={() => navigateMobile('/mobile/fleet/inspection?submitted=1')}
            className="flex min-h-24 items-center gap-4 rounded-lg border border-slate-200 bg-white p-5 text-left text-slate-900 shadow-sm"
          >
            <span className="flex h-11 w-11 items-center justify-center rounded-md bg-violet-50 text-violet-700">
              <ClipboardList className="h-6 w-6" />
            </span>
            <span>
              <span className="block text-lg font-semibold">Submitted Requests</span>
              <span className="block text-sm text-slate-500">Check inspection status</span>
            </span>
          </button>
        </div>
      </div>
    </main>
  );
}

export default function MobileHomePage() {
  return (
    <Suspense fallback={<div className="light flex min-h-screen items-center justify-center bg-slate-50 text-slate-900" style={{ colorScheme: 'light' }}><Loader2 className="h-6 w-6 animate-spin" /></div>}>
      <MobileHomeContent />
    </Suspense>
  );
}
