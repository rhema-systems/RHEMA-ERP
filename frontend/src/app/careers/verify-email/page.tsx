'use client';

import { Suspense, useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { CheckCircle2, Loader2, MailWarning } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { authService } from '@/services/auth';
import { isCandidateUser } from '@/lib/auth-routing';
import { candidateService } from '@/services/hr/careers.service';

/**
 * Lands the email-confirmation link from the candidate's inbox. Confirming requires being
 * signed in (the token is bound to the account) — an anonymous visitor is sent to log in and
 * comes straight back here with the same link.
 */
function VerifyEmailInner() {
  const params = useSearchParams();
  const token = params?.get('token') ?? '';
  const [state, setState] = useState<'working' | 'done' | 'failed' | 'signin'>('working');
  const [message, setMessage] = useState('');
  const ran = useRef(false);

  useEffect(() => {
    if (ran.current) return;
    ran.current = true;

    const user = authService.getStoredUser();
    if (!isCandidateUser(user)) {
      setState('signin');
      return;
    }
    if (!token) {
      setState('failed');
      setMessage('The link is missing its confirmation token. Request a new one from your profile.');
      return;
    }
    candidateService
      .confirmEmail(token)
      .then((r) => {
        setState('done');
        setMessage(r.message);
      })
      .catch((e: any) => {
        setState('failed');
        setMessage(e?.message ?? 'The confirmation link is invalid or has expired.');
      });
  }, [token]);

  const selfUrl = typeof window !== 'undefined' ? window.location.pathname + window.location.search : '/careers/verify-email';

  return (
    <div className="mx-auto max-w-md">
      <Card>
        <CardHeader>
          <CardTitle className="text-xl">Confirm your email</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {state === 'working' && (
            <div className="flex items-center gap-2 text-muted-foreground">
              <Loader2 className="h-5 w-5 animate-spin" />
              Confirming…
            </div>
          )}
          {state === 'signin' && (
            <>
              <p className="text-sm text-muted-foreground">
                Sign in to your candidate account first — the confirmation is tied to it.
              </p>
              <Button asChild>
                <Link href={`/login?redirect=${encodeURIComponent(selfUrl)}`}>Sign in and confirm</Link>
              </Button>
            </>
          )}
          {state === 'done' && (
            <>
              <p className="flex items-center gap-2 text-sm">
                <CheckCircle2 className="h-5 w-5 text-green-600" />
                {message}
              </p>
              <Button asChild>
                <Link href="/external-portal/careers/profile">Back to my profile</Link>
              </Button>
            </>
          )}
          {state === 'failed' && (
            <>
              <p className="flex items-center gap-2 text-sm text-destructive">
                <MailWarning className="h-5 w-5" />
                {message}
              </p>
              <Button asChild variant="outline">
                <Link href="/external-portal/careers/profile">Back to my profile</Link>
              </Button>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

export default function VerifyEmailPage() {
  return (
    <Suspense
      fallback={
        <div className="flex items-center justify-center py-24">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      }
    >
      <VerifyEmailInner />
    </Suspense>
  );
}
