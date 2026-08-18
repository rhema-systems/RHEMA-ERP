'use client';

import React, { FormEvent, useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { KeyRound, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { authService } from '@/services/auth';
import { profileService } from '@/services/profile';
import type { User } from '@/types';

export default function ChangeTemporaryPasswordPage() {
  const router = useRouter();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const user = useMemo(() => authService.getStoredUser() as User | null, []);

  useEffect(() => {
    if (!authService.isAuthenticated()) router.replace('/login');
  }, [router]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (newPassword !== confirmPassword) {
      toast.error('The new passwords do not match.');
      return;
    }
    setBusy(true);
    try {
      await profileService.changePassword({ currentPassword, newPassword });
      await authService.logout();
      toast.success('Password replaced. Sign in with your new password.');
      router.replace('/login');
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Password change failed.'
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50 p-5 text-slate-950">
      <Card className="w-full max-w-lg border-slate-200 bg-white text-slate-950 shadow-lg">
        <CardHeader>
          <div className="mb-3 flex h-11 w-11 items-center justify-center rounded-xl bg-emerald-500/15">
            <ShieldCheck className="h-6 w-6 text-emerald-700" />
          </div>
          <CardTitle>Replace your temporary password</CardTitle>
          <CardDescription className="text-slate-600">
            This one-time step activates the approved supplier account. No other
            portal function is available until it is complete.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {user?.temporaryPasswordExpiresAtUtc && (
            <Alert className="mb-5 border-amber-200 bg-amber-50 text-amber-950">
              <AlertDescription>
                Temporary credential expires{' '}
                {new Date(user.temporaryPasswordExpiresAtUtc).toLocaleString()}.
              </AlertDescription>
            </Alert>
          )}
          <form className="grid gap-4" onSubmit={submit}>
            <div className="grid gap-2">
              <Label className="text-slate-800" htmlFor="temporary-password">
                Temporary password
              </Label>
              <Input
                id="temporary-password"
                type="password"
                autoComplete="current-password"
                className="border-slate-300 bg-white text-slate-950 caret-slate-950 focus:bg-white focus-visible:border-emerald-600 focus-visible:bg-white focus-visible:ring-emerald-600/20"
                value={currentPassword}
                onChange={(event) => setCurrentPassword(event.target.value)}
                required
              />
            </div>
            <div className="grid gap-2">
              <Label className="text-slate-800" htmlFor="new-password">
                New password
              </Label>
              <Input
                id="new-password"
                type="password"
                autoComplete="new-password"
                className="border-slate-300 bg-white text-slate-950 caret-slate-950 focus:bg-white focus-visible:border-emerald-600 focus-visible:bg-white focus-visible:ring-emerald-600/20"
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                minLength={8}
                required
              />
            </div>
            <div className="grid gap-2">
              <Label className="text-slate-800" htmlFor="confirm-password">
                Confirm new password
              </Label>
              <Input
                id="confirm-password"
                type="password"
                autoComplete="new-password"
                className="border-slate-300 bg-white text-slate-950 caret-slate-950 focus:bg-white focus-visible:border-emerald-600 focus-visible:bg-white focus-visible:ring-emerald-600/20"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                minLength={8}
                required
              />
            </div>
            <Button
              className="mt-2 bg-emerald-600 hover:bg-emerald-500"
              disabled={busy}
              type="submit"
            >
              <KeyRound className="mr-2 h-4 w-4" />
              {busy ? 'Activating…' : 'Activate supplier account'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </main>
  );
}
