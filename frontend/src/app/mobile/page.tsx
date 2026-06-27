'use client';

import { Suspense, useEffect, useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { ClipboardCheck, ClipboardList, Cloud, Loader2, LogOut, RefreshCw, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import authService from '@/services/auth';
import { TenantService } from '@/services/tenant';
import type { User } from '@/types';

const tenantService = new TenantService();
const MOBILE_FLASH_KEY = 'erp.mobile.flash.v1';

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

function MobileHomeContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const redirectTarget = searchParams.get('redirect') || '';
  const [user, setUser] = useState<User | null>(null);
  const [form, setForm] = useState<LoginForm>({ username: '', password: '', tenantCode: '' });
  const [loggingIn, setLoggingIn] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (authService.isAuthenticated()) {
      setUser(getStoredUser());
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
    await authService.logout();
    setUser(null);
    setMessage(null);
    setError(null);
  };

  if (!user) {
    return (
      <main className="min-h-screen bg-slate-950 px-4 py-6 text-white">
        <div className="mx-auto flex min-h-[calc(100vh-3rem)] max-w-md flex-col justify-center">
          <div className="mb-8">
            <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-xl bg-emerald-500 text-slate-950">
              <ShieldCheck className="h-8 w-8" />
            </div>
            <h1 className="text-3xl font-semibold tracking-normal">Mobile Inspection</h1>
            <p className="mt-2 text-sm text-slate-300">Fleet and maintenance checks</p>
          </div>

          <Card className="rounded-lg border-slate-800 bg-white text-slate-950 shadow-xl">
            <CardContent className="space-y-4 p-5">
              <div className="space-y-2">
                <Label htmlFor="mobile-username">Username</Label>
                <Input
                  id="mobile-username"
                  autoComplete="username"
                  value={form.username}
                  onChange={(event) => setForm((current) => ({ ...current, username: event.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="mobile-password">Password</Label>
                <Input
                  id="mobile-password"
                  type="password"
                  autoComplete="current-password"
                  value={form.password}
                  onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' && form.username && form.password) void login();
                  }}
                />
              </div>
              {error ? <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div> : null}
              {message ? <div className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-700">{message}</div> : null}

              <Button className="h-12 w-full" onClick={login} disabled={loggingIn || !form.username.trim() || !form.password}>
                {loggingIn ? <Loader2 className="mr-2 h-5 w-5 animate-spin" /> : null}
                Login
              </Button>
            </CardContent>
          </Card>
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-slate-950 px-4 py-5 text-white">
      <div className="mx-auto max-w-md">
        <div className="mb-5 flex items-start justify-between gap-3">
          <div>
            <p className="text-sm text-slate-300">Signed in as</p>
            <h1 className="text-2xl font-semibold tracking-normal">{displayName}</h1>
            <p className="text-sm text-slate-400">{user.currentTenantName || 'Mobile workspace'}</p>
          </div>
          <Button variant="outline" size="icon" className="border-slate-700 bg-transparent text-white hover:bg-slate-900" onClick={() => void logout()}>
            <LogOut className="h-4 w-4" />
          </Button>
        </div>

        {message ? (
          <div className="mb-4 rounded-md border border-emerald-400/40 bg-emerald-400/15 px-3 py-2 text-sm text-emerald-100">
            {message}
          </div>
        ) : null}
        {error ? (
          <div className="mb-4 rounded-md border border-red-400/40 bg-red-400/15 px-3 py-2 text-sm text-red-100">
            {error}
          </div>
        ) : null}

        <div className="grid grid-cols-1 gap-3">
          <button
            type="button"
            onClick={() => router.push('/mobile/fleet/inspection')}
            className="flex min-h-28 items-center gap-4 rounded-lg border border-emerald-400/30 bg-emerald-400 p-5 text-left text-slate-950 shadow-lg"
          >
            <span className="flex h-12 w-12 items-center justify-center rounded-md bg-slate-950 text-emerald-300">
              <ClipboardCheck className="h-7 w-7" />
            </span>
            <span>
              <span className="block text-xl font-semibold">Asset Inspection</span>
              <span className="block text-sm text-slate-800">Scan QR and submit checklist</span>
            </span>
          </button>

          <button
            type="button"
            onClick={() => router.push('/mobile/fleet/inspection?sync=1')}
            className="flex min-h-24 items-center gap-4 rounded-lg border border-slate-800 bg-slate-900 p-5 text-left shadow"
          >
            <span className="flex h-11 w-11 items-center justify-center rounded-md bg-slate-800 text-cyan-300">
              <RefreshCw className="h-6 w-6" />
            </span>
            <span>
              <span className="block text-lg font-semibold">Sync Checklists</span>
              <span className="block text-sm text-slate-400">Refresh offline sheets</span>
            </span>
          </button>

          <button
            type="button"
            onClick={() => router.push('/mobile/fleet/inspection?pending=1')}
            className="flex min-h-24 items-center gap-4 rounded-lg border border-slate-800 bg-slate-900 p-5 text-left shadow"
          >
            <span className="flex h-11 w-11 items-center justify-center rounded-md bg-slate-800 text-blue-300">
              <Cloud className="h-6 w-6" />
            </span>
            <span>
              <span className="block text-lg font-semibold">Pending Uploads</span>
              <span className="block text-sm text-slate-400">Submit saved inspections</span>
            </span>
          </button>

          <button
            type="button"
            onClick={() => router.push('/mobile/fleet/inspection?submitted=1')}
            className="flex min-h-24 items-center gap-4 rounded-lg border border-slate-800 bg-slate-900 p-5 text-left shadow"
          >
            <span className="flex h-11 w-11 items-center justify-center rounded-md bg-slate-800 text-violet-300">
              <ClipboardList className="h-6 w-6" />
            </span>
            <span>
              <span className="block text-lg font-semibold">Submitted Requests</span>
              <span className="block text-sm text-slate-400">Check inspection status</span>
            </span>
          </button>
        </div>
      </div>
    </main>
  );
}

export default function MobileHomePage() {
  return (
    <Suspense fallback={<div className="flex min-h-screen items-center justify-center bg-slate-950 text-white"><Loader2 className="h-6 w-6 animate-spin" /></div>}>
      <MobileHomeContent />
    </Suspense>
  );
}
