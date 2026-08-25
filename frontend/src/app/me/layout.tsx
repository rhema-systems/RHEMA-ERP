'use client';

/**
 * Area 25 slice 2 — the self-service portal shell.
 *
 * D8 route gate: authenticated + internal + LINKED employee. The link is resolved once
 * here from the fresh `Auth/me` read (slice 1 put `employeeId` on it); an unlinked user
 * gets one friendly explanation page instead of twenty broken screens, and external-portal
 * accounts are routed back to their own world. No permission family — the backend's
 * self-arms carry every read this shell fronts.
 */

import { AuthGuard } from '@/components/auth/auth-guard';
import { PortalTopNav } from '@/components/me/portal-top-nav';
import { Button } from '@/components/ui/button';
import { Briefcase, LogOut, UserX } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { useEffect } from 'react';
import { useAuth } from '@/hooks/use-auth';
import { getAuthenticatedHomePath, hasDeskAccess, isExternalPortalUser } from '@/lib/auth-routing';

export const dynamic = 'force-dynamic';

function UnlinkedNotice() {
  const { user, logout, isLoggingOut } = useAuth();
  const router = useRouter();
  const deskUser = hasDeskAccess(user);

  return (
    <div className="flex min-h-[70vh] items-center justify-center px-4">
      <div className="max-w-md rounded-xl border bg-card p-8 text-center shadow-sm">
        <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-muted">
          <UserX className="h-7 w-7 text-muted-foreground" />
        </div>
        <h1 className="text-xl font-semibold">Your account isn&apos;t linked yet</h1>
        <p className="mt-3 text-sm text-muted-foreground">
          Self-service shows <em>your</em> leave, payslips, training and more — so your
          sign-in (<span className="font-medium">{user?.username}</span>) needs to be linked
          to your employee record first. Please contact HR to get linked; everything here
          lights up the moment that happens.
        </p>
        <div className="mt-6 flex justify-center gap-3">
          {deskUser && (
            <Button variant="outline" onClick={() => router.push('/dashboard')}>
              <Briefcase className="mr-2 h-4 w-4" /> Back to ERP
            </Button>
          )}
          <Button variant="outline" disabled={isLoggingOut} onClick={() => logout(undefined)}>
            <LogOut className="mr-2 h-4 w-4" /> Sign out
          </Button>
        </div>
      </div>
    </div>
  );
}

function PortalFrame({ children }: { children: React.ReactNode }) {
  const { user, isLoading } = useAuth();
  const router = useRouter();
  const isExternal = isExternalPortalUser(user);

  useEffect(() => {
    if (!isLoading && user && isExternal) {
      router.replace(getAuthenticatedHomePath(user));
    }
  }, [isLoading, user, isExternal, router]);

  if (isLoading || !user) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-muted/30">
        <div className="text-center">
          <div className="mx-auto h-10 w-10 animate-spin rounded-full border-b-2 border-primary" />
          <p className="mt-4 text-sm text-muted-foreground">Opening your workspace…</p>
        </div>
      </div>
    );
  }

  if (isExternal) return null;

  return (
    <div className="min-h-screen bg-muted/30">
      <PortalTopNav />
      <main className="mx-auto max-w-7xl px-4 py-6">
        {user.employeeId ? children : <UnlinkedNotice />}
      </main>
    </div>
  );
}

export default function MeLayout({ children }: { children: React.ReactNode }) {
  return (
    <AuthGuard>
      <PortalFrame>{children}</PortalFrame>
    </AuthGuard>
  );
}
