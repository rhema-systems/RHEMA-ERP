'use client';

import Link from 'next/link';
import { Briefcase } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { authService } from '@/services/auth';
import { isCandidateUser } from '@/lib/auth-routing';

/**
 * The public careers shell — anonymous by design (browse public, apply logged-in), so there is
 * deliberately no AuthGuard here. A signed-in candidate gets a link back to their portal instead
 * of the sign-in buttons.
 */
export default function CareersLayout({ children }: { children: React.ReactNode }) {
  const user = typeof window !== 'undefined' ? authService.getStoredUser() : null;
  const signedInCandidate = isCandidateUser(user);

  return (
    <div className="min-h-screen bg-background">
      <header className="border-b">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-4">
          <Link href="/careers" className="flex items-center gap-2 font-semibold">
            <Briefcase className="h-5 w-5" />
            Careers
          </Link>
          <div className="flex items-center gap-2">
            {signedInCandidate ? (
              <Button asChild variant="outline" size="sm">
                <Link href="/external-portal/careers">My applications</Link>
              </Button>
            ) : (
              <>
                <Button asChild variant="ghost" size="sm">
                  <Link href="/login?redirect=%2Fexternal-portal%2Fcareers">Sign in</Link>
                </Button>
                <Button asChild size="sm">
                  <Link href="/careers/register">Create an account</Link>
                </Button>
              </>
            )}
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-5xl px-4 py-8">{children}</main>
    </div>
  );
}
